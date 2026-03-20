using System;
using System.Linq;
using UchetNZP.Domain.Entities;
using UchetNZP.Web.Infrastructure;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class LookupSearchExtensionsTests
{
    [Theory]
    [InlineData("Втулка ЭС", "Втулка", "ЭСУВТ101")]
    [InlineData("Ток ВТ", "Токарная", "ВТ-01")]
    [InlineData("втулка эсу", "Втулка", "ЭСУВТ101")]
    public void MatchesLookup_FindsSequentialPrefixesAcrossFields(string search, string? primary, string? secondary)
    {
        var matches = LookupSearchExtensions.MatchesLookup(search, primary, secondary);

        Assert.True(matches);
    }

    [Fact]
    public void MatchesLookup_DoesNotMatchMiddleOfValue()
    {
        var matches = LookupSearchExtensions.MatchesLookup("РП", "Корпус", "КРП-01");

        Assert.False(matches);
    }

    [Fact]
    public void WhereMatchesLookup_FindsPartByCodePrefixTokens()
    {
        var parts = new[]
        {
            new Part { Id = Guid.NewGuid(), Name = "Втулка", Code = "ЭСУВТ(ЭРЧМ)" },
            new Part { Id = Guid.NewGuid(), Name = "Корпус", Code = "КРП-01" },
        }.AsQueryable();

        var result = parts
            .WhereMatchesLookup("ЭСУВТ ЭР", part => part.Name, part => part.Code)
            .ToList();

        var part = Assert.Single(result);
        Assert.Equal("Втулка", part.Name);
    }

    [Fact]
    public void WhereMatchesLookup_FindsPartBySequentialPrefixesAcrossNameAndCode()
    {
        var parts = new[]
        {
            new Part { Id = Guid.NewGuid(), Name = "Втулка", Code = "ЭСУВТ101" },
            new Part { Id = Guid.NewGuid(), Name = "Втулка", Code = "КРП-01" },
            new Part { Id = Guid.NewGuid(), Name = "Корпус", Code = "ЭСУВТ101" },
        }.AsQueryable();

        var result = parts
            .WhereMatchesLookup("втул эсу", part => part.Name, part => part.Code)
            .ToList();

        var part = Assert.Single(result);
        Assert.Equal("ЭСУВТ101", part.Code);
    }

    [Fact]
    public void WhereMatchesLookup_DoesNotMatchSubstringInsideField()
    {
        var parts = new[]
        {
            new Part { Id = Guid.NewGuid(), Name = "Корпус", Code = "КРП-01" },
            new Part { Id = Guid.NewGuid(), Name = "Кронштейн", Code = "КН-02" },
        }.AsQueryable();

        var result = parts
            .WhereMatchesLookup("РП", part => part.Name, part => part.Code)
            .ToList();

        Assert.Empty(result);
    }
}
