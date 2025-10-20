using System;
using System.Text.RegularExpressions;
using UchetNZP.Shared;
using Xunit;

namespace UchetNZP.Application.Tests.Shared;

public class OperationNumberTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("7", 7)]
    [InlineData("010", 10)]
    [InlineData("010/1", 10)]
    [InlineData("1234567890", 1234567890)]
    [InlineData("1234567890/12345", 1234567890)]
    public void TryParse_AllowsExpectedFormats(string input, int expected)
    {
        var result = OperationNumber.TryParse(input, out var parsed);

        Assert.True(result);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abc")]
    [InlineData("10/")]
    [InlineData("10//1")]
    [InlineData("10/1/2")]
    [InlineData("12345678901")] // 11 digits
    [InlineData("10/123456")] // больше 5 цифр после дроби
    public void TryParse_ReturnsFalseForInvalidValues(string? input)
    {
        var result = OperationNumber.TryParse(input, out var parsed);

        Assert.False(result);
        Assert.Equal(0, parsed);
    }

    [Fact]
    public void AllowedPattern_IsConsistentWithTryParse()
    {
        var match = Regex.IsMatch("015/2", OperationNumber.AllowedPattern);

        Assert.True(match);
        Assert.True(OperationNumber.TryParse("015/2", out _));
    }

    [Fact]
    public void Parse_ThrowsForInvalidValue()
    {
        var exception = Assert.Throws<ArgumentException>(() => OperationNumber.Parse("invalid", "value"));

        Assert.Contains("должен содержать от 1 до 10 цифр", exception.Message, StringComparison.Ordinal);
        Assert.Equal("value", exception.ParamName);
    }
}
