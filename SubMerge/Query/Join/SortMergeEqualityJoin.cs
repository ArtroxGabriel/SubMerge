using System.Linq.Expressions;
using Serilog;
using SubMerge.Models;
using SubMerge.Storage.Buffer;
using SubMerge.Storage.Page;

namespace SubMerge.Query.Join;

public class SortMergeEqualityJoin<TLeft, TRight, TJoinResult>(
    IBufferManager bufferManager,
    IPageManager pageManager
) : EqualityJoin<TLeft, TRight, TJoinResult>
    where TRight : IRecord, new()
    where TLeft : IRecord, new()
    where TJoinResult : new()
{
    private readonly ILogger _logger = Log.ForContext<SortMergeEqualityJoin<TLeft, TRight, TJoinResult>>();

    public async Task<Result<IEnumerable<TJoinResult>, EqualityJoinError>> JoinIntoMemory(Table leftTable,
        Table rightTable,
        Expression<Func<TLeft, object>> leftJoinSelector, Expression<Func<TRight, object>> rightJoinSelector,
        Func<TLeft, TRight, TJoinResult> resultSelector)
    {
        _logger.Debug("Performing sort-merge equality join on tables {LeftTable} and {RightTable}", leftTable.Name,
            rightTable.Name);

        var leftTableTuples = new TLeft[leftTable.TupleCount];

        for (var i = 0; i < leftTable.TupleCount; i++)
        {
            var pageId = new PageId(i, leftTable.Name);
            var pinResult = await bufferManager.PinPageAsync(pageId);
            if (pinResult.IsError)
            {
                _logger.Error("Failed to pin page {PageId}: {Error}", pageId, pinResult.GetErrorOrThrow());
                return Result<IEnumerable<TJoinResult>, EqualityJoinError>.Error(
                    new EqualityJoinError($"Failed to pin page {pageId}"));
            }

            var page = pinResult.GetValueOrThrow();

            foreach (var tuple in page.Content)
            {
                var record = new TLeft();

                record.FromTuple(tuple);

                leftTableTuples[i] = record;
            }
        }

        var sortedLeftTableTuples = leftTableTuples
            .OrderBy(tuple => leftJoinSelector.Compile()(tuple))
            .ToList();

        var rightTableTuples = new TRight[rightTable.TupleCount];

        for (var i = 0; i < rightTable.TupleCount; i++)
        {
            var pageId = new PageId(i, rightTable.Name);
            var pinResult = await bufferManager.PinPageAsync(pageId);
            if (pinResult.IsError)
            {
                _logger.Error("Failed to pin page {PageId}: {Error}", pageId, pinResult.GetErrorOrThrow());
                return Result<IEnumerable<TJoinResult>, EqualityJoinError>.Error(
                    new EqualityJoinError($"Failed to pin page {pageId}"));
            }

            var page = pinResult.GetValueOrThrow();

            foreach (var tuple in page.Content)
            {
                var record = new TRight();

                record.FromTuple(tuple);

                rightTableTuples[i] = record;
            }
        }

        var sortedRightTableTuples = rightTableTuples
            .OrderBy(tuple => rightJoinSelector.Compile()(tuple))
            .ToList();

        var rightTablePointer = 0;

        var joinResults = new List<TJoinResult>();
        // Table a
        // id,nome, idade
        // 1, joao, 3
        // 2, joao, 5

        // Table b
        // id, pessoa_nome, funcao
        // 1, joao, joao
        // 2, maria, maria

        // join
        // a.id, nome, idade, pessoa_nome, funcao, b.id
        // 1, joao, 3, joao, joao
        // 2, joao, 5, joao, joao

        // FIXME: Confere isso, ve o que tu acha @TalDoFlemis
        foreach (var leftTuple in sortedLeftTableTuples)
        {
            var leftJoinValue = leftJoinSelector.Compile()(leftTuple);

            var rightStart = rightTablePointer;
            var foundMatch = false;

            // Process all right tuples with matching join value
            while (rightTablePointer < sortedRightTableTuples.Count)
            {
                var rightTuple = sortedRightTableTuples.ElementAt(rightTablePointer);
                var rightJoinValue = rightJoinSelector.Compile()(rightTuple);

                var comparison = Comparer<object>.Default.Compare(leftJoinValue, rightJoinValue);

                if (comparison > 0)
                {
                    // Left value is greater, advance right pointer to find potential matches
                    rightTablePointer++;
                    rightStart = rightTablePointer;
                }
                else if (comparison < 0)
                {
                    // Left value is smaller, move to next left tuple
                    break;
                }
                else // Equal values - we have a match
                {
                    foundMatch = true;
                    rightTablePointer++;
                }
            }

            // If we found at least one match, process all matches
            if (foundMatch)
            {
                // Process all right tuples with the matching join value
                for (var j = rightStart; j < rightTablePointer; j++)
                    joinResults.Add(resultSelector(leftTuple, sortedRightTableTuples.ElementAt(j)));

                // Reset right pointer to start of matching partition for next left tuple with same value
                rightTablePointer = rightStart;
            }
        }

        foreach (var leftTuple in sortedLeftTableTuples)
            while (rightTablePointer < sortedRightTableTuples.Count)
            {
                var rightTuple = sortedRightTableTuples.ElementAt(rightTablePointer);
                var leftJoinValue = leftJoinSelector.Compile()(leftTuple);
                var rightJoinValue = rightJoinSelector.Compile()(rightTuple);

                rightTablePointer++;

                if (leftJoinValue.Equals(rightJoinValue))
                    joinResults.Add(resultSelector(leftTuple, rightTuple));
                else
                    break; // No match found, move to next left tuple
            }

        _logger.Information("Sort-merge equality join completed with {JoinResultCount} results",
            joinResults.Count);

        return Result<IEnumerable<TJoinResult>, EqualityJoinError>.Success(joinResults);
    }
}
