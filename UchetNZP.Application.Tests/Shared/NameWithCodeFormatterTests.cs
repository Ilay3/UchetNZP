using UchetNZP.Shared;
using Xunit;

namespace UchetNZP.Application.Tests.Shared;

public class NameWithCodeFormatterTests
{
    [Theory]
    [InlineData("Вал", "ЭГУ102.03.203", "Вал (ЭГУ102.03.203)")]
    [InlineData("Вал ЭГУ102.03.203", "ЭГУ102.03.203", "Вал ЭГУ102.03.203")]
    [InlineData("Вал\nЭГУ102.03.203", "ЭГУ102.03.203", "Вал ЭГУ102.03.203")]
    [InlineData("Вал ЭГУ102 03 203", "ЭГУ102.03.203", "Вал ЭГУ102 03 203")]
    public void GetNameWithCode_DoesNotDuplicateExistingCode(string partName, string? partCode, string expected)
    {
        var result = NameWithCodeFormatter.getNameWithCode(partName, partCode);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Вал", "ЭГУ102.03.203", true)]
    [InlineData("Вал ЭГУ102.03.203", "ЭГУ102.03.203", false)]
    [InlineData("Вал\nЭГУ102.03.203", "ЭГУ102.03.203", false)]
    [InlineData("Вал ЭГУ102 03 203", "ЭГУ102.03.203", false)]
    public void HasDistinctCode_DetectsWhetherCodeShouldBeShownSeparately(string partName, string? partCode, bool expected)
    {
        var result = NameWithCodeFormatter.HasDistinctCode(partName, partCode);

        Assert.Equal(expected, result);
    }
}
