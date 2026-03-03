(function () {
    const root = document.getElementById("wipHistoryRoot");
    if (!root) {
        return;
    }

    const namespace = window.UchetNZP;
    if (!namespace) {
        throw new Error("Не инициализировано пространство имён UchetNZP.");
    }

    const partInput = document.getElementById("historyFilterPart");
    const partOptions = document.getElementById("historyPartOptions");
    const partHiddenInput = document.getElementById("historyFilterPartId");
    const sectionInput = document.getElementById("historyFilterSection");
    const sectionOptions = document.getElementById("historySectionOptions");
    const sectionHiddenInput = document.getElementById("historyFilterSectionId");

    if (partInput && partOptions && partHiddenInput) {
        namespace.initSearchableInput({
            input: partInput,
            datalist: partOptions,
            hiddenInput: partHiddenInput,
            fetchUrl: "/wip/history/parts",
            minLength: 2,
        });
    }

    if (sectionInput && sectionOptions && sectionHiddenInput) {
        namespace.initSearchableInput({
            input: sectionInput,
            datalist: sectionOptions,
            hiddenInput: sectionHiddenInput,
            fetchUrl: "/wip/history/sections",
            minLength: 2,
        });
    }

    const feedback = document.getElementById("historyActionFeedback");

    function formatQuantity(value) {
        const number = Number(value);
        if (!Number.isFinite(number)) {
            return "0";
        }

        return number.toLocaleString("ru-RU", { minimumFractionDigits: 0, maximumFractionDigits: 3 });
    }

    function showMessage(style, text) {
        if (!feedback || !text) {
            return;
        }

        feedback.classList.remove("d-none", "alert-info", "alert-success", "alert-danger", "alert-warning");
        feedback.classList.add(`alert-${style}`);
        feedback.textContent = text;
    }

    function getActiveEntries() {
        return Array.from(root.querySelectorAll(".js-history-entry"));
    }

    function recalcTotals() {
        const groups = new Map();
        let totalEntries = 0;
        let totalQuantity = 0;

        getActiveEntries().forEach(entry => {
            if (entry.dataset.cancelled === "true") {
                return;
            }

            const quantity = Number(entry.dataset.quantity) || 0;
            const groupDate = entry.dataset.groupDate ?? "";
            const entryType = entry.dataset.entryType ?? "";

            totalEntries += 1;
            totalQuantity += quantity;

            let groupInfo = groups.get(groupDate);
            if (!groupInfo) {
                groupInfo = { count: 0, quantity: 0, byType: new Map() };
                groups.set(groupDate, groupInfo);
            }

            groupInfo.count += 1;
            groupInfo.quantity += quantity;

            if (!groupInfo.byType.has(entryType)) {
                groupInfo.byType.set(entryType, { count: 0, quantity: 0 });
            }

            const typeInfo = groupInfo.byType.get(entryType);
            typeInfo.count += 1;
            typeInfo.quantity += quantity;
        });

        const totalEntriesElement = document.getElementById("historyTotalEntriesValue");
        if (totalEntriesElement) {
            totalEntriesElement.textContent = totalEntries.toString();
        }

        const totalQuantityElement = document.getElementById("historyTotalQuantityValue");
        if (totalQuantityElement) {
            totalQuantityElement.textContent = formatQuantity(totalQuantity);
        }

        root.querySelectorAll(".js-history-group").forEach(groupElement => {
            const date = groupElement.getAttribute("data-group-date") ?? "";
            const info = groups.get(date) ?? { count: 0, quantity: 0, byType: new Map() };

            groupElement.querySelectorAll(`.js-history-group-count[data-group-date="${date}"]`).forEach(element => {
                element.textContent = info.count.toString();
            });

            groupElement.querySelectorAll(`.js-history-group-quantity[data-group-date="${date}"]`).forEach(element => {
                element.textContent = formatQuantity(info.quantity);
            });

            groupElement.querySelectorAll(`.js-history-group-summary[data-group-date="${date}"]`).forEach(summaryElement => {
                const summaryType = summaryElement.getAttribute("data-summary-type") ?? "";
                const typeInfo = info.byType.get(summaryType) ?? { count: 0, quantity: 0 };

                const countElement = summaryElement.querySelector(".js-history-group-summary-count");
                if (countElement) {
                    countElement.textContent = typeInfo.count.toString();
                }

                const quantityElement = summaryElement.querySelector(".js-history-group-summary-quantity");
                if (quantityElement) {
                    quantityElement.textContent = formatQuantity(typeInfo.quantity);
                }
            });
        });
    }

    function cleanupEmptyGroups() {
        root.querySelectorAll(".js-history-group").forEach(group => {
            const hasEntries = group.querySelector(".js-history-entry") !== null;
            if (!hasEntries) {
                group.remove();
            }
        });
    }

    function updateEntryQuantity(entry, quantity) {
        entry.dataset.quantity = (Number(quantity) || 0).toString();
        const quantityElement = entry.querySelector(".js-history-quantity");
        if (quantityElement) {
            quantityElement.textContent = `${formatQuantity(quantity)} шт`;
        }
    }

    function disableEntryActions(entry) {
        entry.dataset.cancelled = "true";
        entry.querySelectorAll(".js-history-actions button").forEach(button => {
            button.disabled = true;
        });
    }

    async function revertTransfer(entry) {
        const auditId = entry.getAttribute("data-audit-id");
        if (!auditId) {
            return false;
        }

        const partName = entry.getAttribute("data-part-name") ?? "Передача";
        const opRange = entry.getAttribute("data-operation-range") ?? "";
        const quantityText = formatQuantity(entry.dataset.quantity ?? 0);
        const confirmText = [`Отменить передачу ${partName}?`, opRange, `${quantityText} шт`].filter(Boolean).join("\n");

        if (!window.confirm(confirmText)) {
            return false;
        }

        const response = await fetch(`/wip/transfer/revert/${encodeURIComponent(auditId)}`, {
            method: "POST",
            headers: { Accept: "application/json" },
        });

        if (!response.ok) {
            const text = await response.text();
            showMessage("danger", text?.trim() || "Не удалось отменить передачу.");
            return false;
        }

        const result = await response.json();
        entry.dataset.cancelled = "true";

        const statusBadge = entry.querySelector(".js-history-status");
        if (statusBadge) {
            statusBadge.textContent = "Отменено";
            statusBadge.classList.remove("bg-success");
            statusBadge.classList.add("bg-danger");
        }

        disableEntryActions(entry);
        recalcTotals();

        const fromAfter = result?.fromBalanceAfter ?? result?.fromBalance ?? null;
        const toAfter = result?.toBalanceAfter ?? result?.toBalance ?? null;
        const messageParts = ["Передача отменена."];

        if (fromAfter !== null) {
            messageParts.push(`От: ${formatQuantity(fromAfter)} шт`);
        }

        if (toAfter !== null) {
            messageParts.push(`В: ${formatQuantity(toAfter)} шт`);
        }

        showMessage("success", messageParts.join(" "));
        return true;
    }

    async function deleteReceipt(entry) {
        const receiptId = entry.getAttribute("data-entry-id");
        if (!receiptId) {
            showMessage("danger", "Не удалось определить идентификатор прихода для удаления.");
            return false;
        }

        const partName = entry.getAttribute("data-part-name") ?? "Приход";
        const opRange = entry.getAttribute("data-operation-range") ?? "";
        const quantityText = formatQuantity(entry.dataset.quantity ?? 0);
        const labelNumber = entry.getAttribute("data-label-number") ?? "";
        const warning = "Удаление окончательное. Будет создан аудит, ярлык отвяжется.";

        const confirmLines = [
            `Удалить приход ${partName}?`,
            opRange,
            `${quantityText} шт${labelNumber ? ` • ярлык ${labelNumber}` : ""}`,
            warning,
        ].filter(Boolean);

        if (!window.confirm(confirmLines.join("\n"))) {
            return false;
        }

        const response = await fetch(`/wip/history/receipt/${encodeURIComponent(receiptId)}`, {
            method: "DELETE",
            headers: { Accept: "application/json" },
        });

        if (!response.ok) {
            const text = await response.text();
            showMessage("danger", text?.trim() || "Не удалось удалить приход.");
            return false;
        }

        const result = await response.json();
        entry.remove();
        cleanupEmptyGroups();
        recalcTotals();

        const previousText = formatQuantity(result?.previousQuantity ?? 0);
        const restoredText = formatQuantity(result?.restoredQuantity ?? 0);
        const messageParts = [
            "Приход удалён.",
            `Баланс: ${previousText} → ${restoredText} шт.`,
        ];

        showMessage("success", messageParts.join(" "));
        return true;
    }

    root.addEventListener("click", event => {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }

        const transferButton = target.closest(".js-history-transfer-revert");
        if (transferButton instanceof HTMLButtonElement) {
            const entry = transferButton.closest(".js-history-entry");
            if (entry) {
                transferButton.disabled = true;
                revertTransfer(entry)
                    .then(success => {
                        if (!success) {
                            transferButton.disabled = false;
                        }
                    })
                    .catch(() => {
                        transferButton.disabled = false;
                    });
            }
        }

        const deleteButton = target.closest(".js-history-receipt-delete");
        if (deleteButton instanceof HTMLButtonElement) {
            const entry = deleteButton.closest(".js-history-entry");
            if (entry) {
                deleteButton.disabled = true;
                deleteReceipt(entry)
                    .then(success => {
                        if (!success) {
                            deleteButton.disabled = false;
                        }
                    })
                    .catch(() => {
                        deleteButton.disabled = false;
                    });
            }
        }

    });

    recalcTotals();
})();
