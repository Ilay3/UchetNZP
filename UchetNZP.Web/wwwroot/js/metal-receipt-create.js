(function () {
    const quantityInput = document.getElementById("quantityInput");
    const unitsContainer = document.getElementById("unitsContainer");
    const materialSelect = document.getElementById("materialSelect");

    if (!quantityInput || !unitsContainer || !materialSelect) {
        return;
    }

    function getUnitText() {
        const optionText = materialSelect.options[materialSelect.selectedIndex]?.text?.toLowerCase() ?? "";
        return optionText.includes("лист") ? "м2" : "м";
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
                    <div class="fw-semibold mb-2">Единица ${i} → Размер в ${unitText}</div>
                    <input type="hidden" name="Units[${i - 1}].ItemIndex" value="${i}" />
                    <input type="number"
                           id="Units_${i - 1}__SizeValue"
                           class="form-control"
                           name="Units[${i - 1}].SizeValue"
                           data-index="${i}"
                           min="0.001"
                           step="0.001"
                           value="${values.get(i) ?? ""}"
                           required />
                    <span class="text-danger field-validation-valid"
                          data-valmsg-for="Units[${i - 1}].SizeValue"
                          data-valmsg-replace="true"></span>
                </div>`;
            unitsContainer.appendChild(col);
        }

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
    materialSelect.addEventListener("change", renderUnitInputs);
    renderUnitInputs();
})();
