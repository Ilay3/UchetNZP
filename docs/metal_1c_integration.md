# Интеграция металла с 1С

Источник данных - наша БД. Приложение не отправляет данные в 1С; разработчик 1С забирает их на стороне 1С в режиме чтения.

## Общие правила

- 1С читает данные только через `SELECT`; писать в наши таблицы нельзя.
- В 1С нужно хранить внешний ключ нашей БД: `MetalSuppliers.Id`, `MetalMaterials.Id`, `MetalReceipts.Id`, `MetalReceiptItems.Id`, `MetalIssues.Id`.
- Повторно создавать объект с тем же `Id` нельзя.
- Справочники забираются до документов: поставщики, металлы, потом приходы и расходы.
- Расходы забирать только из `MetalIssues`, где `Status = 'Completed'`.
- Для прихода в 1С не суммировать каждую складскую единицу как отдельную строку веса. В `MetalReceiptItems` несколько единиц могут относиться к одной строке прихода, поэтому строку документа строить по `MetalReceiptId + ReceiptLineIndex + MetalMaterialId`, вес брать один раз из строки группы.

## Поставщики: `MetalSuppliers`

Соответствует справочнику 1С `Контрагенты`.

| Поле | Что значит |
|---|---|
| `Id` | Внешний ключ поставщика из нашей БД |
| `Identifier` | Код поставщика |
| `Name` | Краткое наименование |
| `FullName` | Полное наименование 1С |
| `Inn` | ИНН |
| `Kpp` | КПП |
| `LegalEntityKind` | Тип лица: юрлицо, физлицо, ИП |
| `CountryOfRegistration` | Страна регистрации |
| `Okpo` | ОКПО |
| `MainBankAccount` | Основной банковский счет |
| `MainContractName` | Основной договор |
| `ContactPerson` | Контактное лицо |
| `AdditionalInfo` | Дополнительная информация |
| `Comment` | Комментарий |
| `IsActive` | Активность поставщика |

## Металлы: `MetalMaterials`

Соответствует справочнику 1С `Номенклатура`.

| Поле | Что значит |
|---|---|
| `Id` | Внешний ключ номенклатуры из нашей БД |
| `Code` | Код номенклатуры |
| `Name` | Краткое наименование металла |
| `FullName` | Полное наименование 1С |
| `Article` | Артикул |
| `NomenclatureType` | Вид номенклатуры |
| `UnitOfMeasure` | Единица измерения 1С |
| `NomenclatureGroup` | Номенклатурная группа |
| `VatRateType` | Вид/ставка НДС |
| `CountryOfOrigin` | Страна происхождения |
| `CustomsDeclarationNumber` | Номер ГТД |
| `TnVedCode` | Код ТН ВЭД |
| `Okpd2Code` | Код ОКПД2 |
| `IsService` | Признак услуги; для металла обычно `false` |
| `MassPerMeterKg` | Масса 1 метра, кг |
| `MassPerSquareMeterKg` | Масса 1 м², кг |
| `CoefConsumption` | Коэффициент расхода |
| `StockUnit` | Складская единица: `m`, `m2`, `kg`, `pcs` |
| `WeightPerUnitKg` | Вес одной складской единицы |
| `Coefficient` | Коэффициент пересчета |
| `Comment` | Комментарий |
| `IsActive` | Активность номенклатуры |

## Приходы: `MetalReceipts` + `MetalReceiptItems`

`MetalReceipts` - шапка документа поступления. `MetalReceiptItems` - складские единицы и строки металла.

| Поле `MetalReceipts` | Что значит |
|---|---|
| `Id` | Внешний ключ документа прихода |
| `ReceiptNumber` | Номер прихода в нашей системе |
| `ReceiptDate` | Дата прихода |
| `OrganizationName` | Организация |
| `WarehouseName` | Склад |
| `OperationType` | Вид операции 1С |
| `CurrencyCode` | Валюта |
| `ContractName` | Договор |
| `MetalSupplierId` | Ссылка на поставщика |
| `Supplier*Snapshot` | Данные поставщика, зафиксированные на дату прихода |
| `SupplierDocumentNumber`, `SupplierDocumentDate` | Номер и дата документа поставщика |
| `InvoiceOrUpiNumber` | Накладная/УПД |
| `AccountingAccount`, `VatAccount`, `SettlementAccount`, `AdvanceAccount` | Счета учета |
| `ResponsibleUserName` | Ответственный |
| `BatchNumber` | Партия |
| `PricePerKg`, `AmountWithoutVat`, `VatRatePercent`, `VatAmount`, `TotalAmountWithVat` | Цена и суммы |
| `OriginalDocument*` | Файл первичного документа, опционально |

