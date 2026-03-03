using UchetNZP.Application.Services;
using UchetNZP.Domain.Entities;
using Xunit;

namespace UchetNZP.Application.Tests.Services;

public class WipLabelInvariantsTests
{
    [Fact]
    public void IsLive_WithPositiveRemaining_ReturnsTrue()
    {
        Assert.True(WipLabelInvariants.IsLive(0.5m));
    }

    [Fact]
    public void IsClosed_WithZeroRemaining_ReturnsTrue()
    {
        Assert.True(WipLabelInvariants.IsClosed(0m));
    }

    [Fact]
    public void EnsurePositiveLabelQuantity_WithZero_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => WipLabelInvariants.EnsurePositiveLabelQuantity(0m));
        Assert.Contains("Количество ярлыка", ex.Message);
    }

    [Fact]
    public void EnsureRemainingWithinBounds_WithNegativeRemaining_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => WipLabelInvariants.EnsureRemainingWithinBounds(10m, -0.1m));
        Assert.Contains("не может быть отрицательным", ex.Message);
    }

    [Fact]
    public void EnsureRemainingWithinBounds_WithRemainingGreaterThanQuantity_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => WipLabelInvariants.EnsureRemainingWithinBounds(10m, 12m));
        Assert.Contains("не может превышать", ex.Message);
    }

    [Fact]
    public void EnsurePositiveTransferQuantity_WithZero_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => WipLabelInvariants.EnsurePositiveTransferQuantity(0m));
        Assert.Contains("перемещения", ex.Message);
    }

    [Fact]
    public void EnsureCanTransferFromBalance_WhenTransferExceedsBalance_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => WipLabelInvariants.EnsureCanTransferFromBalance(5m, 6m, "операции 10"));
        Assert.Contains("Недостаточно остатка", ex.Message);
    }

    [Fact]
    public void ConsumeLabelQuantity_WhenLabelClosed_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => WipLabelInvariants.ConsumeLabelQuantity(10m, 0m, 1m, "100"));
        Assert.Contains("закрыт", ex.Message);
    }

    [Fact]
    public void ConsumeLabelQuantity_WhenConsumedExceedsRemaining_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => WipLabelInvariants.ConsumeLabelQuantity(10m, 3m, 4m, "100"));
        Assert.Contains("Нельзя списать", ex.Message);
    }

    [Fact]
    public void ConsumeLabelQuantity_WhenConsumedEqualsRemaining_ReturnsZero()
    {
        var remaining = WipLabelInvariants.ConsumeLabelQuantity(10m, 3m, 3m, "100");
        Assert.Equal(0m, remaining);
    }

    [Fact]
    public void ConsumeLabelQuantity_WhenConsumedIsValid_ReturnsNewRemaining()
    {
        var remaining = WipLabelInvariants.ConsumeLabelQuantity(10m, 7m, 2m, "100");
        Assert.Equal(5m, remaining);
    }


    [Fact]
    public void ParseNumber_WithSuffix_ReturnsRootAndSuffix()
    {
        var parsed = WipLabelInvariants.ParseNumber("100/3");
        Assert.Equal("100", parsed.RootNumber);
        Assert.Equal(3, parsed.Suffix);
    }


    [Fact]
    public void FormatNumber_WithSuffix_ReturnsText()
    {
        var number = WipLabelInvariants.FormatNumber("100", 2);
        Assert.Equal("100/2", number);
    }

    [Fact]
    public void GetStatusAfterConsume_WithZeroRemaining_ReturnsConsumed()
    {
        var status = WipLabelInvariants.GetStatusAfterConsume(0m);
        Assert.Equal(WipLabelStatus.Consumed, status);
    }

    [Fact]
    public void EnsureCanTearOff_WhenRemainingIsZero_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => WipLabelInvariants.EnsureCanTearOff(0m));
        Assert.Contains("отрыв", ex.Message);
    }
}
