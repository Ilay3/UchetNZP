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
    const paginationContainer = document.getElementById("labelsPagination");
    const modeRadios = document.querySelectorAll('input[name="labelMode"]');
    const manualNumberRow = document.getElementById("labelManualNumberRow");
    const manualNumberInput = document.getElementById("labelNumberInput");
    const countRow = document.getElementById("labelCountRow");
    const editModalElement = document.getElementById("labelEditModal");
    const editModal = editModalElement ? new bootstrap.Modal(editModalElement) : null;
    const editPartName = document.getElementById("labelEditPartName");
    const editNumberInput = document.getElementById("editLabelNumberInput");
    const editDateInput = document.getElementById("editLabelDateInput");
    const editQuantityInput = document.getElementById("editLabelQuantityInput");
    const editMessage = document.getElementById("labelEditMessage");
    const editSaveButton = document.getElementById("labelEditSaveButton");

    const partLookup = namespace.initSearchableInput({
        input: document.getElementById("labelPartInput"),
        hiddenInput: document.getElementById("labelPartId"),
        datalist: document.getElementById("labelPartOptions"),
        fetchUrl: "/wip/labels/parts",
        minLength: 2,
    });

    let isSaving = false;
    let isLoading = false;
    let currentMode = "auto";
    let editLabelId = "";
    let isUpdating = false;
    const pageSize = 25;
    let currentPage = 1;
    let totalPages = 0;
    let totalCount = 0;

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

    function hideEditMessage() {
        if (!editMessage) {
            return;
        }

        if (typeof namespace.hideInlineMessage === "function") {
            namespace.hideInlineMessage(editMessage);
        }
        else {
            editMessage.classList.add("d-none");
            editMessage.textContent = "";
        }
    }

    function showEditMessage(message, type) {
        if (!editMessage) {
            return;
        }

        showMessage(editMessage, message, type);
    }

    function sanitizeNumberValue(value) {
        if (typeof value !== "string") {
            return "";
        }

        const digitsOnly = value.replace(/\D+/g, "").slice(0, 5);
        return digitsOnly;
    }

    function setMode(mode) {
        currentMode = mode === "manual" ? "manual" : "auto";

        const isManual = currentMode === "manual";
        if (manualNumberRow) {
            manualNumberRow.classList.toggle("d-none", !isManual);
        }

        if (countRow) {
            countRow.classList.toggle("d-none", isManual);
        }

        if (countInput) {
            countInput.disabled = isManual;
            if (isManual) {
                countInput.value = "1";
            }
        }

        updateButtonState();
    }

    function getCurrentMode() {
        return currentMode;
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
        cell.colSpan = 6;
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
            const partDisplay = item.partDisplayName
                || formatNameWithCode(item.partName, item.partCode);
            let isoDate = "";
            if (typeof item.labelDate === "string") {
                isoDate = item.labelDate.substring(0, 10);
            }
            else if (item.labelDate) {
                const parsedDate = new Date(item.labelDate);
                if (!Number.isNaN(parsedDate.getTime())) {
                    isoDate = parsedDate.toISOString().substring(0, 10);
                }
            }
            const quantityValue = typeof item.quantity === "number"
                ? item.quantity.toString()
                : String(item.quantity ?? "");

            row.dataset.id = item.id ?? "";
            row.dataset.partId = item.partId ?? "";
            row.dataset.number = item.number ?? "";
            row.dataset.labelDate = isoDate;
            row.dataset.quantity = quantityValue;
            row.dataset.partDisplay = partDisplay ?? "";
            row.dataset.isAssigned = item.isAssigned ? "true" : "false";

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
            partCell.textContent = partDisplay;
            row.appendChild(partCell);

            const statusCell = document.createElement("td");
            statusCell.textContent = item.isAssigned ? "Назначен" : "Свободен";
            row.appendChild(statusCell);

            const actionsCell = document.createElement("td");
            actionsCell.className = "text-end text-nowrap";

            const editButton = document.createElement("button");
            editButton.type = "button";
            editButton.className = "btn btn-link btn-sm text-decoration-none px-1";
            editButton.dataset.action = "edit";
            editButton.title = "Редактировать";
            editButton.setAttribute("aria-label", "Редактировать");
            editButton.innerHTML = "<span aria-hidden=\"true\">✏</span>";

            const deleteButton = document.createElement("button");
            deleteButton.type = "button";
            deleteButton.className = "btn btn-link btn-sm text-danger text-decoration-none px-1";
            deleteButton.dataset.action = "delete";
            deleteButton.title = "Удалить";
            deleteButton.setAttribute("aria-label", "Удалить");
            deleteButton.innerHTML = "<span aria-hidden=\"true\">🗑</span>";

            if (item.isAssigned) {
                editButton.disabled = true;
                deleteButton.disabled = true;
                editButton.title = "Ярлык назначен и не может быть изменён";
                deleteButton.title = "Ярлык назначен и не может быть удалён";
                editButton.classList.add("disabled", "text-muted");
                deleteButton.classList.add("disabled", "text-muted");
                deleteButton.classList.remove("text-danger");
            }

            actionsCell.appendChild(editButton);
            actionsCell.appendChild(deleteButton);
            row.appendChild(actionsCell);

            tableBody.appendChild(row);
        });
    }

    function resetPagination() {
        currentPage = 1;
        totalPages = 0;
        totalCount = 0;

        if (paginationContainer) {
            paginationContainer.innerHTML = "";
            paginationContainer.classList.add("d-none");
        }
    }

    function renderPagination() {
        if (!paginationContainer) {
            return;
        }

        paginationContainer.innerHTML = "";

        if (totalCount <= 0) {
            paginationContainer.classList.add("d-none");
            return;
        }

        const normalizedTotalPages = totalPages > 0 ? totalPages : 1;
        const normalizedPage = Math.min(Math.max(currentPage, 1), normalizedTotalPages);

        paginationContainer.classList.remove("d-none");

        const info = document.createElement("div");
        info.className = "small text-muted flex-grow-1";
        info.textContent = `Страница ${normalizedPage} из ${normalizedTotalPages} (всего ${totalCount})`;
        paginationContainer.appendChild(info);

        const controls = document.createElement("div");
        controls.className = "btn-group btn-group-sm ms-auto";

        const prevButton = document.createElement("button");
        prevButton.type = "button";
        prevButton.className = "btn btn-outline-secondary";
        prevButton.textContent = "Назад";
        prevButton.disabled = normalizedPage <= 1;
        prevButton.addEventListener("click", () => {
            goToPage(normalizedPage - 1);
        });
        controls.appendChild(prevButton);

        const nextButton = document.createElement("button");
        nextButton.type = "button";
        nextButton.className = "btn btn-outline-secondary";
        nextButton.textContent = "Вперёд";
        nextButton.disabled = normalizedPage >= normalizedTotalPages;
        nextButton.addEventListener("click", () => {
            goToPage(normalizedPage + 1);
        });
        controls.appendChild(nextButton);

        paginationContainer.appendChild(controls);
    }

    function goToPage(page) {
        if (isLoading) {
            return;
        }

        const numericPage = Number(page);
        if (!Number.isFinite(numericPage)) {
            return;
        }

        const normalizedPage = Math.max(1, Math.floor(numericPage));
        if (normalizedPage === currentPage) {
            return;
        }

        loadLabels(true, normalizedPage);
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

        if (getCurrentMode() === "manual") {
            const numberValue = sanitizeNumberValue(manualNumberInput?.value ?? "");
            if (!numberValue) {
                return false;
            }

            const numericValue = Number(numberValue);
            if (!Number.isFinite(numericValue) || numericValue <= 0) {
                return false;
            }
        }
        else {
            const count = Number(countInput?.value ?? 0);
            if (!Number.isFinite(count) || count < 1) {
                return false;
            }
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

    async function loadLabels(showErrors, page) {
        if (isLoading) {
            return;
        }

        const shouldShowErrors = showErrors !== false;
        let targetPage = currentPage;
        if (typeof page === "number" && Number.isFinite(page) && page > 0) {
            targetPage = Math.floor(page);
        }
        else if (!Number.isFinite(targetPage) || targetPage < 1) {
            targetPage = 1;
        }

        isLoading = true;
        if (listMessage) {
            namespace.hideInlineMessage?.(listMessage);
        }
        renderLoadingState();
        if (paginationContainer) {
            paginationContainer.classList.add("d-none");
        }

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

            url.searchParams.set("page", String(targetPage));
            url.searchParams.set("pageSize", String(pageSize));

            const response = await fetch(url.toString(), { headers: { "Accept": "application/json" } });
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || "Не удалось загрузить список ярлыков.");
            }

            const data = await response.json();
            const items = Array.isArray(data?.items) ? data.items : [];
            const responsePage = typeof data?.page === "number" && Number.isFinite(data.page)
                ? data.page
                : targetPage;
            const responseTotalPages = typeof data?.totalPages === "number" && Number.isFinite(data.totalPages)
                ? data.totalPages
                : 0;
            const responseTotalCount = typeof data?.totalCount === "number" && Number.isFinite(data.totalCount)
                ? data.totalCount
                : items.length;

            currentPage = responsePage > 0 ? responsePage : 1;
            totalPages = responseTotalPages > 0 ? responseTotalPages : 0;
            totalCount = responseTotalCount >= 0 ? responseTotalCount : items.length;

            if (totalPages > 0 && currentPage > totalPages) {
                currentPage = totalPages;
            }

            if (totalCount === 0) {
                currentPage = 1;
                totalPages = 0;
            }

            renderLabels(items);
            renderPagination();
        }
        catch (error) {
            renderLabels([]);
            resetPagination();
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
        const payload = {
            partId,
            labelDate: dateInput.value,
            quantity,
        };

        let endpoint = "/wip/labels/create";

        if (getCurrentMode() === "manual") {
            const numberValue = sanitizeNumberValue(manualNumberInput?.value ?? "");
            payload.number = numberValue;
            if (manualNumberInput) {
                manualNumberInput.value = numberValue;
            }
            endpoint = "/wip/labels/manual";
        }
        else {
            const count = Number(countInput.value);
            if (count > 1) {
                payload.count = count;
                endpoint = "/wip/labels/batch";
            }
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
            if (getCurrentMode() === "manual" && manualNumberInput) {
                manualNumberInput.value = "";
            }
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

    function updateEditSaveState() {
        if (!editSaveButton) {
            return;
        }

        const numberValue = sanitizeNumberValue(editNumberInput?.value ?? "");
        const dateValue = editDateInput?.value ?? "";
        const quantityValue = Number(editQuantityInput?.value ?? 0);
        const numericNumber = Number(numberValue);
        const isValid = Boolean(editLabelId)
            && Boolean(numberValue)
            && Number.isFinite(numericNumber)
            && numericNumber > 0
            && Boolean(dateValue)
            && Number.isFinite(quantityValue)
            && quantityValue > 0;

        editSaveButton.disabled = !isValid || isUpdating;
    }

    function fillEditModalFromRow(row) {
        if (!row) {
            return;
        }

        editLabelId = row.dataset.id ?? "";

        if (editPartName) {
            editPartName.textContent = row.dataset.partDisplay || "";
        }

        if (editNumberInput) {
            editNumberInput.value = row.dataset.number || "";
        }

        if (editDateInput) {
            editDateInput.value = row.dataset.labelDate || "";
        }

        if (editQuantityInput) {
            editQuantityInput.value = row.dataset.quantity || "";
        }

        hideEditMessage();
        updateEditSaveState();
        editModal?.show();
    }

    async function handleEditSave() {
        if (!editLabelId || isUpdating || !editSaveButton) {
            return;
        }

        const numberValue = sanitizeNumberValue(editNumberInput?.value ?? "");
        const dateValue = editDateInput?.value ?? "";
        const quantityValue = Number(editQuantityInput?.value ?? 0);

        const numericNumber = Number(numberValue);
        if (!numberValue || !Number.isFinite(numericNumber) || numericNumber <= 0 || !dateValue || !Number.isFinite(quantityValue) || quantityValue <= 0) {
            updateEditSaveState();
            return;
        }

        hideEditMessage();
        isUpdating = true;
        updateEditSaveState();

        const payload = {
            id: editLabelId,
            number: numberValue,
            labelDate: dateValue,
            quantity: quantityValue,
        };

        try {
            const response = await fetch(`/wip/labels/${encodeURIComponent(editLabelId)}`, {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload),
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || "Не удалось сохранить изменения.");
            }

            const updatedItem = await response.json();
            const updatedNumber = typeof updatedItem?.number === "string"
                ? updatedItem.number
                : payload.number.padStart(5, "0");
            editModal?.hide();
            await loadLabels(false);
            showMessage(listMessage, `Ярлык ${updatedNumber} обновлён.`, "success");
        }
        catch (error) {
            const message = error instanceof Error ? error.message : "Не удалось сохранить изменения.";
            showEditMessage(message, "danger");
        }
        finally {
            isUpdating = false;
            updateEditSaveState();
        }
    }

    async function handleDeleteLabel(row) {
        if (!row) {
            return;
        }

        const id = row.dataset.id;
        if (!id) {
            return;
        }

        const number = row.dataset.number || "";
        const partDisplay = row.dataset.partDisplay || "";
        const confirmationMessage = number
            ? `Удалить ярлык ${number}${partDisplay ? ` (${partDisplay})` : ""}?`
            : "Удалить выбранный ярлык?";

        if (!window.confirm(confirmationMessage)) {
            return;
        }

        try {
            const response = await fetch(`/wip/labels/${encodeURIComponent(id)}`, { method: "DELETE" });
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || "Не удалось удалить ярлык.");
            }

            await loadLabels(false);
            showMessage(listMessage, number ? `Ярлык ${number} удалён.` : "Ярлык удалён.", "success");
        }
        catch (error) {
            const message = error instanceof Error ? error.message : "Не удалось удалить ярлык.";
            showMessage(listMessage, message, "danger");
        }
    }

    function handleTableClick(event) {
        const target = event.target instanceof HTMLElement
            ? event.target.closest('button[data-action]')
            : null;

        if (!target || target.disabled) {
            return;
        }

        const row = target.closest("tr");
        if (!row) {
            return;
        }

        const action = target.dataset.action;
        if (action === "edit") {
            fillEditModalFromRow(row);
        }
        else if (action === "delete") {
            handleDeleteLabel(row);
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

    if (manualNumberInput) {
        manualNumberInput.addEventListener("input", () => {
            manualNumberInput.value = sanitizeNumberValue(manualNumberInput.value);
            updateButtonState();
        });

        manualNumberInput.addEventListener("blur", () => {
            const value = sanitizeNumberValue(manualNumberInput.value);
            const numeric = Number(value);
            manualNumberInput.value = value && Number.isFinite(numeric) && numeric > 0
                ? numeric.toString().padStart(5, "0")
                : "";
            updateButtonState();
        });
    }

    if (refreshButton) {
        refreshButton.addEventListener("click", () => {
            hideMessages();
            currentPage = 1;
            loadLabels();
        });
    }

    modeRadios.forEach(radio => {
        radio.addEventListener("change", event => {
            if (event.target instanceof HTMLInputElement && event.target.checked) {
                setMode(event.target.value);
            }
        });
    });

    if (partLookup.inputElement) {
        partLookup.inputElement.addEventListener("lookup:selected", () => {
            hideMessages();
            updateButtonState();
            currentPage = 1;
            loadLabels();
        });

        partLookup.inputElement.addEventListener("input", () => {
            if (!partLookup.getSelected()) {
                if (tableBody) {
                    renderLabels([]);
                }
                resetPagination();
            }

            updateButtonState();
        });
    }

    if (tableBody) {
        tableBody.addEventListener("click", handleTableClick);
    }

    if (editSaveButton) {
        editSaveButton.addEventListener("click", handleEditSave);
    }

    [editDateInput, editQuantityInput].forEach(input => {
        if (!input) {
            return;
        }

        input.addEventListener("input", updateEditSaveState);
    });

    if (editNumberInput) {
        editNumberInput.addEventListener("input", () => {
            editNumberInput.value = sanitizeNumberValue(editNumberInput.value);
            updateEditSaveState();
        });

        editNumberInput.addEventListener("blur", () => {
            const value = sanitizeNumberValue(editNumberInput.value);
            const numeric = Number(value);
            editNumberInput.value = value && Number.isFinite(numeric) && numeric > 0
                ? numeric.toString().padStart(5, "0")
                : "";
            updateEditSaveState();
        });
    }

    if (editModalElement) {
        editModalElement.addEventListener("hidden.bs.modal", () => {
            editLabelId = "";
            hideEditMessage();
            if (editNumberInput) {
                editNumberInput.value = "";
            }
            if (editDateInput) {
                editDateInput.value = "";
            }
            if (editQuantityInput) {
                editQuantityInput.value = "";
            }
            updateEditSaveState();
        });

        editModalElement.addEventListener("shown.bs.modal", () => {
            editNumberInput?.focus();
        });
    }

    updateEditSaveState();

    const initialMode = Array.from(modeRadios).find(radio => radio instanceof HTMLInputElement && radio.checked)?.value || "auto";
    setMode(initialMode);
    loadLabels(false);
})();
