using System.Diagnostics;

namespace MicroMerge;

public class Operator : IDisposable
{
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
    private SortedTable _resultTable;

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


        // Swap the tables for the right be the smaller one
        if (Left.PageAmount < Right.PageAmount)
        {
            (Left, Right) = (Right, Left);
            (LeftJoinColumn, RightJoinColumn) = (RightJoinColumn, LeftJoinColumn);
        }

        var columns = new List<string>(Left.Columns);
        columns.AddRange(Right.Columns);

        _resultTable = new SortedTable($"{Left.Name}_{Right.Name}_joined", columns, LeftJoinColumn);
        using var sortedLeftTable = ExternalMergeSort(Left, LeftJoinColumn);
        sortedLeftTable.WriteToCsv("tubias.csv");
        using var sortedRightTable = ExternalMergeSort(Right, RightJoinColumn);

        var leftRecordsEnumerable = GetRecordsFromSortedTable(sortedLeftTable);
        var rightFirstPointerRecordsEnumerable = GetRecordsFromSortedTable(sortedRightTable);
        var secondPointerRecordsEnumerable
            = GetRecordsFromSortedTable(sortedRightTable);

        Debug.Assert(leftRecordsEnumerable != null, "Left records must not be null.");
        Debug.Assert(rightFirstPointerRecordsEnumerable != null, "Right records first pointer must not be null.");
        Debug.Assert(secondPointerRecordsEnumerable != null, "Right records second pointer must not be null.");

        if (!leftRecordsEnumerable.Any() || !rightFirstPointerRecordsEnumerable.Any())
        {
            // If either table is empty, return an empty result
            Result.NameOfResultTable = _resultTable.Name;
            return Result;
        }

        using var leftRecordsIterator = leftRecordsEnumerable.GetEnumerator();
        using var rightRecordsFirstIterator = rightFirstPointerRecordsEnumerable.GetEnumerator();
        using var rightRecordsSecondIterator = secondPointerRecordsEnumerable.GetEnumerator();


        // Initialize the iterators
        leftRecordsIterator.MoveNext();
        rightRecordsFirstIterator.MoveNext();
        rightRecordsSecondIterator.MoveNext();


        var pageNumber = 0;
        var currentPage = new Page(new PageId(_resultTable.Name, pageNumber), []);
        Record? leftRecord = leftRecordsIterator.Current;
        Record? rightSecondPointerRecord = rightRecordsSecondIterator.Current;

        do
        {
            Debug.Assert(leftRecord != null, "Left record must not be null.");
            Debug.Assert(rightSecondPointerRecord != null, "Right second pointer record must not be null.");

            while (string.CompareOrdinal(leftRecord[LeftJoinColumn], rightSecondPointerRecord[RightJoinColumn]) < 0
                   && leftRecordsIterator.MoveNext())
                ;

            while (string.CompareOrdinal(leftRecord[LeftJoinColumn], rightSecondPointerRecord[RightJoinColumn]) > 0
                   && rightRecordsSecondIterator.MoveNext()) ;

            var rightFirstPointerRecord = rightSecondPointerRecord;

            while (string.CompareOrdinal(leftRecord[LeftJoinColumn], rightSecondPointerRecord[RightJoinColumn]) == 0)
            {

                rightFirstPointerRecord = rightSecondPointerRecord;

                // Here we use the first pointer to find the first matching record
                while (string.CompareOrdinal(leftRecord[LeftJoinColumn], rightFirstPointerRecord[RightJoinColumn]) ==
                       0)
                {
                    // We have a match, create a new record with combined columns
                    var newRecord = new Record();
                    newRecord.Columns.AddRange(leftRecord.Columns);
                    newRecord.Columns.AddRange(rightFirstPointerRecord.Columns);

                    // Add the new record to the current page
                    currentPage.Records.Add(newRecord);
                    Result.NumberOfCreatedRecords++;

                    // Check if the current page has reached its limit
                    if (currentPage.RecordAmount == 10)
                    {
                        // Write the current page to the result table
                        _resultTable.WriteToFile(currentPage);
                        Result.NumberOfIOOperations++;

                        pageNumber++;
                        currentPage = new Page(new PageId(_resultTable.Name, pageNumber), []);
                        Result.NumberOfCreatedPages++;
                    }

                    // Get the next record from the right first pointer
                    rightRecordsFirstIterator.MoveNext();
                    rightFirstPointerRecord = rightRecordsFirstIterator.Current;
                }

                if (!leftRecordsIterator.MoveNext())
                    break;

                leftRecord =   leftRecordsIterator.Current;
            }

            rightSecondPointerRecord = rightFirstPointerRecord;
        } while (leftRecord != null && rightSecondPointerRecord != null);

        // Check if the current page has something
        if (currentPage.RecordAmount != 0)
        {
            // Write the current page to the result table
            _resultTable.WriteToFile(currentPage);
            Result.NumberOfIOOperations++;
            Result.NumberOfCreatedPages++;
        }

