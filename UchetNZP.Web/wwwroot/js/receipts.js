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

    const operationsTableBody = document.querySelector("#receiptOperationsTable tbody");
    const cartTableBody = document.querySelector("#receiptCartTable tbody");
    const summaryTableBody = document.querySelector("#receiptSummaryTable tbody");
    const summaryIntro = document.getElementById("receiptSummaryIntro");
    const summaryModalElement = document.getElementById("receiptSummaryModal");
    const balanceLabel = document.getElementById("receiptBalanceLabel");
    const historyTableBody = document.querySelector("#receiptHistoryTable tbody");

    const sectionLookup = namespace.initSearchableInput({
        input: document.getElementById("receiptSectionInput"),
        datalist: document.getElementById("receiptSectionOptions"),
        hiddenInput: document.getElementById("receiptSectionId"),
        fetchUrl: "/wip/receipts/sections",
        minLength: 2,
    });

    const partLookup = namespace.initSearchableInput({
        input: document.getElementById("receiptPartInput"),
        datalist: document.getElementById("receiptPartOptions"),
        hiddenInput: document.getElementById("receiptPartId"),
        fetchUrl: "/wip/receipts/parts",
        minLength: 2,
    });

    const dateInput = document.getElementById("receiptDateInput");
    const quantityInput = document.getElementById("receiptQuantityInput");
    const commentInput = document.getElementById("receiptCommentInput");
    const labelSearchInput = document.getElementById("receiptLabelSearchInput");
    const labelSelect = document.getElementById("receiptLabelSelect");
    const labelHiddenInput = document.getElementById("receiptLabelId");
    const labelMessage = document.getElementById("receiptLabelMessage");
    const addButton = document.getElementById("receiptAddButton");
    const bulkAddButton = document.getElementById("receiptBulkAddButton");
    const saveButton = document.getElementById("receiptSaveButton");
    const resetButton = document.getElementById("receiptResetButton");
    const bulkModalElement = document.getElementById("receiptBulkModal");
    const bulkLabelsInput = document.getElementById("receiptBulkLabelsInput");
    const bulkConfirmButton = document.getElementById("receiptBulkConfirmButton");

    dateInput.value = new Date().toISOString().slice(0, 10);

    addButton.disabled = true;
    saveButton.disabled = true;

    const baselineBalances = new Map();
    const pendingAdjustments = new Map();
    let operations = [];
    let selectedOperation = null;
    let cart = [];
    let history = [];
    let editingIndex = null;
    let isLoadingOperations = false;
    let isLoadingBalance = false;
    let operationsAbortController = null;
    let operationsRequestId = 0;
    let balanceRequestId = 0;
    const balanceRequests = new Map();
    let labels = [];
    let filteredLabels = [];
    let selectedLabel = null;
    let isLoadingLabels = false;
    let labelsAbortController = null;
    let labelsRequestId = 0;
    let loadedLabelsPartId = null;
    let isUpdatingLabels = false;

    const bootstrapModal = summaryModalElement ? new bootstrap.Modal(summaryModalElement) : null;
    const bulkModal = bulkModalElement ? new bootstrap.Modal(bulkModalElement) : null;

    const labelNumberPattern = /^\d{1,5}(?:\/\d{1,5})?$/;

    function getManualLabelNumber()
    {
        if (!labelSearchInput)
        {
            return "";
        }

        const rawValue = typeof labelSearchInput.value === "string" ? labelSearchInput.value.trim() : "";

        if (!rawValue)
        {
            return "";
        }

        if (!labelNumberPattern.test(rawValue))
        {
            return "";
        }

        return rawValue;
    }

    partLookup.inputElement?.addEventListener("lookup:selected", event => {
        const part = event.detail;
        if (!part || !part.id) {
            return;
        }

        void loadLabels(part.id);
        loadOperations(part.id);
        updateFormState();
    });

    sectionLookup.inputElement?.addEventListener("lookup:selected", () => {
        renderOperations();
        updateBalanceLabel();
        updateFormState();
    });

    partLookup.inputElement?.addEventListener("input", () => {
        operations = [];
        selectedOperation = null;
        renderOperations();
        updateBalanceLabel();
        resetLabels();
        updateFormState();
    });

    sectionLookup.inputElement?.addEventListener("input", () => {
        renderOperations();
        updateBalanceLabel();
        updateFormState();
    });

    labelSearchInput?.addEventListener("input", () => {
        filterLabels(labelSearchInput.value);
    });

    labelSelect?.addEventListener("change", () => {
        if (!labelSelect) {
            return;
        }

        const labelId = labelSelect.value;
        if (!labelId) {
            setSelectedLabel(null);
            return;
        }

        const label = labels.find(item => item.id === labelId) || filteredLabels.find(item => item.id === labelId) || null;
        if (!label) {
            setSelectedLabel(null);
            return;
        }

        setSelectedLabel(label);
    });

    function getBalanceKey(partId, sectionId, opNumber) {
        return `${partId}:${sectionId}:${opNumber}`;
    }

    function canAddToCart() {
        const part = partLookup.getSelected();
        const section = sectionLookup.getSelected();

        if (!part || !part.id || !section || !section.id) {
            return false;
        }

        if (!selectedOperation || selectedOperation.sectionId !== section.id) {
            return false;
        }

        if (isLoadingOperations || isLoadingBalance) {
            return false;
        }

        const quantity = Number(quantityInput.value);
        if (!quantity || quantity < 1) {
            return false;
        }

        if (!dateInput.value) {
            return false;
        }

        if (selectedLabel) {
            const labelQuantity = Number(selectedLabel.quantity);
            if (!Number.isFinite(labelQuantity) || Math.abs(labelQuantity - quantity) > 0.000001) {
                return false;
            }
        }
        else {
            const manualLabelNumber = getManualLabelNumber();
            if (!manualLabelNumber) {
                return false;
            }
        }

        return true;
    }

    function canOpenBulkAdd() {
        const part = partLookup.getSelected();
        const section = sectionLookup.getSelected();
        if (!part || !part.id || !section || !section.id) {
            return false;
        }

        if (!selectedOperation || selectedOperation.sectionId !== section.id) {
            return false;
        }

        if (isLoadingOperations || isLoadingBalance) {
            return false;
        }

        const quantity = Number(quantityInput.value);
        return Boolean(quantity && quantity >= 1 && dateInput.value);
    }

    function updateFormState() {
        const canAdd = canAddToCart();
        addButton.disabled = !canAdd;
        if (bulkAddButton) {
            bulkAddButton.disabled = !canOpenBulkAdd();
        }
        saveButton.disabled = cart.length === 0;
        updateLabelControlsState();
        updateLabelAvailabilityMessage();
    }

    function showLabelMessage(text, type = "warning")
    {
        if (!labelMessage)
        {
            return;
        }

        labelMessage.textContent = text;
        labelMessage.classList.remove("d-none");
        labelMessage.classList.remove("alert-warning", "alert-info", "alert-danger");

        let alertClass = "alert-warning";
        if (type === "info")
        {
            alertClass = "alert-info";
        }
        else if (type === "danger")
        {
            alertClass = "alert-danger";
        }

        labelMessage.classList.add(alertClass);
    }

    function hideLabelMessage()
    {
        if (!labelMessage)
        {
            return;
        }

        labelMessage.textContent = "";
        labelMessage.classList.add("d-none");
        labelMessage.classList.remove("alert-warning", "alert-info", "alert-danger");
    }

    function updateLabelAvailabilityMessage()
    {
        if (!labelMessage)
        {
            return;
        }

        const part = partLookup.getSelected();
        if (!part || !part.id)
        {
            hideLabelMessage();
            return;
        }

        if (isLoadingLabels || loadedLabelsPartId !== part.id)
        {
            hideLabelMessage();
            return;
        }

        const manualLabelNumber = getManualLabelNumber();
        const rawInput = labelSearchInput && typeof labelSearchInput.value === "string"
            ? labelSearchInput.value.trim()
            : "";

        if (rawInput && !manualLabelNumber && !labelNumberPattern.test(rawInput))
        {
            showLabelMessage("Номер ярлыка должен быть в формате 12345 или 12345/1.", "danger");
            return;
        }

        if (!selectedLabel && !manualLabelNumber && labels.length === 0)
        {
            const quantity = Number(quantityInput.value);
            let messageText = "Для выбранной детали нет свободных ярлыков. Введите номер ярлыка, чтобы создать его автоматически.";

            if (Number.isFinite(quantity) && quantity > 0)
            {
                messageText = `Для выбранной детали нет свободных ярлыков на ${quantity.toLocaleString("ru-RU")} шт. Введите номер ярлыка, чтобы создать его автоматически.`;
            }

            showLabelMessage(messageText, "warning");
            return;
        }

        hideLabelMessage();
    }

    async function loadOperations(partId) {
        const requestId = ++operationsRequestId;
        if (operationsAbortController) {
            operationsAbortController.abort();
        }

        operationsAbortController = new AbortController();
        const signal = operationsAbortController.signal;

        isLoadingOperations = true;
        updateFormState();
        operationsTableBody.innerHTML = "<tr><td colspan=\"5\" class=\"text-center text-muted\">Загрузка операций...</td></tr>";
        try {
            const response = await fetch(`/wip/receipts/operations?partId=${encodeURIComponent(partId)}`, { signal });
            if (!response.ok) {
                throw new Error("Не удалось загрузить операции детали.");
            }

            const items = await response.json();
            if (requestId !== operationsRequestId) {
                return;
            }

            operations = Array.isArray(items) ? items : [];
            selectedOperation = null;
            renderOperations();
            updateBalanceLabel();
        }
        catch (error) {
            if (signal.aborted) {
                return;
            }

            console.error(error);
            if (requestId !== operationsRequestId) {
                return;
            }

            operationsTableBody.innerHTML = "<tr><td colspan=\"5\" class=\"text-danger text-center\">Ошибка загрузки операций. Попробуйте выбрать деталь ещё раз.</td></tr>";
        }
        finally {
            if (requestId === operationsRequestId) {
                isLoadingOperations = false;
                operationsAbortController = null;
                updateFormState();
            }
        }
    }

    function renderOperations() {
        if (!operations.length) {
            operationsTableBody.innerHTML = "<tr><td colspan=\"5\" class=\"text-center text-muted\">Выберите деталь, чтобы увидеть операции.</td></tr>";
            updateFormState();
            return;
        }

        const selectedSectionId = sectionLookup.hiddenInput?.value || "";
        const normalizedSectionId = selectedSectionId.length ? selectedSectionId : null;

        if (selectedOperation && normalizedSectionId && selectedOperation.sectionId !== normalizedSectionId) {
            selectedOperation = null;
        }

        operationsTableBody.innerHTML = "";
        operations.forEach(operation => {
            const row = document.createElement("tr");
            const belongsToSection = !normalizedSectionId || normalizedSectionId === operation.sectionId;
            if (!belongsToSection) {
                row.classList.add("table-warning");
            }

            if (!belongsToSection && selectedOperation && selectedOperation.opNumber === operation.opNumber) {
                selectedOperation = null;
            }

            const choiceCell = document.createElement("td");
            choiceCell.classList.add("text-center");
            const radio = document.createElement("input");
            radio.type = "radio";
            radio.name = "receiptOperation";
            radio.classList.add("form-check-input", "fs-4");
            radio.setAttribute("aria-label", `Выбрать операцию ${operation.opNumber}`);
            radio.dataset.opNumber = String(operation.opNumber);
            radio.disabled = !belongsToSection;
            if (!belongsToSection) {
                radio.setAttribute("title", "Операция относится к другому виду работ");
            }

            radio.addEventListener("change", () => {
                if (radio.disabled) {
                    return;
                }

                selectedOperation = operation;
                void updateBalanceLabel();
                updateFormState();
            });

            if (selectedOperation && selectedOperation.opNumber === operation.opNumber) {
                radio.checked = true;
            }

            choiceCell.appendChild(radio);

            const numberCell = document.createElement("td");
            numberCell.textContent = operation.opNumber;

            const nameCell = document.createElement("td");
            nameCell.textContent = operation.operationName || "Без названия";

            const normCell = document.createElement("td");
            normCell.textContent = operation.normHours.toFixed(3);

            const sectionCell = document.createElement("td");
            sectionCell.textContent = operation.sectionName || "-";

            row.appendChild(choiceCell);
            row.appendChild(numberCell);
            row.appendChild(nameCell);
            row.appendChild(normCell);
            row.appendChild(sectionCell);

            operationsTableBody.appendChild(row);
        });

        updateFormState();
    }

    async function ensureBalanceLoaded(partId, sectionId, opNumber) {
        const key = getBalanceKey(partId, sectionId, opNumber);
        if (baselineBalances.has(key)) {
            return baselineBalances.get(key);
        }

        if (balanceRequests.has(key)) {
            return balanceRequests.get(key);
        }

        const loadPromise = (async () => {
            try {
                const response = await fetch(`/wip/receipts/balance?partId=${encodeURIComponent(partId)}&sectionId=${encodeURIComponent(sectionId)}&opNumber=${encodeURIComponent(opNumber)}`);
                if (!response.ok) {
                    throw new Error("Не удалось получить остаток по операции.");
                }

                const quantity = await response.json();
                const numeric = typeof quantity === "number" ? quantity : Number(quantity ?? 0);
                baselineBalances.set(key, isNaN(numeric) ? 0 : numeric);
            }
            catch (error) {
                console.error(error);
                baselineBalances.set(key, 0);
            }
            finally {
                balanceRequests.delete(key);
            }

            return baselineBalances.get(key);
        })();

        balanceRequests.set(key, loadPromise);
        return loadPromise;
    }

    async function updateBalanceLabel() {
        const requestId = ++balanceRequestId;
        const part = partLookup.getSelected();
        const section = sectionLookup.getSelected();

        if (!part || !part.id || !section || !section.id || !selectedOperation) {
            balanceLabel.textContent = "0 шт";
            isLoadingBalance = false;
            updateFormState();
            return;
        }

        if (selectedOperation.sectionId !== section.id) {
            balanceLabel.textContent = "—";
            isLoadingBalance = false;
            updateFormState();
            return;
        }

        isLoadingBalance = true;
        updateFormState();

        const key = getBalanceKey(part.id, section.id, selectedOperation.opNumber);
        try {
            const base = await ensureBalanceLoaded(part.id, section.id, selectedOperation.opNumber);
            if (requestId !== balanceRequestId) {
                return;
            }

            const partAfterAwait = partLookup.getSelected();
            const sectionAfterAwait = sectionLookup.getSelected();
            const operationAfterAwait = selectedOperation;
            if (!partAfterAwait || !partAfterAwait.id || !sectionAfterAwait || !sectionAfterAwait.id || !operationAfterAwait) {
                return;
            }

            const currentKey = getBalanceKey(partAfterAwait.id, sectionAfterAwait.id, operationAfterAwait.opNumber);
            if (currentKey !== key) {
                return;
            }

            const pending = pendingAdjustments.get(key) ?? 0;
            const total = (base ?? 0) + pending;
            balanceLabel.textContent = `${total.toLocaleString("ru-RU")} шт`;
        }
        finally {
            if (requestId === balanceRequestId) {
                isLoadingBalance = false;
                updateFormState();
            }
        }
    }

    function normalizeLabel(raw) {
        if (!raw) {
            return null;
        }

        const id = typeof raw.id === "string" ? raw.id : raw.id ? String(raw.id) : "";
        const number = typeof raw.number === "string" ? raw.number.trim() : "";
        const quantityValue = typeof raw.quantity === "number" ? raw.quantity : Number(raw.quantity ?? 0);
        const quantity = Number.isFinite(quantityValue) ? quantityValue : 0;
        const isAssignedFlag = Boolean(raw.isAssigned);

        if (!id || !number) {
            return null;
        }

        return {
            id,
            number,
            quantity,
            isAssigned: isAssignedFlag,
        };
    }

    function renderLabelOptions() {
        if (!labelSelect) {
            return;
        }

        labelSelect.innerHTML = "";

        const placeholder = document.createElement("option");
        placeholder.value = "";
        placeholder.textContent = "Автоматический выбор";
        labelSelect.appendChild(placeholder);

        if (!filteredLabels.length) {
            labelSelect.value = "";
            return;
        }

        const sorted = [...filteredLabels].sort((left, right) => left.number.localeCompare(right.number, "ru", { numeric: true }));
        sorted.forEach(label => {
            const option = document.createElement("option");
            option.value = label.id;
            option.textContent = `${label.number} • ${label.quantity.toLocaleString("ru-RU")} шт`;
            labelSelect.appendChild(option);
        });

        if (selectedLabel && sorted.some(label => label.id === selectedLabel.id)) {
            labelSelect.value = selectedLabel.id;
        }
        else {
            labelSelect.value = "";
        }
    }

    function updateLabelControlsState() {
        const part = partLookup.getSelected();
        const hasPart = Boolean(part && part.id);
        const shouldEnable = hasPart && !isLoadingLabels;

        if (labelSearchInput) {
            labelSearchInput.disabled = !shouldEnable;
        }

        if (labelSelect) {
            labelSelect.disabled = !shouldEnable;
        }
    }

    function filterLabels(term) {
        const normalizedTerm = typeof term === "string" ? term.trim().toLowerCase() : "";

        if (!normalizedTerm) {
            filteredLabels = [...labels];
        }
        else {
            filteredLabels = labels.filter(label => label.number.toLowerCase().includes(normalizedTerm));
        }

        if (!isUpdatingLabels && selectedLabel && !filteredLabels.some(label => label.id === selectedLabel.id)) {
            selectedLabel = null;
            if (labelHiddenInput) {
                labelHiddenInput.value = "";
            }
        }

        renderLabelOptions();
        updateLabelControlsState();
        updateFormState();
    }

    function ensureLabelInList(label) {
        const normalized = normalizeLabel(label);
        if (!normalized) {
            return null;
        }

        const index = labels.findIndex(item => item.id === normalized.id);
        if (index >= 0) {
            labels[index] = normalized;
        }
        else {
            labels.push(normalized);
        }

        return normalized;
    }

    function setSelectedLabel(label) {
        if (!labelHiddenInput) {
            return;
        }

        if (!label) {
            selectedLabel = null;
            labelHiddenInput.value = "";
            if (labelSelect) {
                labelSelect.value = "";
            }
            updateFormState();
            return;
        }

        const normalized = ensureLabelInList(label);
        if (!normalized) {
            selectedLabel = null;
            labelHiddenInput.value = "";
            if (labelSelect) {
                labelSelect.value = "";
            }
            updateFormState();
            return;
        }

        selectedLabel = normalized;
        labelHiddenInput.value = normalized.id;

        const searchTerm = labelSearchInput ? labelSearchInput.value : "";
        isUpdatingLabels = true;
        filterLabels(searchTerm);
        isUpdatingLabels = false;

        if (labelSelect) {
            labelSelect.value = normalized.id;
        }

        updateFormState();
    }

    function resetLabels() {
        labels = [];
        filteredLabels = [];
        loadedLabelsPartId = null;
        setSelectedLabel(null);

        if (labelSearchInput) {
            labelSearchInput.value = "";
        }

        renderLabelOptions();
        updateLabelControlsState();
        hideLabelMessage();
    }

    function removeLabelFromList(labelId) {
        if (!labelId) {
            return;
        }

        labels = labels.filter(item => item.id !== labelId);
        filteredLabels = filteredLabels.filter(item => item.id !== labelId);
        if (selectedLabel && selectedLabel.id === labelId) {
            setSelectedLabel(null);
            renderLabelOptions();
            updateLabelControlsState();
        }
        else {
            renderLabelOptions();
            updateLabelControlsState();
        }
    }

    async function loadLabels(partId, options = {}) {
        if (!partId) {
            resetLabels();
            return;
        }

        const requestId = ++labelsRequestId;

        if (labelsAbortController) {
            labelsAbortController.abort();
        }

        labelsAbortController = new AbortController();
        const signal = labelsAbortController.signal;

        isLoadingLabels = true;
        updateLabelControlsState();

        try {
            const url = new URL("/wip/labels/list", window.location.origin);
            url.searchParams.set("partId", partId);
            const response = await fetch(url.toString(), { signal, headers: { "Accept": "application/json" } });
            if (!response.ok) {
                throw new Error("Не удалось загрузить ярлыки детали.");
            }

            const items = await response.json();
            if (requestId !== labelsRequestId) {
                return;
            }

            loadedLabelsPartId = partId;
            labels = Array.isArray(items)
                ? items
                    .map(normalizeLabel)
                    .filter(item => item && !item.isAssigned)
                : [];

            const ensured = options.ensureLabel ? ensureLabelInList(options.ensureLabel) : null;
            if (ensured && labelSearchInput) {
                labelSearchInput.value = ensured.number;
            }
            else if (!ensured && labelSearchInput) {
                labelSearchInput.value = "";
            }

            const searchTerm = labelSearchInput ? labelSearchInput.value : "";
            isUpdatingLabels = true;
            filterLabels(searchTerm);
            isUpdatingLabels = false;

            setSelectedLabel(ensured);
        }
        catch (error) {
            if (signal.aborted) {
                return;
            }

            console.error(error);
            if (options.ensureLabel) {
                const ensured = ensureLabelInList(options.ensureLabel);
                if (ensured) {
                    if (labelSearchInput) {
                        labelSearchInput.value = ensured.number;
                    }

                    setSelectedLabel(ensured);
                }
            }
            else {
                resetLabels();
            }
        }
        finally {
            if (requestId === labelsRequestId) {
                isLoadingLabels = false;
                labelsAbortController = null;
                updateLabelControlsState();
                updateLabelAvailabilityMessage();
            }
        }
    }

    function renderCart() {
        if (!cart.length) {
            cartTableBody.innerHTML = "<tr><td colspan=\"10\" class=\"text-center text-muted\">Добавьте операции в корзину для сохранения.</td></tr>";
            updateFormState();
            return;
        }

        cartTableBody.innerHTML = "";
        cart.forEach((item, index) => {
            const row = document.createElement("tr");

            row.innerHTML = `
                <td>${item.date}</td>
                <td>${item.sectionName}</td>
                <td>${item.partDisplay}</td>
                <td>${item.operationDisplay}</td>
                <td>${item.isAssigned && item.labelNumber ? item.labelNumber : ""}</td>
                <td>${item.was.toFixed(3)}</td>
                <td>${item.quantity.toFixed(3)}</td>
                <td>${item.become.toFixed(3)}</td>
                <td>${item.comment ?? ""}</td>
                <td class="text-center">
                    <button type="button" class="btn btn-link btn-lg text-decoration-none" data-action="edit" data-index="${index}" aria-label="Изменить запись">✎</button>
                    <button type="button" class="btn btn-link btn-lg text-decoration-none text-danger" data-action="remove" data-index="${index}" aria-label="Удалить запись">✖</button>
                </td>`;

            cartTableBody.appendChild(row);
        });
        updateFormState();
    }

    function renderHistory() {
        if (!historyTableBody) {
            return;
        }

        if (!history.length) {
            historyTableBody.innerHTML = "<tr><td colspan=\"8\" class=\"text-center text-muted\">Сохранённые приходы появятся здесь.</td></tr>";
            return;
        }

        historyTableBody.innerHTML = "";
        history.forEach(item => {
            const row = document.createElement("tr");
            row.innerHTML = `
                <td>${item.date}</td>
                <td>${item.sectionName}</td>
                <td>${item.partDisplay}</td>
                <td>${item.operationDisplay}</td>
                <td>${item.isAssigned && item.labelNumber ? item.labelNumber : ""}</td>
                <td>${item.quantity.toFixed(3)}</td>
                <td>${item.become.toFixed(3)}</td>
                <td class="text-center">
                    <button type="button" class="btn btn-link text-danger text-decoration-none" data-action="cancel" data-receipt-id="${item.receiptId}">Отменить</button>
                </td>`;
            historyTableBody.appendChild(row);
        });
    }

    cartTableBody.addEventListener("click", event => {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }

        const action = target.dataset.action;
        const index = Number(target.dataset.index);
        if (isNaN(index) || index < 0 || index >= cart.length) {
            return;
        }

        if (action === "remove") {
            removeCartItem(index);
        }
        else if (action === "edit") {
            void editCartItem(index);
        }
    });

    if (historyTableBody) {
        historyTableBody.addEventListener("click", event => {
            const target = event.target;
            if (!(target instanceof HTMLElement)) {
                return;
            }

            const button = target.closest("button[data-action=\"cancel\"]");
            if (!(button instanceof HTMLButtonElement)) {
                return;
            }

            const receiptId = button.dataset.receiptId;
            if (!receiptId || button.disabled) {
                return;
            }

            button.disabled = true;
            void cancelSavedReceipt(receiptId, button);
        });
    }

    function removeCartItem(index) {
        const [removed] = cart.splice(index, 1);
        if (removed) {
            const key = getBalanceKey(removed.partId, removed.sectionId, removed.opNumber);
            const current = pendingAdjustments.get(key) ?? 0;
            pendingAdjustments.set(key, current - removed.quantity);
            if (pendingAdjustments.get(key) <= 0) {
                pendingAdjustments.delete(key);
            }

            if (removed.wipLabelId && loadedLabelsPartId && loadedLabelsPartId === removed.partId) {
                const restored = ensureLabelInList({
                    id: removed.wipLabelId,
                    number: removed.labelNumber,
                    quantity: removed.quantity,
                    isAssigned: removed.isAssigned,
                });
                if (restored) {
                    const searchTerm = labelSearchInput ? labelSearchInput.value : "";
                    isUpdatingLabels = true;
                    filterLabels(searchTerm);
                    isUpdatingLabels = false;
                }
            }
        }

        editingIndex = null;
        renderCart();
        updateBalanceLabel();
    }

    async function editCartItem(index) {
        const item = cart.splice(index, 1)[0];
        if (!item) {
            return;
        }

        editingIndex = index;

        const key = getBalanceKey(item.partId, item.sectionId, item.opNumber);
        const current = pendingAdjustments.get(key) ?? 0;
        pendingAdjustments.set(key, current - item.quantity);
        if (pendingAdjustments.get(key) <= 0) {
            pendingAdjustments.delete(key);
        }

        const labelOption = item.wipLabelId
            ? {
                id: item.wipLabelId,
                number: item.labelNumber,
                quantity: item.quantity,
                isAssigned: item.isAssigned,
            }
            : null;

        partLookup.setSelected({ id: item.partId, name: item.partName, code: item.partCode });
        sectionLookup.setSelected({ id: item.sectionId, name: item.sectionName, code: null });
        quantityInput.value = item.quantity;
        commentInput.value = item.comment ?? "";
        dateInput.value = item.date;

        await loadLabels(item.partId, { ensureLabel: labelOption });
        if (!labelOption && labelSearchInput)
        {
            labelSearchInput.value = typeof item.labelNumber === "string" ? item.labelNumber : "";
            filterLabels(labelSearchInput.value);
        }
        await loadOperations(item.partId);
        selectedOperation = operations.find(op => op.opNumber === item.opNumber) ?? null;
        renderOperations();
        renderCart();
        updateBalanceLabel();
    }

    addButton.addEventListener("click", () => addToCart());
    bulkAddButton?.addEventListener("click", () => openBulkModal());
    resetButton.addEventListener("click", () => resetForm());
    saveButton.addEventListener("click", () => saveCart());
    bulkConfirmButton?.addEventListener("click", () => void addBulkToCart());

    quantityInput.addEventListener("input", () => updateFormState());
    dateInput.addEventListener("change", () => updateFormState());
    dateInput.addEventListener("input", () => updateFormState());

    function resetForm() {
        editingIndex = null;
        quantityInput.value = "";
        commentInput.value = "";
        dateInput.value = new Date().toISOString().slice(0, 10);
        selectedOperation = null;
        setSelectedLabel(null);
        if (labelSearchInput)
        {
            labelSearchInput.value = "";
        }
        renderOperations();
        updateBalanceLabel();
        updateFormState();
    }

    async function addToCart() {
        const state = await collectReceiptState();
        if (!state) {
            return;
        }

        const { part, section, quantity, date, partDisplay, operationDisplay, key, base, pending } = state;
        const was = (base ?? 0) + pending;
        const become = was + quantity;
        pendingAdjustments.set(key, pending + quantity);

        const manualLabelNumber = getManualLabelNumber();
        const labelId = selectedLabel ? selectedLabel.id : null;
        const labelNumber = selectedLabel ? selectedLabel.number : (manualLabelNumber || null);
        const labelIsAssigned = selectedLabel ? Boolean(selectedLabel.isAssigned) : Boolean(manualLabelNumber);

        const item = {
            partId: part.id,
            partName: part.name,
            partCode: part.code ?? null,
            sectionId: section.id,
            sectionName: section.name,
            opNumber: selectedOperation.opNumber,
            operationName: selectedOperation.operationName,
            partDisplay,
            operationDisplay,
            date,
            quantity,
            comment: commentInput.value || null,
            was,
            become,
            wipLabelId: labelId,
            labelNumber,
            isAssigned: labelIsAssigned,
        };

        cart.push(item);
        if (labelId) {
            removeLabelFromList(labelId);
            setSelectedLabel(null);
        }
        else if (labelSearchInput)
        {
            labelSearchInput.value = "";
        }
        editingIndex = null;
        renderCart();
        resetForm();
    }

    async function collectReceiptState() {
        const part = partLookup.getSelected();
        const section = sectionLookup.getSelected();

        if (!part || !part.id) {
            alert("Выберите деталь.");
            return null;
        }

        if (!section || !section.id) {
            alert("Выберите вид работ.");
            return null;
        }

        if (!selectedOperation) {
            alert("Выберите операцию детали.");
            return null;
        }

        if (selectedOperation.sectionId !== section.id) {
            alert("Выбранная операция относится к другому виду работ. Выберите корректный вид работ.");
            return null;
        }

        const quantity = Number(quantityInput.value);
        if (!quantity || quantity < 1) {
            alert("Количество прихода должно быть не меньше 1.");
            return null;
        }

        const date = dateInput.value;
        if (!date) {
            alert("Укажите дату прихода.");
            return null;
        }

        const key = getBalanceKey(part.id, section.id, selectedOperation.opNumber);
        const base = await ensureBalanceLoaded(part.id, section.id, selectedOperation.opNumber);
        const pending = pendingAdjustments.get(key) ?? 0;
        const partDisplay = formatNameWithCode(part.name, part.code);
        const operationDisplay = `${selectedOperation.opNumber} ${selectedOperation.operationName ?? ""}`.trim();

        return { part, section, quantity, date, partDisplay, operationDisplay, key, base, pending };
    }

    function openBulkModal() {
        if (!bulkModal) {
            return;
        }

        if (bulkLabelsInput) {
            bulkLabelsInput.value = "";
        }

        bulkModal.show();
    }

    function parseBulkLabelNumbers(rawInput) {
        if (typeof rawInput !== "string") {
            return [];
        }

        const tokens = rawInput
            .split(/[\s,;]+/)
            .map(token => token.trim())
            .filter(token => token.length > 0);

        const unique = [];
        const seen = new Set();
        for (const token of tokens) {
            if (!labelNumberPattern.test(token)) {
                return null;
            }

            if (seen.has(token)) {
                continue;
            }

            seen.add(token);
            unique.push(token);
        }

        return unique;
    }

    async function addBulkToCart() {
        const parsedLabels = parseBulkLabelNumbers(bulkLabelsInput?.value ?? "");
        if (parsedLabels === null) {
            alert("Укажите номера ярлыков в формате 12345 или 12345/1.");
            return;
        }

        if (!parsedLabels.length) {
            alert("Добавьте хотя бы один номер ярлыка для множественного прихода.");
            return;
        }

        const state = await collectReceiptState();
        if (!state) {
            return;
        }

        const { part, section, quantity, date, partDisplay, operationDisplay, key, base, pending } = state;
        let runningPending = pending;

        parsedLabels.forEach(labelNumber => {
            const was = (base ?? 0) + runningPending;
            const become = was + quantity;
            runningPending += quantity;
            cart.push({
                partId: part.id,
                partName: part.name,
                partCode: part.code ?? null,
                sectionId: section.id,
                sectionName: section.name,
                opNumber: selectedOperation.opNumber,
                operationName: selectedOperation.operationName,
                partDisplay,
                operationDisplay,
                date,
                quantity,
                comment: commentInput.value || null,
                was,
                become,
                wipLabelId: null,
                labelNumber,
                isAssigned: true,
            });
        });

        pendingAdjustments.set(key, runningPending);
        bulkModal?.hide();
        renderCart();
        resetForm();
    }

    async function saveCart() {
        if (!cart.length) {
            alert("Корзина пуста.");
            return;
        }

        if (!window.confirm("Сохранить выбранные приходы?")) {
            return;
        }

        const cartSnapshot = cart.map(item => ({ ...item }));

        const payload = {
            items: cart.map(item => ({
                partId: item.partId,
                sectionId: item.sectionId,
                opNumber: item.opNumber,
                receiptDate: item.date,
                quantity: item.quantity,
                comment: item.comment,
                wipLabelId: item.wipLabelId,
                labelNumber: item.labelNumber,
                isAssigned: item.isAssigned,
            })),
        };

        saveButton.disabled = true;
        try {
            const response = await fetch("/wip/receipts/save", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Accept": "application/json",
                },
                body: JSON.stringify(payload),
            });

            if (!response.ok) {
                const errorText = await response.text();
                const normalized = typeof errorText === "string" && errorText.trim().length ? errorText.trim() : null;
                throw new Error(normalized ?? "Не удалось сохранить приходы.");
            }

            const summary = await response.json();
            showSummary(summary);

            cart.forEach(item => {
                const key = getBalanceKey(item.partId, item.sectionId, item.opNumber);
                pendingAdjustments.delete(key);
            });

            cart = [];
            renderCart();
            updateBalanceAfterSave(summary);
            updateHistoryAfterSave(cartSnapshot, summary);
            resetForm();
        }
        catch (error) {
            console.error(error);
            if (error instanceof Error && error.message) {
                alert(error.message);
            }
            else {
                alert("Во время сохранения произошла ошибка. Попробуйте ещё раз.");
            }
        }
        finally {
            saveButton.disabled = false;
        }
    }

    function updateBalanceAfterSave(summary) {
        if (!summary || !summary.items) {
            return;
        }

        summary.items.forEach(item => {
            const key = getBalanceKey(item.partId, item.sectionId, item.opNumber);
            baselineBalances.set(key, item.become);
        });

        updateBalanceLabel();
    }

    function updateHistoryAfterSave(cartSnapshot, summary) {
        if (!historyTableBody || !summary || !Array.isArray(summary.items) || !summary.items.length) {
            return;
        }

        const newEntries = [];

        for (let index = summary.items.length - 1; index >= 0; index -= 1) {
            const item = summary.items[index];
            const snapshot = cartSnapshot[index] ?? null;
            const partDisplay = snapshot ? snapshot.partDisplay : item.partId;
            const sectionName = snapshot ? snapshot.sectionName : "";
            const operationDisplay = snapshot ? snapshot.operationDisplay : item.opNumber;
            const date = snapshot ? snapshot.date : new Date().toISOString().slice(0, 10);
            const quantity = typeof item.quantity === "number" ? item.quantity : Number(item.quantity ?? 0);
            const become = typeof item.become === "number" ? item.become : Number(item.become ?? 0);

            newEntries.push({
                receiptId: item.receiptId,
                balanceId: item.balanceId,
                partId: item.partId,
                sectionId: item.sectionId,
                opNumber: item.opNumber,
                date,
                sectionName,
                partDisplay,
                operationDisplay,
                quantity: isNaN(quantity) ? 0 : quantity,
                become: isNaN(become) ? 0 : become,
                wipLabelId: item.wipLabelId ?? null,
                labelNumber: typeof item.labelNumber === "string" ? item.labelNumber : null,
                isAssigned: Boolean(item.isAssigned),
            });
        }

        if (!newEntries.length) {
            return;
        }

        history = [...newEntries, ...history].slice(0, 10);
        renderHistory();
    }

    async function cancelSavedReceipt(receiptId, button, skipPrompt = false) {
        const index = history.findIndex(entry => entry.receiptId === receiptId);
        if (index < 0) {
            if (button) {
                button.disabled = false;
            }
            return;
        }

        if (!skipPrompt && !window.confirm("Отменить выбранный приход?")) {
            if (button) {
                button.disabled = false;
            }
            return;
        }

        try {
            const response = await fetch(`/wip/receipts/${encodeURIComponent(receiptId)}`, {
                method: "DELETE",
                headers: {
                    "Accept": "application/json",
                },
            });

            if (!response.ok) {
                throw new Error("Ошибка удаления прихода.");
            }

            const result = await response.json();
            const key = getBalanceKey(result.partId, result.sectionId, result.opNumber);
            const restored = typeof result.restoredQuantity === "number"
                ? result.restoredQuantity
                : Number(result.restoredQuantity ?? 0);
            baselineBalances.set(key, isNaN(restored) ? 0 : restored);
            pendingAdjustments.delete(key);

            history.splice(index, 1);
            renderHistory();
            updateBalanceLabel();
        }
        catch (error) {
            console.error(error);
            alert("Не удалось отменить приход. Попробуйте повторить позже.");
            if (button) {
                button.disabled = false;
            }
        }
    }

    function showSummary(summary) {
        summaryTableBody.innerHTML = "";
        summaryIntro.textContent = `Сохранено записей: ${summary.saved}.`;

        summary.items.forEach(item => {
            const row = document.createElement("tr");
            const matchingCartItem = cart.find(x => x.partId === item.partId && x.sectionId === item.sectionId && x.opNumber === item.opNumber && x.quantity === item.quantity);
            const labelDisplay = item.isAssigned && typeof item.labelNumber === "string" && item.labelNumber
                ? item.labelNumber
                : (matchingCartItem && matchingCartItem.isAssigned && matchingCartItem.labelNumber ? matchingCartItem.labelNumber : "");

            row.innerHTML = `
                <td>${matchingCartItem ? matchingCartItem.partDisplay : item.partId}</td>
                <td>${matchingCartItem ? matchingCartItem.operationDisplay : item.opNumber}</td>
                <td>${labelDisplay}</td>
                <td>${item.was.toFixed(3)}</td>
                <td>${item.quantity.toFixed(3)}</td>
                <td>${item.become.toFixed(3)}</td>`;

            summaryTableBody.appendChild(row);
        });

        if (bootstrapModal) {
            bootstrapModal.show();
        }
    }

    updateFormState();
    renderHistory();

    namespace.bindHotkeys({
        onEnter: () => addToCart(),
        onSave: () => saveCart(),
        onCancel: () => resetForm(),
    });
})();
