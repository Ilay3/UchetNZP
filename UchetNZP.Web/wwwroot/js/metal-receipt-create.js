(function () {
    const quantityInput = document.getElementById("quantityInput");
    const unitsContainer = document.getElementById("unitsContainer");
    const materialSelect = document.getElementById("materialSelect");
    const materialSearchInput = document.getElementById("materialSearchInput");
    const materialSearchOptions = document.getElementById("materialSearchOptions");
    const profileTypeSelect = document.getElementById("profileTypeSelect");
    const materialProfileMapRaw = document.getElementById("materialProfileMap")?.textContent ?? "{}";
    let materialProfileMap = {};
    try {
        materialProfileMap = JSON.parse(materialProfileMapRaw);
    } catch {
        materialProfileMap = {};
    }

    if (!quantityInput || !unitsContainer || !materialSelect || !profileTypeSelect) {
        return;
    }

    function getMaterialLabel(option) {
        return typeof option?.textContent === "string" ? option.textContent.trim() : "";
    }

    function syncSearchWithSelect() {
        if (!materialSearchInput) {
            return;
        }

        const selectedOption = materialSelect.selectedOptions?.[0];
        materialSearchInput.value = selectedOption ? getMaterialLabel(selectedOption) : "";
    }

    function fillSearchOptions() {
        if (!materialSearchOptions) {
            return;
        }

        materialSearchOptions.innerHTML = "";
        Array.from(materialSelect.options)
            .filter(option => option.value)
            .forEach(option => {
                const entry = document.createElement("option");
                entry.value = getMaterialLabel(option);
                materialSearchOptions.appendChild(entry);
            });
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
    materialSelect.addEventListener("change", () => {
        const materialId = materialSelect.value;
        const profile = materialProfileMap[materialId];
        if (profile) {
            profileTypeSelect.value = profile;
        }
        syncSearchWithSelect();
        syncProfileVisibility();
        renderUnitInputs();
    });

    materialSearchInput?.addEventListener("change", () => {
        const search = materialSearchInput.value.trim().toLowerCase();
        if (!search) {
            return;
        }

        const matchedOption = Array.from(materialSelect.options).find(option => {
            if (!option.value) {
                return false;
            }

            const text = getMaterialLabel(option).toLowerCase();
            return text === search || text.includes(search);
        });

        if (!matchedOption) {
            return;
        }

        materialSelect.value = matchedOption.value;
        materialSelect.dispatchEvent(new Event("change", { bubbles: true }));
    });
    profileTypeSelect.addEventListener("change", () => {
        syncProfileVisibility();
        renderUnitInputs();
    });
    fillSearchOptions();
    syncSearchWithSelect();
    syncProfileVisibility();
    renderUnitInputs();
})();
