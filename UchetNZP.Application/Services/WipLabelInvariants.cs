using UchetNZP.Domain.Entities;

namespace UchetNZP.Application.Services;

public static class WipLabelInvariants
{
    private const decimal Epsilon = 0.000001m;

    public static bool IsLive(decimal remainingQuantity) => remainingQuantity > Epsilon;

    public static bool IsClosed(decimal remainingQuantity) => !IsLive(remainingQuantity);

    public static void EnsurePositiveLabelQuantity(decimal quantity)
    {
        if (quantity <= 0m)
        {
            throw new InvalidOperationException("Количество ярлыка должно быть больше нуля.");
        }
    }

    public static void EnsureRemainingWithinBounds(decimal quantity, decimal remainingQuantity)
    {
        EnsurePositiveLabelQuantity(quantity);

        if (remainingQuantity < -Epsilon)
        {
            throw new InvalidOperationException("Остаток ярлыка не может быть отрицательным.");
        }

        if (remainingQuantity - quantity > Epsilon)
        {
            throw new InvalidOperationException("Остаток ярлыка не может превышать исходное количество.");
        }
    }

    public static void EnsurePositiveTransferQuantity(decimal transferQuantity)
    {
        if (transferQuantity <= 0m)
        {
            throw new InvalidOperationException("Количество для перемещения должно быть больше нуля.");
        }
    }

    public static void EnsureCanTransferFromBalance(decimal availableQuantity, decimal transferQuantity, string operationDescription)
    {
        EnsurePositiveTransferQuantity(transferQuantity);

        if (transferQuantity - availableQuantity > Epsilon)
        {
            throw new InvalidOperationException(
                $"Недостаточно остатка НЗП на {operationDescription}. Доступно {availableQuantity}, требуется {transferQuantity}.");
        }
    }

    public static decimal ConsumeLabelQuantity(decimal quantity, decimal remainingQuantity, decimal consumedQuantity, string labelNumber)
    {
        EnsureRemainingWithinBounds(quantity, remainingQuantity);

        if (consumedQuantity < 0m)
        {
            throw new InvalidOperationException("Списываемое количество ярлыка не может быть отрицательным.");
        }

        if (consumedQuantity <= Epsilon)
        {
            return remainingQuantity;
        }

        if (IsClosed(remainingQuantity))
        {
            throw new InvalidOperationException($"Ярлык {labelNumber} закрыт и не может участвовать в перемещении.");
        }

        if (consumedQuantity - remainingQuantity > Epsilon)
        {
            throw new InvalidOperationException(
                $"Нельзя списать с ярлыка {labelNumber} {consumedQuantity}, так как остаток составляет {remainingQuantity}.");
        }

        var remainingAfter = remainingQuantity - consumedQuantity;
        return remainingAfter <= Epsilon ? 0m : remainingAfter;
    }

    public static (string RootNumber, int Suffix) SplitNumber(string labelNumber)
    {
        if (string.IsNullOrWhiteSpace(labelNumber))
        {
            throw new InvalidOperationException("Номер ярлыка не может быть пустым.");
        }

        var normalized = labelNumber.Trim();
        var slashIndex = normalized.IndexOf('/');
        if (slashIndex < 0)
        {
            return (normalized, 0);
        }

        var root = normalized[..slashIndex].Trim();
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("Базовый номер ярлыка не может быть пустым.");
        }

        var suffixPart = normalized[(slashIndex + 1)..].Trim();
        if (!int.TryParse(suffixPart, out var suffix) || suffix < 0)
        {
            throw new InvalidOperationException($"Суффикс номера ярлыка {normalized} должен быть неотрицательным целым числом.");
        }

        return (root, suffix);
    }

    public static WipLabelStatus GetStatusAfterConsume(decimal remainingQuantityAfterConsume)
    {
        return IsClosed(remainingQuantityAfterConsume) ? WipLabelStatus.Consumed : WipLabelStatus.Active;
    }

    public static void EnsureCanTearOff(decimal sourceOperationRemainingQuantity)
    {
        if (sourceOperationRemainingQuantity <= Epsilon)
        {
            throw new InvalidOperationException("Нельзя выполнять отрыв ярлыка, если остаток на исходной операции равен 0.");
        }
    }
}
