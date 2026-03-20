using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace UchetNZP.Web.Infrastructure;

public static class LookupSearchExtensions
{
    private static readonly char[] TokenSeparators =
    [
        ' ',
        '\t',
        '-',
        '_',
        '/',
        '\\',
        '.',
        ',',
        ';',
        ':',
        '(',
        ')',
        '[',
        ']',
    ];

    public static IQueryable<T> WhereMatchesLookup<T>(
        this IQueryable<T> query,
        string? search,
        params Expression<Func<T, string?>>[] selectors)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (selectors is null || selectors.Length == 0 || string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var terms = GetLookupTerms(search);
        if (terms.Count == 0)
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "entity");
        Expression? combined = null;

        foreach (var term in terms)
        {
            Expression? termComparison = null;

            foreach (var selector in selectors)
            {
                if (selector is null)
                {
                    continue;
                }

                var body = new ReplaceParameterVisitor(selector.Parameters[0], parameter)
                    .Visit(selector.Body);

                if (body is null)
                {
                    continue;
                }

                var comparison = BuildComparison(body, term);
                termComparison = termComparison is null
                    ? comparison
                    : Expression.OrElse(termComparison, comparison);
            }

            if (termComparison is null)
            {
                continue;
            }

            combined = combined is null
                ? termComparison
                : Expression.AndAlso(combined, termComparison);
        }

        if (combined is null)
        {
            return query;
        }

        var predicate = Expression.Lambda<Func<T, bool>>(combined, parameter);
        return query.Where(predicate);
    }

    public static bool MatchesLookup(string? search, params string?[] values)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var terms = GetLookupTerms(search);
        if (terms.Count == 0)
        {
            return true;
        }

        foreach (var term in terms)
        {
            var matchesTerm = false;

            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (GetLookupSegments(value).Any(segment => segment.StartsWith(term, StringComparison.CurrentCultureIgnoreCase)))
                {
                    matchesTerm = true;
                    break;
                }
            }

            if (!matchesTerm)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<string> GetLookupTerms(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split((char[]?)null, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> GetLookupSegments(string value)
    {
        return value
            .Trim()
            .Split(TokenSeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static Expression BuildComparison(Expression valueExpression, string term)
    {
        var nullCheck = Expression.NotEqual(valueExpression, Expression.Constant(null, typeof(string)));
        var trimmedValue = Expression.Call(valueExpression, typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes)!);
        var normalizedValue = Expression.Call(trimmedValue, typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!);
        var normalizedTerm = term.ToUpper();

        Expression comparison = Expression.Call(
            normalizedValue,
            typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!,
            Expression.Constant(normalizedTerm));

        foreach (var separator in TokenSeparators.Where(ch => !char.IsWhiteSpace(ch)))
        {
            comparison = Expression.OrElse(
                comparison,
                Expression.Call(
                    normalizedValue,
                    typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!,
                    Expression.Constant(separator + normalizedTerm)));
        }

        comparison = Expression.OrElse(
            comparison,
            Expression.Call(
                normalizedValue,
                typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!,
                Expression.Constant(" " + normalizedTerm)));

        return Expression.AndAlso(nullCheck, comparison);
    }

    private sealed class ReplaceParameterVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        public ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }
}
