(function () {
    const namespace = window.UchetNZP;
    if (!namespace) {
        throw new Error("Не инициализировано пространство имён UchetNZP.");
    }

    const partLookup = namespace.initSearchableInput({
        input: document.getElementById("launchPartInput"),
        datalist: document.getElementById("launchPartOptions"),
        hiddenInput: document.getElementById("launchPartId"),
        fetchUrl: "/wip/launch/parts",
        minLength: 2,
    });

    const operationInput = document.getElementById("launchOperationInput");
    const operationOptions = document.getElementById("launchOperationOptions");
    const operationNumberInput = document.getElementById("launchOperationNumber");
    const remainderLabel = document.getElementById("launchRemainderLabel");
    const tailTableBody = document.querySelector("#launchTailTable tbody");
    const hoursLabel = document.getElementById("launchHoursLabel");
    const normLabel = document.getElementById("launchNormLabel");
    const quantityInput = document.getElementById("launchQuantityInput");
    const commentInput = document.getElementById("launchCommentInput");
    const dateInput = document.getElementById("launchDateInput");
    const addButton = document.getElementById("launchAddButton");
    const saveButton = document.getElementById("launchSaveButton");
    const resetButton = document.getElementById("launchResetButton");
    const exportButton = document.getElementById("launchExportButton");
    const exportFromInput = document.getElementById("launchExportFrom");
    const exportToInput = document.getElementById("launchExportTo");
    const cartTableBody = document.querySelector("#launchCartTable tbody");
    const summaryModalElement = document.getElementById("launchSummaryModal");
    const summaryTableBody = document.querySelector("#launchSummaryTable tbody");
    const summaryIntro = document.getElementById("launchSummaryIntro");

    const bootstrapModal = summaryModalElement ? new bootstrap.Modal(summaryModalElement) : null;

    operationInput.disabled = true;
    quantityInput.disabled = true;
    commentInput.disabled = true;
    addButton.disabled = true;
    saveButton.disabled = true;

    const today = new Date().toISOString().slice(0, 10);
    dateInput.value = today;
    exportFromInput.value = today;
    exportToInput.value = today;

    let operations = [];
    let selectedOperation = null;
    let tailSummary = null;
    const baselineRemainders = new Map();
    const pendingLaunches = new Map();
    let cart = [];
    let isLoadingOperations = false;
    let isLoadingTail = false;
    let operationsAbortController = null;
    let tailAbortController = null;
    let operationsRequestId = 0;
    let tailRequestId = 0;
    const addButtonLabel = addButton ? addButton.textContent : "";

    partLookup.inputElement?.addEventListener("lookup:selected", event => {
        const part = event.detail;
        if (part && part.id) {
            loadOperations(part.id);
        }
    });

    partLookup.inputElement?.addEventListener("input", () => {
        operations = [];
        selectedOperation = null;
        tailSummary = null;
        updateOperationsDatalist();
        updateRemainderLabel();
        renderTail();
        updateHours();
        updateStepState();
    });

    operationInput.addEventListener("input", () => {
        operationNumberInput.value = "";
        selectedOperation = null;
        tailSummary = null;
        renderTail();
        updateRemainderLabel();
        updateHours();
        updateStepState();
    });

    operationInput.addEventListener("change", () => {
        const value = operationInput.value.trim();
        const operation = operations.find(x => formatOperation(x) === value);
        if (operation) {
            selectedOperation = operation;
            operationNumberInput.value = operation.opNumber;
            tailSummary = null;
            updateRemainderLabel();
            void loadTail(operation);
        }
        else {
            selectedOperation = null;
            tailSummary = null;
            operationNumberInput.value = "";
            updateRemainderLabel();
            renderTail();
            updateHours();
        }
        updateStepState();
    });

    quantityInput.addEventListener("input", () => {
        updateHours();
        updateStepState();
    });

    dateInput.addEventListener("change", () => updateStepState());
    dateInput.addEventListener("input", () => updateStepState());

    addButton.addEventListener("click", () => addToCart());
    resetButton.addEventListener("click", () => resetForm());
    saveButton.addEventListener("click", () => saveCart());
    exportButton.addEventListener("click", () => exportLaunches());

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

    function formatOperation(operation) {
        const parts = [operation.opNumber];
        if (operation.operationName) {
            parts.push(operation.operationName);
        }

        parts.push(`${operation.normHours.toFixed(3)} н/ч`);
        parts.push(`остаток: ${(operation.balance ?? 0).toFixed(3)}`);
        return parts.join(" | ");
    }

    function getRemainderKey(partId, opNumber) {
        return `${partId}:${opNumber}`;
    }

    function getAvailableQuantity(partId, opNumber, fallbackBalance) {
        const key = getRemainderKey(partId, opNumber);
        const base = baselineRemainders.get(key);
        const pending = pendingLaunches.get(key) ?? 0;
        const baseline = base ?? fallbackBalance ?? 0;
        return baseline - pending;
    }

    function canAddToCart() {
        const part = partLookup.getSelected();
        if (!part || !part.id || !selectedOperation) {
            return false;
        }

        if (isLoadingOperations || isLoadingTail || !tailSummary) {
            return false;
        }

        const quantity = Number(quantityInput.value);
        if (!quantity || quantity <= 0) {
            return false;
        }

        if (!dateInput.value) {
            return false;
        }

        const available = getAvailableQuantity(part.id, selectedOperation.opNumber, selectedOperation.balance ?? 0);
        if (available < 0) {
            return false;
        }

        return quantity <= available + 1e-9;
    }

    function updateStepState() {
        const partSelected = !!(partLookup.getSelected()?.id);
        operationInput.disabled = !partSelected || isLoadingOperations;

        const hasOperation = !!selectedOperation;
        quantityInput.disabled = !hasOperation || isLoadingTail;
        commentInput.disabled = !hasOperation;

        const loadingMessage = "Подождите…";
        if (isLoadingTail) {
            addButton.textContent = loadingMessage;
        }
        else if (addButtonLabel) {
            addButton.textContent = addButtonLabel;
        }

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
        updateStepState();
        operationInput.value = "";
        operationNumberInput.value = "";
        operations = [];
        selectedOperation = null;
        tailSummary = null;
        renderTail();
        updateHours();
        updateRemainderLabel();
        try {
            const response = await fetch(`/wip/launch/operations?partId=${encodeURIComponent(partId)}`, { signal });
            if (!response.ok) {
                throw new Error("Не удалось загрузить операции детали.");
            }

            const items = await response.json();
            if (requestId !== operationsRequestId) {
                return;
            }

            operations = Array.isArray(items) ? items : [];
            operations.forEach(operation => {
                const key = getRemainderKey(partId, operation.opNumber);
                baselineRemainders.set(key, operation.balance ?? 0);
            });
            updateOperationsDatalist();
            updateRemainderLabel();
            renderTail();
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
            updateOperationsDatalist();
            updateRemainderLabel();
            renderTail();
        }
        finally {
            if (requestId === operationsRequestId) {
                isLoadingOperations = false;
                operationsAbortController = null;
                updateStepState();
            }
        }
    }

    function updateOperationsDatalist() {
        operationOptions.innerHTML = "";
        operations.forEach(operation => {
            const option = document.createElement("option");
            option.value = formatOperation(operation);
            operationOptions.appendChild(option);
        });
    }

    async function loadTail(operation) {
        const part = partLookup.getSelected();
        if (!part || !part.id) {
            return;
        }

        const requestId = ++tailRequestId;
        if (tailAbortController) {
            tailAbortController.abort();
        }

        tailAbortController = new AbortController();
        const signal = tailAbortController.signal;

        isLoadingTail = true;
        updateStepState();
        try {
            const response = await fetch(`/wip/launch/tail?partId=${encodeURIComponent(part.id)}&opNumber=${encodeURIComponent(operation.opNumber)}`, { signal });
            if (!response.ok) {
                throw new Error("Не удалось рассчитать хвост маршрута.");
            }

            const summary = await response.json();
            if (requestId !== tailRequestId) {
                return;
            }

            tailSummary = summary;
            renderTail();
            updateHours();
        }
        catch (error) {
            if (signal.aborted) {
                return;
            }

            console.error(error);
            if (requestId === tailRequestId) {
                tailSummary = null;
                renderTail();
                updateHours();
            }
        }
        finally {
            if (requestId === tailRequestId) {
                isLoadingTail = false;
                tailAbortController = null;
                updateStepState();
            }
        }
    }

    function renderTail() {
        if (!tailSummary || !tailSummary.operations?.length) {
            tailTableBody.innerHTML = "<tr><td colspan=\"3\" class=\"text-center text-muted\">Выберите операцию, чтобы увидеть хвост маршрута.</td></tr>";
            normLabel.textContent = "0 н/ч";
            return;
        }

        normLabel.textContent = `${tailSummary.sumNormHours.toFixed(3)} н/ч`;
        tailTableBody.innerHTML = "";
        tailSummary.operations.forEach(operation => {
            const row = document.createElement("tr");
            row.innerHTML = `
                <td>${operation.opNumber}</td>
                <td>${operation.operationName ?? ""}</td>
                <td>${operation.normHours.toFixed(3)}</td>`;
            tailTableBody.appendChild(row);
        });
    }

    function updateHours() {
        const quantity = Number(quantityInput.value) || 0;
        const sumNorm = tailSummary ? tailSummary.sumNormHours : 0;
        const hours = quantity * sumNorm;
        hoursLabel.textContent = `${hours.toFixed(3)} ч`;
        if (tailSummary) {
            normLabel.textContent = `${tailSummary.sumNormHours.toFixed(3)} н/ч`;
        }
    }

    function updateRemainderLabel() {
        const part = partLookup.getSelected();
        if (!part || !part.id || !selectedOperation) {
            remainderLabel.textContent = "0 шт";
            updateStepState();
            return;
        }

        const key = getRemainderKey(part.id, selectedOperation.opNumber);
        const available = getAvailableQuantity(part.id, selectedOperation.opNumber, selectedOperation.balance ?? 0);
        remainderLabel.textContent = `${available.toLocaleString("ru-RU")} шт`;
        updateStepState();
    }

    function resetForm() {
        quantityInput.value = "";
        commentInput.value = "";
        dateInput.value = today;
        selectedOperation = null;
        tailSummary = null;
        operationInput.value = "";
        operationNumberInput.value = "";
        renderTail();
        updateOperationsDatalist();
        updateRemainderLabel();
        updateHours();
        updateStepState();
    }

    async function addToCart() {
        const part = partLookup.getSelected();
        if (!part || !part.id) {
            alert("Выберите деталь.");
            return;
        }

        if (!selectedOperation) {
            alert("Выберите операцию.");
            return;
        }

        if (!tailSummary) {
            await loadTail(selectedOperation);
            if (!tailSummary) {
                alert("Не удалось рассчитать хвост маршрута.");
                return;
            }
        }

        const quantity = Number(quantityInput.value);
        if (!quantity || quantity <= 0) {
            alert("Количество запуска должно быть больше нуля.");
            return;
        }

        const key = getRemainderKey(part.id, selectedOperation.opNumber);
        const pending = pendingLaunches.get(key) ?? 0;
        const available = getAvailableQuantity(part.id, selectedOperation.opNumber, selectedOperation.balance ?? 0);
        if (quantity > available) {
            alert(`Нельзя запустить больше, чем остаток (${available.toFixed(3)}).`);
            return;
        }

        const date = dateInput.value;
        if (!date) {
            alert("Укажите дату запуска.");
            return;
        }

        const hours = quantity * (tailSummary.sumNormHours ?? 0);
        const tailOperations = tailSummary.operations ?? [];
        const lastOp = tailOperations.length ? tailOperations[tailOperations.length - 1].opNumber : selectedOperation.opNumber;
        const item = {
            partId: part.id,
            partName: part.name,
            partCode: part.code ?? null,
            partDisplay: part.code ? `${part.name} (${part.code})` : part.name,
            opNumber: selectedOperation.opNumber,
            operationName: selectedOperation.operationName,
            operationDisplay: formatOperation(selectedOperation),
            fromOp: selectedOperation.opNumber,
            toOp: lastOp,
            date,
            quantity,
            comment: commentInput.value || null,
            hours,
            sumNorm: tailSummary.sumNormHours ?? 0,
            tailOperations: tailOperations.slice(),
        };

        pendingLaunches.set(key, pending + quantity);
        cart.push(item);
        renderCart();
        resetForm();
        updateRemainderLabel();
    }

    function renderCart() {
        if (!cart.length) {
            cartTableBody.innerHTML = "<tr><td colspan=\"7\" class=\"text-center text-muted\">Добавьте операции запуска для сохранения.</td></tr>";
            updateStepState();
            return;
        }

        cartTableBody.innerHTML = "";
        cart.forEach((item, index) => {
            const row = document.createElement("tr");
            row.innerHTML = `
                <td>${item.date}</td>
                <td>${item.partDisplay}</td>
                <td>${item.fromOp} / ${item.toOp}</td>
                <td>${item.quantity.toFixed(3)}</td>
                <td>${item.hours.toFixed(3)}</td>
                <td>${item.comment ?? ""}</td>
                <td class="text-center">
                    <button type="button" class="btn btn-link btn-lg text-decoration-none" data-action="edit" data-index="${index}" aria-label="Изменить запись">✎</button>
                    <button type="button" class="btn btn-link btn-lg text-decoration-none text-danger" data-action="remove" data-index="${index}" aria-label="Удалить запись">✖</button>
                </td>`;
            cartTableBody.appendChild(row);
        });
        updateStepState();
    }

    function removeCartItem(index) {
        const [removed] = cart.splice(index, 1);
        if (removed) {
            const key = getRemainderKey(removed.partId, removed.opNumber);
            const current = pendingLaunches.get(key) ?? 0;
            pendingLaunches.set(key, current - removed.quantity);
            if (pendingLaunches.get(key) <= 0) {
                pendingLaunches.delete(key);
            }
        }

        renderCart();
        updateRemainderLabel();
    }

    async function editCartItem(index) {
        const item = cart.splice(index, 1)[0];
        if (!item) {
            return;
        }

        const key = getRemainderKey(item.partId, item.opNumber);
        const current = pendingLaunches.get(key) ?? 0;
        pendingLaunches.set(key, current - item.quantity);
        if (pendingLaunches.get(key) <= 0) {
            pendingLaunches.delete(key);
        }

        renderCart();

        partLookup.setSelected({ id: item.partId, name: item.partName, code: item.partCode });
        commentInput.value = item.comment ?? "";
        quantityInput.value = item.quantity;
        dateInput.value = item.date;

        await loadOperations(item.partId);
        const operation = operations.find(op => op.opNumber === item.opNumber);
        if (operation) {
            selectedOperation = operation;
            operationInput.value = formatOperation(operation);
            operationNumberInput.value = operation.opNumber;
            tailSummary = {
                operations: item.tailOperations,
                sumNormHours: item.sumNorm,
            };
            renderTail();
            updateHours();
        }
        else {
            selectedOperation = null;
            tailSummary = null;
            renderTail();
            updateHours();
        }

        updateRemainderLabel();
    }

    async function saveCart() {
        if (!cart.length) {
            alert("Корзина пуста.");
            return;
        }

        if (!window.confirm("Сохранить выбранные запуски?")) {
            return;
        }

        const payload = {
            items: cart.map(item => ({
                partId: item.partId,
                fromOpNumber: item.fromOp,
                launchDate: item.date,
                quantity: item.quantity,
                comment: item.comment,
            })),
        };

        saveButton.disabled = true;
        try {
            const response = await fetch("/wip/launch/save", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Accept": "application/json",
                },
                body: JSON.stringify(payload),
            });

            if (!response.ok) {
                throw new Error("Не удалось сохранить запуски.");
            }

            const summary = await response.json();
            showSummary(summary);
            updateRemaindersAfterSave(summary);
            pendingLaunches.clear();
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

    function updateRemaindersAfterSave(summary) {
        if (!summary || !summary.items) {
            return;
        }

        summary.items.forEach(item => {
            const key = getRemainderKey(item.partId, item.fromOpNumber);
            baselineRemainders.set(key, item.remaining);
        });

        updateOperationsDisplay();
        updateRemainderLabel();
    }

    function updateOperationsDisplay() {
        const part = partLookup.getSelected();
        if (!part || !part.id) {
            return;
        }

        operations = operations.map(operation => {
            const key = getRemainderKey(part.id, operation.opNumber);
            const updated = baselineRemainders.get(key);
            if (updated !== undefined) {
                return { ...operation, balance: updated };
            }

            return operation;
        });

        updateOperationsDatalist();

        if (selectedOperation) {
            const refreshed = operations.find(op => op.opNumber === selectedOperation.opNumber);
            if (refreshed) {
                selectedOperation = refreshed;
                operationInput.value = formatOperation(refreshed);
            }
        }
    }

    function showSummary(summary) {
        summaryTableBody.innerHTML = "";
        summaryIntro.textContent = `Сохранено записей: ${summary.saved}.`;

        summary.items.forEach(item => {
            const row = document.createElement("tr");
            const cartItem = cart.find(x => x.partId === item.partId && x.fromOp === item.fromOpNumber);
            row.innerHTML = `
                <td>${cartItem ? cartItem.partDisplay : item.partId}</td>
                <td>${item.fromOpNumber}</td>
                <td>${item.quantity.toFixed(3)}</td>
                <td>${item.remaining.toFixed(3)}</td>
                <td>${item.sumHoursToFinish.toFixed(3)}</td>`;
            summaryTableBody.appendChild(row);
        });

        if (bootstrapModal) {
            bootstrapModal.show();
        }
    }

    async function exportLaunches() {
        const from = exportFromInput.value || today;
        const to = exportToInput.value || from;

        try {
            const response = await fetch(`/wip/launch/export?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`);
            if (!response.ok) {
                throw new Error("Не удалось сформировать файл.");
            }

            const blob = await response.blob();
            const url = URL.createObjectURL(blob);
            const link = document.createElement("a");
            link.href = url;
            link.download = `Запуски_${from}_${to}.xlsx`;
            document.body.appendChild(link);
            link.click();
            window.setTimeout(() => {
                URL.revokeObjectURL(url);
                link.remove();
            }, 0);
        }
        catch (error) {
            console.error(error);
            alert("Не удалось выполнить экспорт. Попробуйте ещё раз.");
        }
    }

    updateStepState();

    namespace.bindHotkeys({
        onEnter: () => addToCart(),
        onSave: () => saveCart(),
        onCancel: () => resetForm(),
    });
})();
