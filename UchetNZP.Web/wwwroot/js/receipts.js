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
                result = `${result} / ${normalizedCode}`;
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
    const labelHiddenInput = document.getElementById("receiptLabelId");
    const labelMessage = document.getElementById("receiptLabelMessage");
    const addButton = document.getElementById("receiptAddButton");
    const bulkAddButton = document.getElementById("receiptBulkAddButton");
    const saveButton = document.getElementById("receiptSaveButton");
    const resetButton = document.getElementById("receiptResetButton");
    const bulkModalElement = document.getElementById("receiptBulkModal");
    const bulkLabelsInput = document.getElementById("receiptBulkLabelsInput");
    const bulkConfirmButton = document.getElementById("receiptBulkConfirmButton");
    const materialSelect = document.getElementById("receiptMaterialSelect");
    const materialUnitInput = document.getElementById("receiptMaterialUnitInput");
    const materialAvailableLabel = document.getElementById("receiptMaterialAvailableLabel");
    const materialUnitsCountLabel = document.getElementById("receiptMaterialUnitsCountLabel");
    const materialTotalSizeLabel = document.getElementById("receiptMaterialTotalSizeLabel");
    const materialTotalWeightLabel = document.getElementById("receiptMaterialTotalWeightLabel");
    const materialEmptyState = document.getElementById("receiptMaterialEmptyState");
    const materialUnitsTableBody = document.querySelector("#receiptMaterialUnitsTable tbody");
    const materialNormPerPartLabel = document.getElementById("receiptMaterialNormPerPartLabel");
    const materialRequiredLabel = document.getElementById("receiptMaterialRequiredLabel");
    const materialRecommendationLabel = document.getElementById("receiptMaterialRecommendationLabel");

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
    let selectedLabel = null;
    let labelExistsInSystem = false;
    let labelCheckRequestId = 0;
    let currentMaterialStockSummary = "";
    let currentMaterialTotalSize = 0;
    const materialNormById = new Map();

    const bootstrapModal = summaryModalElement ? new bootstrap.Modal(summaryModalElement) : null;
    const bulkModal = bulkModalElement ? new bootstrap.Modal(bulkModalElement) : null;
    const draftStorage = typeof namespace.createDraftStorage === "function"
        ? namespace.createDraftStorage("uchetnzp.receipts.draft", { ttlMs: 24 * 60 * 60 * 1000 })
        : null;

    const labelNumberPattern = /^\d{1,5}(?:\/\d{1,5})?$/;

    function collectDraftState() {
        return {
            part: partLookup.getSelected(),
            date: dateInput.value || "",
            quantity: quantityInput.value || "",
            comment: commentInput.value || "",
            labelNumber: labelSearchInput?.value || "",
            metalMaterialId: materialSelect?.value || "",
            cart,
        };
    }

    function saveDraft() {
        draftStorage?.save(collectDraftState());
    }

    async function restoreDraft(state) {
        if (!state || typeof state !== "object") {
            return;
        }

        if (state.part?.id) {
            partLookup.setSelected({ id: state.part.id, name: state.part.name, code: state.part.code ?? null });
            await loadOperations(state.part.id);
        }

        if (typeof state.date === "string" && state.date) {
            dateInput.value = state.date;
        }
        if (state.quantity !== undefined && state.quantity !== null) {
            quantityInput.value = state.quantity;
        }
        if (typeof state.comment === "string") {
            commentInput.value = state.comment;
        }
        if (labelSearchInput && typeof state.labelNumber === "string") {
            labelSearchInput.value = state.labelNumber;
        }
        if (materialSelect && typeof state.metalMaterialId === "string") {
            materialSelect.value = state.metalMaterialId;
            await loadMaterialStock(state.metalMaterialId);
        }

        if (Array.isArray(state.cart)) {
            cart = state.cart;
            pendingAdjustments.clear();
            cart.forEach(item => {
                const key = getBalanceKey(item.partId, item.sectionId, item.opNumber);
                const current = pendingAdjustments.get(key) ?? 0;
                pendingAdjustments.set(key, current + Number(item.quantity ?? 0));
            });
            renderCart();
        }

        renderOperations();
        updateBalanceLabel();
        updateFormState();
    }

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

    partLookup.inputElement?.addEventListener("lookup:selected", async event => {
        const part = event.detail;
        if (!part || !part.id) {
            return;
        }

        await loadPartMaterials(part.id);
        loadOperations(part.id);
        updateFormState();
        saveDraft();
    });

    partLookup.inputElement?.addEventListener("input", () => {
        operations = [];
        selectedOperation = null;
        renderOperations();
        updateBalanceLabel();
        updateFormState();
        saveDraft();
    });

    materialSelect?.addEventListener("change", async () => {
        await loadMaterialStock(materialSelect.value || "");
        updateFormState();
        saveDraft();
    });

    labelSearchInput?.addEventListener("input", () => {
        void checkLabelExistsInSystem();
        saveDraft();
    });

    dateInput?.addEventListener("change", () => {
        void checkLabelExistsInSystem();
        saveDraft();
    });

    dateInput?.addEventListener("input", () => {
        void checkLabelExistsInSystem();
        saveDraft();
    });

    function getBalanceKey(partId, sectionId, opNumber) {
        return `${partId}:${sectionId}:${opNumber}`;
    }

    function canAddToCart() {
        const part = partLookup.getSelected();
        if (!part || !part.id) {
            return false;
        }

        if (!selectedOperation) {
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

        const selectedMaterialId = materialSelect?.value || "";
        if (!selectedMaterialId) {
            return false;
        }

        const manualLabelNumber = getManualLabelNumber();
        if (!manualLabelNumber) {
            return false;
        }

        if (isLabelAlreadyInCart(manualLabelNumber)) {
            return false;
        }

        if (labelExistsInSystem) {
            return false;
        }

        return true;
    }

    function isLabelAlreadyInCart(labelNumber) {
        if (typeof labelNumber !== "string" || !labelNumber.trim()) {
            return false;
        }

        return cart.some(item => item.isAssigned && item.labelNumber === labelNumber);
    }

    function canOpenBulkAdd() {
        const part = partLookup.getSelected();
        if (!part || !part.id) {
            return false;
        }

        if (!selectedOperation) {
            return false;
        }

        if (isLoadingOperations || isLoadingBalance) {
            return false;
        }

        const quantity = Number(quantityInput.value);
        const selectedMaterialId = materialSelect?.value || "";
        return Boolean(quantity && quantity >= 1 && dateInput.value && selectedMaterialId);
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

    function resetMaterialStockView() {
        currentMaterialStockSummary = "";
        currentMaterialTotalSize = 0;
        if (materialUnitInput) {
            materialUnitInput.value = "—";
        }
        if (materialAvailableLabel) {
            materialAvailableLabel.textContent = "—";
        }
        if (materialUnitsCountLabel) {
            materialUnitsCountLabel.textContent = "0";
        }
        if (materialTotalSizeLabel) {
            materialTotalSizeLabel.textContent = "0";
        }
        if (materialTotalWeightLabel) {
            materialTotalWeightLabel.textContent = "0";
        }
        if (materialUnitsTableBody) {
            materialUnitsTableBody.innerHTML = "<tr><td colspan=\"5\" class=\"text-center text-muted\">Выберите материал, чтобы увидеть доступные единицы.</td></tr>";
        }
    }

    async function loadMaterials() {
        if (!materialSelect) {
            return;
        }

        try {
            const response = await fetch("/wip/receipts/materials");
            if (!response.ok) {
                throw new Error("Не удалось загрузить материалы.");
            }

            const materials = await response.json();
            materialSelect.innerHTML = "<option value=\"\">Выберите материал</option>";
            (Array.isArray(materials) ? materials : []).forEach(item => {
                const option = document.createElement("option");
                option.value = item.id;
                option.textContent = formatNameWithCode(item.name ?? "", item.code ?? "");
                materialSelect.appendChild(option);
                materialNormById.set(item.id, Number(item.baseConsumptionQty ?? 0));
            });
        }
        catch (error) {
            console.error(error);
            materialSelect.innerHTML = "<option value=\"\">Материалы недоступны</option>";
        }
    }

    async function loadPartMaterials(partId) {
        if (!materialSelect) {
            return;
        }

        materialSelect.innerHTML = "<option value=\"\">Выберите материал</option>";
        resetMaterialStockView();

        if (!partId) {
            return;
        }

        try {
            const response = await fetch(`/wip/receipts/part-materials?partId=${encodeURIComponent(partId)}`);
            if (!response.ok) {
                throw new Error("Не удалось загрузить материалы детали.");
            }

            const materials = await response.json();
            const list = Array.isArray(materials) ? materials : [];
            for (const item of list) {
                const option = document.createElement("option");
                option.value = item.id;
                option.textContent = `${formatNameWithCode(item.name ?? "", item.code ?? "")} (норма: ${Number(item.baseConsumptionQty ?? 0).toLocaleString("ru-RU", { maximumFractionDigits: 6 })})`;
                materialSelect.appendChild(option);
                materialNormById.set(item.id, Number(item.baseConsumptionQty ?? 0));
            }

            if (list.length === 1) {
                materialSelect.value = list[0].id;
                await loadMaterialStock(materialSelect.value);
            } else if (list.length > 1) {
                const available = [];
                for (const m of list) {
                    const stockResponse = await fetch(`/wip/receipts/material-stock?materialId=${encodeURIComponent(m.id)}`);
                    if (!stockResponse.ok) {
                        continue;
                    }
                    const stock = await stockResponse.json();
                    if (Number(stock?.summary?.unitsCount ?? 0) > 0) {
                        available.push(m);
                    }
                }
                if (available.length === 1) {
                    materialSelect.value = available[0].id;
                    await loadMaterialStock(materialSelect.value);
                }
            }
        } catch (error) {
            console.error(error);
            materialSelect.innerHTML = "<option value=\"\">Материалы детали недоступны</option>";
        }
    }

    async function loadMaterialStock(materialId) {
        if (!materialId) {
            resetMaterialStockView();
            materialEmptyState?.classList.remove("d-none");
            return;
        }

        if (materialUnitsTableBody) {
            materialUnitsTableBody.innerHTML = "<tr><td colspan=\"5\" class=\"text-center text-muted\">Загрузка...</td></tr>";
        }

        try {
            const response = await fetch(`/wip/receipts/material-stock?materialId=${encodeURIComponent(materialId)}`);
            if (!response.ok) {
                throw new Error("Не удалось загрузить складскую сводку.");
            }

            const payload = await response.json();
            const summary = payload?.summary;
            const units = Array.isArray(payload?.units) ? payload.units : [];

            if (!summary) {
                resetMaterialStockView();
                materialEmptyState?.classList.remove("d-none");
                return;
            }

            materialEmptyState?.classList.add("d-none");
            if (materialUnitInput) {
                materialUnitInput.value = summary.unitOfMeasure ?? "—";
            }
            if (materialAvailableLabel) {
                materialAvailableLabel.textContent = summary.availableInStock ?? "Нет";
            }
            if (materialUnitsCountLabel) {
                materialUnitsCountLabel.textContent = Number(summary.unitsCount ?? 0).toLocaleString("ru-RU");
            }
            if (materialTotalSizeLabel) {
                materialTotalSizeLabel.textContent = Number(summary.totalSize ?? 0).toLocaleString("ru-RU", { maximumFractionDigits: 3 });
            }
            currentMaterialTotalSize = Number(summary.totalSize ?? 0);
            if (materialTotalWeightLabel) {
                materialTotalWeightLabel.textContent = Number(summary.totalWeightKg ?? 0).toLocaleString("ru-RU", { maximumFractionDigits: 3 });
            }
            currentMaterialStockSummary = `ед.: ${Number(summary.unitsCount ?? 0).toLocaleString("ru-RU")}, вес: ${Number(summary.totalWeightKg ?? 0).toLocaleString("ru-RU", { maximumFractionDigits: 3 })} кг`;

            if (materialUnitsTableBody) {
                if (!units.length) {
                    materialUnitsTableBody.innerHTML = "<tr><td colspan=\"5\" class=\"text-center text-muted\">По выбранному материалу пока нет единиц на складе.</td></tr>";
                }
                else {
                    materialUnitsTableBody.innerHTML = "";
                    units.forEach(unit => {
                        const row = document.createElement("tr");
                        const date = unit.receiptDate ? new Date(unit.receiptDate).toLocaleDateString("ru-RU") : "—";
                        row.innerHTML = `
                            <td>${unit.code ?? ""}</td>
                            <td>${Number(unit.size ?? 0).toFixed(3)}</td>
                            <td>${unit.unitOfMeasure ?? ""}</td>
                            <td>${Number(unit.weightKg ?? 0).toFixed(3)}</td>
                            <td>${date}</td>`;
                        materialUnitsTableBody.appendChild(row);
                    });
                }
            }
        }
        catch (error) {
            console.error(error);
            resetMaterialStockView();
            if (materialUnitsTableBody) {
                materialUnitsTableBody.innerHTML = "<tr><td colspan=\"5\" class=\"text-danger text-center\">Не удалось загрузить данные склада по материалу.</td></tr>";
            }
        }
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

        const manualLabelNumber = getManualLabelNumber();
        const rawInput = labelSearchInput && typeof labelSearchInput.value === "string"
            ? labelSearchInput.value.trim()
            : "";

        if (rawInput && !manualLabelNumber && !labelNumberPattern.test(rawInput))
        {
            showLabelMessage("Номер ярлыка должен быть в формате 12345 или 12345/1.", "danger");
            return;
        }

        if (manualLabelNumber && isLabelAlreadyInCart(manualLabelNumber))
        {
            showLabelMessage("Такой ярлык уже добавлен в корзину. Выберите другой номер.", "danger");
            return;
        }

        if (manualLabelNumber && labelExistsInSystem)
        {
            showLabelMessage("Ярлык с таким номером уже существует в системе. Используйте другой номер.", "danger");
            return;
        }

        hideLabelMessage();
    }

    async function checkLabelExistsInSystem()
    {
        const manualLabelNumber = getManualLabelNumber();
        const receiptDate = dateInput?.value || "";

        if (!manualLabelNumber || !receiptDate)
        {
            labelExistsInSystem = false;
            updateFormState();
            return;
        }

        const requestId = ++labelCheckRequestId;

        try
        {
            const response = await fetch(`/wip/receipts/labels/exists?labelNumber=${encodeURIComponent(manualLabelNumber)}&receiptDate=${encodeURIComponent(receiptDate)}`);
            if (!response.ok)
            {
                throw new Error("Не удалось проверить существование ярлыка.");
            }

            const result = await response.json();
            if (requestId !== labelCheckRequestId)
            {
                return;
            }

            labelExistsInSystem = Boolean(result?.exists);
        }
        catch (error)
        {
            console.error(error);
            if (requestId !== labelCheckRequestId)
            {
                return;
            }

            labelExistsInSystem = false;
        }

        updateFormState();
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

        operationsTableBody.innerHTML = "";
        operations.forEach(operation => {
            const row = document.createElement("tr");

            const choiceCell = document.createElement("td");
            choiceCell.classList.add("text-center");
            const radio = document.createElement("input");
            radio.type = "radio";
            radio.name = "receiptOperation";
            radio.classList.add("form-check-input", "fs-4");
            radio.setAttribute("aria-label", `Выбрать операцию ${operation.opNumber}`);
            radio.dataset.opNumber = String(operation.opNumber);

            radio.addEventListener("change", () => {
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
        if (!part || !part.id || !selectedOperation) {
            balanceLabel.textContent = "0 шт";
            isLoadingBalance = false;
            updateFormState();
            return;
        }

        isLoadingBalance = true;
        updateFormState();

        const key = getBalanceKey(part.id, selectedOperation.sectionId, selectedOperation.opNumber);
        try {
            const base = await ensureBalanceLoaded(part.id, selectedOperation.sectionId, selectedOperation.opNumber);
            if (requestId !== balanceRequestId) {
                return;
            }

            const partAfterAwait = partLookup.getSelected();
            const operationAfterAwait = selectedOperation;
            if (!partAfterAwait || !partAfterAwait.id || !operationAfterAwait) {
                return;
            }

            const currentKey = getBalanceKey(partAfterAwait.id, operationAfterAwait.sectionId, operationAfterAwait.opNumber);
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

    function updateLabelControlsState() {
        const part = partLookup.getSelected();
        const hasPart = Boolean(part && part.id);
        const shouldEnable = hasPart;

        if (labelSearchInput) {
            labelSearchInput.disabled = !shouldEnable;
        }
    }

    function setSelectedLabel(label) {
        if (!labelHiddenInput) {
            return;
        }

        if (!label) {
            selectedLabel = null;
            labelHiddenInput.value = "";
            updateFormState();
            return;
        }
        selectedLabel = label;
        labelHiddenInput.value = label.id;

        updateFormState();
    }

    function resetLabels() {
        setSelectedLabel(null);

        if (labelSearchInput) {
            labelSearchInput.value = "";
        }

        updateLabelControlsState();
        hideLabelMessage();
    }



    function updateMaterialNeedInfo() {
        const materialId = materialSelect?.value || "";
        const qty = Number(quantityInput?.value ?? 0);
        const norm = materialNormById.get(materialId) ?? 0;
        const unit = materialUnitInput?.value && materialUnitInput.value !== "—" ? materialUnitInput.value : "ед.";
        const required = norm > 0 && qty > 0 ? norm * qty : 0;
        const remainder = currentMaterialTotalSize - required;

        if (materialNormPerPartLabel) {
            materialNormPerPartLabel.textContent = norm > 0 ? `${norm.toLocaleString("ru-RU", { maximumFractionDigits: 6 })} ${unit}` : "—";
        }
        if (materialRequiredLabel) {
            materialRequiredLabel.textContent = required > 0 ? `${required.toLocaleString("ru-RU", { maximumFractionDigits: 3 })} ${unit}` : "—";
        }
        if (materialRecommendationLabel) {
            if (!materialId || qty <= 0 || norm <= 0) {
                materialRecommendationLabel.textContent = "Выберите деталь, материал и количество.";
            } else if (remainder < 0) {
                materialRecommendationLabel.textContent = `Недостаточно материала: не хватает ${Math.abs(remainder).toLocaleString("ru-RU", { maximumFractionDigits: 3 })} ${unit}.`;
            } else {
                materialRecommendationLabel.textContent = `Подходит. Ожидаемый остаток: ${remainder.toLocaleString("ru-RU", { maximumFractionDigits: 3 })} ${unit}.`;
            }
        }
    }

    function renderCart() {
        if (!cart.length) {
            cartTableBody.innerHTML = "<tr><td colspan=\"11\" class=\"text-center text-muted\">Добавьте операции в корзину для сохранения.</td></tr>";
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
                <td>${item.materialDisplay ?? "—"}<div class="small text-muted">${item.materialStockSummary ?? ""}</div></td>
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
            historyTableBody.innerHTML = "<tr><td colspan=\"10\" class=\"text-center text-muted\">Сохранённые приходы появятся здесь.</td></tr>";
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
                <td>${item.materialDisplay ?? "—"}<div class="small text-muted">${item.materialStockSummary ?? ""}</div></td>
                <td>${item.isAssigned && item.labelNumber ? item.labelNumber : ""}</td>
                <td>${item.quantity.toFixed(3)}</td>
                <td>${item.become.toFixed(3)}</td>
                <td>
                    <a class="btn btn-link text-decoration-none" title="Скачать сопроводительный ярлык" href="/wip/receipts/${encodeURIComponent(item.receiptId)}/escort-label">🏷️</a>
                </td>
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

        }

        editingIndex = null;
        renderCart();
        updateBalanceLabel();
        saveDraft();
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
        quantityInput.value = item.quantity;
        commentInput.value = item.comment ?? "";
        dateInput.value = item.date;
        if (materialSelect) {
            materialSelect.value = item.metalMaterialId ?? "";
            await loadMaterialStock(materialSelect.value);
        }

        if (labelSearchInput) {
            labelSearchInput.value = typeof item.labelNumber === "string" ? item.labelNumber : "";
        }
        await loadOperations(item.partId);
        selectedOperation = operations.find(op => op.opNumber === item.opNumber) ?? null;
        renderOperations();
        renderCart();
        updateBalanceLabel();
        saveDraft();
    }

    addButton.addEventListener("click", () => addToCart());
    bulkAddButton?.addEventListener("click", () => openBulkModal());
    resetButton.addEventListener("click", () => resetForm());
    saveButton.addEventListener("click", () => saveCart());
    bulkConfirmButton?.addEventListener("click", () => void addBulkToCart());

    quantityInput.addEventListener("input", () => {
        updateFormState();
        updateMaterialNeedInfo();
        saveDraft();
    });
    commentInput.addEventListener("input", () => saveDraft());
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
        labelExistsInSystem = false;
        renderOperations();
        updateBalanceLabel();
        updateFormState();
        saveDraft();
    }

    async function addToCart() {
        const state = await collectReceiptState();
        if (!state) {
            return;
        }

        const { part, section, quantity, date, partDisplay, operationDisplay, key, base, pending, metalMaterialId, materialDisplay } = state;
        const manualLabelNumber = getManualLabelNumber();
        if (manualLabelNumber && isLabelAlreadyInCart(manualLabelNumber)) {
            alert("Этот ярлык уже есть в корзине. Дублирование ярлыков запрещено.");
            return;
        }

        await checkLabelExistsInSystem();
        if (manualLabelNumber && labelExistsInSystem) {
            alert("Ярлык с таким номером уже существует в системе. Используйте другой номер.");
            return;
        }

        const was = (base ?? 0) + pending;
        const become = was + quantity;
        pendingAdjustments.set(key, pending + quantity);

        const labelId = null;
        const labelNumber = manualLabelNumber || null;
        const labelIsAssigned = Boolean(manualLabelNumber);

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
            metalMaterialId,
            materialDisplay,
            materialStockSummary: currentMaterialStockSummary,
        };

        cart.push(item);
        if (labelSearchInput)
        {
            labelSearchInput.value = "";
        }
        labelExistsInSystem = false;
        editingIndex = null;
        renderCart();
        resetForm();
        saveDraft();
    }

    async function collectReceiptState() {
        const part = partLookup.getSelected();

        if (!part || !part.id) {
            alert("Выберите деталь.");
            return null;
        }

        if (!selectedOperation) {
            alert("Выберите операцию детали.");
            return null;
        }
        const section = { id: selectedOperation.sectionId, name: selectedOperation.sectionName || "-" };

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

        const metalMaterialId = materialSelect?.value || null;
        if (!metalMaterialId) {
            alert("Выберите материал заготовки.");
            return null;
        }

        const key = getBalanceKey(part.id, selectedOperation.sectionId, selectedOperation.opNumber);
        const base = await ensureBalanceLoaded(part.id, selectedOperation.sectionId, selectedOperation.opNumber);
        const pending = pendingAdjustments.get(key) ?? 0;
        const partDisplay = formatNameWithCode(part.name, part.code);
        const operationDisplay = `${selectedOperation.opNumber} ${selectedOperation.operationName ?? ""}`.trim();
        const materialDisplay = materialSelect?.selectedOptions?.[0]?.textContent?.trim() || "—";

        return { part, section, quantity, date, partDisplay, operationDisplay, key, base, pending, metalMaterialId, materialDisplay };
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

        const { part, section, quantity, date, partDisplay, operationDisplay, key, base, pending, metalMaterialId, materialDisplay } = state;
        const duplicateLabels = parsedLabels.filter(labelNumber => isLabelAlreadyInCart(labelNumber));
        if (duplicateLabels.length > 0) {
            alert(`Следующие ярлыки уже есть в корзине: ${duplicateLabels.join(", ")}. Удалите дубликаты и повторите.`);
            return;
        }

        const duplicateInSystem = await findExistingLabelsInSystem(parsedLabels, date);
        if (duplicateInSystem.length > 0) {
            alert(`Следующие ярлыки уже существуют в системе: ${duplicateInSystem.join(", ")}. Удалите дубликаты и повторите.`);
            return;
        }

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
                metalMaterialId,
                materialDisplay,
                materialStockSummary: currentMaterialStockSummary,
            });
        });

        pendingAdjustments.set(key, runningPending);
        bulkModal?.hide();
        renderCart();
        resetForm();
    }

    async function findExistingLabelsInSystem(labelNumbers, receiptDate)
    {
        if (!Array.isArray(labelNumbers) || labelNumbers.length === 0 || !receiptDate)
        {
            return [];
        }

        const checks = await Promise.all(labelNumbers.map(async labelNumber => {
            try
            {
                const response = await fetch(`/wip/receipts/labels/exists?labelNumber=${encodeURIComponent(labelNumber)}&receiptDate=${encodeURIComponent(receiptDate)}`);
                if (!response.ok)
                {
                    return null;
                }

                const result = await response.json();
                return result?.exists ? labelNumber : null;
            }
            catch (error)
            {
                console.error(error);
                return null;
            }
        }));

        return checks.filter(label => typeof label === "string");
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
                metalMaterialId: item.metalMaterialId,
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
            draftStorage?.clear();
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
            const materialDisplay = snapshot ? snapshot.materialDisplay : "—";
            const materialStockSummary = snapshot ? snapshot.materialStockSummary : "";
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
                materialDisplay,
                materialStockSummary,
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
        newEntries.forEach(entry => {
            window.open(`/wip/receipts/${encodeURIComponent(entry.receiptId)}/escort-label`, "_blank");
        });
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

    void checkLabelExistsInSystem();
    resetMaterialStockView();
    void loadMaterials();
    updateFormState();
    renderHistory();

    namespace.bindHotkeys({
        onEnter: () => addToCart(),
        onSave: () => saveCart(),
        onCancel: () => resetForm(),
    });

    queueMicrotask(() => {
        draftStorage?.restoreOrClear(state => {
            void restoreDraft(state);
        });
    });
})();