| Поле `MetalReceiptItems` | Что значит |
|---|---|
| `Id` | Внешний ключ складской единицы |
| `MetalReceiptId` | Ссылка на приход |
| `MetalMaterialId` | Ссылка на металл |
| `ReceiptLineIndex` | Номер строки прихода |
| `ItemIndex` | Номер складской единицы внутри прихода |
| `GeneratedCode` | Уникальный код заготовки/остатка |
| `Quantity` | Количество складских единиц в строке |
| `PassportWeightKg` | Вес строки прихода по документу |
| `PricePerKg` | Цена за кг |
| `SizeValue`, `SizeUnitText`, `ActualBlankSizeText` | Размер заготовки |
| `IsSizeApproximate` | Размер примерный |
| `CalculatedWeightKg`, `WeightDeviationKg` | Расчетный вес и отклонение |
| `StockCategory` | Категория складского остатка |
| `IsConsumed`, `ConsumedAt` | Признак и дата полного расхода |

Безопасная агрегация строк прихода для 1С:

```sql
WITH receipt_lines AS (
    SELECT
        i."MetalReceiptId",
        i."ReceiptLineIndex",
        i."MetalMaterialId",
        MIN(i."Id") AS "SourceLineItemId",
        MAX(i."Quantity") AS "WarehouseUnits",
        MAX(i."PassportWeightKg") AS "WeightKg",
        MAX(i."PricePerKg") AS "PricePerKg"
    FROM "MetalReceiptItems" i
    GROUP BY i."MetalReceiptId", i."ReceiptLineIndex", i."MetalMaterialId"
)
SELECT
    r."Id" AS "ReceiptId",
    r."ReceiptNumber",
    r."ReceiptDate",
    r."OrganizationName",
    r."WarehouseName",
    r."SupplierDocumentNumber",
    r."SupplierDocumentDate",
    r."InvoiceOrUpiNumber",
    s."Id" AS "SupplierId",
    s."Identifier" AS "SupplierCode",
    s."Name" AS "SupplierName",
    m."Id" AS "MaterialId",
    m."Code" AS "MaterialCode",
    m."Name" AS "MaterialName",
    l."ReceiptLineIndex",
    l."WeightKg",
    l."PricePerKg",
    l."WeightKg" * l."PricePerKg" AS "AmountWithoutVat"
FROM "MetalReceipts" r
JOIN "MetalSuppliers" s ON s."Id" = r."MetalSupplierId"
JOIN receipt_lines l ON l."MetalReceiptId" = r."Id"
JOIN "MetalMaterials" m ON m."Id" = l."MetalMaterialId";
```

## Расходы: `MetalIssues` + `MetalIssueItems`

| Поле | Что значит |
|---|---|
| `MetalIssues.Id` | Внешний ключ документа расхода |
| `IssueNumber`, `IssueDate` | Номер и дата расхода |
| `Status` | Статус; для 1С брать только `Completed` |
| `CreatedAt`, `CreatedBy` | Создание документа |
| `CompletedAt`, `CompletedBy` | Проведение документа |
| `MetalIssueItems.MetalReceiptItemId` | Какая складская единица списана |
| `SourceCode` | Код заготовки/остатка |
| `SourceQtyBefore` | Остаток до списания |
| `IssuedQty` | Списанное количество |
| `RemainingQtyAfter` | Остаток после списания |
| `Unit` | Единица измерения |
| `LineStatus` | Полное или частичное списание |

`MetalStockMovements` можно использовать как журнал фактических движений и для инкрементальной проверки: приход имеет `MovementType = 'Receipt'`, расход - `FullConsumption` или `ResidualUpdate`.
