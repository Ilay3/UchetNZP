(function () {
    const quantityInput = document.getElementById("Quantity") || document.getElementById("quantityInput");
    const unitsContainer = document.getElementById("unitsContainer");
    const materialInput = document.getElementById("materialInput");
    const materialIdInput = document.getElementById("materialId");
    const materialOptions = document.getElementById("materialOptions");
    const unitTextHint = document.getElementById("unitTextHint");
    const debugStorageKey = "uchetnzp.metalReceipt.lastSubmit";
    const debugContextRaw = document.getElementById("receiptDebugContext")?.textContent ?? "{}";
    const materialUnitKindMapRaw = document.getElementById("materialUnitKindMap")?.textContent ?? "{}";
    let materialUnitKindMap = {};
    let debugContext = {};

    try {
        materialUnitKindMap = JSON.parse(materialUnitKindMapRaw);
    } catch {
        materialUnitKindMap = {};
    }

    try {
        debugContext = JSON.parse(debugContextRaw);
    } catch {
        debugContext = {};
    }

    if (!quantityInput || !unitsContainer || !materialInput || !materialIdInput || !materialOptions) {
        return;
    }

    const materialsByDisplay = new Map();
    Array.from(materialOptions.querySelectorAll("option")).forEach(option => {
        const text = (option.getAttribute("value") || "").trim();
        const id = (option.getAttribute("data-id") || "").trim();
        if (text && id) {
            materialsByDisplay.set(text.toLowerCase(), { id, text });
        }
    });

    function syncMaterialSelection() {
        const rawValue = (materialInput.value || "").trim();
        if (!rawValue) {
            materialIdInput.value = "";
            return;
        }

        const match = materialsByDisplay.get(rawValue.toLowerCase());
        materialIdInput.value = match?.id || "";
    }

    function getSelectedMaterialId() {
        return (materialIdInput.value || "").trim();
    }

    function getSelectedMaterialDisplay() {
        return (materialInput.value || "").trim();
    }

    function getUnitText() {
        const materialId = getSelectedMaterialId();
        const unitKind = materialUnitKindMap[materialId];
        return unitKind === "SquareMeter" ? "м2" : "м";
    }

    function resolveActualBlankText(sizeValue) {
        const unitText = getUnitText();
        return sizeValue ? `${sizeValue} ${unitText}` : "—";
    }

    function currentUnitValues() {
        const result = new Map();
        unitsContainer.querySelectorAll("input[data-index]").forEach(input => {
            const index = Number.parseInt(input.dataset.index || "", 10);
            if (!Number.isNaN(index)) {
                result.set(index, input.value);
            }
        });
        return result;
    }

    function renderUnitInputs() {
        const count = Number.parseInt(quantityInput.value || "0", 10);
        const safeCount = Number.isNaN(count) || count < 1 ? 0 : Math.min(count, 200);
        const unitText = getUnitText();
        const values = currentUnitValues();

        if (unitTextHint) {
            unitTextHint.textContent = unitText;
        }

        unitsContainer.innerHTML = "";

        if (safeCount === 0) {
            unitsContainer.innerHTML = '<div class="col-12 text-muted">Укажите количество, чтобы заполнить размеры по единицам.</div>';
            return;
        }

        for (let i = 1; i <= safeCount; i += 1) {
            const col = document.createElement("div");
            col.className = "col-md-6";
            col.innerHTML = `
                <div class="border rounded p-3 bg-white">
                    <div class="fw-semibold mb-2">Единица ${i}</div>
                    <label class="form-label">Размер в ${unitText}</label>
                    <input type="hidden" name="Units[${i - 1}].ItemIndex" value="${i}" />
                    <input type="number"
                           id="Units_${i - 1}__SizeValue"
                           class="form-control"
                           name="Units[${i - 1}].SizeValue"
                           data-index="${i}"
                           min="0.001"
                           step="0.001"
                           value="${values.get(i) ?? ""}"
                           />
                    <div class="form-text">Фактический размер заготовки: <span data-actual-size="${i}">${resolveActualBlankText(values.get(i) ?? "")}</span></div>
                    <span class="text-danger field-validation-valid"
                          data-valmsg-for="Units[${i - 1}].SizeValue"
                          data-valmsg-replace="true"></span>
                </div>`;
            unitsContainer.appendChild(col);
        }

        unitsContainer.querySelectorAll("input[data-index]").forEach(input => {
            input.addEventListener("input", () => {
                const index = input.dataset.index;
                const marker = unitsContainer.querySelector(`[data-actual-size="${index}"]`);
                if (marker) {
                    marker.textContent = resolveActualBlankText(input.value);
                }
            });
        });

        if (window.jQuery && window.jQuery.validator && window.jQuery.validator.unobtrusive) {
            const form = document.getElementById("metalReceiptForm");
            if (form) {
                window.jQuery(form).removeData("validator");
                window.jQuery(form).removeData("unobtrusiveValidation");
                window.jQuery.validator.unobtrusive.parse(form);
            }
        }
    }

    quantityInput.addEventListener("input", renderUnitInputs);
    materialInput.addEventListener("input", syncMaterialSelection);
    materialInput.addEventListener("change", () => {
        syncMaterialSelection();
        renderUnitInputs();
    });

    const form = document.getElementById("metalReceiptForm");
    form?.addEventListener("submit", () => {
        syncMaterialSelection();

        const payloadPreview = {
            receiptDate: document.getElementById("ReceiptDate")?.value || null,
            materialDisplay: getSelectedMaterialDisplay(),
            materialId: materialIdInput.value || null,
            quantity: quantityInput.value || null,
            passportWeightKg: document.getElementById("PassportWeightKg")?.value || null,
        };

        if (materialIdInput.value) {
            materialInput.setCustomValidity("");
        } else if (getSelectedMaterialDisplay()) {
            materialInput.setCustomValidity("Выберите материал из списка подсказок.");
        } else {
            materialInput.setCustomValidity("");
        }

        try {
            sessionStorage.setItem(debugStorageKey, JSON.stringify({
                submittedAt: new Date().toISOString(),
                payloadPreview,
            }));
        } catch {
            // ignore
        }
    });

    form?.addEventListener("invalid", event => {
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

    materialInput.addEventListener("input", () => {
        materialInput.setCustomValidity("");
    });

    syncMaterialSelection();
    renderUnitInputs();

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
