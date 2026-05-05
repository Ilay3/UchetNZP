(function () {
    const form = document.getElementById("metalReceiptForm");
    const itemsContainer = document.getElementById("receiptItemsContainer");
    const addLineButton = document.getElementById("addReceiptLine");
    const lineTemplate = document.getElementById("receiptLineTemplate");
    const materialsRaw = document.getElementById("materialOptionsJson")?.textContent ?? "[]";
    const materialUnitKindMapRaw = document.getElementById("materialUnitKindMap")?.textContent ?? "{}";
    const materialCoefficientMapRaw = document.getElementById("materialCoefficientMap")?.textContent ?? "{}";
    const materialWeightPerUnitMapRaw = document.getElementById("materialWeightPerUnitMap")?.textContent ?? "{}";
    const debugContextRaw = document.getElementById("receiptDebugContext")?.textContent ?? "{}";
    const debugStorageKey = "uchetnzp.metalReceipt.lastSubmit";

    if (!form || !itemsContainer || !addLineButton || !lineTemplate) {
        return;
    }

    let materials = [];
    let materialUnitKindMap = {};
    let debugContext = {};
    let materialCoefficientMap = {};
    let materialWeightPerUnitMap = {};

    try {
        materials = JSON.parse(materialsRaw).map(item => ({
            id: String(item.id || ""),
            text: String(item.text || ""),
            search: String(item.text || "").toLowerCase(),
        })).filter(item => item.id && item.text);
    } catch {
        materials = [];
    }

    try {
        materialUnitKindMap = JSON.parse(materialUnitKindMapRaw);
    } catch {
        materialUnitKindMap = {};
    }

    try {
        materialCoefficientMap = JSON.parse(materialCoefficientMapRaw);
    } catch {
        materialCoefficientMap = {};
    }

    try {
        materialWeightPerUnitMap = JSON.parse(materialWeightPerUnitMapRaw);
    } catch {
        materialWeightPerUnitMap = {};
    }

    try {
        debugContext = JSON.parse(debugContextRaw);
    } catch {
        debugContext = {};
    }

    function normalizeText(value) {
        return String(value || "")
            .trim()
            .toLowerCase()
            .replace(/\s+/g, " ");
    }

    const materialsByDisplay = new Map(materials.map(item => [normalizeText(item.text), item]));

    function escapeHtml(value) {
        return String(value)
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#39;");
    }

    function receiptLines() {
        return Array.from(itemsContainer.querySelectorAll("[data-receipt-line]"));
    }

    function getLineIndex(line) {
        return Number.parseInt(line.dataset.lineIndex || "0", 10) || 0;
    }

    function getMaterialId(line) {
        return (line.querySelector("[data-material-id]")?.value || "").trim();
    }

    function getUnitText(line) {
        const materialId = getMaterialId(line);
        return materialUnitKindMap[materialId] === "SquareMeter" ? "м²" : "м";
    }

    function getMaterialText(line) {
        const input = line.querySelector("[data-material-input]");
        return (input?.value || "").trim();
    }

    function updateLineHeader(line, index = getLineIndex(line)) {
        const materialName = getMaterialText(line);
        const title = line.querySelector("[data-line-title]");
        const summary = line.querySelector("[data-line-summary]");
        if (title) {
            title.textContent = materialName || `Металл ${index + 1}`;
        }

        if (summary) {
            const qty = line.querySelector("[data-quantity-input]")?.value || "0";
            const weight = line.querySelector("[data-weight-input]")?.value || "—";
            summary.textContent = materialName ? `шт: ${qty}, кг: ${weight}` : "Не заполнено";
        }
    }

    function setCollapsed(line, collapsed) {
        line.dataset.collapsed = collapsed ? "true" : "false";
    }


    function setValidationTarget(line, field, index) {
        line.querySelectorAll(`[data-valmsg-for$=".${field}"]`).forEach(span => {
            span.setAttribute("data-valmsg-for", `Items[${index}].${field}`);
        });
    }

    function applyLineIndex(line, index) {
        line.dataset.lineIndex = String(index);
        updateLineHeader(line, index);

        const materialId = line.querySelector("[data-material-id]");
        const weight = line.querySelector("[data-weight-input]");
        const quantity = line.querySelector("[data-quantity-input]");
        const averageHidden = Array.from(line.querySelectorAll("input[type='hidden']"))
            .find(input => /\.UseAverageSize$/.test(input.name));
        const averageCheckbox = line.querySelector("[data-average-checkbox]");
        const averageSize = line.querySelector("[data-average-size-input]");

        if (materialId) materialId.name = `Items[${index}].MetalMaterialId`;
        if (weight) weight.name = `Items[${index}].PassportWeightKg`;
        if (quantity) quantity.name = `Items[${index}].Quantity`;
        if (averageHidden) averageHidden.name = `Items[${index}].UseAverageSize`;
        if (averageCheckbox) averageCheckbox.name = `Items[${index}].UseAverageSize`;
        if (averageSize) averageSize.name = `Items[${index}].AverageSizeValue`;

        setValidationTarget(line, "MetalMaterialId", index);
        setValidationTarget(line, "PassportWeightKg", index);
        setValidationTarget(line, "Quantity", index);
        setValidationTarget(line, "AverageSizeValue", index);
        setValidationTarget(line, "Units", index);
    }

    function currentUnitValues(line) {
        const values = new Map();
        line.querySelectorAll("[data-unit-size]").forEach(input => {
            const index = Number.parseInt(input.dataset.index || "", 10);
            if (!Number.isNaN(index)) {
                values.set(index, input.value);
            }
        });
        return values;
    }

    function renderUnits(line) {
        const lineIndex = getLineIndex(line);
        const unitText = getUnitText(line);
        const averageCheckbox = line.querySelector("[data-average-checkbox]");
        const averageSection = line.querySelector("[data-average-section]");
        const unitsSection = line.querySelector("[data-units-section]");
        const unitsContainer = line.querySelector("[data-units-container]");
        const quantityInput = line.querySelector("[data-quantity-input]");
        const isAverage = averageCheckbox?.checked === true;

        line.querySelectorAll("[data-unit-text]").forEach(item => {
            item.textContent = unitText;
        });

        averageSection?.classList.toggle("d-none", !isAverage);
        unitsSection?.classList.toggle("d-none", isAverage);

        if (!unitsContainer || isAverage) {
            return;
        }

        const count = Number.parseInt(quantityInput?.value || "0", 10);
        const safeCount = Number.isNaN(count) || count < 1 ? 0 : Math.min(count, 200);
        const values = currentUnitValues(line);
        unitsContainer.innerHTML = "";

        if (safeCount === 0) {
            unitsContainer.innerHTML = '<div class="col-12 text-muted">Введите количество, чтобы появились поля размеров.</div>';
            return;
        }

        if (count > 200) {
            unitsContainer.innerHTML = '<div class="col-12"><div class="alert alert-warning mb-0">Для такой большой партии включите среднюю длину. Так не придется заполнять сотни полей.</div></div>';
            return;
        }

        for (let i = 1; i <= safeCount; i += 1) {
            const col = document.createElement("div");
            col.className = "col-sm-6 col-lg-3";
            col.innerHTML = `
                <label class="form-label small mb-1">Штука ${i}</label>
                <input type="hidden" name="Items[${lineIndex}].Units[${i - 1}].ItemIndex" value="${i}" />
                <input class="form-control"
                       type="number"
                       min="0.001"
                       step="0.001"
                       name="Items[${lineIndex}].Units[${i - 1}].SizeValue"
                       value="${escapeHtml(values.get(i) ?? "")}"
                       data-unit-size
                       data-index="${i}" />`;
            unitsContainer.appendChild(col);
        }
    }


    function updateWeightInfo(line) {
        const materialId = getMaterialId(line);
        const coefficientRaw = materialCoefficientMap[materialId];
        const weightPerUnitRaw = materialWeightPerUnitMap[materialId];
        const coefficient = Number.parseFloat(String(coefficientRaw ?? "1").replace(",", "."));
        const weightPerUnit = Number.parseFloat(String(weightPerUnitRaw ?? "0").replace(",", "."));
        const safeCoefficient = Number.isFinite(coefficient) && coefficient > 0 ? coefficient : 1;
        const safeWeightPerUnit = Number.isFinite(weightPerUnit) && weightPerUnit > 0 ? weightPerUnit : 0;

        const passportRaw = line.querySelector("[data-weight-input]")?.value || "";
        const passport = Number.parseFloat(String(passportRaw).replace(",", "."));

        const averageCheckbox = line.querySelector("[data-average-checkbox]");
        const isAverage = averageCheckbox?.checked === true;
        let totalSize = 0;
        if (isAverage) {
            const avgRaw = line.querySelector("[data-average-size-input]")?.value || "";
            const avg = Number.parseFloat(String(avgRaw).replace(",", "."));
            const qty = Number.parseInt(line.querySelector("[data-quantity-input]")?.value || "0", 10);
            if (Number.isFinite(avg) && Number.isFinite(qty) && qty > 0) totalSize = avg * qty;
        } else {
            line.querySelectorAll("[data-unit-size]").forEach(input => {
                const val = Number.parseFloat(String(input.value || "").replace(",", "."));
                if (Number.isFinite(val) && val > 0) totalSize += val;
            });
        }

        const hasPassport = Number.isFinite(passport);
        const calculated = safeWeightPerUnit > 0 && totalSize > 0 ? (safeWeightPerUnit * totalSize * safeCoefficient) : NaN;
        const deviation = hasPassport && Number.isFinite(calculated) ? calculated - passport : NaN;

        const fmt = value => Number.isFinite(value) ? value.toFixed(3).replace(/\.000$/, "") : "—";
        const set = (selector, value) => { const el = line.querySelector(selector); if (el) el.textContent = value; };
        set("[data-info-weight-per-unit]", fmt(safeWeightPerUnit));
        set("[data-info-coefficient]", fmt(safeCoefficient));
        set("[data-info-total-size]", fmt(totalSize));
        set("[data-info-passport]", fmt(passport));
        set("[data-info-calculated]", fmt(calculated));
        set("[data-info-deviation]", fmt(deviation));
    }

    function syncMaterialSelection(line) {
        const input = line.querySelector("[data-material-input]");
        const hidden = line.querySelector("[data-material-id]");
        if (!input || !hidden) {
            return;
        }

        const rawValue = input.value || "";
        const normalizedValue = normalizeText(rawValue);
        const selectedMaterialId = (input.dataset.selectedMaterialId || "").trim();

        let match = materialsByDisplay.get(normalizedValue);
        if (!match && selectedMaterialId) {
            match = materials.find(item => item.id === selectedMaterialId) || null;
        }
        if (!match && normalizedValue) {
            const byId = materials.find(item => item.id.toLowerCase() === normalizedValue);
            const startsWith = materials.filter(item => normalizeText(item.text).startsWith(normalizedValue));
            const contains = materials.filter(item => normalizeText(item.text).includes(normalizedValue));
            match = byId
                || (startsWith.length === 1 ? startsWith[0] : null)
                || (contains.length === 1 ? contains[0] : null);
        }

        const resolvedId = match?.id || hidden.value || selectedMaterialId || "";
        hidden.value = resolvedId;
        input.dataset.selectedMaterialId = resolvedId;
        renderUnits(line);
        updateLineHeader(line);
        updateWeightInfo(line);
    }


    function normalizeDecimalInputValue(input) {
        if (!(input instanceof HTMLInputElement)) {
            return;
        }

        const raw = (input.value || "").trim();
        if (!raw) {
            return;
        }

        const normalized = raw.replace('.', ',');
        if (normalized !== raw) {
            input.value = normalized;
        }
    }

    function normalizeLineDecimalValues(line) {
        line.querySelectorAll("[data-weight-input], [data-average-size-input], [data-unit-size]").forEach(input => {
            normalizeDecimalInputValue(input);
        });
    }

    function closeSuggestions(line) {
        const suggestions = line.querySelector("[data-material-suggestions]");
        if (!suggestions) return;
        suggestions.classList.add("d-none");
        suggestions.innerHTML = "";
    }

    function renderMaterialSuggestions(line) {
        const input = line.querySelector("[data-material-input]");
        const suggestions = line.querySelector("[data-material-suggestions]");
        if (!input || !suggestions) {
            return;
        }

        const query = (input.value || "").trim().toLowerCase();
        if (query.length < 2) {
            closeSuggestions(line);
            return;
        }

        const filtered = materials
            .filter(item => item.search.includes(query))
            .slice(0, 30);

        if (filtered.length === 0) {
            closeSuggestions(line);
            return;
        }

        suggestions.innerHTML = filtered.map(item => `
            <button type="button" class="list-group-item list-group-item-action" data-id="${escapeHtml(item.id)}" data-text="${escapeHtml(item.text)}">
                ${escapeHtml(item.text)}
            </button>`).join("");
        suggestions.classList.remove("d-none");
    }

    function refreshRemoveButtons() {
        const lines = receiptLines();
        lines.forEach(line => {
            const button = line.querySelector("[data-remove-line]");
            if (button) {
                button.classList.toggle("d-none", lines.length <= 1);
            }
        });
    }

    function reindexLines() {
        receiptLines().forEach((line, index) => {
            applyLineIndex(line, index);
            renderUnits(line);
        });
        refreshRemoveButtons();
        reparseValidation();
    }

    function reparseValidation() {
        if (window.jQuery && window.jQuery.validator && window.jQuery.validator.unobtrusive) {
            window.jQuery(form).removeData("validator");
            window.jQuery(form).removeData("unobtrusiveValidation");
            window.jQuery.validator.unobtrusive.parse(form);
        }
    }

    function setupLine(line) {
        if (line.dataset.bound === "true") {
            return;
        }

        line.dataset.bound = "true";
        const materialInput = line.querySelector("[data-material-input]");
        const suggestions = line.querySelector("[data-material-suggestions]");
        const quantityInput = line.querySelector("[data-quantity-input]");
        const averageCheckbox = line.querySelector("[data-average-checkbox]");
        const removeButton = line.querySelector("[data-remove-line]");
        const toggleButton = line.querySelector("[data-line-toggle]");

        materialInput?.addEventListener("input", () => {
            materialInput.setCustomValidity("");
            syncMaterialSelection(line);
            renderMaterialSuggestions(line);
        });
        materialInput?.addEventListener("focus", () => renderMaterialSuggestions(line));
        materialInput?.addEventListener("blur", () => {
            setTimeout(() => closeSuggestions(line), 120);
        });
        materialInput?.addEventListener("change", () => {
            syncMaterialSelection(line);
            renderUnits(line);
        });

        const applySuggestionSelection = event => {
            const button = event.target.closest("button[data-id]");
            if (!button) {
                return;
            }

            event.preventDefault();
            if (materialInput) {
                materialInput.value = button.dataset.text || "";
                materialInput.dataset.selectedMaterialId = button.dataset.id || "";
                materialInput.setCustomValidity("");
            }

            const hidden = line.querySelector("[data-material-id]");
            if (hidden) {
                hidden.value = button.dataset.id || "";
            }

            closeSuggestions(line);
            renderUnits(line);
            updateLineHeader(line);
        updateWeightInfo(line);
        };

        suggestions?.addEventListener("mousedown", applySuggestionSelection);
        suggestions?.addEventListener("click", applySuggestionSelection);

        quantityInput?.addEventListener("input", () => { renderUnits(line); updateLineHeader(line); updateWeightInfo(line); });
        line.querySelectorAll("[data-weight-input], [data-average-size-input], [data-unit-size]").forEach(input => {
            input.addEventListener("change", () => { normalizeDecimalInputValue(input); updateLineHeader(line); updateWeightInfo(line); });
            input.addEventListener("blur", () => { normalizeDecimalInputValue(input); updateLineHeader(line); updateWeightInfo(line); });
        });
        averageCheckbox?.addEventListener("change", () => { renderUnits(line); updateWeightInfo(line); });

        line.addEventListener("input", event => {
            const target = event.target;
            if (target instanceof HTMLInputElement && target.matches("[data-unit-size], [data-average-size-input], [data-weight-input]")) {
                updateWeightInfo(line);
            }
        });

        toggleButton?.addEventListener("click", () => {
            const isCollapsed = line.dataset.collapsed === "true";
            setCollapsed(line, !isCollapsed);
        });
        removeButton?.addEventListener("click", () => {
            if (receiptLines().length <= 1) {
                return;
            }

            line.remove();
            reindexLines();
        });

        syncMaterialSelection(line);
        renderUnits(line);
        updateLineHeader(line);
        updateWeightInfo(line);
    }

    addLineButton.addEventListener("click", () => {
        const index = receiptLines().length;
        const html = lineTemplate.innerHTML
            .replaceAll("__index__", String(index))
            .replaceAll("__number__", String(index + 1));
        const wrapper = document.createElement("div");
        wrapper.innerHTML = html.trim();
        const line = wrapper.firstElementChild;
        itemsContainer.appendChild(line);
        setupLine(line);
        setCollapsed(line, false);
        reindexLines();
        line.querySelector("[data-material-input]")?.focus();
    });

    form.addEventListener("submit", () => {
        const payloadPreview = [];
        receiptLines().forEach(line => {
            normalizeLineDecimalValues(line);
            syncMaterialSelection(line);

            const materialInput = line.querySelector("[data-material-input]");
            const materialId = getMaterialId(line);
            if (materialInput) {
                materialInput.setCustomValidity(materialId ? "" : "Выберите материал из подсказок.");
            }

            payloadPreview.push({
                material: materialInput?.value || null,
                materialId: materialId || null,
                selectedMaterialId: materialInput?.dataset?.selectedMaterialId || null,
                quantity: line.querySelector("[data-quantity-input]")?.value || null,
                useAverageSize: line.querySelector("[data-average-checkbox]")?.checked === true,
            });
        });

        console.log("[MetalReceipt] Submit payload preview", payloadPreview);

        try {
            sessionStorage.setItem(debugStorageKey, JSON.stringify({
                submittedAt: new Date().toISOString(),
                receiptDate: document.getElementById("ReceiptDate")?.value || null,
                lines: payloadPreview,
            }));
        } catch {
            // ignore
        }
    });

    form.addEventListener("invalid", event => {
        const target = event.target;
        if (!(target instanceof HTMLInputElement || target instanceof HTMLSelectElement || target instanceof HTMLTextAreaElement)) {
            return;
        }

        console.warn("[MetalReceipt] Browser blocked submit: invalid field", {
            id: target.id || null,
            name: target.name || null,
            value: target.value,
            validationMessage: target.validationMessage,
        });
    }, true);

    receiptLines().forEach(setupLine);
    reindexLines();

    try {
        const previousSubmitRaw = sessionStorage.getItem(debugStorageKey);
        if (previousSubmitRaw) {
            sessionStorage.removeItem(debugStorageKey);
        }
    } catch {
        // ignore
    }

    if (debugContext && debugContext.isPostBack === true) {
        console.log("[MetalReceipt] ModelState valid", debugContext.modelStateIsValid === true);
    }
})();
