(function () {
    const namespace = window.UchetNZP;
    if (!namespace) {
        throw new Error("Не инициализировано пространство имён UchetNZP.");
    }

    const operationsTableBody = document.querySelector("#receiptOperationsTable tbody");
    const cartTableBody = document.querySelector("#receiptCartTable tbody");
    const summaryTableBody = document.querySelector("#receiptSummaryTable tbody");
    const summaryIntro = document.getElementById("receiptSummaryIntro");
    const summaryModalElement = document.getElementById("receiptSummaryModal");
    const balanceLabel = document.getElementById("receiptBalanceLabel");

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
    const addButton = document.getElementById("receiptAddButton");
    const saveButton = document.getElementById("receiptSaveButton");
    const resetButton = document.getElementById("receiptResetButton");

    dateInput.value = new Date().toISOString().slice(0, 10);

    addButton.disabled = true;
    saveButton.disabled = true;

    const baselineBalances = new Map();
    const pendingAdjustments = new Map();
    let operations = [];
    let selectedOperation = null;
    let cart = [];
    let editingIndex = null;
    let isLoadingOperations = false;
    let isLoadingBalance = false;
    let operationsAbortController = null;
    let operationsRequestId = 0;
    let balanceRequestId = 0;
    const balanceRequests = new Map();

    const bootstrapModal = summaryModalElement ? new bootstrap.Modal(summaryModalElement) : null;

    partLookup.inputElement?.addEventListener("lookup:selected", event => {
        const part = event.detail;
        if (!part || !part.id) {
            return;
        }

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
        updateFormState();
    });

    sectionLookup.inputElement?.addEventListener("input", () => {
        renderOperations();
        updateBalanceLabel();
        updateFormState();
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

        return true;
    }

    function updateFormState() {
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

    function renderCart() {
        if (!cart.length) {
            cartTableBody.innerHTML = "<tr><td colspan=\"9\" class=\"text-center text-muted\">Добавьте операции в корзину для сохранения.</td></tr>";
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

    function removeCartItem(index) {
        const [removed] = cart.splice(index, 1);
        if (removed) {
            const key = getBalanceKey(removed.partId, removed.sectionId, removed.opNumber);
            const current = pendingAdjustments.get(key) ?? 0;
            pendingAdjustments.set(key, current - removed.quantity);
            if (pendingAdjustments.get(key) <= 0) {
                pendingAdjustments.delete(key);
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

        partLookup.setSelected({ id: item.partId, name: item.partName, code: item.partCode });
        sectionLookup.setSelected({ id: item.sectionId, name: item.sectionName, code: null });
        quantityInput.value = item.quantity;
        commentInput.value = item.comment ?? "";
        dateInput.value = item.date;

        await loadOperations(item.partId);
        selectedOperation = operations.find(op => op.opNumber === item.opNumber) ?? null;
        renderOperations();
        renderCart();
        updateBalanceLabel();
    }

    addButton.addEventListener("click", () => addToCart());
    resetButton.addEventListener("click", () => resetForm());
    saveButton.addEventListener("click", () => saveCart());

    quantityInput.addEventListener("input", () => updateFormState());
    dateInput.addEventListener("change", () => updateFormState());
    dateInput.addEventListener("input", () => updateFormState());

    function resetForm() {
        editingIndex = null;
        quantityInput.value = "";
        commentInput.value = "";
        dateInput.value = new Date().toISOString().slice(0, 10);
        selectedOperation = null;
        renderOperations();
        updateBalanceLabel();
        updateFormState();
    }

    async function addToCart() {
        const part = partLookup.getSelected();
        const section = sectionLookup.getSelected();

        if (!part || !part.id) {
            alert("Выберите деталь.");
            return;
        }

        if (!section || !section.id) {
            alert("Выберите вид работ.");
            return;
        }

        if (!selectedOperation) {
            alert("Выберите операцию детали.");
            return;
        }

        if (selectedOperation.sectionId !== section.id) {
            alert("Выбранная операция относится к другому виду работ. Выберите корректный вид работ.");
            return;
        }

        const quantity = Number(quantityInput.value);
        if (!quantity || quantity < 1) {
            alert("Количество прихода должно быть не меньше 1.");
            return;
        }

        const date = dateInput.value;
        if (!date) {
            alert("Укажите дату прихода.");
            return;
        }

        const key = getBalanceKey(part.id, section.id, selectedOperation.opNumber);
        const base = await ensureBalanceLoaded(part.id, section.id, selectedOperation.opNumber);
        const pending = pendingAdjustments.get(key) ?? 0;
        const was = (base ?? 0) + pending;
        const become = was + quantity;

        pendingAdjustments.set(key, pending + quantity);

        const partDisplay = part.code ? `${part.name} (${part.code})` : part.name;
        const operationDisplay = `${selectedOperation.opNumber} ${selectedOperation.operationName ?? ""}`.trim();

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
        };

        cart.push(item);
        editingIndex = null;
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

        const payload = {
            items: cart.map(item => ({
                partId: item.partId,
                sectionId: item.sectionId,
                opNumber: item.opNumber,
                receiptDate: item.date,
                quantity: item.quantity,
                comment: item.comment,
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
                throw new Error("Не удалось сохранить приходы.");
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

    function showSummary(summary) {
        summaryTableBody.innerHTML = "";
        summaryIntro.textContent = `Сохранено записей: ${summary.saved}.`;

        summary.items.forEach(item => {
            const row = document.createElement("tr");
            const matchingCartItem = cart.find(x => x.partId === item.partId && x.sectionId === item.sectionId && x.opNumber === item.opNumber && x.quantity === item.quantity);

            row.innerHTML = `
                <td>${matchingCartItem ? matchingCartItem.partDisplay : item.partId}</td>
                <td>${matchingCartItem ? matchingCartItem.operationDisplay : item.opNumber}</td>
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

    namespace.bindHotkeys({
        onEnter: () => addToCart(),
        onSave: () => saveCart(),
        onCancel: () => resetForm(),
    });
})();
