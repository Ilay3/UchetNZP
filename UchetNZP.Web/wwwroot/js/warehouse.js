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

    if (partInput && partOptions && partHiddenInput) {
        namespace.initSearchableInput({
            input: partInput,
            datalist: partOptions,
            hiddenInput: partHiddenInput,
            fetchUrl: "/warehouse/parts",
            minLength: 2,
        });
    }
})();
