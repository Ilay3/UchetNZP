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
        minLength: 2,
    });

    const operationLookup = namespace.initSearchableInput({
        input: document.getElementById("routeOperationNameInput"),
        datalist: document.getElementById("routeOperationOptions"),
        hiddenInput: document.createElement("input"),
        fetchUrl: "/routes/import/operations",
        minLength: 2,
    });

    const sectionLookup = namespace.initSearchableInput({
        input: document.getElementById("routeSectionNameInput"),
        datalist: document.getElementById("routeSectionOptions"),
        hiddenInput: document.createElement("input"),
        fetchUrl: "/routes/import/sections",
        minLength: 2,
    });

    const opNumberInput = document.getElementById("routeOpNumberInput");
    const normInput = document.getElementById("routeNormInput");
    const saveButton = document.getElementById("routeSaveButton");
    const resetButton = document.getElementById("routeResetButton");
    const messageBox = document.getElementById("routeSaveMessage");

    const fileInput = document.getElementById("routeFileInput");
    const importButton = document.getElementById("routeImportButton");
    const importSummaryContainer = document.getElementById("routeImportSummary");
    let lastDownloadedJobId = null;

    saveButton.addEventListener("click", () => saveRoute());
    resetButton.addEventListener("click", () => resetForm());
    importButton.addEventListener("click", () => importRoutes());

    async function saveRoute() {
        const partName = partLookup.inputElement.value.trim();
        if (!partName) {
            alert("Заполните наименование детали.");
            return;
        }

        const operationName = operationLookup.inputElement.value.trim();
        if (!operationName) {
            alert("Заполните наименование операции.");
            return;
        }

        const sectionName = sectionLookup.inputElement.value.trim();
        if (!sectionName) {
            alert("Укажите участок.");
            return;
        }

        const opNumber = Number(opNumberInput.value);
        if (!opNumber || opNumber <= 0) {
            alert("Номер операции должен быть больше нуля.");
            return;
        }

        const normHours = Number(normInput.value);
        if (!normHours || normHours <= 0) {
            alert("Норматив должен быть больше нуля.");
            return;
        }

        const payload = {
            partName,
            partCode: null,
            operationName,
            opNumber,
            normHours,
            sectionName,
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
                throw new Error("Не удалось сохранить маршрут.");
            }

            namespace.showInlineMessage(messageBox, "Маршрут успешно сохранён.", "success");
            resetForm();
        }
        catch (error) {
            console.error(error);
            namespace.showInlineMessage(messageBox, "Ошибка при сохранении маршрута.", "danger");
        }
        finally {
            saveButton.disabled = false;
        }
    }

    function resetForm() {
        partLookup.setSelected(null);
        operationLookup.setSelected(null);
        opNumberInput.value = "";
        normInput.value = "";
        sectionLookup.setSelected(null);
        namespace.hideInlineMessage(messageBox);
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
        onEnter: () => saveRoute(),
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
