using System.Linq.Expressions;

namespace SupportDesk.Infrastructure.Queries;

public static class QueryableExtensions
{
    /// <summary>
    /// Keeps the rows whose <paramref name="selector"/> part satisfies <paramref name="predicate"/>.
    /// Lets a condition written for one entity be used on a query that wraps it, without writing it
    /// again for every shape the query takes.
    /// </summary>
    public static IQueryable<TSource> WhereOn<TSource, TPart>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, TPart>> selector,
        Expression<Func<TPart, bool>> predicate)
    {
        // Wherever the predicate used its own parameter, use the selected part of the row instead.
        var body = new ParameterReplacer(predicate.Parameters[0], selector.Body).Visit(predicate.Body);

        return source.Where(Expression.Lambda<Func<TSource, bool>>(body, selector.Parameters));
    }

    private sealed class ParameterReplacer(ParameterExpression target, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == target ? replacement : base.VisitParameter(node);
    }
}
