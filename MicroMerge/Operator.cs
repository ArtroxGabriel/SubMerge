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
    private SortedTable? _resultTable;

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
        using var sortedRightTable = ExternalMergeSort(Right, RightJoinColumn);

        // Perform sort-merge join with 4-page memory constraint
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
        _resultTable?.Cleanup();
        GC.SuppressFinalize(this);
    }

    private void PerformSortMergeJoin(SortedTable leftTable, SortedTable rightTable)
    {
        // Memory constraint: 4 pages total (10 records each)
        // 1 page for left table buffer
        // 1 page for right table buffer  
        // 1 page for right table mark buffer (for duplicate handling)
        // 1 page for output buffer

        var leftPageIterator = leftTable.GetPagesIterable().GetEnumerator();
        var rightPageIterator = rightTable.GetPagesIterable().GetEnumerator();
        
        var leftBuffer = new List<Record>();
        var rightBuffer = new List<Record>();
        var rightMarkBuffer = new List<Record>(); // Mark buffer for handling duplicates
        var outputBuffer = new List<Record>();
        
        int leftIndex = 0;
        int rightIndex = 0;
        int rightMarkIndex = 0;
        
        bool hasLeftData = LoadNextPageToBuffer(leftPageIterator, leftBuffer, ref leftIndex);
        bool hasRightData = LoadNextPageToBuffer(rightPageIterator, rightBuffer, ref rightIndex);
        
        var leftColumnIndex = Left.Columns.IndexOf(LeftJoinColumn);
        var rightColumnIndex = Right.Columns.IndexOf(RightJoinColumn);

        while (hasLeftData && hasRightData)
        {
            var leftRecord = leftBuffer[leftIndex];
            var rightRecord = rightBuffer[rightIndex];
            
            var leftValue = GetComparableValue(leftRecord.Columns[leftColumnIndex]);
            var rightValue = GetComparableValue(rightRecord.Columns[rightColumnIndex]);
            
            int comparison = string.CompareOrdinal(leftValue, rightValue);
            
            if (comparison < 0)
            {
                // Left value is smaller, advance left pointer
                leftIndex++;
                if (leftIndex >= leftBuffer.Count)
                {
                    hasLeftData = LoadNextPageToBuffer(leftPageIterator, leftBuffer, ref leftIndex);
                }
            }
            else if (comparison > 0)
            {
                // Right value is smaller, advance right pointer
                rightIndex++;
                if (rightIndex >= rightBuffer.Count)
                {
                    hasRightData = LoadNextPageToBuffer(rightPageIterator, rightBuffer, ref rightIndex);
                }
            }
            else
            {
                // Values are equal - handle duplicates
                var currentJoinValue = leftValue;
                
                // Mark the position in right buffer to handle duplicates
                rightMarkBuffer.Clear();
                rightMarkBuffer.AddRange(rightBuffer.Skip(rightIndex).Take(rightBuffer.Count - rightIndex));
                rightMarkIndex = 0;
                
                // Process all left records with the same join value
                while (hasLeftData && leftIndex < leftBuffer.Count)
                {
                    var currentLeftRecord = leftBuffer[leftIndex];
                    var currentLeftValue = GetComparableValue(currentLeftRecord.Columns[leftColumnIndex]);
                    
                    if (string.CompareOrdinal(currentLeftValue, currentJoinValue) != 0)
                        break; // No more matching left records
                    
                    // Reset right position to the mark for each left record
                    var rightTempIndex = rightMarkIndex;
                    var rightTempBuffer = rightMarkBuffer;
                    
                    // Join current left record with all matching right records
                    while (rightTempIndex < rightTempBuffer.Count)
                    {
                        var currentRightRecord = rightTempBuffer[rightTempIndex];
                        var currentRightValue = GetComparableValue(currentRightRecord.Columns[rightColumnIndex]);
                        
                        if (string.CompareOrdinal(currentRightValue, currentJoinValue) != 0)
                            break; // No more matching right records
                        
                        // Create joined record
                        var joinedRecord = new Record();
                        joinedRecord.Columns.AddRange(currentLeftRecord.Columns);
                        joinedRecord.Columns.AddRange(currentRightRecord.Columns);
                        
                        outputBuffer.Add(joinedRecord);
                        Result.NumberOfCreatedRecords++;
                        
                        // Check if output buffer is full (10 records = 1 page)
                        if (outputBuffer.Count >= 10)
                        {
                            FlushOutputBuffer(outputBuffer);
                        }
                        
                        rightTempIndex++;
                    }
                    
                    leftIndex++;
                }
                
                // Advance right pointer past all processed records
                while (rightIndex < rightBuffer.Count)
                {
                    var currentRightValue = GetComparableValue(rightBuffer[rightIndex].Columns[rightColumnIndex]);
                    if (string.CompareOrdinal(currentRightValue, currentJoinValue) != 0)
                        break;
                    rightIndex++;
                }
                
                // Check if we need to load next left page
                if (leftIndex >= leftBuffer.Count)
                {
                    hasLeftData = LoadNextPageToBuffer(leftPageIterator, leftBuffer, ref leftIndex);
                }
                
                // Check if we need to load next right page
                if (rightIndex >= rightBuffer.Count)
                {
                    hasRightData = LoadNextPageToBuffer(rightPageIterator, rightBuffer, ref rightIndex);
                }
            }
        }
        
        // Write remaining records in output buffer
        FlushOutputBuffer(outputBuffer);
        
        // Clean up iterators
        leftPageIterator.Dispose();
        rightPageIterator.Dispose();
    }
    
    private void FlushOutputBuffer(List<Record> outputBuffer)
    {
        if (outputBuffer.Count > 0 && _resultTable != null)
        {
            // Write the output buffer to the result table
            var existingRecords = new List<Record>();
            
            // Read existing records if any
            try
            {
                existingRecords.AddRange(_resultTable.GetPagesIterable().SelectMany(p => p.Records));
            }
            catch
            {
                // No existing records, which is fine
            }
            
            // Add new records
            existingRecords.AddRange(outputBuffer);
            
            // Write all records back to the result table
            _resultTable.WriteToFile(existingRecords);
            
            Result.NumberOfIOOperations++; // Writing a page
            Result.NumberOfCreatedPages = (int)Math.Ceiling(existingRecords.Count / 10.0);
            
            outputBuffer.Clear();
        }
    }

    private bool LoadNextPageToBuffer(IEnumerator<Page> pageIterator, List<Record> buffer, ref int bufferIndex)
    {
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
}

public class OperatorResult
{
    public int NumberOfCreatedPages { get; set; }
    public int NumberOfIOOperations { get; set; }
    public int NumberOfCreatedRecords { get; set; }
    public string NameOfResultTable { get; set; } = string.Empty;
}
