using System.Linq.Expressions;
using SubMerge.Models;

namespace SubMerge.Query.Join;

public interface EqualityJoin<TLeft, TRight, TJoinResult>
{
    public Task<Result<IEnumerable<TJoinResult>, EqualityJoinError>> JoinIntoMemory(
        Table leftTable,
        Table rightTable,
        Expression<Func<TLeft, object>> leftJoinSelector,
        Expression<Func<TRight, object>> rightJoinSelector,
        Func<TLeft, TRight, TJoinResult> resultSelector
    );
}

public class EqualityJoinError
{
    public string Message { get; }

    public EqualityJoinError(string message)
    {
        Message = message;
    }
}
