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

    function getLookupValues(item, formatItem) {
        if (!item) {
            return [];
        }

        const values = [
            formatItem(item),
            toTrimmedString(item.name),
            toTrimmedString(item.code),
        ];

        return values.filter((value, index) => value && values.indexOf(value) === index);
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

    function showDraftToast(action) {
        if (action === "restored") {
            showToast("Черновик восстановлен.", "info");
            return;
        }

        if (action === "removed") {
            showToast("Черновик удалён.", "secondary");
        }
    }

    globalNamespace.showDraftToast = showDraftToast;

    function createDraftStorage(in_key, in_options = {}) {
        if (!in_key) {
            throw new Error("Не задан ключ черновика.");
        }

        const ttlMs = Number(in_options.ttlMs) > 0 ? Number(in_options.ttlMs) : 24 * 60 * 60 * 1000;

        function readRaw() {
            const raw = window.localStorage.getItem(in_key);
            if (!raw) {
                return null;
            }

            try {
                return JSON.parse(raw);
            }
            catch (error) {
                console.error(error);
                window.localStorage.removeItem(in_key);
                return null;
            }
        }

        function save(data) {
            if (!data) {
                return;
            }

            const payload = {
                savedAt: new Date().toISOString(),
                data,
            };
            window.localStorage.setItem(in_key, JSON.stringify(payload));
        }

        function clear() {
            window.localStorage.removeItem(in_key);
        }

        function getFresh() {
            const payload = readRaw();
            if (!payload || !payload.savedAt || !payload.data) {
                return null;
            }

            const savedAtMs = Date.parse(payload.savedAt);
            if (!Number.isFinite(savedAtMs) || (Date.now() - savedAtMs) > ttlMs) {
                clear();
                return null;
            }

            return payload;
        }

        function restoreOrClear(onRestore) {
            const payload = getFresh();
            if (!payload) {
                return;
            }

            const shouldRestore = window.confirm("Найден свежий черновик. Восстановить его?\nНажмите «Отмена», чтобы удалить черновик.");
            if (!shouldRestore) {
                clear();
                showDraftToast("removed");
                return;
            }

            if (typeof onRestore === "function") {
                onRestore(payload.data);
            }
            showDraftToast("restored");
        }

        return {
            save,
            clear,
            restoreOrClear,
        };
    }

    globalNamespace.createDraftStorage = createDraftStorage;

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
        const dropdownId = `${input.id || "lookup"}Suggestions`;
        let renderItems = [];
        let activeIndex = -1;
        let suppressBlurHide = false;

        input.removeAttribute("list");
        input.setAttribute("autocomplete", "off");
        input.setAttribute("aria-autocomplete", "list");
        input.setAttribute("aria-expanded", "false");
        input.setAttribute("aria-controls", dropdownId);

        const wrapper = document.createElement("div");
        wrapper.className = "lookup-dropdown";
        input.parentNode?.insertBefore(wrapper, input);
        wrapper.appendChild(input);
        wrapper.appendChild(datalist);

        const suggestionList = document.createElement("div");
        suggestionList.id = dropdownId;
        suggestionList.className = "lookup-dropdown__menu";
        suggestionList.setAttribute("role", "listbox");
        suggestionList.hidden = true;
        wrapper.appendChild(suggestionList);

        function getAllItems() {
            return [...customItems, ...lastItems];
        }

        function isDropdownVisible() {
            return !suggestionList.hidden;
        }

        function hideSuggestions() {
            suggestionList.hidden = true;
            suggestionList.innerHTML = "";
            input.setAttribute("aria-expanded", "false");
            input.removeAttribute("aria-activedescendant");
            activeIndex = -1;
        }

        function showSuggestions() {
            if (renderItems.length === 0) {
                hideSuggestions();
                return;
            }

            suggestionList.hidden = false;
            input.setAttribute("aria-expanded", "true");
        }

        function renderActiveItem() {
            Array.from(suggestionList.children).forEach((node, index) => {
                if (!(node instanceof HTMLElement)) {
                    return;
                }

                const isActive = index === activeIndex;
                node.classList.toggle("active", isActive);
                node.setAttribute("aria-selected", isActive ? "true" : "false");

                if (isActive) {
                    input.setAttribute("aria-activedescendant", node.id);
                    node.scrollIntoView({ block: "nearest" });
                }
            });

            if (activeIndex < 0) {
                input.removeAttribute("aria-activedescendant");
            }
        }

        function ensureCustomItem(item) {
            if (!item) {
                return false;
            }

            const values = getLookupValues(item, formatItem);
            if (values.length === 0) {
                return false;
            }

            const exists = customItems.some(existing => {
                const existingValues = getLookupValues(existing, formatItem);
                return existingValues.some(value => values.includes(value));
            });

            if (!exists) {
                customItems.push(item);
                return true;
            }

            return false;
        }

        function render(items) {
            datalist.innerHTML = "";
            const combined = [...customItems, ...items];
            const seenValues = new Set();

            renderItems = combined.filter(item => {
                const displayValue = formatItem(item);
                if (!displayValue || seenValues.has(displayValue)) {
                    return false;
                }

                seenValues.add(displayValue);
                const option = document.createElement("option");
                option.value = displayValue;
                datalist.appendChild(option);
                return true;
            });

            suggestionList.innerHTML = "";
            renderItems.forEach((item, index) => {
                const button = document.createElement("button");
                button.type = "button";
                button.id = `${dropdownId}Option${index}`;
                button.className = "lookup-dropdown__option";
                button.setAttribute("role", "option");
                button.setAttribute("aria-selected", "false");
                button.textContent = formatItem(item);
                button.addEventListener("mousedown", event => {
                    event.preventDefault();
                    suppressBlurHide = true;
                });
                button.addEventListener("click", () => {
                    applySelection(item, { dispatchEvent: true, normalizeDisplay: true });
                    suppressBlurHide = false;
                });
                suggestionList.appendChild(button);
            });

            if (renderItems.length === 0) {
                hideSuggestions();
                return;
            }

            activeIndex = Math.min(activeIndex, renderItems.length - 1);
            renderActiveItem();

            if (document.activeElement === input) {
                showSuggestions();
            }
        }

        function applySelection(item, options = {}) {
            const normalizeDisplay = options.normalizeDisplay !== false;
            const dispatchEvent = options.dispatchEvent === true;

            selectedItem = item;
            hiddenInput.value = item?.id ?? "";
            if (normalizeDisplay && item) {
                input.value = formatItem(item);
            }

            if (item && ensureCustomItem(item)) {
                render(lastItems);
            }
            else {
                hideSuggestions();
            }

            if (dispatchEvent && item) {
                input.dispatchEvent(new CustomEvent("lookup:selected", { detail: item }));
            }
        }

        function updateSelectionFromValue(value, options = {}) {
            const trimmed = value.trim();
            const normalizedTerm = trimmed.toLowerCase();
            const match = getAllItems().find(item => getLookupValues(item, formatItem)
                .some(candidate => candidate.toLowerCase() === normalizedTerm));
            if (match) {
                applySelection(match, options);
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
                else if (document.activeElement === input) {
                    showSuggestions();
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
                showSuggestions();
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

            if (value.length === 0 && !prefetch) {
                hideSuggestions();
            }
        });

        input.addEventListener("change", () => {
            const value = input.value.trim();
            updateSelectionFromValue(value, { normalizeDisplay: true });
        });

        input.addEventListener("focus", () => {
            if (renderItems.length > 0) {
                showSuggestions();
            }
        });

        input.addEventListener("blur", () => {
            window.setTimeout(() => {
                if (suppressBlurHide) {
                    suppressBlurHide = false;
                    return;
                }

                hideSuggestions();
            }, 150);
        });

        input.addEventListener("keydown", event => {
            if (!isDropdownVisible()) {
                if (event.key === "ArrowDown" && renderItems.length > 0) {
                    activeIndex = 0;
                    renderActiveItem();
                    showSuggestions();
                    event.preventDefault();
                }
                return;
            }

            if (event.key === "ArrowDown") {
                activeIndex = Math.min(activeIndex + 1, renderItems.length - 1);
                renderActiveItem();
                event.preventDefault();
            }
            else if (event.key === "ArrowUp") {
                activeIndex = activeIndex <= 0 ? 0 : activeIndex - 1;
                renderActiveItem();
                event.preventDefault();
            }
            else if (event.key === "Enter" && activeIndex >= 0 && renderItems[activeIndex]) {
                applySelection(renderItems[activeIndex], { dispatchEvent: true, normalizeDisplay: true });
                event.preventDefault();
            }
            else if (event.key === "Escape") {
                hideSuggestions();
            }
        });

        document.addEventListener("pointerdown", event => {
            const target = event.target;
            if (target instanceof Node && wrapper.contains(target)) {
                return;
            }

            hideSuggestions();
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
                    hideSuggestions();
                    return;
                }

                applySelection(item, { normalizeDisplay: true });
            },
            clear: () => {
                input.value = "";
                hiddenInput.value = "";
                selectedItem = null;
                lastItems = [];
                customItems.length = 0;
                renderItems = [];
                render(lastItems);
                hideSuggestions();
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
