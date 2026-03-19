using System;
using System.Linq;
using UchetNZP.Domain.Entities;
using UchetNZP.Web.Infrastructure;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class LookupSearchExtensionsTests
{
    [Theory]
    [InlineData("ЭСУВТ ЭРЧМ", "эсувтэрчм")]
    [InlineData("ЭСУВТ(ЭРЧМ)", "эсувтэрчм")]
    [InlineData("ЭСУВТ-ЭРЧМ", "эсувтэрчм")]
    public void NormalizeLookupTerm_RemovesSeparators(string value, string expected)
    {
        var normalized = LookupSearchExtensions.NormalizeLookupTerm(value);

        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("ЭСУВТ ЭРЧМ", "Втулка", "ЭСУВТ(ЭРЧМ)")]
    [InlineData("ЭСУВТ-ЭРЧМ", "Втулка", "ЭСУВТ ЭРЧМ")]
    [InlineData(" втулка эсувтэрчм ", "Втулка (ЭСУВТ ЭРЧМ)", null)]
    public void MatchesLookup_IgnoresBracketsAndCommonPunctuation(string search, string? primary, string? secondary)
    {
        var matches = LookupSearchExtensions.MatchesLookup(search, primary, secondary);

        Assert.True(matches);
    }

    [Fact]
    public void WhereMatchesLookup_FindsPartByNormalizedCode()
    {
        var parts = new[]
        {
            new Part { Id = Guid.NewGuid(), Name = "Втулка", Code = "ЭСУВТ(ЭРЧМ)" },
            new Part { Id = Guid.NewGuid(), Name = "Корпус", Code = "КРП-01" },
        }.AsQueryable();

        var result = parts
            .WhereMatchesLookup("ЭСУВТ ЭРЧМ", part => part.Name, part => part.Code)
            .ToList();

        var part = Assert.Single(result);
        Assert.Equal("Втулка", part.Name);
    }
}
