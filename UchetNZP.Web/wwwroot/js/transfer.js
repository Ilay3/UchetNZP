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

    const partLookup = namespace.initSearchableInput({
        input: document.getElementById("transferPartInput"),
        datalist: document.getElementById("transferPartOptions"),
        hiddenInput: document.getElementById("transferPartId"),
        fetchUrl: "/wip/transfer/parts",
        minLength: 2,
    });

    const fromOperationInput = document.getElementById("transferFromOperationInput");
    const fromOperationOptions = document.getElementById("transferFromOperationOptions");
    const fromOperationNumberInput = document.getElementById("transferFromOperationNumber");
    const toOperationInput = document.getElementById("transferToOperationInput");
    const toOperationOptions = document.getElementById("transferToOperationOptions");
    const toOperationNumberInput = document.getElementById("transferToOperationNumber");
    const dateInput = document.getElementById("transferDateInput");
    const quantityInput = document.getElementById("transferQuantityInput");
    const commentInput = document.getElementById("transferCommentInput");
    const fromBalanceLabel = document.getElementById("transferFromBalanceLabel");
    const toBalanceLabel = document.getElementById("transferToBalanceLabel");
    const fromLabelsElement = document.getElementById("transferFromLabels");
    const toLabelsElement = document.getElementById("transferToLabels");
    const addButton = document.getElementById("transferAddButton");
    const resetButton = document.getElementById("transferResetButton");
    const saveButton = document.getElementById("transferSaveButton");
    const cartTableBody = document.querySelector("#transferCartTable tbody");
    const summaryModalElement = document.getElementById("transferSummaryModal");
    const summaryTableBody = document.querySelector("#transferSummaryTable tbody");
    const summaryIntro = document.getElementById("transferSummaryIntro");
    const scrapModalElement = document.getElementById("transferScrapModal");
    const scrapIntro = document.getElementById("transferScrapIntro");
    const scrapPrimaryActions = document.getElementById("transferScrapPrimaryActions");
    const scrapDetails = document.getElementById("transferScrapDetails");
    const scrapMarkButton = document.getElementById("transferScrapMarkButton");
    const scrapKeepButton = document.getElementById("transferScrapKeepButton");
    const scrapConfirmButton = document.getElementById("transferScrapConfirmButton");
    const scrapCommentInput = document.getElementById("transferScrapComment");
    const scrapTypeButtons = Array.from(scrapDetails?.querySelectorAll("[data-scrap-type]") ?? []);
    const recentTableBody = document.querySelector("#transferRecentTable tbody");
    const recentTableWrapper = document.getElementById("transferRecentTableWrapper");
    const recentEmptyPlaceholder = document.getElementById("transferRecentEmpty");

    const bootstrapModal = summaryModalElement ? new bootstrap.Modal(summaryModalElement) : null;
    const scrapModal = scrapModalElement ? new bootstrap.Modal(scrapModalElement, { backdrop: "static", keyboard: false }) : null;

    fromOperationInput.disabled = true;
    toOperationInput.disabled = true;
    quantityInput.disabled = true;
    commentInput.disabled = true;
    addButton.disabled = true;
    saveButton.disabled = true;

    const scrapTypeOptions = {
        technological: { value: 0, code: "Technological", label: "Технологический" },
        employee: { value: 1, code: "EmployeeFault", label: "По вине сотрудника" },
    };

    const RECENT_TRANSFER_LIMIT = 10;

    let scrapModalResolver = null;
    let scrapSelectedTypeKey = null;

    const today = new Date().toISOString().slice(0, 10);
    if (dateInput) {
        dateInput.value = today;
    }

    let operations = [];
    let selectedFromOperation = null;
    let selectedToOperation = null;
    let balanceRequestId = 0;
    let operationsAbortController = null;
    let operationsRequestId = 0;
    let balancesAbortController = null;

    const balanceCache = new Map();
    const balanceLabels = new Map();
    const pendingChanges = new Map();
    let cart = [];
    let recentTransfers = [];
    let isLoadingOperations = false;
    let isLoadingBalances = false;

    partLookup.inputElement?.addEventListener("lookup:selected", event => {
        const part = event.detail;
        if (part && part.id) {
            void loadOperations(part.id);
        }
        updateFormState();
    });

    partLookup.inputElement?.addEventListener("input", () => {
        selectedFromOperation = null;
        selectedToOperation = null;
        fromOperationInput.value = "";
        fromOperationNumberInput.value = "";
        toOperationInput.value = "";
        toOperationNumberInput.value = "";
        operations = [];
        updateFromOperationsDatalist();
        updateToOperationsDatalist();
        updateBalanceLabels();
        updateFormState();
    });

    fromOperationInput.addEventListener("input", () => {
        selectedFromOperation = null;
        fromOperationNumberInput.value = "";
        updateToOperationsDatalist();
        updateBalanceLabels();
        updateFormState();
    });

    toOperationInput.addEventListener("input", () => {
        selectedToOperation = null;
        toOperationNumberInput.value = "";
        updateBalanceLabels();
        updateFormState();
    });

    fromOperationInput.addEventListener("change", () => {
        const value = fromOperationInput.value.trim();
        const operation = operations.find(x => formatOperation(x) === value);
        if (operation) {
            selectedFromOperation = operation;
            fromOperationNumberInput.value = operation.opNumber;
            if (selectedToOperation && !selectedToOperation.isWarehouse && parseOpNumber(selectedToOperation.opNumber) <= parseOpNumber(operation.opNumber)) {
                selectedToOperation = null;
                toOperationInput.value = "";
                toOperationNumberInput.value = "";
            }
            updateToOperationsDatalist();
            void refreshBalances();
        }
        else {
            selectedFromOperation = null;
            fromOperationNumberInput.value = "";
            updateToOperationsDatalist();
            updateBalanceLabels();
        }
        updateFormState();
    });

    toOperationInput.addEventListener("change", () => {
        const value = toOperationInput.value.trim();
        const operation = operations.find(x => formatOperation(x) === value);
        if (!operation) {
            selectedToOperation = null;
            toOperationNumberInput.value = "";
            updateBalanceLabels();
            updateFormState();
            return;
        }

        if (selectedFromOperation && !operation.isWarehouse && parseOpNumber(operation.opNumber) <= parseOpNumber(selectedFromOperation.opNumber)) {
            alert("Операция после должна быть позже операции до.");
            toOperationInput.value = "";
            toOperationNumberInput.value = "";
            selectedToOperation = null;
            updateBalanceLabels();
            updateFormState();
            return;
        }

        selectedToOperation = operation;
        toOperationNumberInput.value = operation.opNumber;
        void refreshBalances();
        updateFormState();
    });

    addButton.addEventListener("click", () => void addToCart());
    resetButton.addEventListener("click", () => resetForm());
    saveButton.addEventListener("click", () => void saveCart());
    quantityInput.addEventListener("input", () => updateFormState());
    dateInput?.addEventListener("change", () => updateFormState());
    dateInput?.addEventListener("input", () => updateFormState());

    cartTableBody.addEventListener("click", event => {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }

        const index = Number(target.dataset.index);
        if (Number.isNaN(index) || index < 0 || index >= cart.length) {
            return;
        }

        if (target.dataset.action === "remove") {
            removeCartItem(index);
        }
        else if (target.dataset.action === "edit") {
            void editCartItem(index);
        }
    });

    recentTableBody?.addEventListener("click", event => {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }

        if (target.dataset.action !== "cancel") {
            return;
        }

        const index = Number(target.dataset.index);
        if (Number.isNaN(index) || index < 0 || index >= recentTransfers.length) {
            return;
        }

        void cancelRecentTransfer(index);
    });

    scrapMarkButton?.addEventListener("click", () => {
        if (!scrapDetails || !scrapPrimaryActions) {
            return;
        }

        scrapPrimaryActions.classList.add("d-none");
        scrapDetails.classList.remove("d-none");
        scrapSelectedTypeKey = null;
        updateScrapTypeButtons();
        if (scrapConfirmButton) {
            scrapConfirmButton.disabled = true;
        }
    });

    scrapKeepButton?.addEventListener("click", () => {
        resolveScrapModal({ confirmed: false });
        scrapModal?.hide();
    });

    scrapConfirmButton?.addEventListener("click", () => {
        if (!scrapSelectedTypeKey) {
            return;
        }

        const option = scrapTypeOptions[scrapSelectedTypeKey];
        resolveScrapModal({
            confirmed: true,
            typeKey: scrapSelectedTypeKey,
            typeValue: option?.value ?? scrapSelectedTypeKey,
            typeLabel: option?.label ?? scrapSelectedTypeKey,
            comment: scrapCommentInput?.value?.trim() ?? "",
        });
        scrapModal?.hide();
    });

    scrapTypeButtons.forEach(button => {
        button.addEventListener("click", () => {
            scrapSelectedTypeKey = button.dataset.scrapType ?? null;
            updateScrapTypeButtons();
            if (scrapConfirmButton) {
                scrapConfirmButton.disabled = !scrapSelectedTypeKey;
            }
        });
    });

    scrapModalElement?.addEventListener("show.bs.modal", () => {
        if (!scrapPrimaryActions || !scrapDetails || !scrapConfirmButton) {
            return;
        }

        scrapPrimaryActions.classList.remove("d-none");
        scrapDetails.classList.add("d-none");
        scrapConfirmButton.disabled = true;
        scrapSelectedTypeKey = null;
        if (scrapCommentInput) {
            scrapCommentInput.value = "";
        }
        updateScrapTypeButtons();
    });

    scrapModalElement?.addEventListener("hidden.bs.modal", () => {
        resolveScrapModal({ confirmed: false }, true);
        scrapSelectedTypeKey = null;
        updateScrapTypeButtons();
    });

    function updateScrapTypeButtons() {
        scrapTypeButtons.forEach(button => {
            const isSelected = button.dataset.scrapType === scrapSelectedTypeKey;
            button.classList.toggle("btn-danger", isSelected);
            button.classList.toggle("btn-outline-danger", !isSelected);
            if (isSelected) {
                button.classList.add("fw-semibold");
            }
            else {
                button.classList.remove("fw-semibold");
            }
        });
    }

    function resolveScrapModal(result, silent = false) {
        if (!scrapModalResolver) {
            return;
        }

        if (silent && result.confirmed) {
            return;
        }

        const resolver = scrapModalResolver;
        scrapModalResolver = null;
        resolver(result);
    }

    function askScrapDecision(context) {
        if (!scrapModal || !scrapIntro) {
            return Promise.resolve({ confirmed: false });
        }

        const leftoverText = Number(context.leftover).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 });
        const partText = context.partDisplay ? ` для ${context.partDisplay}` : "";
        const fromText = context.fromOperation ? ` на операции ${context.fromOperation}` : "";
        scrapIntro.textContent = `Осталось ${leftoverText} шт${partText}${fromText}. Решите, что сделать с остатком.`;

        return new Promise(resolve => {
            scrapModalResolver = resolve;
            scrapModal.show();
        });
    }

    function parseOpNumber(value) {
        return Number(value ?? 0);
    }

    function formatOperation(operation) {
        const opNumber = operation.opNumber;
        const balanceValue = Number(operation.balance ?? 0);
        if (operation.isWarehouse) {
            const name = operation.operationName || "Склад";
            return `${opNumber} | ${name} | остаток на складе: ${balanceValue.toFixed(3)}`;
        }

        const parts = [opNumber];
        if (operation.operationName) {
            parts.push(operation.operationName);
        }
        const norm = Number(operation.normHours ?? 0);
        parts.push(`${norm.toFixed(3)} н/ч`);
        parts.push(`остаток: ${balanceValue.toFixed(3)}`);
        return parts.join(" | ");
    }

    function normalizeOpNumberValue(value) {
        if (value === null || value === undefined) {
            return "";
        }

        if (typeof value === "number" && Number.isFinite(value)) {
            const numeric = Math.trunc(value);
            return numeric.toString().padStart(3, "0");
        }

        if (typeof value === "string") {
            const trimmed = value.trim();
            if (!trimmed.length) {
                return "";
            }

            if (/^\d+$/.test(trimmed)) {
                const parsed = Number(trimmed);
                if (Number.isFinite(parsed)) {
                    return Math.trunc(parsed).toString().padStart(3, "0");
                }
            }

            return trimmed;
        }

        return String(value);
    }

    function getBalanceKey(partId, opNumber) {
        return `${partId}:${normalizeOpNumberValue(opNumber)}`;
    }

    function getAvailableBalance(partId, opNumber) {
        if (!partId || !opNumber) {
            return 0;
        }

        const key = getBalanceKey(partId, opNumber);
        const base = balanceCache.get(key) ?? 0;
        const pending = pendingChanges.get(key) ?? 0;
        return base + pending;
    }

    function applyPendingChange(partId, opNumber, delta) {
        const key = getBalanceKey(partId, opNumber);
        const current = pendingChanges.get(key) ?? 0;
        const next = current + delta;
        if (Math.abs(next) < 1e-9) {
            pendingChanges.delete(key);
        }
        else {
            pendingChanges.set(key, next);
        }
    }

    function canAddToCart() {
        const part = partLookup.getSelected();
        if (!part || !part.id) {
            return false;
        }

        if (!selectedFromOperation || !selectedToOperation) {
            return false;
        }

        if (!selectedToOperation.isWarehouse && parseOpNumber(selectedToOperation.opNumber) <= parseOpNumber(selectedFromOperation.opNumber)) {
            return false;
        }

        if (isLoadingOperations || isLoadingBalances) {
            return false;
        }

        const quantity = Number(quantityInput.value);
        if (!quantity || quantity <= 0) {
            return false;
        }

        if (!dateInput.value) {
            return false;
        }

        const availableFrom = getAvailableBalance(part.id, selectedFromOperation.opNumber);
        if (quantity > availableFrom + 1e-9) {
            return false;
        }

        return true;
    }

    function updateFormState() {
        const partSelected = !!(partLookup.getSelected()?.id);
        fromOperationInput.disabled = !partSelected || isLoadingOperations;
        toOperationInput.disabled = !partSelected || isLoadingOperations || !selectedFromOperation;

        const hasOperations = selectedFromOperation && selectedToOperation;
        quantityInput.disabled = !hasOperations || isLoadingBalances;
        commentInput.disabled = !hasOperations;

        addButton.disabled = !canAddToCart();
        saveButton.disabled = cart.length === 0;
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
        selectedFromOperation = null;
        selectedToOperation = null;
        fromOperationInput.value = "";
        fromOperationNumberInput.value = "";
        toOperationInput.value = "";
        toOperationNumberInput.value = "";
        operations = [];
        updateFromOperationsDatalist();
        updateToOperationsDatalist();
        updateBalanceLabels();

        try {
            const response = await fetch(`/wip/transfer/operations?partId=${encodeURIComponent(partId)}`, { signal });
            if (!response.ok) {
                throw new Error("Не удалось загрузить операции.");
            }

            const items = await response.json();
            if (requestId !== operationsRequestId) {
                return;
            }

            operations = Array.isArray(items) ? items : [];
            operations.forEach(operation => {
                const key = getBalanceKey(partId, operation.opNumber);
                balanceCache.set(key, operation.balance ?? 0);
            });
            updateFromOperationsDatalist();
            updateToOperationsDatalist();
            updateBalanceLabels();
        }
        catch (error) {
            if (signal.aborted) {
                return;
            }

            console.error(error);
            if (requestId !== operationsRequestId) {
                return;
            }

            operations = [];
            updateFromOperationsDatalist();
            updateToOperationsDatalist();
            updateBalanceLabels();
        }
        finally {
            if (requestId === operationsRequestId) {
                isLoadingOperations = false;
                operationsAbortController = null;
                updateFormState();
            }
        }
    }

    function updateFromOperationsDatalist() {
        fromOperationOptions.innerHTML = "";
        operations
            .filter(operation => !operation.isWarehouse)
            .forEach(operation => {
                const option = document.createElement("option");
                option.value = formatOperation(operation);
                fromOperationOptions.appendChild(option);
            });
    }

    function updateToOperationsDatalist() {
        toOperationOptions.innerHTML = "";
        const fromNumber = selectedFromOperation?.opNumber ?? null;
        operations
            .filter(operation => !fromNumber || operation.isWarehouse || parseOpNumber(operation.opNumber) > parseOpNumber(fromNumber))
            .forEach(operation => {
                const option = document.createElement("option");
                option.value = formatOperation(operation);
                toOperationOptions.appendChild(option);
            });
    }

    async function refreshBalances() {
        updateBalanceLabels();

        const part = partLookup.getSelected();
        const requestId = ++balanceRequestId;
        if (balancesAbortController) {
            balancesAbortController.abort();
            balancesAbortController = null;
        }

        if (!part || !part.id || !selectedFromOperation || !selectedToOperation) {
            isLoadingBalances = false;
            updateFormState();
            return;
        }

        balancesAbortController = new AbortController();
        const signal = balancesAbortController.signal;

        isLoadingBalances = true;
        updateFormState();
        try {
            const response = await fetch(`/wip/transfer/balances?partId=${encodeURIComponent(part.id)}&fromOpNumber=${encodeURIComponent(selectedFromOperation.opNumber)}&toOpNumber=${encodeURIComponent(selectedToOperation.opNumber)}`, { signal });
            if (!response.ok) {
                throw new Error("Не удалось загрузить остатки.");
            }

            const data = await response.json();
            if (requestId !== balanceRequestId) {
                return;
            }

            if (data?.from) {
                const key = getBalanceKey(part.id, data.from.opNumber);
                balanceCache.set(key, Number(data.from.balance) || 0);
                balanceLabels.set(key, normalizeLabelArray(data.from.labels));
            }

            if (data?.to) {
                const key = getBalanceKey(part.id, data.to.opNumber);
                balanceCache.set(key, Number(data.to.balance) || 0);
                balanceLabels.set(key, normalizeLabelArray(data.to.labels));
            }
        }
        catch (error) {
            if (signal.aborted) {
                return;
            }

            console.error(error);
        }
        finally {
            if (requestId === balanceRequestId) {
                isLoadingBalances = false;
                balancesAbortController = null;
                updateBalanceLabels();
                updateFormState();
            }
        }
    }

    function updateBalanceLabels() {
        const part = partLookup.getSelected();
        if (!part || !part.id) {
            fromBalanceLabel.textContent = "0 шт";
            toBalanceLabel.textContent = "0 шт";
            updateLabelElement(fromLabelsElement, []);
            updateLabelElement(toLabelsElement, []);
            updateFormState();
            return;
        }

        const fromNumber = selectedFromOperation?.opNumber;
        const toNumber = selectedToOperation?.opNumber;
        const fromAvailable = fromNumber ? getAvailableBalance(part.id, fromNumber) : 0;
        const toAvailable = toNumber ? getAvailableBalance(part.id, toNumber) : 0;
        fromBalanceLabel.textContent = `${fromAvailable.toLocaleString("ru-RU")} шт`;
        toBalanceLabel.textContent = `${toAvailable.toLocaleString("ru-RU")} шт`;
        const fromLabels = fromNumber ? balanceLabels.get(getBalanceKey(part.id, fromNumber)) : null;
        const toLabels = toNumber ? balanceLabels.get(getBalanceKey(part.id, toNumber)) : null;
        updateLabelElement(fromLabelsElement, fromLabels);
        updateLabelElement(toLabelsElement, toLabels);
        updateFormState();
    }

    function resetForm() {
        quantityInput.value = "";
        commentInput.value = "";
        if (dateInput) {
            dateInput.value = today;
        }

        selectedFromOperation = null;
        selectedToOperation = null;
        fromOperationInput.value = "";
        fromOperationNumberInput.value = "";
        toOperationInput.value = "";
        toOperationNumberInput.value = "";
        updateToOperationsDatalist();
        updateBalanceLabels();
        updateFormState();
    }

    async function addToCart() {
        const part = partLookup.getSelected();
        if (!part || !part.id) {
            alert("Выберите деталь.");
            return;
        }

        if (!selectedFromOperation) {
            alert("Выберите операцию до.");
            return;
        }

        if (!selectedToOperation) {
            alert("Выберите операцию после.");
            return;
        }

        if (parseOpNumber(selectedToOperation.opNumber) <= parseOpNumber(selectedFromOperation.opNumber)) {
            alert("Операция после должна быть позже операции до.");
            return;
        }

        const quantity = Number(quantityInput.value);
        if (!quantity || quantity <= 0) {
            alert("Количество должно быть больше нуля.");
            return;
        }

        const date = dateInput?.value;
        if (!date) {
            alert("Укажите дату передачи.");
            return;
        }

        const fromAvailable = getAvailableBalance(part.id, selectedFromOperation.opNumber);
        if (quantity > fromAvailable) {
            alert(`Нельзя передать больше, чем остаток (${fromAvailable.toFixed(3)}).`);
            return;
        }

        const toAvailable = getAvailableBalance(part.id, selectedToOperation.opNumber);
        const fromLabelKey = getBalanceKey(part.id, selectedFromOperation.opNumber);
        const fromLabels = balanceLabels.get(fromLabelKey);

        const item = {
            partId: part.id,
            partName: part.name,
            partCode: part.code ?? null,
            partDisplay: formatNameWithCode(part.name, part.code),
            fromOpNumber: selectedFromOperation.opNumber,
            fromOperationName: selectedFromOperation.operationName,
            fromDisplay: formatOperation(selectedFromOperation),
            toOpNumber: selectedToOperation.opNumber,
            toOperationName: selectedToOperation.operationName,
            toDisplay: formatOperation(selectedToOperation),
            date,
            quantity,
            comment: commentInput.value || null,
            fromBalanceBefore: fromAvailable,
            fromBalanceAfter: fromAvailable - quantity,
            toBalanceBefore: toAvailable,
            toBalanceAfter: toAvailable + quantity,
            scrapType: null,
            scrapTypeLabel: null,
            scrapQuantity: 0,
            scrapComment: null,
            scrap: null,
            labelNumbers: normalizeLabelArray(fromLabels),
        };

        const leftover = item.fromBalanceAfter;
        let fromDelta = -quantity;
        if (leftover > 1e-9) {
            const decision = await askScrapDecision({
                leftover,
                partDisplay: item.partDisplay,
                fromOperation: `${selectedFromOperation.opNumber} ${selectedFromOperation.operationName ?? ""}`.trim(),
            });

            if (decision?.confirmed) {
                item.scrapType = decision.typeValue;
                item.scrapTypeLabel = decision.typeLabel;
                item.scrapQuantity = leftover;
                item.scrapComment = decision.comment ? decision.comment : null;
                item.scrap = {
                    scrapType: item.scrapType,
                    quantity: item.scrapQuantity,
                    comment: item.scrapComment,
                };
                item.fromBalanceAfter = 0;
                fromDelta -= leftover;
            }
        }

        applyPendingChange(part.id, selectedFromOperation.opNumber, fromDelta);
        applyPendingChange(part.id, selectedToOperation.opNumber, quantity);
        cart.push(item);
        renderCart();
        resetForm();
        updateBalanceLabels();
    }

    function renderCart() {
        if (!cart.length) {
            cartTableBody.innerHTML = "<tr><td colspan=\"11\" class=\"text-center text-muted\">Добавьте записи передачи, чтобы подготовить пакет к сохранению.</td></tr>";
            updateFormState();
            return;
        }

        cartTableBody.innerHTML = "";
        cart.forEach((item, index) => {
            const row = document.createElement("tr");
            row.innerHTML = `
                <td>${item.date}</td>
                <td>${item.partDisplay}</td>
                <td>
                    <div class="fw-semibold">${item.fromOpNumber}</div>
                    <div class="small text-muted">${item.fromOperationName ?? ""}</div>
                </td>
                <td>
                    <div class="fw-semibold">${item.toOpNumber}</div>
                    <div class="small text-muted">${item.toOperationName ?? ""}</div>
                </td>
                <td>${formatLabelList(item.labelNumbers)}</td>
                <td>${item.quantity.toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 })}</td>
                <td>${formatBalanceChange(item.fromBalanceBefore, item.fromBalanceAfter)}</td>
                <td>${formatBalanceChange(item.toBalanceBefore, item.toBalanceAfter)}</td>
                <td>${item.comment ? escapeHtml(item.comment) : ""}</td>
                <td>${formatScrapCell(item)}</td>
                <td class="text-center">
                    <button type="button" class="btn btn-link btn-lg text-decoration-none" data-action="edit" data-index="${index}" aria-label="Изменить запись">✎</button>
                    <button type="button" class="btn btn-link btn-lg text-decoration-none text-danger" data-action="remove" data-index="${index}" aria-label="Удалить запись">✖</button>
                </td>`;
            cartTableBody.appendChild(row);
        });
        updateFormState();
    }

    function formatBalanceChange(before, after) {
        const beforeText = Number(before).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 });
        const afterText = Number(after).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 });
        return `${beforeText} → ${afterText}`;
    }

    function escapeHtml(value) {
        const element = document.createElement("div");
        element.textContent = value;
        return element.innerHTML;
    }

    function normalizeLabelArray(source) {
        if (!Array.isArray(source)) {
            return [];
        }

        const seen = new Set();
        const ret = [];
        source.forEach(label => {
            if (label === null || label === undefined) {
                return;
            }

            const normalized = String(label).trim();
            if (!normalized.length) {
                return;
            }

            const key = normalized.toLowerCase();
            if (!seen.has(key)) {
                seen.add(key);
                ret.push(normalized);
            }
        });

        return ret;
    }

    function renderLabelSummary(labels) {
        const normalized = normalizeLabelArray(labels);
        return normalized.length ? normalized.join(", ") : "—";
    }

    function formatLabelList(labels) {
        const normalized = normalizeLabelArray(labels);
        if (!normalized.length) {
            return "<span class=\"text-muted\">—</span>";
        }

        return normalized.map(entry => escapeHtml(entry)).join(", ");
    }

    function updateLabelElement(element, labels) {
        if (!element) {
            return;
        }

        element.textContent = renderLabelSummary(labels);
    }

    function getScrapLabel(type, fallbackLabel) {
        const normalized = normalizeScrapTypeValue(type);
        if (normalized !== null) {
            const option = Object.values(scrapTypeOptions).find(entry => entry.value === normalized);
            if (option) {
                return option.label;
            }
        }

        if (typeof type === "string" && type.trim().length > 0) {
            const trimmed = type.trim();
            const option = Object.values(scrapTypeOptions).find(entry => entry.code === trimmed);
            if (option) {
                return option.label;
            }
        }

        if (fallbackLabel && fallbackLabel.length > 0) {
            return fallbackLabel;
        }

        if (type === null || type === undefined) {
            return "";
        }

        return String(type);
    }

    function formatScrapCell(item) {
        const hasType = item && item.scrapType !== undefined && item.scrapType !== null;
        const hasQuantity = item && Number(item.scrapQuantity) > 0;
        if (!hasType || !hasQuantity) {
            return "<span class=\"text-muted\">Остаток остаётся на операции</span>";
        }

        const quantityText = Number(item.scrapQuantity).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 });
        const commentHtml = item.scrapComment ? `<div class=\"mt-2 text-muted\">${escapeHtml(item.scrapComment)}</div>` : "";
        const label = getScrapLabel(item.scrapType, item.scrapTypeLabel);
        return `
            <div class="d-flex align-items-start gap-3">
                <div class="display-6" aria-hidden="true">⚠️</div>
                <div>
                    <div class="fw-semibold text-danger">Брак: ${escapeHtml(label)}</div>
                    <div>Количество: ${quantityText} шт</div>
                    ${commentHtml}
                </div>
            </div>`;
    }

    function renderRecentTransfers() {
        if (!recentTableBody || !recentTableWrapper || !recentEmptyPlaceholder) {
            return;
        }

        if (!recentTransfers.length) {
            recentTableBody.innerHTML = "";
            recentTableWrapper.classList.add("d-none");
            recentEmptyPlaceholder.classList.remove("d-none");
            return;
        }

        recentTableWrapper.classList.remove("d-none");
        recentEmptyPlaceholder.classList.add("d-none");
        recentTableBody.innerHTML = "";

        recentTransfers.forEach((item, index) => {
            const row = document.createElement("tr");
            const quantityText = Number(item.quantity).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 });
            const fromBalanceText = formatBalanceChange(item.fromBalanceBefore, item.fromBalanceAfter);
            const toBalanceText = formatBalanceChange(item.toBalanceBefore, item.toBalanceAfter);
            const fromName = item.fromOperationName ? `<div class=\"small text-muted\">${escapeHtml(item.fromOperationName)}</div>` : "";
            const toName = item.toOperationName ? `<div class=\"small text-muted\">${escapeHtml(item.toOperationName)}</div>` : "";

            row.innerHTML = `
                <td>${item.date ? escapeHtml(item.date) : ""}</td>
                <td>${escapeHtml(item.partDisplay ?? item.partId ?? "")}</td>
                <td>
                    <div class="fw-semibold">${escapeHtml(item.fromOpNumber ?? "")}</div>
                    ${fromName}
                </td>
                <td>
                    <div class="fw-semibold">${escapeHtml(item.toOpNumber ?? "")}</div>
                    ${toName}
                </td>
                <td>${formatLabelList(item.labelNumbers)}</td>
                <td>${quantityText}</td>
                <td>${fromBalanceText}</td>
                <td>${toBalanceText}</td>
                <td>${formatScrapCell(item)}</td>
                <td class="text-center">
                    <button type="button" class="btn btn-outline-danger btn-sm" data-action="cancel" data-index="${index}" data-transfer-id="${item.transferId}">Отменить</button>
                </td>`;

            recentTableBody.appendChild(row);
        });
    }

    function rememberRecentTransfers(summary, cartSnapshot) {
        if (!summary || !Array.isArray(summary.items) || !Array.isArray(cartSnapshot)) {
            return;
        }

        const additions = [];
        summary.items.forEach((item, index) => {
            if (!item || !item.transferId) {
                return;
            }

            const cartItem = cartSnapshot[index] ?? null;
            const partDisplay = cartItem?.partDisplay ?? cartItem?.partName ?? item.partId ?? "";
            const scrapSource = extractScrapSource(item) ?? extractScrapSource(cartItem);
            const scrapInfo = normalizeScrapInfo(scrapSource);

            additions.push({
                transferId: item.transferId,
                partId: item.partId,
                partDisplay,
                date: cartItem?.date ?? null,
                fromOpNumber: item.fromOpNumber,
                fromOperationName: cartItem?.fromOperationName ?? null,
                toOpNumber: item.toOpNumber,
                toOperationName: cartItem?.toOperationName ?? null,
                labelNumbers: normalizeLabelArray(item.labelNumbers ?? cartItem?.labelNumbers),
                quantity: Number(item.quantity) || 0,
                fromBalanceBefore: Number(item.fromBalanceBefore) || 0,
                fromBalanceAfter: Number(item.fromBalanceAfter) || 0,
                toBalanceBefore: Number(item.toBalanceBefore) || 0,
                toBalanceAfter: Number(item.toBalanceAfter) || 0,
                fromSectionId: item.fromSectionId,
                toSectionId: item.toSectionId,
                scrapType: scrapInfo?.scrapType ?? null,
                scrapTypeLabel: scrapInfo?.scrapTypeLabel ?? null,
                scrapQuantity: scrapInfo?.scrapQuantity ?? 0,
                scrapComment: scrapInfo?.scrapComment ?? null,
                scrap: scrapInfo,
            });
        });

        if (!additions.length) {
            return;
        }

        additions.reverse().forEach(entry => {
            recentTransfers = recentTransfers.filter(existing => existing.transferId !== entry.transferId);
            recentTransfers.unshift(entry);
        });

        if (recentTransfers.length > RECENT_TRANSFER_LIMIT) {
            recentTransfers = recentTransfers.slice(0, RECENT_TRANSFER_LIMIT);
        }

        renderRecentTransfers();
    }

    async function cancelRecentTransfer(index) {
        const item = recentTransfers[index];
        if (!item) {
            return;
        }

        if (!window.confirm("Отменить выбранную передачу?")) {
            return;
        }

        try {
            const response = await fetch(`/wip/transfer/${encodeURIComponent(item.transferId)}`, {
                method: "DELETE",
                headers: { "Accept": "application/json" },
            });

            if (!response.ok) {
                let message = "Не удалось отменить передачу.";
                try {
                    const data = await response.json();
                    if (typeof data === "string" && data.trim().length > 0) {
                        message = data.trim();
                    }
                    else if (data && typeof data.message === "string" && data.message.trim().length > 0) {
                        message = data.message.trim();
                    }
                }
                catch {
                    const text = await response.text();
                    if (text && text.trim().length > 0) {
                        message = text.trim();
                    }
                }

                throw new Error(message);
            }

            const result = await response.json();
            recentTransfers.splice(index, 1);
            renderRecentTransfers();
            applyDeleteResult(result);
            alert("Передача отменена.");
        }
        catch (error) {
            console.error(error);
            alert(error instanceof Error ? error.message : "Не удалось отменить передачу.");
        }
    }

    function applyDeleteResult(result) {
        if (!result) {
            return;
        }

        const partId = result.partId;
        const fromKey = getBalanceKey(partId, result.fromOpNumber);
        const toKey = getBalanceKey(partId, result.toOpNumber);

        balanceCache.set(fromKey, Number(result.fromBalanceAfter) || 0);
        balanceCache.set(toKey, Number(result.toBalanceAfter) || 0);
        balanceLabels.delete(fromKey);
        balanceLabels.delete(toKey);

        const part = partLookup.getSelected();
        if (part && part.id === partId) {
            updateOperationsDisplay(part.id);
            updateBalanceLabels();
        }
    }

    function extractScrapSource(source) {
        if (!source) {
            return null;
        }

        if (source.scrap) {
            const scrap = source.scrap;
            if (scrap && scrap.scrapType === undefined && scrap.type !== undefined) {
                return { ...scrap, scrapType: scrap.type };
            }

            return scrap;
        }

        if (source.scrapQuantity || source.scrapType || source.scrapComment || source.scrapTypeLabel) {
            return {
                quantity: source.scrapQuantity,
                type: source.scrapType,
                scrapType: source.scrapType,
                comment: source.scrapComment,
                typeLabel: source.scrapTypeLabel,
            };
        }

        return null;
    }

    function normalizeScrapTypeValue(raw) {
        if (raw === null || raw === undefined) {
            return null;
        }

        if (typeof raw === "number") {
            return Number.isFinite(raw) ? raw : null;
        }

        if (typeof raw === "string") {
            const trimmed = raw.trim();
            if (!trimmed.length) {
                return null;
            }

            const numeric = Number(trimmed);
            if (Number.isFinite(numeric)) {
                return numeric;
            }

            const byCode = Object.values(scrapTypeOptions)
                .find(option => option.code === trimmed || option.label === trimmed);
            if (byCode) {
                return byCode.value;
            }
        }

        return null;
    }

    function normalizeScrapInfo(raw) {
        if (!raw) {
            return null;
        }

        const type = raw.type ?? raw.typeValue ?? raw.scrapType ?? null;
        const quantityRaw = raw.quantity ?? raw.scrapQuantity ?? 0;
        const parsedQuantity = Number(quantityRaw);
        const quantity = Number.isFinite(parsedQuantity) ? parsedQuantity : 0;
        const commentRaw = raw.comment ?? raw.scrapComment ?? null;
        const comment = commentRaw !== null && commentRaw !== undefined ? String(commentRaw) : null;
        const normalizedType = normalizeScrapTypeValue(type);
        const label = getScrapLabel(normalizedType ?? type, raw.typeLabel ?? raw.scrapTypeLabel ?? null);

        return {
            scrapType: normalizedType ?? type,
            scrapTypeLabel: label,
            scrapQuantity: quantity,
            scrapComment: comment,
        };
    }

    function buildScrapPayload(item) {
        if (!item) {
            return null;
        }

        const typeSource = item.scrapType ?? item.scrap?.scrapType ?? item.scrap?.type ?? null;
        const quantitySource = item.scrapQuantity ?? item.scrap?.quantity ?? 0;
        const commentSource = item.scrapComment ?? item.scrap?.comment ?? null;
        const normalizedType = normalizeScrapTypeValue(typeSource);
        const parsedQuantity = Number(quantitySource);
        if (normalizedType === null || !Number.isFinite(parsedQuantity) || !(parsedQuantity > 0)) {
            return null;
        }

        return {
            scrapType: normalizedType,
            quantity: parsedQuantity,
            comment: commentSource ?? null,
        };
    }

    function removeCartItem(index) {
        const [removed] = cart.splice(index, 1);
        if (removed) {
            applyPendingChange(removed.partId, removed.fromOpNumber, removed.quantity);
            applyPendingChange(removed.partId, removed.toOpNumber, -removed.quantity);
            if (removed.scrapQuantity && removed.scrapQuantity > 0) {
                applyPendingChange(removed.partId, removed.fromOpNumber, removed.scrapQuantity);
            }
        }

        renderCart();
        updateBalanceLabels();
    }

    async function editCartItem(index) {
        const item = cart.splice(index, 1)[0];
        if (!item) {
            return;
        }

        applyPendingChange(item.partId, item.fromOpNumber, item.quantity);
        applyPendingChange(item.partId, item.toOpNumber, -item.quantity);
        if (item.scrapQuantity && item.scrapQuantity > 0) {
            applyPendingChange(item.partId, item.fromOpNumber, item.scrapQuantity);
        }

        renderCart();

        partLookup.setSelected({ id: item.partId, name: item.partName, code: item.partCode });
        commentInput.value = item.comment ?? "";
        quantityInput.value = item.quantity;
        if (dateInput) {
            dateInput.value = item.date;
        }

        await loadOperations(item.partId);
        const fromOperation = operations.find(op => op.opNumber === item.fromOpNumber);
        if (fromOperation) {
            selectedFromOperation = fromOperation;
            fromOperationInput.value = formatOperation(fromOperation);
            fromOperationNumberInput.value = fromOperation.opNumber;
        }

        updateToOperationsDatalist();

        const toOperation = operations.find(op => op.opNumber === item.toOpNumber);
        if (toOperation) {
            selectedToOperation = toOperation;
            toOperationInput.value = formatOperation(toOperation);
            toOperationNumberInput.value = toOperation.opNumber;
        }

        updateBalanceLabels();
        updateFormState();
    }

    async function saveCart() {
        if (!cart.length) {
            alert("Корзина пуста.");
            return;
        }

        if (!window.confirm("Сохранить выбранные передачи?")) {
            return;
        }

        const payload = {
            items: cart.map(item => ({
                partId: item.partId,
                fromOpNumber: item.fromOpNumber,
                toOpNumber: item.toOpNumber,
                transferDate: item.date,
                quantity: item.quantity,
                comment: item.comment,
                scrap: buildScrapPayload(item),
            })),
        };

        saveButton.disabled = true;
        const cartSnapshot = cart.slice();
        try {
            const response = await fetch("/wip/transfer/save", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Accept": "application/json",
                },
                body: JSON.stringify(payload),
            });

            if (!response.ok) {
                throw new Error("Не удалось сохранить передачи.");
            }

            const summary = await response.json();
            rememberRecentTransfers(summary, cartSnapshot);
            showSummary(summary);
            updateBalancesAfterSave(summary);
            pendingChanges.clear();
            cart = [];
            renderCart();
            resetForm();
        }
        catch (error) {
            console.error(error);
            alert("Во время сохранения произошла ошибка. Попробуйте ещё раз.");
        }
        finally {
            saveButton.disabled = false;
        }
    }

    function updateBalancesAfterSave(summary) {
        if (!summary || !Array.isArray(summary.items)) {
            return;
        }

        summary.items.forEach(item => {
            const fromKey = getBalanceKey(item.partId, item.fromOpNumber);
            const toKey = getBalanceKey(item.partId, item.toOpNumber);
            balanceCache.set(fromKey, Number(item.fromBalanceAfter) || 0);
            balanceCache.set(toKey, Number(item.toBalanceAfter) || 0);
            const labels = normalizeLabelArray(item.labelNumbers);
            balanceLabels.set(fromKey, labels);
            balanceLabels.set(toKey, labels);
        });

        const part = partLookup.getSelected();
        if (part && part.id) {
            updateOperationsDisplay(part.id);
        }

        updateBalanceLabels();
    }

    function updateOperationsDisplay(partId) {
        if (!operations.length) {
            return;
        }

        operations = operations.map(operation => {
            const key = getBalanceKey(partId, operation.opNumber);
            const balance = balanceCache.get(key);
            if (balance === undefined) {
                return operation;
            }

            return { ...operation, balance };
        });

        updateFromOperationsDatalist();
        updateToOperationsDatalist();

        if (selectedFromOperation) {
            const refreshedFrom = operations.find(op => op.opNumber === selectedFromOperation.opNumber);
            if (refreshedFrom) {
                selectedFromOperation = refreshedFrom;
                fromOperationInput.value = formatOperation(refreshedFrom);
            }
        }

        if (selectedToOperation) {
            const refreshedTo = operations.find(op => op.opNumber === selectedToOperation.opNumber);
            if (refreshedTo) {
                selectedToOperation = refreshedTo;
                toOperationInput.value = formatOperation(refreshedTo);
            }
        }
    }

    function showSummary(summary) {
        if (!summaryTableBody || !summaryIntro) {
            return;
        }

        summaryTableBody.innerHTML = "";
        summaryIntro.textContent = `Сохранено записей: ${summary?.saved ?? 0}.`;

        (summary?.items ?? []).forEach((item, index) => {
            const row = document.createElement("tr");
            const cartItem = cart[index];
            const partDisplay = cartItem?.partDisplay ?? item.partDisplay ?? item.partName ?? item.partId;
            const fromText = `${item.fromOpNumber}: ${Number(item.fromBalanceBefore).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 })} → ${Number(item.fromBalanceAfter).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 })}`;
            const toText = `${item.toOpNumber}: ${Number(item.toBalanceBefore).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 })} → ${Number(item.toBalanceAfter).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 })}`;
            const scrapSource = extractScrapSource(item) ?? extractScrapSource(cartItem);
            const scrapInfo = normalizeScrapInfo(scrapSource);
            const scrapCell = formatScrapCell(scrapInfo ?? {});
            const labelsHtml = formatLabelList(item.labelNumbers ?? cartItem?.labelNumbers);
            row.innerHTML = `
                <td>${partDisplay}</td>
                <td>${fromText}</td>
                <td>${toText}</td>
                <td>${labelsHtml}</td>
                <td>${Number(item.quantity).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 })}</td>
                <td>${scrapCell}</td>
                <td>${item.transferId}</td>`;
            summaryTableBody.appendChild(row);
        });

        if (bootstrapModal) {
            bootstrapModal.show();
        }
    }

    renderRecentTransfers();
    updateFormState();

    namespace.bindHotkeys({
        onEnter: () => void addToCart(),
        onSave: () => void saveCart(),
        onCancel: () => resetForm(),
    });
})();
