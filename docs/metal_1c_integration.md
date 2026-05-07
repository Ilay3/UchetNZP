# Интеграция прихода металла и поставщиков с 1С

## Что уже есть в БД

### Справочник поставщиков
Таблица: `MetalSuppliers`
- `Id` (GUID, PK)
- `Identifier` (код поставщика, пример: `00-001828`)
- `Name` (наименование)
- `Inn` (ИНН)
- `IsActive` (активность)
- `CreatedAt`, `UpdatedAt`

### Приход металла (шапка)
Таблица: `MetalReceipts`
- `Id` (GUID, PK)
- `ReceiptNumber` (внутренний номер документа)
- `ReceiptDate` (дата)
- `MetalSupplierId` (FK на `MetalSuppliers.Id`)
- `SupplierDocumentNumber` (номер документа поставщика)
- `InvoiceOrUpiNumber` (накладная/УПД №)
- `AccountingAccount` (счет учета, фиксировано `10.01`)
- `VatAccount` (счет НДС, фиксировано `19.03`)
- `PricePerKg` (цена за кг)
- `AmountWithoutVat` (сумма без НДС)
- `VatRatePercent` (ставка НДС, сейчас 22)
- `VatAmount` (сумма НДС)
- `TotalAmountWithVat` (итого с НДС)
- `Comment`

### Строки прихода
Таблица: `MetalReceiptItems`
- `Id` (GUID, PK)
- `MetalReceiptId` (FK на `MetalReceipts.Id`)
- `MetalMaterialId` (FK на номенклатуру металла)
- `PassportWeightKg` (паспортная масса)
- `Quantity`, `SizeValue`, `SizeUnitText` (внутренние поля, для 1С можно не использовать для количества)

### Номенклатура металла
Таблица: `MetalMaterials`
- `Id` (GUID, PK)
- `Code` (код материала)
- `Name` (наименование)
- `UnitKind`
- `MassPerMeterKg`, `MassPerSquareMeterKg`, `WeightPerUnitKg`
- `Coefficient`
- `IsActive`

## Правила выгрузки в 1С

1. **Новый поставщик** (`MetalSuppliers`) -> создавать/обновлять контрагента в 1С по ключу `ИНН + Наименование`.
2. **Новый металл** (`MetalMaterials`) -> создавать/обновлять элемент номенклатуры 1С по ключу `Code` (если пустой, fallback на `Name`).
3. **Документ прихода** (`MetalReceipts` + `MetalReceiptItems`) -> передавать как приход с агрегированием **по металлу в весе**, без разбиения на количество штук.

## Маппинг документа прихода в 1С

- Дата документа = `MetalReceipts.ReceiptDate`
- Номер документа поставщика = `MetalReceipts.SupplierDocumentNumber`
- Поставщик = `MetalSuppliers` по `MetalReceipts.MetalSupplierId`
- Ставка НДС = `MetalReceipts.VatRatePercent` (по умолчанию 22)

Строки документа 1С:
- Номенклатура = `MetalMaterials` по `MetalReceiptItems.MetalMaterialId`
- Масса (кг) = `SUM(MetalReceiptItems.PassportWeightKg)` по каждой номенклатуре
- Цена за кг = `MetalReceipts.PricePerKg`
- Сумма без НДС = Масса * Цена
- Сумма НДС = СуммаБезНДС * 22%
- Сумма с НДС = СуммаБезНДС + СуммаНДС

## SQL-шаблон для разработчика 1С

```sql
SELECT
    r.Id AS ReceiptId,
    r.ReceiptDate,
    r.SupplierDocumentNumber,
    r.PricePerKg,
    r.VatRatePercent,
    s.Identifier AS SupplierCode,
    s.Name AS SupplierName,
    s.Inn AS SupplierInn,
    m.Code AS MaterialCode,
    m.Name AS MaterialName,
    SUM(i.PassportWeightKg) AS TotalWeightKg,
    SUM(i.PassportWeightKg) * r.PricePerKg AS AmountWithoutVat,
    (SUM(i.PassportWeightKg) * r.PricePerKg) * (r.VatRatePercent / 100.0) AS VatAmount,
    (SUM(i.PassportWeightKg) * r.PricePerKg) * (1 + r.VatRatePercent / 100.0) AS TotalWithVat
FROM MetalReceipts r
JOIN MetalSuppliers s ON s.Id = r.MetalSupplierId
JOIN MetalReceiptItems i ON i.MetalReceiptId = r.Id
JOIN MetalMaterials m ON m.Id = i.MetalMaterialId
GROUP BY
    r.Id, r.ReceiptDate, r.SupplierDocumentNumber, r.PricePerKg, r.VatRatePercent,
    s.Identifier, s.Name, s.Inn,
    m.Code, m.Name;
```
