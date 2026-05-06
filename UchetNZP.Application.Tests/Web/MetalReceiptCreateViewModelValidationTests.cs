using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using UchetNZP.Web.Models;
using Xunit;

namespace UchetNZP.Application.Tests.Web;

public class MetalReceiptCreateViewModelValidationTests
{
    [Fact]
    public void Validate_AllowsTinyDecimalValues_WhenCurrentCultureUsesComma()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var ruCulture = CultureInfo.GetCultureInfo("ru-RU");
            CultureInfo.CurrentCulture = ruCulture;
            CultureInfo.CurrentUICulture = ruCulture;

                var model = new MetalReceiptCreateViewModel
                {
                    ReceiptDate = new DateTime(2026, 4, 23),
                    SupplierDocumentNumber = "DOC-1",
                    PricePerKg = 114.04m,
                    MetalMaterialId = Guid.NewGuid(),
                    TotalWeightKg = 0.000001m,
                Quantity = 1,
                Units = new List<MetalReceiptUnitInputViewModel>
                {
                    new()
                    {
                        ItemIndex = 1,
                        SizeValue = 0.000001m,
                    },
                },
            };

            var context = new ValidationContext(model);
            var results = new List<ValidationResult>();

            var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

            Assert.True(isValid);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
