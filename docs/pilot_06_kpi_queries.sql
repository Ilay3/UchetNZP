-- KPI-1: точность пересчетов (план vs факт), по периоду и источнику подбора
-- Подставьте :date_from/:date_to (UTC).
SELECT
    mri."SelectionSource"                                   AS selection_source,
    SUM(COALESCE(mri."TotalRequiredWeightKg", 0))          AS planned_weight_kg,
    SUM(COALESCE(cr."ActualProducedMassKg", 0))            AS actual_weight_kg,
    CASE
        WHEN SUM(COALESCE(mri."TotalRequiredWeightKg", 0)) = 0 THEN NULL
        ELSE 1 - ABS(
            SUM(COALESCE(mri."TotalRequiredWeightKg", 0))
            - SUM(COALESCE(cr."ActualProducedMassKg", 0))
        ) / SUM(COALESCE(mri."TotalRequiredWeightKg", 0))
    END                                                      AS accuracy_ratio
FROM "MetalRequirementItems" mri
JOIN "MetalRequirements" mr ON mr."Id" = mri."MetalRequirementId"
LEFT JOIN "CuttingPlans" cp ON cp."MetalRequirementId" = mr."Id" AND cp."IsCurrent" = TRUE
LEFT JOIN "CuttingReports" cr ON cr."CuttingPlanId" = cp."Id"
WHERE mr."RequirementDate" >= :date_from
  AND mr."RequirementDate" <  :date_to
GROUP BY mri."SelectionSource";

-- KPI-2: снижение отхода относительно базового периода
WITH baseline AS (
    SELECT AVG(COALESCE(cp."WastePercent", 0)) AS waste_pct
    FROM "CuttingPlans" cp
    WHERE cp."CreatedAt" >= :baseline_from
      AND cp."CreatedAt" <  :baseline_to
), pilot AS (
    SELECT AVG(COALESCE(cp."WastePercent", 0)) AS waste_pct
    FROM "CuttingPlans" cp
    WHERE cp."CreatedAt" >= :pilot_from
      AND cp."CreatedAt" <  :pilot_to
)
SELECT
    baseline.waste_pct    AS baseline_waste_pct,
    pilot.waste_pct       AS pilot_waste_pct,
    CASE
        WHEN baseline.waste_pct = 0 THEN NULL
        ELSE (baseline.waste_pct - pilot.waste_pct) / baseline.waste_pct
    END                   AS waste_reduction_ratio
FROM baseline, pilot;

-- KPI-3: доля повторно использованных остатков
-- Остаток считается повторно использованным, если позже фигурирует
-- как SourceMetalReceiptItemId в CuttingReports.
WITH residuals AS (
    SELECT
        mri."Id"                        AS residual_item_id,
        COALESCE(mri."ActualWeightKg", 0) AS residual_weight_kg,
        mri."CreatedAt"                 AS created_at
    FROM "MetalReceiptItems" mri
    WHERE lower(mri."StockCategory") = 'residual'
      AND mri."CreatedAt" >= :pilot_from
      AND mri."CreatedAt" <  :pilot_to
), reused AS (
    SELECT DISTINCT cr."SourceMetalReceiptItemId" AS residual_item_id
    FROM "CuttingReports" cr
    WHERE cr."CreatedAt" >= :pilot_from
      AND cr."CreatedAt" <  :pilot_to
)
SELECT
    SUM(r.residual_weight_kg) AS all_residual_weight_kg,
    SUM(CASE WHEN ru.residual_item_id IS NOT NULL THEN r.residual_weight_kg ELSE 0 END) AS reused_residual_weight_kg,
    CASE
        WHEN SUM(r.residual_weight_kg) = 0 THEN NULL
        ELSE SUM(CASE WHEN ru.residual_item_id IS NOT NULL THEN r.residual_weight_kg ELSE 0 END)
             / SUM(r.residual_weight_kg)
    END AS reuse_share
FROM residuals r
LEFT JOIN reused ru ON ru.residual_item_id = r.residual_item_id;
