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
    const documentInput = document.getElementById("launchDocumentInput");
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
    });

    operationInput.addEventListener("input", () => {
        operationNumberInput.value = "";
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
    });

    quantityInput.addEventListener("input", () => updateHours());

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
        const parts = [operation.opNumber.toString().padStart(3, "0")];
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

    async function loadOperations(partId) {
        operationInput.value = "";
        operationNumberInput.value = "";
        operations = [];
        selectedOperation = null;
        tailSummary = null;
        renderTail();
        updateHours();
        try {
            const response = await fetch(`/wip/launch/operations?partId=${encodeURIComponent(partId)}`);
            if (!response.ok) {
                throw new Error("Не удалось загрузить операции детали.");
            }

            operations = await response.json();
            operations.forEach(operation => {
                const key = getRemainderKey(partId, operation.opNumber);
                baselineRemainders.set(key, operation.balance ?? 0);
            });
            updateOperationsDatalist();
            updateRemainderLabel();
            renderTail();
        }
        catch (error) {
            console.error(error);
            operations = [];
            updateOperationsDatalist();
            updateRemainderLabel();
            renderTail();
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

        try {
            const response = await fetch(`/wip/launch/tail?partId=${encodeURIComponent(part.id)}&opNumber=${encodeURIComponent(operation.opNumber)}`);
            if (!response.ok) {
                throw new Error("Не удалось рассчитать хвост маршрута.");
            }

            tailSummary = await response.json();
            renderTail();
            updateHours();
        }
        catch (error) {
            console.error(error);
            tailSummary = null;
            renderTail();
            updateHours();
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
                <td>${operation.opNumber.toString().padStart(3, "0")}</td>
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
            return;
        }

        const key = getRemainderKey(part.id, selectedOperation.opNumber);
        const base = baselineRemainders.get(key);
        const pending = pendingLaunches.get(key) ?? 0;
        const available = (base ?? selectedOperation.balance ?? 0) - pending;
        remainderLabel.textContent = `${available.toLocaleString("ru-RU")} шт`;
    }

    function resetForm() {
        quantityInput.value = "";
        documentInput.value = "";
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
        const base = baselineRemainders.get(key) ?? selectedOperation.balance ?? 0;
        const pending = pendingLaunches.get(key) ?? 0;
        const available = base - pending;
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
            documentNumber: documentInput.value || null,
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
            cartTableBody.innerHTML = "<tr><td colspan=\"8\" class=\"text-center text-muted\">Добавьте операции запуска для сохранения.</td></tr>";
            return;
        }

        cartTableBody.innerHTML = "";
        cart.forEach((item, index) => {
            const row = document.createElement("tr");
            row.innerHTML = `
                <td>${item.date}</td>
                <td>${item.partDisplay}</td>
                <td>${item.fromOp.toString().padStart(3, "0")} / ${item.toOp.toString().padStart(3, "0")}</td>
                <td>${item.quantity.toFixed(3)}</td>
                <td>${item.hours.toFixed(3)}</td>
                <td>${item.documentNumber ?? ""}</td>
                <td>${item.comment ?? ""}</td>
                <td class="text-center">
                    <button type="button" class="btn btn-link btn-lg text-decoration-none" data-action="edit" data-index="${index}" aria-label="Изменить запись">✎</button>
                    <button type="button" class="btn btn-link btn-lg text-decoration-none text-danger" data-action="remove" data-index="${index}" aria-label="Удалить запись">✖</button>
                </td>`;
            cartTableBody.appendChild(row);
        });
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
        documentInput.value = item.documentNumber ?? "";
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
                documentNumber: item.documentNumber,
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
                <td>${item.fromOpNumber.toString().padStart(3, "0")}</td>
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

    namespace.bindHotkeys({
        onEnter: () => addToCart(),
        onSave: () => saveCart(),
        onCancel: () => resetForm(),
    });
})();
