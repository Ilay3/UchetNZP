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

    const partCodeInput = document.getElementById("routePartCodeInput");
    const opNumberInput = document.getElementById("routeOpNumberInput");
    const normInput = document.getElementById("routeNormInput");
    const saveButton = document.getElementById("routeSaveButton");
    const resetButton = document.getElementById("routeResetButton");
    const messageBox = document.getElementById("routeSaveMessage");

    const fileInput = document.getElementById("routeFileInput");
    const importButton = document.getElementById("routeImportButton");
    const importSummaryContainer = document.getElementById("routeImportSummary");

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
            partCode: partCodeInput.value.trim() || null,
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
        partCodeInput.value = "";
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

        const header = document.createElement("div");
        header.className = "alert alert-info";
        header.textContent = `Обработано: ${summary.processed}, сохранено: ${summary.saved}, пропущено: ${summary.skipped}.`;

        const table = document.createElement("table");
        table.className = "table table-bordered table-sm mt-3";
        table.innerHTML = `
            <thead class=\"table-light\">
                <tr>
                    <th scope=\"col\">Строка</th>
                    <th scope=\"col\">Статус</th>
                    <th scope=\"col\">Комментарий</th>
                </tr>
            </thead>
            <tbody></tbody>`;

        const body = table.querySelector("tbody");
        (summary.items ?? []).forEach(item => {
            const row = document.createElement("tr");
            row.innerHTML = `
                <td>${item.rowNumber}</td>
                <td>${item.status}</td>
                <td>${item.message ?? ""}</td>`;
            if (item.status === "Skipped") {
                row.classList.add("table-warning");
            }
            body.appendChild(row);
        });

        importSummaryContainer.innerHTML = "";
        importSummaryContainer.appendChild(header);
        importSummaryContainer.appendChild(table);
    }

    namespace.bindHotkeys({
        onEnter: () => saveRoute(),
        onSave: () => saveRoute(),
        onCancel: () => resetForm(),
    });
})();
