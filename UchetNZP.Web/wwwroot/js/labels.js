(function () {
    const namespace = window.UchetNZP;
    if (!namespace) {
        throw new Error("Не инициализировано пространство имён UchetNZP.");
    }

    const formatNameWithCode = typeof namespace.formatNameWithCode === "function"
        ? namespace.formatNameWithCode
        : (name, code) => {
            const normalizedName = typeof name === "string" ? name.trim() : "";
            const normalizedCode = typeof code === "string" ? code.trim() : "";
            let result = normalizedName;

            if (!result && normalizedCode) {
                result = normalizedCode;
            }
            else if (normalizedCode && !result.toLowerCase().includes(normalizedCode.toLowerCase())) {
                result = `${result} (${normalizedCode})`;
            }

            return result;
        };

    const formMessage = document.getElementById("labelsFormMessage");
    const listMessage = document.getElementById("labelsListMessage");
    const dateInput = document.getElementById("labelDateInput");
    const quantityInput = document.getElementById("labelQuantityInput");
    const countInput = document.getElementById("labelCountInput");
    const addButton = document.getElementById("labelAddButton");
    const filterFromInput = document.getElementById("labelsFilterFrom");
    const filterToInput = document.getElementById("labelsFilterTo");
    const refreshButton = document.getElementById("labelsRefreshButton");
    const tableBody = document.querySelector("#labelsTable tbody");

    const partLookup = namespace.initSearchableInput({
        input: document.getElementById("labelPartInput"),
        hiddenInput: document.getElementById("labelPartId"),
        datalist: document.getElementById("labelPartOptions"),
        fetchUrl: "/wip/labels/parts",
        minLength: 2,
    });

    let isSaving = false;
    let isLoading = false;

    function hideMessages() {
        if (typeof namespace.hideInlineMessage === "function") {
            namespace.hideInlineMessage(formMessage);
            namespace.hideInlineMessage(listMessage);
        }
        else if (formMessage) {
            formMessage.classList.add("d-none");
            formMessage.textContent = "";
        }
    }

    function showMessage(target, message, type) {
        if (!target) {
            return;
        }

        const messageType = type || "danger";
        if (typeof namespace.showInlineMessage === "function") {
            namespace.showInlineMessage(target, message, messageType);
            return;
        }

        target.classList.remove("d-none", "alert-success", "alert-danger", "alert-warning", "alert-info");
        target.classList.add(`alert-${messageType}`);
        target.textContent = message;
    }

    function formatDateText(value) {
        let ret = "";
        if (!value) {
            return ret;
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            ret = value;
        }
        else {
            ret = date.toLocaleDateString("ru-RU");
        }

        return ret;
    }

    function formatQuantityText(value) {
        let ret = "0";
        if (typeof value !== "number") {
            const parsed = Number(value);
            if (!Number.isNaN(parsed)) {
                ret = parsed.toLocaleString("ru-RU", { minimumFractionDigits: 0, maximumFractionDigits: 3 });
            }

            return ret;
        }

        ret = value.toLocaleString("ru-RU", { minimumFractionDigits: 0, maximumFractionDigits: 3 });
        return ret;
    }

    function renderEmptyRow(text) {
        if (!tableBody) {
            return;
        }

        const row = document.createElement("tr");
        row.className = "text-center text-muted";
        const cell = document.createElement("td");
        cell.colSpan = 5;
        cell.textContent = text;
        row.appendChild(cell);
        tableBody.innerHTML = "";
        tableBody.appendChild(row);
    }

    function renderLoadingState() {
        renderEmptyRow("Загрузка...");
    }

    function renderLabels(items) {
        if (!tableBody) {
            return;
        }

        tableBody.innerHTML = "";

        if (!Array.isArray(items) || items.length === 0) {
            renderEmptyRow("Нет данных для отображения");
            return;
        }

        items.forEach(item => {
            const row = document.createElement("tr");

            const numberCell = document.createElement("td");
            numberCell.className = "fw-semibold";
            numberCell.textContent = item.number ?? "";
            row.appendChild(numberCell);

            const dateCell = document.createElement("td");
            dateCell.textContent = formatDateText(item.labelDate);
            row.appendChild(dateCell);

            const quantityCell = document.createElement("td");
            quantityCell.textContent = formatQuantityText(item.quantity);
            row.appendChild(quantityCell);

            const partCell = document.createElement("td");
            const partDisplay = item.partDisplayName
                || formatNameWithCode(item.partName, item.partCode);
            partCell.textContent = partDisplay;
            row.appendChild(partCell);

            const statusCell = document.createElement("td");
            statusCell.textContent = item.isAssigned ? "Назначен" : "Свободен";
            row.appendChild(statusCell);

            tableBody.appendChild(row);
        });
    }

    function getSelectedPartId() {
        const selected = typeof partLookup.getSelected === "function" ? partLookup.getSelected() : null;
        let ret = "";
        if (selected && selected.id) {
            ret = String(selected.id);
        }

        return ret;
    }

    function canSubmit() {
        const partId = getSelectedPartId();
        if (!partId) {
            return false;
        }

        if (!dateInput || !dateInput.value) {
            return false;
        }

        const quantity = Number(quantityInput?.value ?? 0);
        if (!Number.isFinite(quantity) || quantity <= 0) {
            return false;
        }

        const count = Number(countInput?.value ?? 0);
        if (!Number.isFinite(count) || count < 1) {
            return false;
        }

        return true;
    }

    function updateButtonState() {
        if (!addButton) {
            return;
        }

        const isDisabled = !canSubmit() || isSaving;
        addButton.disabled = isDisabled;
    }

    async function loadLabels(showErrors) {
        if (isLoading) {
            return;
        }

        const shouldShowErrors = showErrors !== false;
        isLoading = true;
        if (listMessage) {
            namespace.hideInlineMessage?.(listMessage);
        }
        renderLoadingState();

        try {
            const url = new URL("/wip/labels/list", window.location.origin);
            const partId = getSelectedPartId();
            if (partId) {
                url.searchParams.set("partId", partId);
            }

            if (filterFromInput?.value) {
                url.searchParams.set("from", filterFromInput.value);
            }

            if (filterToInput?.value) {
                url.searchParams.set("to", filterToInput.value);
            }

            const response = await fetch(url.toString(), { headers: { "Accept": "application/json" } });
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || "Не удалось загрузить список ярлыков.");
            }

            const data = await response.json();
            const items = Array.isArray(data) ? data : [];
            renderLabels(items);
        }
        catch (error) {
            renderLabels([]);
            if (shouldShowErrors) {
                const message = error instanceof Error ? error.message : "Не удалось загрузить список ярлыков.";
                showMessage(listMessage, message, "danger");
            }
        }
        finally {
            isLoading = false;
        }
    }

    async function handleCreate() {
        if (isSaving || !canSubmit()) {
            return;
        }

        hideMessages();
        isSaving = true;
        updateButtonState();

        const partId = getSelectedPartId();
        const quantity = Number(quantityInput.value);
        const count = Number(countInput.value);
        const payload = {
            partId,
            labelDate: dateInput.value,
            quantity,
        };

        let endpoint = "/wip/labels/create";
        if (count > 1) {
            payload.count = count;
            endpoint = "/wip/labels/batch";
        }

        try {
            const response = await fetch(endpoint, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload),
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || "Не удалось создать ярлыки.");
            }

            const data = await response.json();
            const createdItems = Array.isArray(data) ? data : [data];
            const numbers = createdItems.map(item => item.number).filter(Boolean);
            const successMessage = numbers.length > 0
                ? `Созданы ярлыки: ${numbers.join(", ")}`
                : "Ярлыки успешно созданы.";
            showMessage(formMessage, successMessage, "success");
            await loadLabels(false);
        }
        catch (error) {
            const message = error instanceof Error ? error.message : "Не удалось создать ярлыки.";
            showMessage(formMessage, message, "danger");
        }
        finally {
            isSaving = false;
            updateButtonState();
        }
    }

    if (addButton) {
        addButton.addEventListener("click", handleCreate);
    }

    [dateInput, quantityInput, countInput].forEach(input => {
        if (!input) {
            return;
        }

        input.addEventListener("input", updateButtonState);
    });

    if (refreshButton) {
        refreshButton.addEventListener("click", () => {
            hideMessages();
            loadLabels();
        });
    }

    if (partLookup.inputElement) {
        partLookup.inputElement.addEventListener("lookup:selected", () => {
            hideMessages();
            updateButtonState();
            loadLabels();
        });

        partLookup.inputElement.addEventListener("input", () => {
            if (!partLookup.getSelected()) {
                if (tableBody) {
                    renderLabels([]);
                }
            }

            updateButtonState();
        });
    }

    updateButtonState();
    loadLabels(false);
})();
