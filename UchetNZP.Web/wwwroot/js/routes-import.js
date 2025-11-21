(function () {
    const namespace = window.UchetNZP;
    if (!namespace) {
        throw new Error("Не инициализировано пространство имён UchetNZP.");
    }

    const partLookup = namespace.initSearchableInput({
        input: document.getElementById("routePartNameInput"),
        datalist: document.getElementById("routePartOptions"),
        hiddenInput: document.createElement("input"),
        fetchUrl: "/routes/import/parts",
        minLength: 1,
        prefetch: true,
    });

    const operationLookup = namespace.initSearchableInput({
        input: document.getElementById("routeOperationNameInput"),
        datalist: document.getElementById("routeOperationOptions"),
        hiddenInput: document.createElement("input"),
        fetchUrl: "/routes/import/operations",
        minLength: 1,
        prefetch: true,
    });

    const sectionLookup = namespace.initSearchableInput({
        input: document.getElementById("routeSectionNameInput"),
        datalist: document.getElementById("routeSectionOptions"),
        hiddenInput: document.createElement("input"),
        fetchUrl: "/routes/import/sections",
        minLength: 1,
        prefetch: true,
    });

    const opNumberInput = document.getElementById("routeOpNumberInput");
    const normInput = document.getElementById("routeNormInput");
    const addOperationButton = document.getElementById("routeAddOperationButton");
    const saveButton = document.getElementById("routeSaveButton");
    const resetButton = document.getElementById("routeResetButton");
    const messageBox = document.getElementById("routeSaveMessage");
    const operationsContainer = document.getElementById("routeOperationsContainer");
    const operationsTable = operationsContainer ? operationsContainer.querySelector("table") : null;
    const operationsTableBody = document.getElementById("routeOperationsTableBody");
    const operationsEmptyState = document.getElementById("routeOperationsEmptyState");
    const sectionHintCache = new Set();

    const fileInput = document.getElementById("routeFileInput");
    const importButton = document.getElementById("routeImportButton");
    const importSummaryContainer = document.getElementById("routeImportSummary");
    let lastDownloadedJobId = null;
    const pendingOperations = [];

    addOperationButton.addEventListener("click", () => addOperationToList());
    saveButton.addEventListener("click", () => saveRoute());
    resetButton.addEventListener("click", () => resetForm());
    if (operationsTableBody) {
        operationsTableBody.addEventListener("click", event => {
            const target = event.target instanceof HTMLElement ? event.target.closest("[data-action='remove-operation']") : null;
            if (!target) {
                return;
            }

            const index = Number(target.dataset.index ?? "NaN");
            if (Number.isNaN(index) || index < 0 || index >= pendingOperations.length) {
                return;
            }

            pendingOperations.splice(index, 1);
            renderOperations();
        });
    }

    operationLookup.inputElement?.addEventListener("lookup:selected", event => {
        const selected = event.detail;
        applyOperationSectionHints(selected);
    });

    renderOperations();
    importButton.addEventListener("click", () => importRoutes());

    function applyOperationSectionHints(operation) {
        const suggestions = extractSectionSuggestions(operation);
        if (suggestions.length === 0) {
            return;
        }

        const items = suggestions.map(name => ({ name }));
        sectionLookup.addCustomItems(items);

        const currentSection = sectionLookup.inputElement.value.trim();
        if (!currentSection) {
            sectionLookup.setSelected(items[0]);
        }

        const newHints = suggestions.filter(name => !sectionHintCache.has(name));
        if (newHints.length > 0) {
            newHints.forEach(name => sectionHintCache.add(name));
            sectionLookup.refresh(newHints[0]);
        }
    }

    function extractSectionSuggestions(operation) {
        if (!operation) {
            return [];
        }

        const raw = Array.isArray(operation.sections) ? operation.sections : [];
        return raw
            .map(item => typeof item === "string" ? item.trim() : "")
            .filter(item => item.length > 0);
    }

    async function saveRoute() {
        const partName = partLookup.inputElement.value.trim();
        if (!partName) {
            alert("Заполните наименование детали.");
            return;
        }

        ensureOperationsPrepared();

        if (pendingOperations.length === 0) {
            alert("Добавьте хотя бы одну операцию для сохранения.");
            return;
        }

        const selectedPart = typeof partLookup.getSelected === "function" ? partLookup.getSelected() : null;
        const partCode = selectedPart && typeof selectedPart.code === "string" && selectedPart.code.trim().length > 0
            ? selectedPart.code
            : null;

        const payload = {
            partName,
            partCode,
            operations: pendingOperations.map(item => ({
                operationName: item.operationName,
                opNumber: item.opNumber,
                normHours: item.normHours,
                sectionName: item.sectionName,
            })),
        };

        saveButton.disabled = true;
        try {
            const response = await fetch("/routes/import/upsert", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify(payload),
            });

            if (!response.ok) {
                const errorText = await response.text().catch(() => "");
                throw new Error(errorText?.trim() || "Не удалось сохранить маршрут.");
            }

            const result = await response.json().catch(() => null);
            const savedCount = result && typeof result.saved === "number" ? result.saved : pendingOperations.length;

            namespace.showInlineMessage(messageBox, `Маршрут успешно сохранён. Операций: ${savedCount}.`, "success");
            resetForm({ keepPart: true, preserveMessage: true });
        }
        catch (error) {
            console.error(error);
            const message = error && typeof error.message === "string" && error.message.trim().length > 0
                ? error.message
                : "Ошибка при сохранении маршрута.";

            namespace.showInlineMessage(messageBox, message, "danger");
        }
        finally {
            saveButton.disabled = false;
        }
    }

    function resetForm({ keepPart = false, preserveMessage = false } = {}) {
        if (!keepPart) {
            partLookup.setSelected(null);
        }

        clearOperationInputs();
        pendingOperations.splice(0, pendingOperations.length);
        renderOperations();
        if (!preserveMessage) {
            namespace.hideInlineMessage(messageBox);
        }
    }

    function clearOperationInputs() {
        operationLookup.setSelected(null);
        opNumberInput.value = "";
        normInput.value = "";
        sectionLookup.setSelected(null);
    }

    function renderOperations() {
        if (!operationsTableBody || !operationsEmptyState || !operationsTable) {
            return;
        }

        operationsTableBody.innerHTML = "";

        if (pendingOperations.length === 0) {
            operationsTable.classList.add("d-none");
            operationsEmptyState.classList.remove("d-none");
            return;
        }

        operationsTable.classList.remove("d-none");
        operationsEmptyState.classList.add("d-none");

        pendingOperations.forEach((item, index) => {
            const row = document.createElement("tr");

            const opNumberCell = document.createElement("td");
            opNumberCell.textContent = item.opNumber;
            row.appendChild(opNumberCell);

            const operationNameCell = document.createElement("td");
            operationNameCell.textContent = item.operationName;
            row.appendChild(operationNameCell);

            const normCell = document.createElement("td");
            normCell.textContent = item.normHours.toFixed(3);
            row.appendChild(normCell);

            const sectionCell = document.createElement("td");
            sectionCell.textContent = item.sectionName;
            row.appendChild(sectionCell);

            const actionsCell = document.createElement("td");
            actionsCell.className = "text-end";
            const removeButton = document.createElement("button");
            removeButton.type = "button";
            removeButton.className = "btn btn-sm btn-outline-danger";
            removeButton.dataset.action = "remove-operation";
            removeButton.dataset.index = String(index);
            removeButton.textContent = "Удалить";
            actionsCell.appendChild(removeButton);
            row.appendChild(actionsCell);

            operationsTableBody.appendChild(row);
        });
    }

    function addOperationToList({ showAlerts = true } = {}) {
        const operation = getOperationFromInputs(showAlerts);
        if (!operation) {
            return false;
        }

        pendingOperations.push(operation);
        clearOperationInputs();
        renderOperations();
        return true;
    }

    function ensureOperationsPrepared() {
        if (!hasAnyOperationInput()) {
            return;
        }

        addOperationToList({ showAlerts: true });
    }

    function hasAnyOperationInput() {
        return Boolean(
            operationLookup.inputElement.value.trim() ||
            opNumberInput.value.trim() ||
            normInput.value.trim() ||
            sectionLookup.inputElement.value.trim()
        );
    }

    function getOperationFromInputs(showAlerts) {
        const operationName = operationLookup.inputElement.value.trim();
        if (!operationName) {
            if (showAlerts) {
                alert("Заполните наименование операции.");
            }

            return null;
        }

        const sectionName = sectionLookup.inputElement.value.trim();
        if (!sectionName) {
            if (showAlerts) {
                alert("Укажите вид работ.");
            }

            return null;
        }

        const normalizedOperationName = operationName.replace(/\s+/g, "").toLowerCase();
        const normalizedSectionName = sectionName.replace(/\s+/g, "").toLowerCase();

        if (normalizedOperationName && normalizedSectionName && normalizedOperationName !== normalizedSectionName) {
            if (showAlerts) {
                alert("Наименование операции и выбранный вид работ должны совпадать.");
            }

            return null;
        }

        const opNumberText = opNumberInput.value.trim();
        const opNumberPattern = new RegExp(opNumberInput.dataset.pattern ?? "^\\d{1,10}(?:/\\d{1,5})?$");
        if (!opNumberPattern.test(opNumberText)) {
            if (showAlerts) {
                alert("Номер операции должен состоять из 1–10 цифр и может содержать дробную часть через «/».");
            }

            return null;
        }

        const normValue = normInput.value.trim();
        const normHours = Number(normValue);
        if (!normValue || Number.isNaN(normHours) || normHours <= 0) {
            if (showAlerts) {
                alert("Норматив должен быть больше нуля.");
            }

            return null;
        }

        const roundedNorm = Math.round(normHours * 1000) / 1000;

        return {
            operationName,
            opNumber: opNumberText,
            normHours: roundedNorm,
            sectionName,
        };
    }

    async function importRoutes() {
        if (!fileInput.files || fileInput.files.length === 0) {
            alert("Выберите файл для импорта.");
            return;
        }

        const formData = new FormData();
        formData.append("file", fileInput.files[0]);

        importButton.disabled = true;
        importSummaryContainer.innerHTML = "Загрузка файла...";

        try {
            const response = await fetch("/routes/import/upload", {
                method: "POST",
                body: formData,
            });

            if (!response.ok) {
                throw new Error("Не удалось выполнить импорт.");
            }

            const summary = await response.json();
            renderImportSummary(summary);
        }
        catch (error) {
            console.error(error);
            importSummaryContainer.innerHTML = "<div class=\"alert alert-danger\">Не удалось выполнить импорт. Попробуйте ещё раз.</div>";
        }
        finally {
            importButton.disabled = false;
        }
    }

    function renderImportSummary(summary) {
        if (!summary) {
            importSummaryContainer.innerHTML = "";
            return;
        }

        const summaryCard = document.createElement("div");
        summaryCard.className = "alert alert-info";

        const fileLine = document.createElement("div");
        fileLine.innerHTML = `<strong>Файл:</strong> ${summary.fileName}`;

        const totalsLine = document.createElement("div");
        totalsLine.textContent = `Всего строк: ${summary.totalRows}, успешно: ${summary.succeeded}, пропущено: ${summary.skipped}.`;

        summaryCard.appendChild(fileLine);
        summaryCard.appendChild(totalsLine);

        if (summary.skipped > 0) {
            const hint = document.createElement("p");
            hint.className = "mt-3 mb-0";
            hint.textContent = "Найденные ошибки можно скачать отдельным Excel-файлом.";
            summaryCard.appendChild(hint);

            if (summary.errorFileContent) {
                const downloadButton = document.createElement("button");
                downloadButton.type = "button";
                downloadButton.className = "btn btn-outline-danger btn-lg mt-3";
                downloadButton.textContent = "Скачать отчёт об ошибках";
                downloadButton.addEventListener("click", () => downloadErrorReport(summary));
                summaryCard.appendChild(downloadButton);
            }
        }

        importSummaryContainer.innerHTML = "";
        importSummaryContainer.appendChild(summaryCard);

        if (summary.skipped > 0 && summary.errorFileContent && summary.jobId !== lastDownloadedJobId) {
            downloadErrorReport(summary);
            lastDownloadedJobId = summary.jobId;
        }
    }

    namespace.bindHotkeys({
        onEnter: () => {
            if (!addOperationToList({ showAlerts: false })) {
                ensureOperationsPrepared();
            }
        },
        onSave: () => saveRoute(),
        onCancel: () => resetForm(),
    });

    function downloadErrorReport(summary) {
        if (!summary || !summary.errorFileContent) {
            return;
        }

        try {
            const binary = atob(summary.errorFileContent);
            const length = binary.length;
            const bytes = new Uint8Array(length);
            for (let i = 0; i < length; i++) {
                bytes[i] = binary.charCodeAt(i);
            }

            const blob = new Blob([bytes], {
                type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            });

            const url = URL.createObjectURL(blob);
            const link = document.createElement("a");
            link.href = url;
            link.download = summary.errorFileName || `Ошибки_${summary.fileName}`;
            document.body.appendChild(link);
            link.click();
            requestAnimationFrame(() => {
                URL.revokeObjectURL(url);
                link.remove();
            });
        }
        catch (error) {
            console.error("Не удалось подготовить файл ошибок", error);
        }
    }
})();
