(function () {
    const namespace = window.UchetNZP;
    if (!namespace) {
        throw new Error("Не инициализировано пространство имён UchetNZP.");
    }

    const labelNumberPattern = /^\d{1,5}(?:\/\d{1,5})?$/;

    function normalizeLabelNumber(rawValue) {
        if (typeof rawValue !== "string") {
            return "";
        }

        const value = rawValue.trim();
        return labelNumberPattern.test(value) ? value : "";
    }

    const partLookup = namespace.initSearchableInput({
        input: document.getElementById("labelMovementPartInput"),
        datalist: document.getElementById("labelMovementPartOptions"),
        hiddenInput: document.getElementById("labelMovementPartId"),
        fetchUrl: "/reports/label-movement/parts",
        minLength: 2,
    });

    const form = document.getElementById("labelMovementFilterForm");
    const queryLabelInput = document.getElementById("labelMovementQueryLabel");
    const labelSelect = document.getElementById("labelMovementLabelSelect");
    const applyButton = document.getElementById("labelMovementApplyButton");
    const labelHint = document.getElementById("labelMovementLabelHint");
    const searchError = document.getElementById("labelMovementSearchError");
    const selectedLabelId = labelSelect?.value || "";

    function showSearchError(message) {
        if (!searchError) {
            return;
        }

        searchError.textContent = message;
        searchError.classList.remove("d-none");
    }

    function clearSearchError() {
        if (!searchError) {
            return;
        }

        searchError.textContent = "";
        searchError.classList.add("d-none");
    }

    function updateState() {
        const hasPart = Boolean(partLookup.getSelected()?.id || document.getElementById("labelMovementPartId")?.value);
        const hasLabel = Boolean(labelSelect?.value);

        if (labelSelect) {
            labelSelect.disabled = !hasPart;
        }

        if (applyButton) {
            applyButton.disabled = !(hasPart && hasLabel);
        }

        if (labelHint) {
            if (!hasPart) {
                labelHint.textContent = "Сначала выберите деталь.";
            }
            else if (!hasLabel) {
                labelHint.textContent = "Выберите ярлык из списка.";
            }
            else {
                labelHint.textContent = "";
            }
        }
    }

    function renderLabels(items) {
        if (!labelSelect) {
            return;
        }

        labelSelect.innerHTML = "<option value=''>Выберите ярлык</option>";

        items.forEach(item => {
            const option = document.createElement("option");
            option.value = item.id;
            option.textContent = `${item.number} • ${Number(item.remainingQuantity).toLocaleString("ru-RU", { maximumFractionDigits: 3 })} из ${Number(item.quantity).toLocaleString("ru-RU", { maximumFractionDigits: 3 })} шт`;
            labelSelect.appendChild(option);
        });

        if (selectedLabelId && items.some(x => x.id === selectedLabelId)) {
            labelSelect.value = selectedLabelId;
        }

        updateState();
    }

    async function loadLabels(partId) {
        if (!partId || !labelSelect) {
            renderLabels([]);
            return;
        }

        const response = await fetch(`/reports/label-movement/labels?partId=${encodeURIComponent(partId)}`);
        if (!response.ok) {
            renderLabels([]);
            return;
        }

        const data = await response.json();
        renderLabels(Array.isArray(data) ? data : []);
    }

    async function autoSearchByLabel() {
        const rawQueryLabel = queryLabelInput?.value ?? "";
        if (!rawQueryLabel) {
            return;
        }

        const normalizedLabel = normalizeLabelNumber(rawQueryLabel);
        if (!normalizedLabel) {
            showSearchError("Некорректный номер ярлыка. Используйте формат 12345 или 12345/1.");
            return;
        }

        clearSearchError();

        const response = await fetch(`/reports/label-movement/resolve?label=${encodeURIComponent(normalizedLabel)}`);
        if (!response.ok) {
            showSearchError("Не удалось выполнить поиск по номеру ярлыка. Попробуйте позже.");
            return;
        }

        const payload = await response.json();
        if (!payload?.found || !payload.partId || !payload.labelId) {
            showSearchError(`Ярлык ${normalizedLabel} не найден.`);
            return;
        }

        if (partLookup.inputElement) {
            const partCaption = payload.partCode ? `${payload.partName} (${payload.partCode})` : payload.partName;
            partLookup.inputElement.value = partCaption;
        }

        partLookup.setSelected({
            id: payload.partId,
            name: payload.partName,
            code: payload.partCode,
        });

        if (labelSelect) {
            await loadLabels(payload.partId);
            labelSelect.value = payload.labelId;
            updateState();
        }

        const current = new URLSearchParams(window.location.search);
        current.delete("label");
        current.set("partId", payload.partId);
        current.set("labelId", payload.labelId);
        const splitOnlyChecked = document.getElementById("labelMovementSplitOnly")?.checked;
        if (splitOnlyChecked) {
            current.set("splitOnly", "true");
        }
        else {
            current.delete("splitOnly");
        }

        window.location.replace(`${window.location.pathname}?${current.toString()}`);
    }

    partLookup.inputElement?.addEventListener("lookup:selected", event => {
        clearSearchError();
        const selected = event.detail;
        void loadLabels(selected?.id);
    });

    partLookup.inputElement?.addEventListener("input", () => {
        clearSearchError();
        if (!partLookup.getSelected()) {
            renderLabels([]);
        }
    });

    labelSelect?.addEventListener("change", () => {
        clearSearchError();
        updateState();
    });

    form?.addEventListener("submit", () => {
        clearSearchError();
    });

    void loadLabels(document.getElementById("labelMovementPartId")?.value || "");
    updateState();
    void autoSearchByLabel();
})();
