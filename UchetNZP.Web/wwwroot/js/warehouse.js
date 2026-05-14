(function () {
    const root = document.getElementById("warehouseRoot");
    if (!root) {
        return;
    }

    const namespace = window.UchetNZP;
    if (!namespace) {
        throw new Error("Не инициализировано пространство имён UchetNZP.");
    }

    const partInput = document.getElementById("warehouseFilterPart");
    const partOptions = document.getElementById("warehousePartOptions");
    const partHiddenInput = document.getElementById("warehouseFilterPartId");
    const manualPartInput = document.getElementById("warehouseManualPart");
    const manualPartOptions = document.getElementById("warehouseManualPartOptions");
    const manualPartHiddenInput = document.getElementById("warehouseManualPartId");
    const manualLabelInput = document.getElementById("warehouseManualLabel");
    const manualLabelOptions = document.getElementById("warehouseManualLabelOptions");
    const manualLabelHiddenInput = document.getElementById("warehouseManualLabelId");
    const manualAssemblyUnitInput = document.getElementById("warehouseManualAssemblyUnit");
    const manualAssemblyUnitOptions = document.getElementById("warehouseManualAssemblyUnitOptions");
    const manualAssemblyUnitHiddenInput = document.getElementById("warehouseManualAssemblyUnitId");
    const manualAssemblyLabelInput = document.getElementById("warehouseManualAssemblyLabel");
    const manualAssemblyLabelOptions = document.getElementById("warehouseManualAssemblyLabelOptions");
    const issuePartInput = document.getElementById("warehouseIssuePart");
    const issuePartOptions = document.getElementById("warehouseIssuePartOptions");
    const issuePartHiddenInput = document.getElementById("warehouseIssuePartId");
    const issueLabelInput = document.getElementById("warehouseIssueLabel");
    const issueLabelOptions = document.getElementById("warehouseIssueLabelOptions");
    const issueLabelHiddenInput = document.getElementById("warehouseIssueLabelId");
    const issueAssemblyUnitInput = document.getElementById("warehouseIssueAssemblyUnit");
    const issueAssemblyUnitOptions = document.getElementById("warehouseIssueAssemblyUnitOptions");
    const issueAssemblyUnitHiddenInput = document.getElementById("warehouseIssueAssemblyUnitId");
    const issueAssemblyLabelInput = document.getElementById("warehouseIssueAssemblyLabel");
    const issueAssemblyLabelOptions = document.getElementById("warehouseIssueAssemblyLabelOptions");

    if (partInput && partOptions && partHiddenInput) {
        namespace.initSearchableInput({
            input: partInput,
            datalist: partOptions,
            hiddenInput: partHiddenInput,
            fetchUrl: "/warehouse/parts",
            minLength: 2,
        });
    }

    let manualPartLookup = null;
    if (manualPartInput && manualPartOptions && manualPartHiddenInput) {
        manualPartLookup = namespace.initSearchableInput({
            input: manualPartInput,
            datalist: manualPartOptions,
            hiddenInput: manualPartHiddenInput,
            fetchUrl: "/warehouse/parts",
            minLength: 2,
        });
    }

    let manualAssemblyUnitLookup = null;
    if (manualAssemblyUnitInput && manualAssemblyUnitOptions && manualAssemblyUnitHiddenInput) {
        manualAssemblyUnitLookup = namespace.initSearchableInput({
            input: manualAssemblyUnitInput,
            datalist: manualAssemblyUnitOptions,
            hiddenInput: manualAssemblyUnitHiddenInput,
            fetchUrl: "/warehouse/assembly-units",
            minLength: 2,
        });
    }

    let issuePartLookup = null;
    if (issuePartInput && issuePartOptions && issuePartHiddenInput) {
        issuePartLookup = namespace.initSearchableInput({
            input: issuePartInput,
            datalist: issuePartOptions,
            hiddenInput: issuePartHiddenInput,
            fetchUrl: "/warehouse/parts",
            minLength: 2,
        });
    }

    let issueAssemblyUnitLookup = null;
    if (issueAssemblyUnitInput && issueAssemblyUnitOptions && issueAssemblyUnitHiddenInput) {
        issueAssemblyUnitLookup = namespace.initSearchableInput({
            input: issueAssemblyUnitInput,
            datalist: issueAssemblyUnitOptions,
            hiddenInput: issueAssemblyUnitHiddenInput,
            fetchUrl: "/warehouse/assembly-units",
            minLength: 2,
        });
    }

    const manualLabelLookup = initWarehouseLabelInput({
        input: manualLabelInput,
        datalist: manualLabelOptions,
        hiddenInput: manualLabelHiddenInput,
        partHiddenInput: manualPartHiddenInput,
        mode: "receipt",
    });

    const issueLabelLookup = initWarehouseLabelInput({
        input: issueLabelInput,
        datalist: issueLabelOptions,
        hiddenInput: issueLabelHiddenInput,
        partHiddenInput: issuePartHiddenInput,
        mode: "issue",
    });

    const manualAssemblyLabelLookup = initWarehouseAssemblyLabelInput({
        input: manualAssemblyLabelInput,
        datalist: manualAssemblyLabelOptions,
        assemblyHiddenInput: manualAssemblyUnitHiddenInput,
        mode: "receipt",
    });

    const issueAssemblyLabelLookup = initWarehouseAssemblyLabelInput({
        input: issueAssemblyLabelInput,
        datalist: issueAssemblyLabelOptions,
        assemblyHiddenInput: issueAssemblyUnitHiddenInput,
        mode: "issue",
    });

    manualPartLookup?.inputElement?.addEventListener("lookup:selected", () => manualLabelLookup?.clear());
    manualPartInput?.addEventListener("input", () => manualLabelLookup?.clear());
    issuePartLookup?.inputElement?.addEventListener("lookup:selected", () => issueLabelLookup?.clear());
    issuePartInput?.addEventListener("input", () => issueLabelLookup?.clear());
    manualAssemblyUnitLookup?.inputElement?.addEventListener("lookup:selected", () => manualAssemblyLabelLookup?.clear());
    manualAssemblyUnitInput?.addEventListener("input", () => manualAssemblyLabelLookup?.clear());
    issueAssemblyUnitLookup?.inputElement?.addEventListener("lookup:selected", () => issueAssemblyLabelLookup?.clear());
    issueAssemblyUnitInput?.addEventListener("input", () => issueAssemblyLabelLookup?.clear());

    const autoPrintUrl = root.getAttribute("data-auto-print-control-card-url");
    if (autoPrintUrl && autoPrintUrl.trim().length > 0) {
        window.open(autoPrintUrl, "_blank", "noopener");
    }

    function initWarehouseLabelInput({ input, datalist, hiddenInput, partHiddenInput, mode }) {
        if (!input || !datalist || !hiddenInput || !partHiddenInput) {
            return null;
        }

        let lastItems = [];
        let requestId = 0;
        let timer = null;

        function formatQuantity(value) {
            const number = Number(value ?? 0);
            return Number.isFinite(number) ? number.toLocaleString("ru-RU", { maximumFractionDigits: 3 }) : "0";
        }

        function formatItem(item) {
            const available = mode === "issue" ? item.availableQuantity : item.quantity;
            return `${item.number} · ${formatQuantity(available)} шт`;
        }

        function clearOptions() {
            datalist.innerHTML = "";
        }

        function render(items) {
            clearOptions();
            items.forEach(item => {
                const option = document.createElement("option");
                option.value = formatItem(item);
                datalist.appendChild(option);
            });
        }

        function findMatch(rawValue) {
            const value = (rawValue || "").trim().toLowerCase();
            if (!value) {
                return null;
            }

            return lastItems.find(item => {
                const number = String(item.number || "").toLowerCase();
                const display = formatItem(item).toLowerCase();
                return value === number || value === display;
            }) || null;
        }

        function syncSelection(normalizeDisplay) {
            const match = findMatch(input.value);
            if (!match) {
                hiddenInput.value = "";
                return;
            }

            hiddenInput.value = match.id || "";
            if (normalizeDisplay) {
                input.value = match.number || "";
            }
        }

        async function load(term) {
            const partId = (partHiddenInput.value || "").trim();
            if (!partId) {
                lastItems = [];
                clearOptions();
                hiddenInput.value = "";
                return;
            }

            const currentRequestId = ++requestId;
            const url = new URL("/warehouse/labels", window.location.origin);
            url.searchParams.set("partId", partId);
            url.searchParams.set("mode", mode);
            if (term) {
                url.searchParams.set("search", term);
            }

            try {
                const response = await fetch(url.toString(), { headers: { "Accept": "application/json" } });
                if (!response.ok) {
                    throw new Error(`Не удалось загрузить ярлыки (${response.status}).`);
                }

                const items = await response.json();
                if (currentRequestId !== requestId) {
                    return;
                }

                lastItems = Array.isArray(items) ? items : [];
                render(lastItems);
                syncSelection(false);
            }
            catch (error) {
                if (currentRequestId !== requestId) {
                    return;
                }

                console.error(error);
                lastItems = [];
                clearOptions();
            }
        }

        input.addEventListener("input", () => {
            hiddenInput.value = "";
            const value = input.value.trim();
            window.clearTimeout(timer);
            if (value.length < 1) {
                lastItems = [];
                clearOptions();
                return;
            }

            timer = window.setTimeout(() => {
                void load(value);
            }, 200);
        });

        input.addEventListener("change", () => syncSelection(true));
        input.addEventListener("focus", () => {
            if ((input.value || "").trim().length === 0) {
                void load("");
            }
        });

        return {
            clear() {
                input.value = "";
                hiddenInput.value = "";
                lastItems = [];
                clearOptions();
            },
        };
    }

    function initWarehouseAssemblyLabelInput({ input, datalist, assemblyHiddenInput, mode }) {
        if (!input || !datalist || !assemblyHiddenInput) {
            return null;
        }

        let lastItems = [];
        let requestId = 0;
        let timer = null;

        function formatQuantity(value) {
            const number = Number(value ?? 0);
            return Number.isFinite(number) ? number.toLocaleString("ru-RU", { maximumFractionDigits: 3 }) : "0";
        }

        function clearOptions() {
            datalist.innerHTML = "";
        }

        function render(items) {
            clearOptions();
            items.forEach(item => {
                const option = document.createElement("option");
                option.value = item.number || "";
                const available = mode === "issue" ? item.availableQuantity : item.quantity;
                option.label = `${formatQuantity(available)} шт`;
                datalist.appendChild(option);
            });
        }

        function syncSelection() {
            const value = (input.value || "").trim().toLowerCase();
            if (!value) {
                return;
            }

            const match = lastItems.find(item => String(item.number || "").toLowerCase() === value);
            if (match) {
                input.value = match.number || "";
            }
        }

        async function load(term) {
            const assemblyUnitId = (assemblyHiddenInput.value || "").trim();
            if (!assemblyUnitId) {
                lastItems = [];
                clearOptions();
                return;
            }

            const currentRequestId = ++requestId;
            const url = new URL("/warehouse/assembly-labels", window.location.origin);
            url.searchParams.set("assemblyUnitId", assemblyUnitId);
            url.searchParams.set("mode", mode);
            if (term) {
                url.searchParams.set("search", term);
            }

            try {
                const response = await fetch(url.toString(), { headers: { "Accept": "application/json" } });
                if (!response.ok) {
                    throw new Error(`Не удалось загрузить ярлыки узла (${response.status}).`);
                }

                const items = await response.json();
                if (currentRequestId !== requestId) {
                    return;
                }

                lastItems = Array.isArray(items) ? items : [];
                render(lastItems);
                syncSelection();
            }
            catch (error) {
                if (currentRequestId !== requestId) {
                    return;
                }

                console.error(error);
                lastItems = [];
                clearOptions();
            }
        }

        input.addEventListener("input", () => {
            const value = input.value.trim();
            window.clearTimeout(timer);
            if (value.length < 1) {
                lastItems = [];
                clearOptions();
                return;
            }

            timer = window.setTimeout(() => {
                void load(value);
            }, 200);
        });

        input.addEventListener("change", syncSelection);
        input.addEventListener("focus", () => {
            if ((input.value || "").trim().length === 0) {
                void load("");
            }
        });

        return {
            clear() {
                input.value = "";
                lastItems = [];
                clearOptions();
            },
        };
    }
})();
