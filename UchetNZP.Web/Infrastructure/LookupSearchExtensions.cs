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

                var comparison = BuildComparison(body, term.Raw, term.Normalized);
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

                var lowered = value.Trim().ToLowerInvariant();
                if (lowered.Contains(term.Raw, StringComparison.Ordinal))
                {
                    matchesTerm = true;
                    break;
                }

                if (!string.IsNullOrEmpty(term.Normalized) &&
                    NormalizeLookupTerm(lowered).Contains(term.Normalized, StringComparison.Ordinal))
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

    private static IReadOnlyList<LookupTerm> GetLookupTerms(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<LookupTerm>();
        }

        var tokens = value
            .Split((char[]?)null, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(token => new LookupTerm(token.ToLowerInvariant(), NormalizeLookupTerm(token)))
            .Where(token => !string.IsNullOrWhiteSpace(token.Raw) || !string.IsNullOrWhiteSpace(token.Normalized))
            .Distinct()
            .ToArray();

        return tokens;
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

    private sealed record LookupTerm(string Raw, string Normalized);
}
