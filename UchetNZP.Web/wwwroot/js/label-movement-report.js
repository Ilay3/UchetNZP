(function () {
    const namespace = window.UchetNZP;
    if (!namespace) {
        throw new Error("Не инициализировано пространство имён UchetNZP.");
    }

    const partLookup = namespace.initSearchableInput({
        input: document.getElementById("labelMovementPartInput"),
        datalist: document.getElementById("labelMovementPartOptions"),
        hiddenInput: document.getElementById("labelMovementPartId"),
        fetchUrl: "/reports/label-movement/parts",
        minLength: 2,
    });

    const labelSelect = document.getElementById("labelMovementLabelSelect");
    const applyButton = document.getElementById("labelMovementApplyButton");
    const labelHint = document.getElementById("labelMovementLabelHint");
    const selectedLabelId = labelSelect?.value || "";

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

    partLookup.inputElement?.addEventListener("lookup:selected", event => {
        const selected = event.detail;
        void loadLabels(selected?.id);
    });

    partLookup.inputElement?.addEventListener("input", () => {
        if (!partLookup.getSelected()) {
            renderLabels([]);
        }
    });

    labelSelect?.addEventListener("change", updateState);

    void loadLabels(document.getElementById("labelMovementPartId")?.value || "");
    updateState();
})();
