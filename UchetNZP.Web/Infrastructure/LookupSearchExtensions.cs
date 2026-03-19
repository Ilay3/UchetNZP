using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace UchetNZP.Web.Infrastructure;

public static class LookupSearchExtensions
{
    private static readonly string[] IgnoredFragments =
    {
        " ",
        "\t",
        "\r",
        "\n",
        "(",
        ")",
        "[",
        "]",
        "{",
        "}",
        "-",
        "_",
        ".",
        ",",
        "/",
        "\\",
        "\"",
        "'",
        "№",
        ":",
        ";",
    };

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

        var term = search.Trim().ToLowerInvariant();
        var normalizedTerm = NormalizeLookupTerm(term);
        var parameter = Expression.Parameter(typeof(T), "entity");
        Expression? combined = null;

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

            var comparison = BuildComparison(body, term, normalizedTerm);
            combined = combined is null ? comparison : Expression.OrElse(combined, comparison);
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

        var term = search.Trim().ToLowerInvariant();
        var normalizedTerm = NormalizeLookupTerm(term);

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var lowered = value.Trim().ToLowerInvariant();
            if (lowered.Contains(term, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(normalizedTerm) &&
                NormalizeLookupTerm(lowered).Contains(normalizedTerm, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static string NormalizeLookupTerm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Trim().Length);
        foreach (var ch in value.Trim())
        {
            var append = true;
            foreach (var ignored in IgnoredFragments)
            {
                if (ignored.Length == 1 && ignored[0] == ch)
                {
                    append = false;
                    break;
                }
            }

            if (append)
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static Expression BuildComparison(Expression valueExpression, string term, string normalizedTerm)
    {
        var nullCheck = Expression.NotEqual(valueExpression, Expression.Constant(null, typeof(string)));
        var loweredValue = Expression.Call(valueExpression, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
        var containsTerm = Expression.Call(
            loweredValue,
            typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!,
            Expression.Constant(term));

        Expression comparison = containsTerm;

        if (!string.IsNullOrEmpty(normalizedTerm))
        {
            var normalizedValue = loweredValue;
            foreach (var ignored in IgnoredFragments)
            {
                normalizedValue = Expression.Call(
                    normalizedValue,
                    typeof(string).GetMethod(nameof(string.Replace), new[] { typeof(string), typeof(string) })!,
                    Expression.Constant(ignored),
                    Expression.Constant(string.Empty));
            }

            var containsNormalizedTerm = Expression.Call(
                normalizedValue,
                typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!,
                Expression.Constant(normalizedTerm));

            comparison = Expression.OrElse(comparison, containsNormalizedTerm);
        }

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
