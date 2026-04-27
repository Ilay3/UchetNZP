(function () {
    const quantityInput = document.getElementById("Quantity") || document.getElementById("quantityInput");
    const unitsContainer = document.getElementById("unitsContainer");
    const materialInput = document.getElementById("materialInput");
    const materialIdInput = document.getElementById("materialId");
    const materialOptions = document.getElementById("materialOptions");
    const profileTypeSelect = document.getElementById("ProfileType") || document.getElementById("profileTypeSelect");
    const unitTextHint = document.getElementById("unitTextHint");
    const debugStorageKey = "uchetnzp.metalReceipt.lastSubmit";
    const debugContextRaw = document.getElementById("receiptDebugContext")?.textContent ?? "{}";
    const materialProfileMapRaw = document.getElementById("materialProfileMap")?.textContent ?? "{}";
    let materialProfileMap = {};
    let debugContext = {};
    try {
        materialProfileMap = JSON.parse(materialProfileMapRaw);
    } catch {
        materialProfileMap = {};
    }
    try {
        debugContext = JSON.parse(debugContextRaw);
    } catch {
        debugContext = {};
    }

    if (!quantityInput || !unitsContainer || !materialInput || !materialIdInput || !materialOptions || !profileTypeSelect) {
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
        console.debug("[MetalReceipt] syncMaterialSelection", {
            enteredValue: rawValue,
            matched: Boolean(match),
            materialId: materialIdInput.value || null,
        });
    }

    function getSelectedMaterialId() {
        return (materialIdInput.value || "").trim();
    }

    function getSelectedMaterialDisplay() {
        return (materialInput.value || "").trim();
    }

    function syncProfileVisibility() {
        const type = profileTypeSelect.value;
        document.querySelectorAll(".profile-sheet").forEach(el => {
            el.classList.toggle("d-none", type !== "sheet");
        });
        document.querySelectorAll(".profile-rod").forEach(el => {
            el.classList.toggle("d-none", type !== "rod");
        });
        document.querySelectorAll(".profile-pipe").forEach(el => {
            el.classList.toggle("d-none", type !== "pipe");
        });
    }

    function getUnitText() {
        const profileType = profileTypeSelect.value;
        return profileType === "sheet" ? "м2" : "м";
    }

    function toNumber(id) {
        const value = document.getElementById(id)?.value ?? "";
        const parsed = Number.parseFloat(value);
        return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
    }

    function formatMm(value) {
        return `${value.toFixed(3).replace(/\.?0+$/, "")} мм`;
    }

    function resolveActualBlankText(sizeValue) {
        const profileType = profileTypeSelect.value;
        const widthMm = toNumber("WidthMm");
        const lengthMm = toNumber("LengthMm");
        const diameterMm = toNumber("DiameterMm");
        const wallThicknessMm = toNumber("WallThicknessMm");
        const unitText = getUnitText();

        if (profileType === "sheet" && widthMm && lengthMm) {
            return `${formatMm(widthMm)} × ${formatMm(lengthMm)}`;
        }

        if (profileType === "rod" && diameterMm && lengthMm) {
            return `Ø ${formatMm(diameterMm)} × ${formatMm(lengthMm)}`;
        }

        if (profileType === "pipe" && diameterMm && wallThicknessMm && lengthMm) {
            return `Ø ${formatMm(diameterMm)} × ${formatMm(lengthMm)}, стенка ${formatMm(wallThicknessMm)}`;
        }

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
            console.debug("[MetalReceipt] Units not rendered: quantity is empty/invalid", {
                quantity: quantityInput.value,
            });
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

        console.debug("[MetalReceipt] Units rendered", {
            quantity: safeCount,
            unitType: unitText,
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
    materialInput.addEventListener("input", () => {
        syncMaterialSelection();
    });

    materialInput.addEventListener("change", () => {
        syncMaterialSelection();
        const materialId = getSelectedMaterialId();
        const profile = materialProfileMap[materialId];
        if (profile) {
            profileTypeSelect.value = profile;
        }
        syncProfileVisibility();
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
            profileType: profileTypeSelect.value || null,
            passportWeightKg: document.getElementById("PassportWeightKg")?.value || null,
            thicknessMm: document.getElementById("ThicknessMm")?.value || null,
            widthMm: document.getElementById("WidthMm")?.value || null,
            lengthMm: document.getElementById("LengthMm")?.value || null,
            diameterMm: document.getElementById("DiameterMm")?.value || null,
            wallThicknessMm: document.getElementById("WallThicknessMm")?.value || null,
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
            // ignore session storage errors in private mode
        }

        console.group("[MetalReceipt] Submit attempt");
        console.log("Payload preview", payloadPreview);
        console.log("Material selection valid", Boolean(materialIdInput.value));
        console.groupEnd();
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

    profileTypeSelect.addEventListener("change", () => {
        syncProfileVisibility();
        renderUnitInputs();
    });
    document.querySelectorAll(".js-dimension-input").forEach(input => {
        input.addEventListener("input", renderUnitInputs);
    });

    syncMaterialSelection();
    const initialProfile = materialProfileMap[getSelectedMaterialId()];
    if (initialProfile) {
        profileTypeSelect.value = initialProfile;
    }
    syncProfileVisibility();
    renderUnitInputs();

    try {
        const previousSubmitRaw = sessionStorage.getItem(debugStorageKey);
        if (previousSubmitRaw) {
            const previousSubmit = JSON.parse(previousSubmitRaw);
            const modelErrors = Array.from(document.querySelectorAll(".validation-summary-errors li, .field-validation-error"))
                .map(el => (el.textContent || "").trim())
                .filter(Boolean);

            console.group("[MetalReceipt] Previous submit result");
            console.log("Submitted at", previousSubmit?.submittedAt ?? null);
            console.log("Payload preview", previousSubmit?.payloadPreview ?? null);
            if (modelErrors.length > 0) {
                console.warn("Server returned validation errors (save not completed):", modelErrors);
            } else {
                console.log("No validation errors on current page. If page redirected, save likely succeeded.");
            }
            console.groupEnd();

            sessionStorage.removeItem(debugStorageKey);
        }
    } catch {
        // ignore malformed debug payload
    }

    if (debugContext && debugContext.isPostBack === true) {
        console.group("[MetalReceipt] Server POST result");
        console.log("ModelState valid", debugContext.modelStateIsValid === true);
        if (Array.isArray(debugContext.errors) && debugContext.errors.length > 0) {
            console.warn("Server-side errors", debugContext.errors);
        } else if (debugContext.modelStateIsValid === false) {
            console.warn("Server returned POST view without explicit ModelState messages.");
        } else {
            console.log("Server returned POST view with valid ModelState.");
        }
        console.groupEnd();
    }
})();
