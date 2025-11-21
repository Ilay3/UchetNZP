(function () {
    const root = document.getElementById("wipHistoryRoot");
    if (!root) {
        return;
    }

    const feedback = document.getElementById("historyActionFeedback");
    const modalElement = document.getElementById("historyRevertModal");
    const versionsContainer = document.getElementById("historyRevertVersions");
    const confirmButton = document.getElementById("historyRevertConfirm");
    const modalLabel = document.getElementById("historyRevertModalLabel");
    const modal = modalElement ? new bootstrap.Modal(modalElement) : null;

    let currentReceiptContext = null;

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

    function buildVersionItem(version, index) {
        const label = document.createElement("label");
        label.className = "list-group-item list-group-item-action d-flex justify-content-between align-items-start gap-3";

        const radio = document.createElement("input");
        radio.type = "radio";
        radio.name = "historyReceiptVersion";
        radio.value = version.versionId ?? "";
        radio.className = "form-check-input mt-1";
        radio.checked = index === 0;
        radio.required = true;

        const body = document.createElement("div");
        body.className = "flex-grow-1";

        const title = document.createElement("div");
        title.className = "fw-semibold";
        title.textContent = `${version.action ?? "Версия"} — ${formatQuantity(version.newQuantity ?? version.previousQuantity ?? 0)} шт`;

        const details = document.createElement("div");
        details.className = "text-muted small";
        const previous = formatQuantity(version.previousQuantity ?? 0);
        const next = formatQuantity(version.newQuantity ?? 0);
        const createdAt = version.createdAt ? new Date(version.createdAt).toLocaleString("ru-RU") : "";
        const labelInfo = version.labelNumber ? `, ярлык ${version.labelNumber}` : "";
        details.textContent = `${previous} → ${next} шт • ${createdAt}${labelInfo}`;

        if (version.comment) {
            const comment = document.createElement("div");
            comment.className = "small";
            comment.textContent = version.comment;
            body.append(title, details, comment);
        }
        else {
            body.append(title, details);
        }

        label.append(radio, body);
        return label;
    }

    async function loadReceiptVersions(entry) {
        if (!modal || !versionsContainer || !confirmButton) {
            return;
        }

        const receiptId = entry.getAttribute("data-entry-id");
        if (!receiptId) {
            showMessage("danger", "Не удалось определить идентификатор прихода.");
            return;
        }

        versionsContainer.innerHTML = "";
        confirmButton.disabled = true;
        currentReceiptContext = { entry, receiptId };

        const response = await fetch(`/wip/receipts/${encodeURIComponent(receiptId)}/versions`, {
            headers: { Accept: "application/json" },
        });

        if (!response.ok) {
            showMessage("danger", "Не удалось загрузить версии прихода.");
            return;
        }

        const data = await response.json();
        const versions = Array.isArray(data?.versions) ? data.versions : [];

        if (modalLabel) {
            const partName = entry.getAttribute("data-part-name") ?? "Приход";
            modalLabel.textContent = `Восстановление прихода — ${partName}`;
        }

        if (!versions.length) {
            const empty = document.createElement("div");
            empty.className = "text-muted";
            empty.textContent = "Версии для восстановления не найдены.";
            versionsContainer.append(empty);
            modal.show();
            return;
        }

        versions.forEach((version, index) => {
            const item = buildVersionItem(version, index);
            versionsContainer.append(item);
        });

        confirmButton.disabled = false;
        modal.show();
    }

    function getSelectedVersionId() {
        const selected = versionsContainer?.querySelector("input[name=\"historyReceiptVersion\"]:checked");
        return selected ? selected.value : null;
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
            showMessage("danger", "Не найден идентификатор аудита передачи.");
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

    async function revertReceipt() {
        if (!currentReceiptContext || !confirmButton) {
            return false;
        }

        const versionId = getSelectedVersionId();
        if (!versionId) {
            showMessage("warning", "Выберите версию для восстановления.");
            return false;
        }

        const { entry, receiptId } = currentReceiptContext;
        const response = await fetch(`/wip/receipts/${encodeURIComponent(receiptId)}/revert`, {
            method: "POST",
            headers: {
                Accept: "application/json",
                "Content-Type": "application/json",
            },
            body: JSON.stringify({ versionId }),
        });

        if (!response.ok) {
            const errorText = await response.text();
            showMessage("danger", errorText?.trim() || "Не удалось восстановить приход.");
            return false;
        }

        const result = await response.json();
        modal?.hide();

        updateEntryQuantity(entry, result?.newQuantity ?? result?.targetQuantity ?? entry.dataset.quantity ?? 0);
        entry.setAttribute("data-version-id", result?.versionId ?? versionId);
        currentReceiptContext = null;
        recalcTotals();

        const previousText = formatQuantity(result?.previousQuantity ?? 0);
        const newText = formatQuantity(result?.newQuantity ?? result?.targetQuantity ?? 0);
        showMessage("success", `Приход обновлён: ${previousText} → ${newText} шт.`);
        return true;
    }

    root.addEventListener("click", event => {
        const target = event.target;
        if (!(target instanceof HTMLElement)) {
            return;
        }

        if (target.classList.contains("js-history-transfer-revert")) {
            const entry = target.closest(".js-history-entry");
            if (entry) {
                target.disabled = true;
                revertTransfer(entry)
                    .then(success => {
                        if (!success) {
                            target.disabled = false;
                        }
                    })
                    .catch(() => {
                        target.disabled = false;
                    });
            }
        }

        if (target.classList.contains("js-history-receipt-revert")) {
            const entry = target.closest(".js-history-entry");
            if (entry) {
                loadReceiptVersions(entry).catch(() => {
                    showMessage("danger", "Не удалось загрузить версии прихода.");
                });
            }
        }

        if (target.name === "historyReceiptVersion") {
            if (confirmButton) {
                confirmButton.disabled = false;
            }
        }
    });

    confirmButton?.addEventListener("click", () => {
        confirmButton.disabled = true;
        revertReceipt()
            .then(success => {
                if (!success) {
                    confirmButton.disabled = false;
                }
            })
            .catch(() => {
                confirmButton.disabled = false;
            });
    });

    modalElement?.addEventListener("change", event => {
        const target = event.target;
        if (target instanceof HTMLInputElement && target.name === "historyReceiptVersion" && confirmButton) {
            confirmButton.disabled = false;
        }
    });

    recalcTotals();
})();