        Result.NameOfResultTable = _resultTable.Name;
        return Result;
    }

    public void WriteToCsv(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));


        var path = Path.Join(filePath, $"{_resultTable.Name}.csv");
        _resultTable.WriteToCsv(path);
    }

    public SortedTable ExternalMergeSort(Table table, string columnName)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));
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
        var availableMemoryPages = 3; // Assuming we can hold 3 pages in memory for sorting
        var currentRun = new List<Record>();
        var runNumber = 0;

        // Process each page from the table
        foreach (var page in table.Pages)
        {
            currentRun.AddRange(page.Records);
            Result.NumberOfIOOperations++; // Reading a page (10 records)

            // If we've collected enough records for memory limit, sort and write to disk
            if (currentRun.Count >= availableMemoryPages * 10) // 10 records per page
            {
                var sortedRecords = currentRun.OrderBy(r => GetComparableValue(r.Columns[columnIndex])).ToList();
                var sortedTable = new SortedTable(table, $"run_{runNumber}");
                sortedTable.WriteToFile(sortedRecords);

                // Count I/O operations for writing (every 10 records = 1 I/O)
                Result.NumberOfIOOperations += (int)Math.Ceiling(sortedRecords.Count / 10.0);

                sortedRuns.Add(sortedTable);
                currentRun.Clear();
                runNumber++;
                Result.NumberOfCreatedRecords += sortedRecords.Count;
            }
        }

        // Handle remaining records
        if (currentRun.Count > 0)
        {
            var sortedRecords = currentRun.OrderBy(r => GetComparableValue(r.Columns[columnIndex])).ToList();
            var sortedTable = new SortedTable(table, $"run_{runNumber}");
            sortedTable.WriteToFile(sortedRecords);

            // Count I/O operations for writing (every 10 records = 1 I/O)
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
            // Rename the single run to have the correct sorted table name
            var singleRun = sortedRuns[0];
            var finalTable = new SortedTable(originalTable, columnName);
            var allRecords = singleRun.GetPagesIterable().SelectMany(p => p.Records).ToList();
            finalTable.WriteToFile(allRecords);

            // Count I/O for reading the single run and writing the final table
            Result.NumberOfIOOperations += singleRun.PageAmount; // Reading pages
            Result.NumberOfIOOperations += (int)Math.Ceiling(allRecords.Count / 10.0); // Writing pages

            return finalTable;
        }

        var finalSortedTable = new SortedTable(originalTable, columnName);
        var mergedRecords = new List<Record>();
        var columnIndex = originalTable.Columns.IndexOf(columnName);

        // Use a priority queue approach for merging
        var iterators = sortedRuns.Select(run =>
            run.GetPagesIterable().SelectMany(p => p.Records).GetEnumerator()).ToList();

        var priorityQueue = new SortedDictionary<string, Queue<(Record record, int iteratorIndex)>>();

        // Count I/O for reading all sorted runs during merge
        foreach (var run in
                 sortedRuns) Result.NumberOfIOOperations += run.PageAmount; // Reading all pages from each run

        // Initialize the priority queue
        for (var i = 0; i < iterators.Count; i++)
            if (iterators[i].MoveNext())
            {
                var record = iterators[i].Current;
                var key = GetComparableValue(record.Columns[columnIndex]);
                if (!priorityQueue.ContainsKey(key))
                    priorityQueue[key] = new Queue<(Record, int)>();
                priorityQueue[key].Enqueue((record, i));
            }

        // Merge process
        while (priorityQueue.Count > 0)
        {
            var minKey = priorityQueue.Keys.First();
            var (record, iteratorIndex) = priorityQueue[minKey].Dequeue();
            mergedRecords.Add(record);

            if (priorityQueue[minKey].Count == 0)
                priorityQueue.Remove(minKey);

            // Try to advance the corresponding iterator
            if (iterators[iteratorIndex].MoveNext())
            {
                var nextRecord = iterators[iteratorIndex].Current;
                var nextKey = GetComparableValue(nextRecord.Columns[columnIndex]);
                if (!priorityQueue.ContainsKey(nextKey))
                    priorityQueue[nextKey] = new Queue<(Record, int)>();
                priorityQueue[nextKey].Enqueue((nextRecord, iteratorIndex));
            }
        }

        // Clean up iterators
        foreach (var iterator in iterators) iterator.Dispose();

        finalSortedTable.WriteToFile(mergedRecords);

        // Count I/O for writing final sorted table (every 10 records = 1 I/O)
        Result.NumberOfIOOperations += (int)Math.Ceiling(mergedRecords.Count / 10.0);

        return finalSortedTable;
    }

    private IEnumerable<Record>? GetRecordsFromSortedTable(SortedTable sortedTable)
    {
        Debug.Assert(sortedTable != null, "Sorted table must not be null.");

        var pages = sortedTable.GetPagesIterable();

        foreach (var page in pages)
        {
            Result.NumberOfIOOperations++;
            foreach (var record in page.Records) yield return record;
        }
    }

    private string GetComparableValue(string value)
    {
        return value ?? string.Empty;
    }

    public void SaveResults()
    {
    }

    public void Dispose()
    {
        _resultTable.Cleanup();
        GC.SuppressFinalize(this);
    }
}

public class OperatorResult
{
    public int NumberOfCreatedPages { get; set; }
    public int NumberOfIOOperations { get; set; }
    public int NumberOfCreatedRecords { get; set; }
    public string NameOfResultTable { get; set; } = string.Empty;
}
