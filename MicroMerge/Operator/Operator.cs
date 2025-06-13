using System.Diagnostics;
using MicroMerge.Models.Record;
using MicroMerge.Tables;

namespace MicroMerge.Operator;

public class Operator : IDisposable
{
    private SortedTable? _resultTable;

    public Operator(Table left, Table right, string leftJoinColumn, string rightJoinColumn)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Right = right ?? throw new ArgumentNullException(nameof(right));
        LeftJoinColumn = leftJoinColumn ?? throw new ArgumentNullException(nameof(leftJoinColumn));
        RightJoinColumn = rightJoinColumn ?? throw new ArgumentNullException(nameof(rightJoinColumn));
    }

    private Table Left { get; set; }
    private Table Right { get; set; }
    private string LeftJoinColumn { get; set; }
    private string RightJoinColumn { get; set; }
    private OperatorResult Result { get; } = new();

    public void Dispose()
    {
        _resultTable?.Cleanup();
        GC.SuppressFinalize(this);
    }

    public OperatorResult Execute()
    {
        Debug.Assert(Left != null, "Left table must not be null.");
        Debug.Assert(Right != null, "Right table must not be null.");
        Debug.Assert(!string.IsNullOrWhiteSpace(LeftJoinColumn), "Left join column must not be null or empty.");
        Debug.Assert(!string.IsNullOrWhiteSpace(RightJoinColumn), "Right join column must not be null or empty.");
        Debug.Assert(Left.Columns.Contains(LeftJoinColumn),
            $"Left join column '{LeftJoinColumn}' does not exist in left table '{Left.Name}'.");
        Debug.Assert(Right.Columns.Contains(RightJoinColumn),
            $"Right join column '{RightJoinColumn}' does not exist in right table '{Right.Name}'.");


        if (Left.PageAmount < Right.PageAmount)
        {
            (Left, Right) = (Right, Left);
            (LeftJoinColumn, RightJoinColumn) = (RightJoinColumn, LeftJoinColumn);
        }

        var columns = new List<string>(Left.Columns);
        columns.AddRange(Right.Columns);

        _resultTable = new SortedTable($"{Left.Name}_{Right.Name}_joined", columns, LeftJoinColumn);
        using var sortedLeftTable = ExternalMergeSort(Left, LeftJoinColumn);
        using var sortedRightTable = ExternalMergeSort(Right, RightJoinColumn);

        PerformSortMergeJoin(sortedLeftTable, sortedRightTable);

        Result.NameOfResultTable = _resultTable.Name;
        return Result;
    }

    public void WriteToCsv(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (_resultTable == null)
            throw new InvalidOperationException("Execute() must be called before WriteToCsv().");

        var path = Path.Join(filePath, $"{_resultTable.Name}.csv");
        _resultTable.WriteToCsv(path);
    }

    private SortedTable ExternalMergeSort(Table table, string columnName)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be null or empty.", nameof(columnName));

        var columnIndex = table.Columns.IndexOf(columnName);
        if (columnIndex == -1)
            throw new ArgumentException($"Column '{columnName}' does not exist in table '{table.Name}'.");

        // Create sorted runs
        var sortedRuns = CreateSortedRuns(table, columnIndex);

        // Merge sorted runs
        var finalSortedTable = MergeSortedRuns(sortedRuns, table, columnName);

        // Clean up temporary files
        foreach (var run in sortedRuns) run.DeleteFile();

        Result.NumberOfCreatedPages += finalSortedTable.PageAmount;

        return finalSortedTable;
    }

    private List<SortedTable> CreateSortedRuns(Table table, int columnIndex)
    {
        var sortedRuns = new List<SortedTable>();
        const int availableMemoryPages = 4;
        var currentRun = new List<Record>();
        var runNumber = 0;

        foreach (var page in table.Pages)
        {
            currentRun.AddRange(page.Records);
            Result.NumberOfIOOperations++;

            if (currentRun.Count >= availableMemoryPages * 10)
            {
                var sortedRecords = currentRun.OrderBy(r => GetComparableValue(r.Columns[columnIndex])).ToList();
                var sortedTable = new SortedTable(table, $"run_{runNumber}");
                sortedTable.WriteToFile(sortedRecords);

                Result.NumberOfIOOperations += (int)Math.Ceiling(sortedRecords.Count / 10.0);

                sortedRuns.Add(sortedTable);
                currentRun.Clear();
                runNumber++;
                Result.NumberOfCreatedRecords += sortedRecords.Count;
            }
        }

        if (currentRun.Count > 0)
        {
            var sortedRecords = currentRun.OrderBy(r => GetComparableValue(r.Columns[columnIndex])).ToList();
            var sortedTable = new SortedTable(table, $"run_{runNumber}");
            sortedTable.WriteToFile(sortedRecords);

            Result.NumberOfIOOperations += (int)Math.Ceiling(sortedRecords.Count / 10.0);

            sortedRuns.Add(sortedTable);
            Result.NumberOfCreatedRecords += sortedRecords.Count;
        }

        return sortedRuns;
    }

    private SortedTable MergeSortedRuns(List<SortedTable> sortedRuns, Table originalTable, string columnName)
    {
        if (sortedRuns.Count == 1)
        {
            var singleRun = sortedRuns[0];
            var finalTable = new SortedTable(originalTable, columnName);

            var outputBuffer = new List<Record>();
            foreach (var page in singleRun.GetPagesIterable())
            {
                Result.NumberOfIOOperations++;

                foreach (var record in page.Records)
                {
                    outputBuffer.Add(record);

                    if (outputBuffer.Count >= 10) FlushToSortedTable(finalTable, outputBuffer);
                }
            }

            if (outputBuffer.Count > 0) FlushToSortedTable(finalTable, outputBuffer);

            return finalTable;
        }

        var maxInputRuns = Math.Min(3, sortedRuns.Count); // Reserve 1 page for output
        var finalSortedTable = new SortedTable(originalTable, columnName);
        var columnIndex = originalTable.Columns.IndexOf(columnName);

        return MergeWithBufferConstraint(sortedRuns, finalSortedTable, columnIndex, maxInputRuns);
    }

    private SortedTable MergeWithBufferConstraint(List<SortedTable> sortedRuns, SortedTable finalTable, int columnIndex,
        int maxInputRuns)
    {
        if (sortedRuns.Count <= maxInputRuns) return MergeRuns(sortedRuns, finalTable, columnIndex);

        var tempRuns = new List<SortedTable>();
        var passNumber = 0;

        for (var i = 0; i < sortedRuns.Count; i += maxInputRuns)
        {
            var runsToMerge = sortedRuns.Skip(i).Take(maxInputRuns).ToList();
            var tempTable = new SortedTable($"temp_pass_{passNumber}", new List<string>(finalTable.Columns),
                "temp_column");
            var mergedTable = MergeRuns(runsToMerge, tempTable, columnIndex);
            tempRuns.Add(mergedTable);
            passNumber++;
        }

        var result = MergeWithBufferConstraint(tempRuns, finalTable, columnIndex, maxInputRuns);

        foreach (var tempRun in tempRuns) tempRun.DeleteFile();

        return result;
    }

    private SortedTable MergeRuns(List<SortedTable> runs, SortedTable outputTable, int columnIndex)
    {
        var runIterators = runs.Select(run => new RunIterator(run)).ToArray();
        var outputBuffer = new List<Record>();

        try
        {
            InitializeRunIterators(runIterators);
            PerformMergeProcess(runIterators, outputBuffer, outputTable, columnIndex);

            if (outputBuffer.Count > 0) FlushToSortedTable(outputTable, outputBuffer);
        }
        finally
        {
            CleanupRunIterators(runIterators);
        }

        return outputTable;
    }

    private void InitializeRunIterators(RunIterator[] runIterators)
    {
        foreach (var run in runIterators)
            if (!run.LoadNextPage())
                run.IsFinished = true;
            else
                Result.NumberOfIOOperations++;
    }

    private void PerformMergeProcess(RunIterator[] runIterators, List<Record> outputBuffer, SortedTable outputTable,
        int columnIndex)
    {
        while (runIterators.Any(it => !it.IsFinished))
        {
            var selectedIterator = FindIteratorWithSmallestRecord(runIterators, columnIndex);

            if (selectedIterator != null) ProcessSelectedRecord(selectedIterator, outputBuffer, outputTable);
        }
    }

    private static RunIterator? FindIteratorWithSmallestRecord(RunIterator[] runIterators, int columnIndex)
    {
        RunIterator? selectedIterator = null;
        var minValue = string.Empty;

        foreach (var runIterator in runIterators)
            if (!runIterator.IsFinished && runIterator.CurrentRecord != null)
            {
                var currentValue = GetComparableValue(runIterator.CurrentRecord.Columns[columnIndex]);
                if (selectedIterator == null || string.CompareOrdinal(currentValue, minValue) < 0)
                {
                    selectedIterator = runIterator;
                    minValue = currentValue;
                }
            }

        return selectedIterator;
    }

    private void ProcessSelectedRecord(RunIterator selectedIterator, List<Record> outputBuffer, SortedTable outputTable)
    {
        outputBuffer.Add(selectedIterator.CurrentRecord!);

        if (!selectedIterator.MoveNext())
        {
            if (!selectedIterator.LoadNextPage())
                selectedIterator.IsFinished = true;
            else
                Result.NumberOfIOOperations++;
        }

        if (outputBuffer.Count >= 10) FlushToSortedTable(outputTable, outputBuffer);
    }

    private static void CleanupRunIterators(RunIterator[] runIterators)
    {
        foreach (var iterator in runIterators) iterator.Dispose();
    }

    private void FlushToSortedTable(SortedTable table, List<Record> buffer)
    {
        if (buffer.Count > 0)
        {
            var allRecords = new List<Record>();
            try
            {
                foreach (var page in table.GetPagesIterable())
                {
                    allRecords.AddRange(page.Records);
                    Result.NumberOfIOOperations++;
                }
            }
            catch
            {
                // ignored
            }

            allRecords.AddRange(buffer);

            table.WriteToFile(allRecords);
            Result.NumberOfIOOperations += (int)Math.Ceiling(allRecords.Count / 10.0);

            buffer.Clear();
        }
    }

    private static string GetComparableValue(string value)
    {
        return value;
    }

    public void SaveResults()
    {
        // Method intentionally left empty - results are managed through WriteToCsv method
    }

    private void PerformSortMergeJoin(SortedTable leftTable, SortedTable rightTable)
    {
        var leftPageIterator = leftTable.GetPagesIterable().GetEnumerator();
        var rightPageIterator = rightTable.GetPagesIterable().GetEnumerator();

        var leftBuffer = new List<Record>();
        var rightBuffer = new List<Record>();
        var rightMarkBuffer = new List<Record>();
        var outputBuffer = new List<Record>();

        var leftIndex = 0;
        var rightIndex = 0;
        var rightMarkIndex = 0;

        var hasLeftData = LoadNextPageToBuffer(leftPageIterator, leftBuffer, ref leftIndex);
        var hasRightData = LoadNextPageToBuffer(rightPageIterator, rightBuffer, ref rightIndex);

        var leftColumnIndex = Left.Columns.IndexOf(LeftJoinColumn);
        var rightColumnIndex = Right.Columns.IndexOf(RightJoinColumn);

        while (hasLeftData && hasRightData)
        {
            var leftRecord = leftBuffer[leftIndex];
            var rightRecord = rightBuffer[rightIndex];

            var leftValue = GetComparableValue(leftRecord.Columns[leftColumnIndex]);
            var rightValue = GetComparableValue(rightRecord.Columns[rightColumnIndex]);

            var comparison = string.CompareOrdinal(leftValue, rightValue);

            if (comparison < 0)
            {
                leftIndex++;
                if (leftIndex >= leftBuffer.Count)
                    hasLeftData = LoadNextPageToBuffer(leftPageIterator, leftBuffer, ref leftIndex);
            }
            else if (comparison > 0)
            {
                rightIndex++;
                if (rightIndex >= rightBuffer.Count)
                    hasRightData = LoadNextPageToBuffer(rightPageIterator, rightBuffer, ref rightIndex);
            }
            else
            {
                var currentJoinValue = leftValue;

                rightMarkBuffer.Clear();
                rightMarkBuffer.AddRange(rightBuffer.Skip(rightIndex).Take(rightBuffer.Count - rightIndex));
                rightMarkIndex = 0;

                while (hasLeftData && leftIndex < leftBuffer.Count)
                {
                    var currentLeftRecord = leftBuffer[leftIndex];
                    var currentLeftValue = GetComparableValue(currentLeftRecord.Columns[leftColumnIndex]);

                    if (string.CompareOrdinal(currentLeftValue, currentJoinValue) != 0)
                        break;

                    var rightTempIndex = rightMarkIndex;
                    var rightTempBuffer = rightMarkBuffer;

                    while (rightTempIndex < rightTempBuffer.Count)
                    {
                        var currentRightRecord = rightTempBuffer[rightTempIndex];
                        var currentRightValue = GetComparableValue(currentRightRecord.Columns[rightColumnIndex]);

                        if (string.CompareOrdinal(currentRightValue, currentJoinValue) != 0)
                            break;

                        var joinedRecord = new Record();
                        joinedRecord.Columns.AddRange(currentLeftRecord.Columns);
                        joinedRecord.Columns.AddRange(currentRightRecord.Columns);

                        outputBuffer.Add(joinedRecord);
                        Result.NumberOfCreatedRecords++;

                        if (outputBuffer.Count >= 10) FlushOutputBuffer(outputBuffer);

                        rightTempIndex++;
                    }

                    leftIndex++;
                }

                while (rightIndex < rightBuffer.Count)
                {
                    var currentRightValue = GetComparableValue(rightBuffer[rightIndex].Columns[rightColumnIndex]);
                    if (string.CompareOrdinal(currentRightValue, currentJoinValue) != 0)
                        break;
                    rightIndex++;
                }

                if (leftIndex >= leftBuffer.Count)
                    hasLeftData = LoadNextPageToBuffer(leftPageIterator, leftBuffer, ref leftIndex);

                if (rightIndex >= rightBuffer.Count)
                    hasRightData = LoadNextPageToBuffer(rightPageIterator, rightBuffer, ref rightIndex);
            }
        }

        FlushOutputBuffer(outputBuffer);

        leftPageIterator.Dispose();
        rightPageIterator.Dispose();
    }

    private void FlushOutputBuffer(List<Record> outputBuffer)
    {
        if (outputBuffer.Count > 0 && _resultTable != null)
        {
            var existingRecords = new List<Record>();

            try
            {
                existingRecords.AddRange(_resultTable.GetPagesIterable().SelectMany(p => p.Records));
            }
            catch
            {
                // ignored
            }

            existingRecords.AddRange(outputBuffer);

            _resultTable.WriteToFile(existingRecords);

            Result.NumberOfIOOperations++; // Writing a page
            Result.NumberOfCreatedPages = (int)Math.Ceiling(existingRecords.Count / 10.0);

            outputBuffer.Clear();
        }
    }

    private bool LoadNextPageToBuffer(IEnumerator<Page> pageIterator, List<Record> buffer, ref int bufferIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bufferIndex);
        buffer.Clear();
        bufferIndex = 0;

        if (pageIterator.MoveNext())
        {
            buffer.AddRange(pageIterator.Current.Records);
            Result.NumberOfIOOperations++; // Reading 1 page
            return buffer.Count > 0;
        }

        return false;
    }

    private sealed class RunIterator : IDisposable
    {
        private readonly IEnumerator<Page> _pageIterator;
        private List<Record> _currentPageRecords;
        private bool _disposed;
        private int _recordIndex;

        public RunIterator(SortedTable sortedTable)
        {
            _pageIterator = sortedTable.GetPagesIterable().GetEnumerator();
            _currentPageRecords = [];
            _recordIndex = 0;
            IsFinished = false;
        }

        public Record? CurrentRecord =>
            _recordIndex < _currentPageRecords.Count ? _currentPageRecords[_recordIndex] : null;

        public bool IsFinished { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool LoadNextPage()
        {
            if (_pageIterator.MoveNext())
            {
                _currentPageRecords = new List<Record>(_pageIterator.Current.Records);
                _recordIndex = 0;
                return _currentPageRecords.Count > 0;
            }

            return false;
        }

        public bool MoveNext()
        {
            _recordIndex++;
            return _recordIndex < _currentPageRecords.Count;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing) _pageIterator?.Dispose();
                _disposed = true;
            }
        }
    }
}

public class OperatorResult
{
    public int NumberOfCreatedPages { get; set; }
    public int NumberOfIOOperations { get; set; }
    public int NumberOfCreatedRecords { get; set; }
    public string NameOfResultTable { get; set; } = string.Empty;
}
