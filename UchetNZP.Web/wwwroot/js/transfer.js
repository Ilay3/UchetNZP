(function () {
    const namespace = window.UchetNZP;
    if (!namespace) {
        throw new Error("Не инициализировано пространство имён UchetNZP.");
    }

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

    const bootstrapModal = summaryModalElement ? new bootstrap.Modal(summaryModalElement) : null;
    const scrapModal = scrapModalElement ? new bootstrap.Modal(scrapModalElement, { backdrop: "static", keyboard: false }) : null;

    const scrapTypeOptions = {
        technological: { value: "Technological", label: "Технологический" },
        employee: { value: "EmployeeFault", label: "По вине сотрудника" },
    };

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

    const balanceCache = new Map();
    const pendingChanges = new Map();
    let cart = [];

    partLookup.inputElement?.addEventListener("lookup:selected", event => {
        const part = event.detail;
        if (part && part.id) {
            void loadOperations(part.id);
        }
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
    });

    fromOperationInput.addEventListener("input", () => {
        selectedFromOperation = null;
        fromOperationNumberInput.value = "";
        updateToOperationsDatalist();
        updateBalanceLabels();
    });

    toOperationInput.addEventListener("input", () => {
        selectedToOperation = null;
        toOperationNumberInput.value = "";
        updateBalanceLabels();
    });

    fromOperationInput.addEventListener("change", () => {
        const value = fromOperationInput.value.trim();
        const operation = operations.find(x => formatOperation(x) === value);
        if (operation) {
            selectedFromOperation = operation;
            fromOperationNumberInput.value = operation.opNumber;
            if (selectedToOperation && selectedToOperation.opNumber <= operation.opNumber) {
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
    });

    toOperationInput.addEventListener("change", () => {
        const value = toOperationInput.value.trim();
        const operation = operations.find(x => formatOperation(x) === value);
        if (!operation) {
            selectedToOperation = null;
            toOperationNumberInput.value = "";
            updateBalanceLabels();
            return;
        }

        if (selectedFromOperation && operation.opNumber <= selectedFromOperation.opNumber) {
            alert("Операция после должна быть позже операции до.");
            toOperationInput.value = "";
            toOperationNumberInput.value = "";
            selectedToOperation = null;
            updateBalanceLabels();
            return;
        }

        selectedToOperation = operation;
        toOperationNumberInput.value = operation.opNumber;
        void refreshBalances();
    });

    addButton.addEventListener("click", () => void addToCart());
    resetButton.addEventListener("click", () => resetForm());
    saveButton.addEventListener("click", () => void saveCart());

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

    function formatOperation(operation) {
        const parts = [operation.opNumber.toString().padStart(3, "0")];
        if (operation.operationName) {
            parts.push(operation.operationName);
        }
        parts.push(`${operation.normHours.toFixed(3)} н/ч`);
        parts.push(`остаток: ${(operation.balance ?? 0).toFixed(3)}`);
        return parts.join(" | ");
    }

    function getBalanceKey(partId, opNumber) {
        return `${partId}:${opNumber}`;
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

    async function loadOperations(partId) {
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
            const response = await fetch(`/wip/transfer/operations?partId=${encodeURIComponent(partId)}`);
            if (!response.ok) {
                throw new Error("Не удалось загрузить операции.");
            }

            const items = await response.json();
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
            console.error(error);
            operations = [];
            updateFromOperationsDatalist();
            updateToOperationsDatalist();
            updateBalanceLabels();
        }
    }

    function updateFromOperationsDatalist() {
        fromOperationOptions.innerHTML = "";
        operations.forEach(operation => {
            const option = document.createElement("option");
            option.value = formatOperation(operation);
            fromOperationOptions.appendChild(option);
        });
    }

    function updateToOperationsDatalist() {
        toOperationOptions.innerHTML = "";
        const fromNumber = selectedFromOperation?.opNumber ?? null;
        operations
            .filter(operation => !fromNumber || operation.opNumber > fromNumber)
            .forEach(operation => {
                const option = document.createElement("option");
                option.value = formatOperation(operation);
                toOperationOptions.appendChild(option);
            });
    }

    async function refreshBalances() {
        updateBalanceLabels();

        const part = partLookup.getSelected();
        if (!part || !part.id || !selectedFromOperation || !selectedToOperation) {
            return;
        }

        const requestId = ++balanceRequestId;
        try {
            const response = await fetch(`/wip/transfer/balances?partId=${encodeURIComponent(part.id)}&fromOpNumber=${encodeURIComponent(selectedFromOperation.opNumber)}&toOpNumber=${encodeURIComponent(selectedToOperation.opNumber)}`);
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
            }

            if (data?.to) {
                const key = getBalanceKey(part.id, data.to.opNumber);
                balanceCache.set(key, Number(data.to.balance) || 0);
            }
        }
        catch (error) {
            console.error(error);
        }
        finally {
            updateBalanceLabels();
        }
    }

    function updateBalanceLabels() {
        const part = partLookup.getSelected();
        if (!part || !part.id) {
            fromBalanceLabel.textContent = "0 шт";
            toBalanceLabel.textContent = "0 шт";
            return;
        }

        const fromNumber = selectedFromOperation?.opNumber;
        const toNumber = selectedToOperation?.opNumber;
        const fromAvailable = fromNumber ? getAvailableBalance(part.id, fromNumber) : 0;
        const toAvailable = toNumber ? getAvailableBalance(part.id, toNumber) : 0;
        fromBalanceLabel.textContent = `${fromAvailable.toLocaleString("ru-RU")} шт`;
        toBalanceLabel.textContent = `${toAvailable.toLocaleString("ru-RU")} шт`;
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

        if (selectedToOperation.opNumber <= selectedFromOperation.opNumber) {
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

        const item = {
            partId: part.id,
            partName: part.name,
            partCode: part.code ?? null,
            partDisplay: part.code ? `${part.name} (${part.code})` : part.name,
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
        };

        const leftover = item.fromBalanceAfter;
        let fromDelta = -quantity;
        if (leftover > 1e-9) {
            const decision = await askScrapDecision({
                leftover,
                partDisplay: item.partDisplay,
                fromOperation: `${selectedFromOperation.opNumber.toString().padStart(3, "0")} ${selectedFromOperation.operationName ?? ""}`.trim(),
            });

            if (decision?.confirmed) {
                item.scrapType = decision.typeValue;
                item.scrapTypeLabel = decision.typeLabel;
                item.scrapQuantity = leftover;
                item.scrapComment = decision.comment ? decision.comment : null;
                item.scrap = {
                    type: item.scrapType,
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
            cartTableBody.innerHTML = "<tr><td colspan=\"10\" class=\"text-center text-muted\">Добавьте записи передачи, чтобы подготовить пакет к сохранению.</td></tr>";
            return;
        }

        cartTableBody.innerHTML = "";
        cart.forEach((item, index) => {
            const row = document.createElement("tr");
            row.innerHTML = `
                <td>${item.date}</td>
                <td>${item.partDisplay}</td>
                <td>
                    <div class="fw-semibold">${item.fromOpNumber.toString().padStart(3, "0")}</div>
                    <div class="small text-muted">${item.fromOperationName ?? ""}</div>
                </td>
                <td>
                    <div class="fw-semibold">${item.toOpNumber.toString().padStart(3, "0")}</div>
                    <div class="small text-muted">${item.toOperationName ?? ""}</div>
                </td>
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

    function getScrapLabel(type, fallbackLabel) {
        if (!type && !fallbackLabel) {
            return "";
        }

        const option = Object.values(scrapTypeOptions).find(entry => entry.value === type);
        if (option) {
            return option.label;
        }

        return fallbackLabel ?? type ?? "";
    }

    function formatScrapCell(item) {
        if (!item?.scrapType || !item?.scrapQuantity || item.scrapQuantity <= 0) {
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

    function extractScrapSource(source) {
        if (!source) {
            return null;
        }

        if (source.scrap) {
            return source.scrap;
        }

        if (source.scrapQuantity || source.scrapType || source.scrapComment || source.scrapTypeLabel) {
            return {
                quantity: source.scrapQuantity,
                type: source.scrapType,
                comment: source.scrapComment,
                typeLabel: source.scrapTypeLabel,
            };
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
        const label = getScrapLabel(type, raw.typeLabel ?? raw.scrapTypeLabel ?? null);

        if (!type || !(quantity > 0)) {
            return {
                scrapType: type,
                scrapTypeLabel: label,
                scrapQuantity: quantity,
                scrapComment: comment,
            };
        }

        return {
            scrapType: type,
            scrapTypeLabel: label,
            scrapQuantity: quantity,
            scrapComment: comment,
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
                scrap: item.scrap ?? (item.scrapType && item.scrapQuantity > 0
                    ? {
                        type: item.scrapType,
                        quantity: item.scrapQuantity,
                        comment: item.scrapComment,
                    }
                    : null),
            })),
        };

        saveButton.disabled = true;
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
            const fromText = `${item.fromOpNumber.toString().padStart(3, "0")}: ${Number(item.fromBalanceBefore).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 })} → ${Number(item.fromBalanceAfter).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 })}`;
            const toText = `${item.toOpNumber.toString().padStart(3, "0")}: ${Number(item.toBalanceBefore).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 })} → ${Number(item.toBalanceAfter).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 })}`;
            const scrapSource = extractScrapSource(item) ?? extractScrapSource(cartItem);
            const scrapInfo = normalizeScrapInfo(scrapSource);
            const scrapCell = formatScrapCell(scrapInfo ?? {});
            row.innerHTML = `
                <td>${partDisplay}</td>
                <td>${fromText}</td>
                <td>${toText}</td>
                <td>${Number(item.quantity).toLocaleString("ru-RU", { minimumFractionDigits: 3, maximumFractionDigits: 3 })}</td>
                <td>${scrapCell}</td>
                <td>${item.transferId}</td>`;
            summaryTableBody.appendChild(row);
        });

        if (bootstrapModal) {
            bootstrapModal.show();
        }
    }

    namespace.bindHotkeys({
        onEnter: () => void addToCart(),
        onSave: () => void saveCart(),
        onCancel: () => resetForm(),
    });
})();
