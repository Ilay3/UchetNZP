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
    const fromOperationHelper = document.getElementById("transferFromOperationHelper");
    const fromOperationNumberInput = document.getElementById("transferFromOperationNumber");
    const toOperationInput = document.getElementById("transferToOperationInput");
    const toOperationOptions = document.getElementById("transferToOperationOptions");
    const toOperationHelper = document.getElementById("transferToOperationHelper");
    const toOperationNumberInput = document.getElementById("transferToOperationNumber");
    const dateInput = document.getElementById("transferDateInput");
    const quantityInput = document.getElementById("transferQuantityInput");
    const commentInput = document.getElementById("transferCommentInput");
    const fromBalanceLabel = document.getElementById("transferFromBalanceLabel");
    const toBalanceLabel = document.getElementById("transferToBalanceLabel");
    const fromLabelsElement = document.getElementById("transferFromLabels");
    const toLabelsElement = document.getElementById("transferToLabels");
    const labelSelect = document.getElementById("transferLabelSelect");
    const labelHintElement = document.getElementById("transferLabelHint");
    const addButton = document.getElementById("transferAddButton");
    const backButton = document.getElementById("transferBackButton");
    const resetButton = document.getElementById("transferResetButton");
    const saveButton = document.getElementById("transferSaveButton");
    const stepBlocks = Array.from(document.querySelectorAll("[data-transfer-step]"));
    const stepProgress = document.getElementById("transferStepProgress");
    const stepHint = document.getElementById("transferStepHint");
    const stepBadge = document.getElementById("transferStepBadge");
    const confirmSummary = document.getElementById("transferConfirmSummary");
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
    const labelBalanceCache = new Map();
    const labelPendingChanges = new Map();
    let currentOperationsPartId = null;
    let cart = [];
    let recentTransfers = [];
    let isLoadingOperations = false;
    let isLoadingBalances = false;
    let isLoadingLabels = false;
    let labelOptions = [];
    let selectedLabelOption = null;
    let labelsAbortController = null;
    let labelsRequestId = 0;
    let currentStep = 1;

    partLookup.inputElement?.addEventListener("lookup:selected", event => {
        const part = event.detail;
        if (part && part.id) {
            resetLabels();
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
        currentOperationsPartId = null;
        resetLabels();
        updateFromOperationsDatalist();
        updateToOperationsDatalist();
        updateBalanceLabels();
        currentStep = 1;
        updateFormState();
    });

    fromOperationInput.addEventListener("input", () => {
        selectedFromOperation = null;
        fromOperationNumberInput.value = "";
        resetLabels();
        updateToOperationsDatalist();
        updateFromOperationHelper();
        updateBalanceLabels();
        updateFormState();
    });

    toOperationInput.addEventListener("input", () => {
        selectedToOperation = null;
        toOperationNumberInput.value = "";
        updateToOperationHelper();
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
            updateToOperationHelper();
            const part = partLookup.getSelected();
            if (part && part.id) {
                void loadLabels(part.id, operation.opNumber);
            }
            void refreshBalances();
        }
        else {
            selectedFromOperation = null;
            fromOperationNumberInput.value = "";
            resetLabels();
            updateToOperationsDatalist();
            updateToOperationHelper();
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
            updateToOperationHelper();
            updateBalanceLabels();
            updateFormState();
            return;
        }

        if (selectedFromOperation && !operation.isWarehouse && parseOpNumber(operation.opNumber) <= parseOpNumber(selectedFromOperation.opNumber)) {
            alert("Операция после должна быть позже операции до.");
            toOperationInput.value = "";
            toOperationNumberInput.value = "";
            selectedToOperation = null;
            updateToOperationHelper();
            updateBalanceLabels();
            updateFormState();
            return;
        }

        selectedToOperation = operation;
        toOperationNumberInput.value = operation.opNumber;
        void refreshBalances();
        updateFormState();
    });

    labelSelect?.addEventListener("change", () => {
        const value = labelSelect.value;
        if (!value) {
            selectedLabelOption = null;
        }
        else {
            selectedLabelOption = labelOptions.find(option => String(option.id) === value) ?? null;
        }

        updateLabelHint();
    });

    addButton.addEventListener("click", () => {
        if (currentStep < 4) {
            if (currentStep < getMaxAvailableStep()) {
                currentStep += 1;
                updateFormState();
            }
            return;
        }

        void addToCart();
    });
    backButton?.addEventListener("click", () => {
        if (currentStep > 1) {
            currentStep -= 1;
            updateFormState();
        }
    });
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

        if (target.dataset.action !== "revert") {
            return;
        }

        const index = Number(target.dataset.index);
        if (Number.isNaN(index) || index < 0 || index >= recentTransfers.length) {
            return;
        }

        void revertRecentTransfer(index);
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

    function handleHelperSelection(inputElement, operation, refreshHelper) {
        inputElement.value = formatOperation(operation);
        const event = new Event("change", { bubbles: true });
        inputElement.dispatchEvent(event);
        if (typeof refreshHelper === "function") {
            refreshHelper();
        }
    }

    function getFromOperationCandidates() {
        return operations.filter(operation => !operation.isWarehouse);
    }

    function getToOperationCandidates() {
        const fromNumber = selectedFromOperation?.opNumber ?? null;
        return operations.filter(operation => !fromNumber || operation.isWarehouse || parseOpNumber(operation.opNumber) > parseOpNumber(fromNumber));
    }

    function updateOperationHelper(inputElement, helperElement, candidates, onSelect) {
        if (!helperElement) {
            return;
        }

        helperElement.innerHTML = "";
        const filter = (inputElement?.value ?? "").trim().toLowerCase();
        const matches = candidates
            .filter(operation => {
                if (!filter.length) {
                    return true;
                }

                return formatOperation(operation).toLowerCase().includes(filter);
            })
            .slice(0, 50);

        if (!matches.length) {
            helperElement.classList.add("d-none");
            return;
        }

        const fragment = document.createDocumentFragment();
        matches.forEach(operation => {
            const item = document.createElement("button");
            item.type = "button";
            item.className = "list-group-item list-group-item-action transfer-operation-helper__item";
            item.textContent = formatOperation(operation);
            item.title = item.textContent;
            item.addEventListener("click", () => onSelect(operation));
            fragment.appendChild(item);
        });

        helperElement.appendChild(fragment);
        helperElement.classList.remove("d-none");
    }

    function updateFromOperationHelper() {
        updateOperationHelper(fromOperationInput, fromOperationHelper, getFromOperationCandidates(), operation => handleHelperSelection(fromOperationInput, operation, updateFromOperationHelper));
    }

    function updateToOperationHelper() {
        updateOperationHelper(toOperationInput, toOperationHelper, getToOperationCandidates(), operation => handleHelperSelection(toOperationInput, operation, updateToOperationHelper));
    }

    function formatOperation(operation) {
        const opNumber = operation.opNumber;
        const name = operation.operationName || (operation.isWarehouse ? "Склад" : "");
        const balanceValue = Number(operation.balance ?? 0);
        const integerBalance = Number.isFinite(balanceValue) ? Math.trunc(balanceValue) : 0;

        const parts = [opNumber];
        if (name) {
            parts.push(name);
        }

        parts.push(`остаток: ${integerBalance}`);
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

    function getLabelBalanceKey(partId, opNumber, labelId) {
        return `${partId}:${normalizeOpNumberValue(opNumber)}:${labelId}`;
    }

    function getAvailableBalance(partId, opNumber, labelId = null) {
        if (!partId || !opNumber) {
            return 0;
        }

        if (labelId) {
            const labelKey = getLabelBalanceKey(partId, opNumber, labelId);
            if (labelBalanceCache.has(labelKey)) {
                const baseLabel = labelBalanceCache.get(labelKey) ?? 0;
                const pendingLabel = labelPendingChanges.get(labelKey) ?? 0;
                return baseLabel + pendingLabel;
            }
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

    function applyLabelPendingChange(partId, opNumber, labelId, delta) {
        if (!partId || !opNumber || !labelId) {
            return;
        }

        const numericDelta = Number(delta);
        if (!Number.isFinite(numericDelta) || Math.abs(numericDelta) < 1e-9) {
            return;
        }

        const key = getLabelBalanceKey(partId, opNumber, labelId);
        const current = labelPendingChanges.get(key) ?? 0;
        const next = current + numericDelta;
        if (Math.abs(next) < 1e-9) {
            labelPendingChanges.delete(key);
        }
        else {
            labelPendingChanges.set(key, next);
        }
    }

    function restoreLabelOptionFromCartItem(item) {
        if (!item || !item.labelId) {
            return;
        }

        const part = partLookup.getSelected();
        if (!part || !part.id || part.id !== item.partId) {
            return;
        }

        if (!selectedFromOperation || selectedFromOperation.opNumber !== item.fromOpNumber) {
            return;
        }

        const restoredRemaining = Number(item.labelQuantityBefore ?? 0);
        const restoredTotal = Number(item.labelQuantityTotal ?? item.labelQuantityBefore ?? 0);
        const number = item.labelNumber ?? item.labelId;
        const existingIndex = labelOptions.findIndex(option => option.id === item.labelId);
        if (existingIndex >= 0) {
            labelOptions[existingIndex] = {
                ...labelOptions[existingIndex],
                remainingQuantity: restoredRemaining,
                quantity: restoredTotal,
                number,
            };
            selectedLabelOption = labelOptions[existingIndex];
        }
        else {
            const option = {
                id: item.labelId,
                number,
                quantity: restoredTotal,
                remainingQuantity: restoredRemaining,
            };
            labelOptions.push(option);
            selectedLabelOption = option;
        }

        updateLabelSelect();
    }

    function isStep1Valid() {
        return !!(partLookup.getSelected()?.id);
    }

    function isStep2Valid() {
        if (!isStep1Valid()) {
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

        return !(labelOptions.length > 0 && !selectedLabelOption);
    }

    function isStep3Valid() {
        if (!isStep2Valid()) {
            return false;
        }

        const quantity = Number(quantityInput.value);
        if (!quantity || quantity <= 0 || !dateInput.value) {
            return false;
        }

        const part = partLookup.getSelected();
        const labelId = selectedLabelOption?.id ?? null;
        const availableFrom = getAvailableBalance(part.id, selectedFromOperation.opNumber, labelId);
        return quantity <= availableFrom + 1e-9;
    }

    function getMaxAvailableStep() {
        if (!isStep1Valid()) {
            return 1;
        }

        if (!isStep2Valid()) {
            return 2;
        }

        if (!isStep3Valid()) {
            return 3;
        }

        return 4;
    }

    function updateConfirmSummary() {
        if (!confirmSummary) {
            return;
        }

        const part = partLookup.getSelected();
        if (!part || !selectedFromOperation || !selectedToOperation || !quantityInput.value) {
            confirmSummary.textContent = "Выберите деталь, операции и количество, чтобы увидеть сводку.";
            return;
        }

        confirmSummary.innerHTML = `
            <div><strong>Деталь:</strong> ${formatNameWithCode(part.name, part.code)}</div>
            <div><strong>Маршрут:</strong> ${selectedFromOperation.opNumber} → ${selectedToOperation.opNumber}</div>
            <div><strong>Количество:</strong> ${Number(quantityInput.value || 0).toFixed(3)} шт, <strong>дата:</strong> ${dateInput.value || "—"}</div>`;
    }

    function updateWizardState() {
        const maxStep = getMaxAvailableStep();
        if (currentStep > maxStep) {
            currentStep = maxStep;
        }

        const descriptors = {
            1: { title: "Выбор детали", hint: "Выберите деталь, с которой будете работать." },
            2: { title: "Выбор операций", hint: "Укажите операции «до» и «после», проверьте доступные остатки." },
            3: { title: "Количество и дата", hint: "Введите количество и дату передачи. Дополнительно можно добавить комментарий." },
            4: { title: "Подтверждение", hint: "Проверьте введённые данные и сохраните запись в корзину." },
        };

        stepBlocks.forEach(block => {
            const step = Number(block.dataset.transferStep);
            block.classList.toggle("d-none", step !== currentStep);
        });

        const descriptor = descriptors[currentStep];
        if (stepProgress) {
            stepProgress.textContent = `Шаг ${currentStep} из 4`;
        }
        if (stepHint) {
            stepHint.textContent = descriptor.hint;
        }
        if (stepBadge) {
            stepBadge.textContent = descriptor.title;
        }

        if (backButton) {
            backButton.disabled = currentStep === 1;
            backButton.classList.toggle("opacity-50", currentStep === 1);
        }

        addButton.textContent = currentStep < 4 ? "Далее" : "Сохранить в корзину";
        addButton.disabled = currentStep < 4 ? currentStep >= maxStep : !canAddToCart();

        updateConfirmSummary();
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

        if (labelOptions.length > 0 && !selectedLabelOption) {
            return false;
        }

        const labelId = selectedLabelOption?.id ?? null;
        const availableFrom = getAvailableBalance(part.id, selectedFromOperation.opNumber, labelId);
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
        if (labelSelect) {
            labelSelect.disabled = !selectedFromOperation || isLoadingOperations || isLoadingLabels;
        }

        saveButton.disabled = cart.length === 0;
        updateWizardState();
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
        currentOperationsPartId = null;
        resetLabels();
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

            operations = Array.isArray(items)
                ? items.map(raw => normalizeOperation(raw))
                : [];
            operations.forEach(operation => {
                const key = getBalanceKey(partId, operation.opNumber);
                balanceCache.set(key, Number(operation.balance ?? 0));
                operation.labelBalances = registerLabelBalances(partId, operation.opNumber, operation.labelBalances ?? []);
            });
            currentOperationsPartId = partId;
            updateOperationsDisplay(partId);
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
            currentOperationsPartId = null;
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

    function normalizeOperation(raw) {
        if (!raw || typeof raw !== "object") {
            return {
                opNumber: "",
                operationName: "",
                normHours: 0,
                balance: 0,
                isWarehouse: false,
                labelBalances: [],
            };
        }

        const opNumber = normalizeOpNumberValue(raw.opNumber ?? raw.OpNumber ?? "");
        const operationName = typeof raw.operationName === "string"
            ? raw.operationName
            : (typeof raw.OperationName === "string" ? raw.OperationName : "");
        const normHours = Number(raw.normHours ?? raw.NormHours ?? 0);
        const balance = Number(raw.balance ?? raw.Balance ?? 0);
        const isWarehouse = Boolean(raw.isWarehouse ?? raw.IsWarehouse);
        const labels = raw.labelBalances ?? raw.LabelBalances ?? raw.labels ?? raw.Labels ?? [];

        return {
            opNumber,
            operationName,
            normHours,
            balance,
            isWarehouse,
            labelBalances: labels,
        };
    }

    function normalizeLabelBalanceItems(source) {
        if (!Array.isArray(source)) {
            return [];
        }

        return source
            .map(item => {
                if (!item || typeof item !== "object") {
                    return null;
                }

                const rawId = item.id ?? item.Id ?? item.ID ?? item.labelId ?? item.LabelId ?? item.wipLabelId ?? item.WipLabelId;
                if (!rawId) {
                    return null;
                }

                const id = String(rawId);
                let numberValue = "";
                if (typeof item.number === "string" && item.number.trim().length > 0) {
                    numberValue = item.number.trim();
                }
                else if (typeof item.Number === "string" && item.Number.trim().length > 0) {
                    numberValue = item.Number.trim();
                }

                const remaining = Number(item.remainingQuantity ?? item.RemainingQuantity ?? item.remaining ?? 0);

                return {
                    id,
                    number: numberValue,
                    remainingQuantity: Number.isFinite(remaining) ? remaining : 0,
                };
            })
            .filter(item => item !== null);
    }

    function registerLabelBalances(partId, opNumber, labels) {
        const normalized = normalizeLabelBalanceItems(labels);
        normalized.forEach(label => {
            const key = getLabelBalanceKey(partId, opNumber, label.id);
            labelBalanceCache.set(key, Number(label.remainingQuantity ?? 0));
        });
        return normalized;
    }

    async function loadLabels(partId, opNumber) {
        const requestId = ++labelsRequestId;
        if (labelsAbortController) {
            labelsAbortController.abort();
        }

        if (!partId || !opNumber) {
            labelsAbortController = null;
            isLoadingLabels = false;
            resetLabels();
            updateFormState();
            return;
        }

        labelsAbortController = new AbortController();
        const signal = labelsAbortController.signal;

        isLoadingLabels = true;
        updateFormState();

        try {
            const response = await fetch(`/wip/transfer/labels?partId=${encodeURIComponent(partId)}&opNumber=${encodeURIComponent(opNumber)}`, { signal });
            if (!response.ok) {
                throw new Error("Не удалось загрузить ярлыки.");
            }

            const data = await response.json();
            if (requestId !== labelsRequestId) {
                return;
            }

            labelOptions = Array.isArray(data)
                ? data.map(option => ({
                    id: option.id ?? option.Id ?? option.ID ?? "",
                    number: option.number ?? option.Number ?? "",
                    quantity: Number(option.quantity ?? option.Quantity ?? 0),
                    remainingQuantity: Number(option.remainingQuantity ?? option.remainingquantity ?? 0),
                }))
                : [];

            selectedLabelOption = labelOptions.find(option => selectedLabelOption && option.id === selectedLabelOption.id) ?? null;
            updateLabelSelect();

            const normalizedLabels = registerLabelBalances(partId, opNumber, labelOptions);
            const summaries = normalizedLabels
                .map(formatLabelBalanceLabel)
                .filter(text => text.length > 0);
            balanceLabels.set(getBalanceKey(partId, opNumber), summaries);
            updateOperationsDisplay(partId);
            updateBalanceLabels();
        }
        catch (error) {
            if (signal.aborted) {
                return;
            }

            console.error(error);
            if (requestId === labelsRequestId) {
                labelOptions = [];
                selectedLabelOption = null;
                updateLabelSelect();
                if (labelHintElement) {
                    labelHintElement.textContent = "Не удалось загрузить список ярлыков.";
                }
            }
        }
        finally {
            if (requestId === labelsRequestId) {
                isLoadingLabels = false;
                labelsAbortController = null;
                updateFormState();
                updateLabelHint();
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
                const labelBalances = registerLabelBalances(part.id, data.from.opNumber, data.from.labelBalances ?? []);
                const summaries = labelBalances
                    .map(formatLabelBalanceLabel)
                    .filter(text => text.length > 0);
                if (summaries.length) {
                    balanceLabels.set(key, summaries);
                }
                else {
                    balanceLabels.set(key, normalizeLabelArray(data.from.labels));
                }
            }

            if (data?.to) {
                const key = getBalanceKey(part.id, data.to.opNumber);
                balanceCache.set(key, Number(data.to.balance) || 0);
                const labelBalances = registerLabelBalances(part.id, data.to.opNumber, data.to.labelBalances ?? []);
                const summaries = labelBalances
                    .map(formatLabelBalanceLabel)
                    .filter(text => text.length > 0);
                if (summaries.length) {
                    balanceLabels.set(key, summaries);
                }
                else {
                    balanceLabels.set(key, normalizeLabelArray(data.to.labels));
                }
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
                updateOperationsDisplay(part.id);
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
            updateLabelHint();
            updateFormState();
            return;
        }

        const fromNumber = selectedFromOperation?.opNumber;
        const toNumber = selectedToOperation?.opNumber;
        const labelId = selectedLabelOption?.id ?? null;
        const fromAvailable = fromNumber ? getAvailableBalance(part.id, fromNumber, labelId) : 0;
        const toAvailable = toNumber ? getAvailableBalance(part.id, toNumber) : 0;
        fromBalanceLabel.textContent = `${fromAvailable.toLocaleString("ru-RU")} шт`;
        toBalanceLabel.textContent = `${toAvailable.toLocaleString("ru-RU")} шт`;
        const fromLabels = fromNumber ? balanceLabels.get(getBalanceKey(part.id, fromNumber)) : null;
        const toLabels = toNumber ? balanceLabels.get(getBalanceKey(part.id, toNumber)) : null;
        updateLabelElement(fromLabelsElement, fromLabels);
        updateLabelElement(toLabelsElement, toLabels);
        updateLabelHint();
        updateFormState();
    }

    function resetLabels() {
        labelOptions = [];
        selectedLabelOption = null;
        updateLabelSelect();
    }

    function updateLabelSelect() {
        if (!labelSelect) {
            return;
        }

        labelSelect.innerHTML = "";

        const placeholder = document.createElement("option");
        placeholder.value = "";
        placeholder.textContent = labelOptions.length ? "— Не выбран —" : "— Свободных ярлыков нет —";
        labelSelect.appendChild(placeholder);

        labelOptions.forEach(option => {
            const element = document.createElement("option");
            element.value = option.id;
            element.textContent = formatLabelOption(option);
            labelSelect.appendChild(element);
        });

        if (selectedLabelOption) {
            const existing = labelOptions.find(option => option.id === selectedLabelOption.id);
            if (existing) {
                selectedLabelOption = existing;
                labelSelect.value = existing.id;
            }
            else {
                selectedLabelOption = null;
                labelSelect.value = "";
            }
        }
        else {
            labelSelect.value = "";
        }

        updateLabelHint();
    }

    function updateLabelHint() {
        if (!labelHintElement) {
            return;
        }

        if (isLoadingLabels) {
            labelHintElement.textContent = "Загрузка доступных ярлыков...";
            return;
        }

        if (!labelOptions.length) {
            labelHintElement.textContent = "Свободных ярлыков на выбранной операции нет.";
            return;
        }

        if (!selectedLabelOption) {
            labelHintElement.textContent = "Выберите ярлык с подходящим остатком.";
            return;
        }

        const remainingText = Number(selectedLabelOption.remainingQuantity ?? selectedLabelOption.remainingquantity ?? 0)
            .toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 });
        const totalText = Number(selectedLabelOption.quantity ?? 0)
            .toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 });
        const numberText = selectedLabelOption.number ? `№ ${selectedLabelOption.number}` : String(selectedLabelOption.id);
        labelHintElement.textContent = `${numberText}: остаток ${remainingText} из ${totalText} шт.`;
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
        currentStep = 1;
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

        const label = selectedLabelOption;
        if (labelOptions.length > 0 && !label) {
            const confirmed = confirm("Добавить передачу без списания по ярлыку?");
            if (!confirmed) {
                return;
            }
        }

        const fromAvailable = getAvailableBalance(part.id, selectedFromOperation.opNumber, label?.id ?? null);
        if (quantity > fromAvailable) {
            alert(`Нельзя передать больше, чем остаток (${fromAvailable.toFixed(3)}).`);
            return;
        }

        const toAvailable = getAvailableBalance(part.id, selectedToOperation.opNumber);
        const fromLabelKey = getBalanceKey(part.id, selectedFromOperation.opNumber);
        const fromLabels = balanceLabels.get(fromLabelKey);
        const labelQuantityBeforeValue = label ? Number(label.remainingQuantity ?? label.remainingquantity ?? 0) : null;
        const labelQuantityTotalValue = label ? Number(label.quantity ?? 0) : null;
        let labelQuantityAfterValue = labelQuantityBeforeValue;

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
            labelId: label ? label.id : null,
            labelNumber: label ? (label.number ?? null) : null,
            labelQuantityBefore: labelQuantityBeforeValue,
            labelQuantityAfter: labelQuantityBeforeValue,
            labelQuantityTotal: labelQuantityTotalValue,
        };

        const leftover = item.fromBalanceAfter;
        let fromDelta = -quantity;
        let scrapQuantityValue = 0;
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
                scrapQuantityValue = leftover;
            }
        }

        const labelConsumption = quantity + scrapQuantityValue;
        if (label && labelQuantityBeforeValue !== null) {
            if (labelConsumption > labelQuantityBeforeValue + 1e-9) {
                alert(`Ярлык ${label.number ?? label.id} имеет недостаточный остаток (${labelQuantityBeforeValue.toFixed(3)}).`);
                return;
            }

            labelQuantityAfterValue = labelQuantityBeforeValue - labelConsumption;
            item.labelQuantityAfter = labelQuantityAfterValue;
        }
        else {
            item.labelQuantityAfter = labelQuantityAfterValue;
        }

        applyPendingChange(part.id, selectedFromOperation.opNumber, fromDelta);
        applyPendingChange(part.id, selectedToOperation.opNumber, quantity);
        if (label && labelQuantityBeforeValue !== null) {
            applyLabelPendingChange(part.id, selectedFromOperation.opNumber, label.id, -labelConsumption);
        }
        cart.push(item);

        if (label && labelQuantityBeforeValue !== null) {
            const updatedRemaining = labelQuantityAfterValue ?? 0;
            if (updatedRemaining <= 1e-9) {
                labelOptions = labelOptions.filter(option => option.id !== label.id);
                selectedLabelOption = null;
            }
            else {
                const index = labelOptions.findIndex(option => option.id === label.id);
                if (index >= 0) {
                    labelOptions[index] = {
                        ...labelOptions[index],
                        remainingQuantity: updatedRemaining,
                    };
                    selectedLabelOption = labelOptions[index];
                }
            }

            updateLabelSelect();
        }

        updateOperationsDisplay(part.id);
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
                <td>${formatSelectedLabel(item)}</td>
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

    function formatLabelBalanceLabel(label) {
        if (!label) {
            return "";
        }

        const remaining = Number(label.remainingQuantity ?? 0);
        if (!(remaining > 0)) {
            return "";
        }

        const number = typeof label.number === "string" && label.number.trim().length > 0
            ? label.number.trim()
            : String(label.id ?? "");
        const remainingText = remaining.toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 });
        return `${number} — остаток ${remainingText} шт`;
    }

    function formatLabelOption(option) {
        if (!option) {
            return "";
        }

        const number = typeof option.number === "string" && option.number.trim().length > 0
            ? option.number.trim()
            : String(option.id ?? "");
        const remaining = Number(option.remainingQuantity ?? option.remainingquantity ?? 0)
            .toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 });
        const total = Number(option.quantity ?? 0)
            .toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 });

        return `${number} — осталось ${remaining} из ${total}`;
    }

    function formatSelectedLabel(item) {
        if (!item) {
            return "<span class=\"text-muted\">—</span>";
        }

        const number = item.labelNumber
            ? item.labelNumber
            : (Array.isArray(item.labelNumbers) && item.labelNumbers.length ? item.labelNumbers[0] : "");

        if (!number) {
            return formatLabelList(item.labelNumbers);
        }

        const before = item.labelQuantityBefore;
        const after = item.labelQuantityAfter;
        let detail = "";

        if (before !== undefined && before !== null && after !== undefined && after !== null) {
            const beforeText = Number(before).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 });
            const afterText = Number(after).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 });
            detail = `<div class="small text-muted">остаток: ${beforeText} → ${afterText}</div>`;
        }

        return `<div>${escapeHtml(number)}${detail}</div>`;
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
            const statusBadge = `<span class=\"badge ${item.isReverted ? "bg-danger" : "bg-success"}\">${item.isReverted ? "Отменено" : "Активно"}</span>`;

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
                <td>${formatSelectedLabel(item)}</td>
                <td>${quantityText}</td>
                <td>${fromBalanceText}</td>
                <td>${toBalanceText}</td>
                <td>${formatScrapCell(item)}</td>
                <td>${statusBadge}</td>
                <td class="text-center">
                    <button type="button" class="btn btn-outline-danger btn-sm" data-action="revert" data-index="${index}" data-audit-id="${item.transferAuditId}" ${item.isReverted ? "disabled" : ""}>Откатить</button>
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
                transferAuditId: item.transferAuditId,
                transactionId: item.transactionId,
                partId: item.partId,
                partDisplay,
                date: cartItem?.date ?? null,
                fromOpNumber: item.fromOpNumber,
                fromOperationName: cartItem?.fromOperationName ?? null,
                toOpNumber: item.toOpNumber,
                toOperationName: cartItem?.toOperationName ?? null,
                labelNumbers: normalizeLabelArray(item.labelNumbers ?? cartItem?.labelNumbers),
                labelId: item.wipLabelId ?? cartItem?.labelId ?? null,
                labelNumber: item.labelNumber ?? cartItem?.labelNumber ?? null,
                labelQuantityBefore: item.labelQuantityBefore ?? cartItem?.labelQuantityBefore ?? null,
                labelQuantityAfter: item.labelQuantityAfter ?? cartItem?.labelQuantityAfter ?? null,
                quantity: Number(item.quantity) || 0,
                fromBalanceBefore: Number(item.fromBalanceBefore) || 0,
                fromBalanceAfter: Number(item.fromBalanceAfter) || 0,
                toBalanceBefore: Number(item.toBalanceBefore) || 0,
                toBalanceAfter: Number(item.toBalanceAfter) || 0,
                fromSectionId: item.fromSectionId,
                toSectionId: item.toSectionId,
                isReverted: Boolean(item.isReverted),
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
            recentTransfers = recentTransfers.filter(existing => existing.transferAuditId !== entry.transferAuditId);
            recentTransfers.unshift(entry);
        });

        if (recentTransfers.length > RECENT_TRANSFER_LIMIT) {
            recentTransfers = recentTransfers.slice(0, RECENT_TRANSFER_LIMIT);
        }

        renderRecentTransfers();
    }

    async function revertRecentTransfer(index) {
        const item = recentTransfers[index];
        if (!item) {
            return;
        }

        const description = [
            item.partDisplay ?? item.partId ?? "",
            item.fromOpNumber && item.toOpNumber ? `${item.fromOpNumber} → ${item.toOpNumber}` : "",
            Number(item.quantity).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 }),
        ].filter(Boolean).join(" / ");

        if (!window.confirm(`Откатить передачу?\n${description}`)) {
            return;
        }

        if (!item.transferAuditId) {
            alert("Не найден идентификатор аудита для отката.");
            return;
        }

        try {
            const response = await fetch(`/wip/transfer/revert/${encodeURIComponent(item.transferAuditId)}`, {
                method: "POST",
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
            recentTransfers[index] = { ...item, isReverted: true };
            renderRecentTransfers();
            applyDeleteResult(result);
            alert("Передача откатена.");
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
        balanceLabels.delete(toKey);

        const part = partLookup.getSelected();
        if (part && part.id === partId) {
            if (selectedFromOperation && selectedFromOperation.opNumber === result.fromOpNumber) {
                void loadLabels(part.id, selectedFromOperation.opNumber);
            }
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
            if (removed.labelId) {
                const labelDelta = Number(removed.quantity ?? 0) + Number(removed.scrapQuantity ?? 0);
                applyLabelPendingChange(removed.partId, removed.fromOpNumber, removed.labelId, labelDelta);
                restoreLabelOptionFromCartItem(removed);
            }
            updateOperationsDisplay(removed.partId);
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
        if (item.labelId) {
            const labelDelta = Number(item.quantity ?? 0) + Number(item.scrapQuantity ?? 0);
            applyLabelPendingChange(item.partId, item.fromOpNumber, item.labelId, labelDelta);
        }

        updateOperationsDisplay(item.partId);

        renderCart();

        partLookup.setSelected({ id: item.partId, name: item.partName, code: item.partCode });
        if (item.labelId) {
            selectedLabelOption = {
                id: item.labelId,
                number: item.labelNumber ?? item.labelId,
                quantity: Number(item.labelQuantityTotal ?? item.labelQuantityBefore ?? 0),
                remainingQuantity: Number(item.labelQuantityBefore ?? 0),
            };
        }
        else {
            selectedLabelOption = null;
        }
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
            await loadLabels(item.partId, fromOperation.opNumber);
            restoreLabelOptionFromCartItem(item);
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
                wipLabelId: item.labelId,
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
            labelPendingChanges.clear();
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
            const labelSummaries = [];

            const rawLabelId = item.wipLabelId ?? item.labelId ?? null;
            if (rawLabelId) {
                const labelId = String(rawLabelId);
                const remaining = Number(item.labelQuantityAfter ?? item.labelQuantityBefore ?? 0);
                const number = item.labelNumber ?? (Array.isArray(item.labelNumbers) && item.labelNumbers.length ? item.labelNumbers[0] : null);
                const labelKey = getLabelBalanceKey(item.partId, item.fromOpNumber, labelId);
                labelBalanceCache.set(labelKey, remaining);
                labelPendingChanges.delete(labelKey);
                const labelText = formatLabelBalanceLabel({ id: labelId, number, remainingQuantity: remaining });
                if (labelText) {
                    labelSummaries.push(labelText);
                }
            }

            if (!labelSummaries.length) {
                const normalized = normalizeLabelArray(item.labelNumbers);
                if (normalized.length) {
                    labelSummaries.push(...normalized);
                }
            }

            balanceLabels.set(fromKey, labelSummaries);
            balanceLabels.delete(toKey);
        });

        const part = partLookup.getSelected();
        if (part && part.id) {
            if (selectedFromOperation) {
                const matches = summary.items.some(item => item.partId === part.id && item.fromOpNumber === selectedFromOperation.opNumber);
                if (matches) {
                    void loadLabels(part.id, selectedFromOperation.opNumber);
                }
            }
            updateOperationsDisplay(part.id);
        }

        updateBalanceLabels();
    }

    function updateOperationsDisplay(partId) {
        if (!operations.length) {
            return;
        }

        const actualPartId = currentOperationsPartId ?? partId;
        if (!actualPartId || (partId && actualPartId !== partId)) {
            return;
        }

        operations = operations.map(operation => {
            const opNumber = operation.opNumber;
            const updatedBalance = getAvailableBalance(actualPartId, opNumber);
            let labelBalances = Array.isArray(operation.labelBalances) ? operation.labelBalances : [];
            if (labelBalances.length) {
                labelBalances = labelBalances
                    .map(label => {
                        const available = getAvailableBalance(actualPartId, opNumber, label.id);
                        return { ...label, remainingQuantity: available };
                    })
                    .filter(label => Number(label.remainingQuantity ?? 0) > 1e-9);

                const labelTexts = labelBalances
                    .map(formatLabelBalanceLabel)
                    .filter(text => text.length > 0);
                balanceLabels.set(getBalanceKey(actualPartId, opNumber), labelTexts);
            }
            else {
                balanceLabels.set(getBalanceKey(actualPartId, opNumber), []);
            }

            return {
                ...operation,
                balance: updatedBalance,
                labelBalances,
            };
        });

        updateFromOperationsDatalist();
        updateToOperationsDatalist();
        updateFromOperationHelper();
        updateToOperationHelper();

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
            const labelSource = { ...cartItem, ...item };
            const labelsHtml = formatSelectedLabel(labelSource);
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
        onEnter: () => addButton?.click(),
        onSave: () => void saveCart(),
        onCancel: () => resetForm(),
    });
})();
