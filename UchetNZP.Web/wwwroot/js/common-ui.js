(function () {
    const globalNamespace = window.UchetNZP || (window.UchetNZP = {});

    function toTrimmedString(value) {
        if (typeof value === "string") {
            return value.trim();
        }

        if (value === null || value === undefined) {
            return "";
        }

        return String(value).trim();
    }

    function isCodePartOfName(name, code) {
        if (!name || !code) {
            return false;
        }

        return name.toLowerCase().includes(code.toLowerCase());
    }

    function formatNameWithCode(name, code) {
        const normalizedName = toTrimmedString(name);
        const normalizedCode = toTrimmedString(code);
        let result = normalizedName;

        if (!result && normalizedCode) {
            result = normalizedCode;
        }
        else if (normalizedCode && !isCodePartOfName(result, normalizedCode)) {
            result = `${result} (${normalizedCode})`;
        }

        return result;
    }

    function defaultFormat(item) {
        if (!item) {
            return "";
        }

        return formatNameWithCode(item.name, item.code);
    }

    globalNamespace.formatNameWithCode = formatNameWithCode;

    function resolveToastContainer() {
        return document.getElementById("appToastContainer");
    }

    function showToast(in_message, in_variant = "success") {
        const container = resolveToastContainer();
        if (!container || !in_message) {
            return;
        }

        const toastElement = document.createElement("div");
        toastElement.className = `toast align-items-center text-bg-${in_variant} border-0`;
        toastElement.setAttribute("role", "status");
        toastElement.setAttribute("aria-live", "polite");
        toastElement.setAttribute("aria-atomic", "true");
        toastElement.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">${in_message}</div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Закрыть"></button>
            </div>`;
        container.appendChild(toastElement);

        const toast = new bootstrap.Toast(toastElement, { delay: 5000 });
        toastElement.addEventListener("hidden.bs.toast", () => {
            toastElement.remove();
        });
        toast.show();
    }

    globalNamespace.showToast = showToast;

    function cleanupModalBackdrops() {
        document.querySelectorAll(".modal-backdrop").forEach(backdrop => backdrop.remove());

        if (!document.querySelector(".modal.show")) {
            document.body.classList.remove("modal-open");
            document.body.style.removeProperty("padding-right");
            document.body.style.removeProperty("overflow");
        }
    }

    function disableModalBackdrops() {
        document.querySelectorAll(".modal").forEach(modalElement => {
            modalElement.setAttribute("data-bs-backdrop", "false");
        });
    }

    function watchModalBackdrops() {
        const observer = new MutationObserver(mutations => {
            let shouldCleanup = false;

            mutations.forEach(mutation => {
                mutation.addedNodes.forEach(node => {
                    if (!(node instanceof HTMLElement)) {
                        return;
                    }

                    if (node.classList.contains("modal-backdrop") || node.querySelector(".modal-backdrop")) {
                        shouldCleanup = true;
                    }
                });
            });

            if (shouldCleanup) {
                cleanupModalBackdrops();
            }
        });

        observer.observe(document.body, { childList: true, subtree: true });
    }

    function setButtonLoading(in_button, in_isLoading, in_label) {
        if (!in_button) {
            return;
        }

        if (in_isLoading) {
            if (!in_button.dataset.originalContent) {
                in_button.dataset.originalContent = in_button.innerHTML;
            }

            const label = in_label || in_button.dataset.loadingLabel || "Выполняется...";
            in_button.disabled = true;
            in_button.innerHTML = `<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span><span class="ms-2">${label}</span>`;
            return;
        }

        if (in_button.dataset.originalContent) {
            in_button.innerHTML = in_button.dataset.originalContent;
        }
        in_button.disabled = false;
    }

    globalNamespace.setButtonLoading = setButtonLoading;

    const confirmModalElement = document.getElementById("appConfirmModal");
    const confirmMessageElement = document.getElementById("appConfirmModalMessage");
    const confirmEntityElement = document.getElementById("appConfirmModalEntity");
    const confirmSubmitButton = document.getElementById("appConfirmModalSubmit");
    const confirmModal = confirmModalElement ? new bootstrap.Modal(confirmModalElement) : null;
    let confirmAction = null;
    let confirmTriggerButton = null;

    function openConfirmDialog(in_options) {
        if (!confirmModal || !in_options) {
            return;
        }

        const message = in_options.message || "Подтвердите действие.";
        const entityName = in_options.entityName || "";
        const confirmLabel = in_options.confirmLabel || "Подтвердить";

        confirmMessageElement.textContent = message;
        confirmEntityElement.textContent = entityName;
        confirmEntityElement.classList.toggle("d-none", !entityName);
        confirmSubmitButton.textContent = confirmLabel;

        confirmAction = typeof in_options.onConfirm === "function" ? in_options.onConfirm : null;
        confirmTriggerButton = in_options.triggerButton || null;

        confirmModal.show();
    }

    globalNamespace.openConfirmDialog = openConfirmDialog;

    function onDeleteConfirm() {
        if (!confirmAction) {
            return;
        }

        const action = confirmAction;
        const triggerButton = confirmTriggerButton;
        confirmAction = null;
        confirmTriggerButton = null;

        if (triggerButton) {
            setButtonLoading(triggerButton, true);
        }

        action();
        confirmModal.hide();
    }

    globalNamespace.onDeleteConfirm = onDeleteConfirm;

    if (confirmSubmitButton) {
        confirmSubmitButton.addEventListener("click", onDeleteConfirm);
    }

    document.addEventListener("click", event => {
        const target = event.target.closest("[data-confirm-delete]");
        if (!target) {
            return;
        }

        event.preventDefault();
        const message = target.dataset.confirmMessage || "Удалить запись?";
        const entityName = target.dataset.confirmEntity || "";
        const confirmLabel = target.dataset.confirmLabel || "Удалить";
        const form = target.closest("form");

        openConfirmDialog({
            message,
            entityName,
            confirmLabel,
            triggerButton: target,
            onConfirm: () => {
                if (form) {
                    form.submit();
                }
            }
        });
    });

    document.addEventListener("DOMContentLoaded", () => {
        disableModalBackdrops();
        cleanupModalBackdrops();
        watchModalBackdrops();

        document.querySelectorAll("[data-toast-message]").forEach(element => {
            const message = element.dataset.toastMessage;
            const variant = element.dataset.toastVariant || "success";
            if (message) {
                showToast(message, variant);
            }
        });

        document.querySelectorAll("form[data-loading-submit]").forEach(form => {
            form.addEventListener("submit", () => {
                const submitButton = form.querySelector("button[type='submit']");
                setButtonLoading(submitButton, true);
            });
        });
    });

    function debounce(fn, delay) {
        let timeoutId;
        return function (...args) {
            window.clearTimeout(timeoutId);
            timeoutId = window.setTimeout(() => fn.apply(this, args), delay);
        };
    }

    globalNamespace.initSearchableInput = function ({
        input,
        datalist,
        hiddenInput,
        fetchUrl,
        formatItem = defaultFormat,
        minLength = 0,
        prefetch = false,
    }) {
        if (!input || !datalist || !hiddenInput || !fetchUrl) {
            throw new Error("searchable input requires input, datalist, hiddenInput and fetchUrl");
        }

        let lastItems = [];
        let selectedItem = null;
        const customItems = [];

        function getAllItems() {
            return [...customItems, ...lastItems];
        }

        function ensureCustomItem(item) {
            if (!item) {
                return false;
            }

            const value = formatItem(item);
            if (!value) {
                return false;
            }

            const exists = customItems.some(existing => formatItem(existing) === value);
            if (!exists) {
                customItems.push(item);
                return true;
            }

            return false;
        }

        function render(items) {
            datalist.innerHTML = "";
            const seenValues = new Set();
            const combined = [...customItems, ...items];
            combined.forEach(item => {
                const value = formatItem(item);
                if (!value || seenValues.has(value)) {
                    return;
                }

                seenValues.add(value);
                const option = document.createElement("option");
                option.value = value;
                datalist.appendChild(option);
            });
        }

        function updateSelectionFromValue(value) {
            const trimmed = value.trim();
            const match = getAllItems().find(item => formatItem(item) === trimmed);
            if (match) {
                hiddenInput.value = match.id ?? "";
                selectedItem = match;
                if (ensureCustomItem(match)) {
                    render(lastItems);
                }
                return;
            }

            hiddenInput.value = "";
            selectedItem = null;
        }

        async function request(term) {
            try {
                const url = new URL(fetchUrl, window.location.origin);
                if (term) {
                    url.searchParams.set("search", term);
                }

                const response = await fetch(url.toString(), { headers: { "Accept": "application/json" } });
                if (!response.ok) {
                    throw new Error(`Ошибка загрузки данных (${response.status})`);
                }

                lastItems = await response.json();
                render(lastItems);
                if (typeof term === "string" && term.trim().length > 0) {
                    updateSelectionFromValue(term);
                }
            }
            catch (error) {
                console.error(error);
            }
        }

        const debouncedRequest = debounce(request, 200);

        input.addEventListener("input", () => {
            const value = input.value.trim();
            const previousSelection = selectedItem;
            const matchesPreviousSelection =
                previousSelection && formatItem(previousSelection) === value;

            if (matchesPreviousSelection) {
                hiddenInput.value = previousSelection.id ?? "";
                selectedItem = previousSelection;
                if (ensureCustomItem(previousSelection)) {
                    render(lastItems);
                }
                return;
            }

            hiddenInput.value = "";
            selectedItem = null;
            if (value.length >= minLength) {
                debouncedRequest(value);
            }
            else if (prefetch && value.length === 0) {
                request("");
            }
            else {
                lastItems = [];
                render(lastItems);
            }

            updateSelectionFromValue(value);
        });

        input.addEventListener("change", () => {
            const value = input.value.trim();
            updateSelectionFromValue(value);
            if (selectedItem) {
                input.dispatchEvent(new CustomEvent("lookup:selected", { detail: selectedItem }));
            }
        });

        if (prefetch) {
            let hasPrefetched = false;
            input.addEventListener("focus", () => {
                if (hasPrefetched) {
                    return;
                }

                hasPrefetched = true;
                request("");
            }, { once: true });
        }

        return {
            inputElement: input,
            hiddenInput,
            datalist,
            getSelected: () => selectedItem,
            setSelected: (item) => {
                if (!item) {
                    input.value = "";
                    hiddenInput.value = "";
                    selectedItem = null;
                    return;
                }

                selectedItem = item;
                input.value = formatItem(item);
                hiddenInput.value = item.id ?? "";
                if (ensureCustomItem(item)) {
                    render(lastItems);
                }
            },
            clear: () => {
                input.value = "";
                hiddenInput.value = "";
                selectedItem = null;
                lastItems = [];
                customItems.length = 0;
                render(lastItems);
            },
            refresh: (term) => request(term ?? input.value.trim()),
            addCustomItems: (items) => {
                if (!Array.isArray(items)) {
                    return;
                }

                let hasChanges = false;
                items.forEach(item => {
                    if (ensureCustomItem(item)) {
                        hasChanges = true;
                    }
                });

                if (hasChanges) {
                    render(lastItems);
                }
            },
        };
    };

    globalNamespace.bindHotkeys = function ({ onEnter, onSave, onCancel }) {
        document.addEventListener("keydown", event => {
            const isModalOpen = document.body.classList.contains("modal-open");
            if (isModalOpen) {
                return;
            }

            const target = event.target;
            const tagName = target instanceof HTMLElement ? target.tagName.toLowerCase() : "";

            if (event.key === "Enter" && !event.ctrlKey && !event.metaKey && !event.shiftKey) {
                if (typeof onEnter === "function") {
                    if (!(tagName === "textarea")) {
                        event.preventDefault();
                        onEnter(event);
                    }
                }
            }

            if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
                if (typeof onSave === "function") {
                    event.preventDefault();
                    onSave(event);
                }
            }

            if (event.key === "Escape") {
                if (typeof onCancel === "function") {
                    event.preventDefault();
                    onCancel(event);
                }
            }
        });
    };

    globalNamespace.showInlineMessage = function (element, message, type = "success") {
        if (!element) {
            return;
        }

        element.classList.remove("d-none", "alert-success", "alert-danger", "alert-warning", "alert-info");
        element.classList.add(`alert-${type}`);
        element.textContent = message;
    };

    globalNamespace.hideInlineMessage = function (element) {
        if (!element) {
            return;
        }

        element.classList.add("d-none");
        element.textContent = "";
    };
})();
