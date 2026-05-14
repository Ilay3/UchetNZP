(function () {
    const namespace = window.UchetNZP;
    if (!namespace) {
        throw new Error("Не инициализировано пространство имен UchetNZP.");
    }

    const partLookup = namespace.initSearchableInput({
        input: document.getElementById("fullMovementPartInput"),
        datalist: document.getElementById("fullMovementPartOptions"),
        hiddenInput: document.getElementById("fullMovementPartId"),
        fetchUrl: "/reports/full-movement/parts",
        minLength: 2,
    });

    const form = document.getElementById("fullMovementForm");
    const labelsBody = document.querySelector("#fullMovementLabelsTable tbody");
    const exportButton = document.getElementById("fullMovementExportButton");
    const selectAllButton = document.getElementById("fullMovementSelectAllButton");
    const clearButton = document.getElementById("fullMovementClearButton");
    const hiddenLabelsHost = document.getElementById("fullMovementHiddenLabels");
    const loading = document.getElementById("fullMovementLoading");
    const hint = document.getElementById("fullMovementHint");
    const error = document.getElementById("fullMovementError");
    const countLabel = document.getElementById("fullMovementCountLabel");
    const selectedLabelsInput = document.getElementById("fullMovementSelectedLabels");
    const selectedFromQuery = new Set((selectedLabelsInput?.value || "")
        .split(",")
        .map(value => value.trim())
        .filter(Boolean));

    let labels = [];
    let abortController = null;

    function showError(message) {
        if (!error) {
            return;
        }

        error.textContent = message;
        error.classList.remove("d-none");
    }

    function clearError() {
        if (!error) {
            return;
        }

        error.textContent = "";
        error.classList.add("d-none");
    }

    function setLoading(isLoading) {
        if (!loading) {
            return;
        }

        loading.classList.toggle("d-none", !isLoading);
        loading.classList.toggle("d-flex", isLoading);
    }

    function getSelectedIds() {
        return Array.from(document.querySelectorAll("input[data-full-movement-label]:checked"))
            .map(input => input.value)
            .filter(Boolean);
    }

    function updateState() {
        const selectedIds = getSelectedIds();
        const hasPart = Boolean(partLookup.getSelected()?.id || document.getElementById("fullMovementPartId")?.value);
        const hasLabels = labels.length > 0;

        if (hiddenLabelsHost) {
            hiddenLabelsHost.innerHTML = "";
            selectedIds.forEach(id => {
                const hidden = document.createElement("input");
                hidden.type = "hidden";
                hidden.name = "labelIds";
                hidden.value = id;
                hiddenLabelsHost.appendChild(hidden);
            });
        }

        if (exportButton) {
            exportButton.disabled = !(hasPart && selectedIds.length > 0);
        }
        if (selectAllButton) {
            selectAllButton.disabled = !hasLabels;
        }
        if (clearButton) {
            clearButton.disabled = selectedIds.length === 0;
        }
        if (countLabel) {
            countLabel.textContent = `${selectedIds.length} выбрано`;
        }
        if (hint) {
            hint.textContent = !hasPart
                ? "Сначала выберите деталь."
                : hasLabels
                    ? "Отметьте ярлыки галочками и сформируйте отчет."
                    : "По выбранной детали ярлыки не найдены.";
        }
    }

    function renderLabels(items) {
        if (!labelsBody) {
            return;
        }

        labels = Array.isArray(items) ? items : [];
        labelsBody.innerHTML = "";

        if (!labels.length) {
            labelsBody.innerHTML = "<tr><td colspan=\"5\" class=\"text-center text-muted\">Ярлыки не найдены.</td></tr>";
            updateState();
            return;
        }

        labels.forEach(item => {
            const row = document.createElement("tr");
            const id = item.id || "";
            const isChecked = selectedFromQuery.has(id);
            row.innerHTML = `
                <td class="text-center">
                    <input class="form-check-input" type="checkbox" value="${id}" data-full-movement-label ${isChecked ? "checked" : ""} aria-label="Выбрать ярлык ${item.number ?? ""}">
                </td>
                <td class="fw-semibold">${item.number ?? ""}</td>
                <td>${formatDate(item.labelDate)}</td>
                <td>${formatQuantity(item.quantity)}</td>
                <td>${formatQuantity(item.remainingQuantity)}</td>`;
            labelsBody.appendChild(row);
        });

        updateState();
    }

    async function loadLabels(partId) {
        if (!partId) {
            labels = [];
            renderLabels([]);
            return;
        }

        if (abortController) {
            abortController.abort();
        }

        abortController = new AbortController();
        const signal = abortController.signal;
        setLoading(true);
        clearError();

        try {
            const response = await fetch(`/reports/full-movement/labels?partId=${encodeURIComponent(partId)}`, { signal });
            if (!response.ok) {
                throw new Error("Не удалось загрузить ярлыки детали.");
            }

            const data = await response.json();
            renderLabels(Array.isArray(data) ? data : []);
        }
        catch (loadError) {
            if (signal.aborted) {
                return;
            }

            console.error(loadError);
            renderLabels([]);
            showError("Не удалось загрузить ярлыки детали. Попробуйте выбрать деталь еще раз.");
        }
        finally {
            if (abortController?.signal === signal) {
                abortController = null;
            }
            setLoading(false);
        }
    }

    function formatDate(value) {
        if (!value) {
            return "";
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "";
        }

        return date.toLocaleDateString("ru-RU");
    }

    function formatQuantity(value) {
        const number = Number(value ?? 0);
        return number.toLocaleString("ru-RU", { maximumFractionDigits: 3 });
    }

    partLookup.inputElement?.addEventListener("lookup:selected", event => {
        selectedFromQuery.clear();
        void loadLabels(event.detail?.id);
    });

    partLookup.inputElement?.addEventListener("input", () => {
        clearError();
        selectedFromQuery.clear();
        if (!partLookup.getSelected()) {
            renderLabels([]);
        }
    });

    labelsBody?.addEventListener("change", event => {
        const target = event.target;
        if (target instanceof HTMLInputElement && target.matches("[data-full-movement-label]")) {
            updateState();
        }
    });

    selectAllButton?.addEventListener("click", () => {
        document.querySelectorAll("input[data-full-movement-label]").forEach(input => {
            input.checked = true;
        });
        updateState();
    });

    clearButton?.addEventListener("click", () => {
        document.querySelectorAll("input[data-full-movement-label]").forEach(input => {
            input.checked = false;
        });
        updateState();
    });

    form?.addEventListener("submit", event => {
        clearError();
        if (getSelectedIds().length === 0) {
            event.preventDefault();
            showError("Выберите хотя бы один ярлык.");
        }
    });

    const initialPartId = document.getElementById("fullMovementPartId")?.value || "";
    if (initialPartId) {
        void loadLabels(initialPartId);
    }
    updateState();
})();
