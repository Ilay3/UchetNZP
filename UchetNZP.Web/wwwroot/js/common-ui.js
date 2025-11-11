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

        let container = null;
        let dropdown = null;
        let lastItems = [];
        let selectedItem = null;
        const customItems = [];
        let renderedItems = [];
        let highlightedIndex = -1;
        let isDropdownOpen = false;

        const parentElement = input.parentElement;
        if (parentElement) {
            container = document.createElement("div");
            container.classList.add("searchable-input", "position-relative", "w-100");
            parentElement.insertBefore(container, input);
            container.appendChild(input);
            if (datalist.parentElement !== container) {
                container.appendChild(datalist);
            }

            dropdown = document.createElement("div");
            dropdown.classList.add("searchable-input__dropdown", "list-group", "shadow-sm");
            dropdown.setAttribute("role", "listbox");
            dropdown.hidden = true;
            container.appendChild(dropdown);

            const originalListId = input.getAttribute("list");
            if (originalListId === datalist.id) {
                input.removeAttribute("list");
            }
        }

        function closeDropdown() {
            if (!dropdown) {
                return;
            }

            dropdown.classList.remove("searchable-input__dropdown--visible");
            dropdown.hidden = true;
            isDropdownOpen = false;
            setHighlightedIndex(-1);
        }

        function openDropdown() {
            if (!dropdown) {
                return;
            }

            if (renderedItems.length === 0) {
                closeDropdown();
                return;
            }

            dropdown.hidden = false;
            dropdown.classList.add("searchable-input__dropdown--visible");
            isDropdownOpen = true;
        }

        function updateDropdownVisibility() {
            if (!dropdown) {
                return;
            }

            if (document.activeElement === input && renderedItems.length > 0) {
                openDropdown();
            }
            else {
                closeDropdown();
            }
        }

        function setHighlightedIndex(index) {
            if (!dropdown) {
                highlightedIndex = -1;
                return;
            }

            const options = Array.from(dropdown.children);
            options.forEach((option, optionIndex) => {
                if (optionIndex === index) {
                    option.classList.add("active");
                    option.setAttribute("aria-selected", "true");
                    option.scrollIntoView({ block: "nearest" });
                }
                else {
                    option.classList.remove("active");
                    option.setAttribute("aria-selected", "false");
                }
            });

            highlightedIndex = index;
        }

        function moveHighlight(delta) {
            if (!dropdown || renderedItems.length === 0) {
                return;
            }

            if (!isDropdownOpen) {
                openDropdown();
            }

            const total = renderedItems.length;
            if (total === 0) {
                return;
            }

            let nextIndex = highlightedIndex + delta;
            if (nextIndex < 0) {
                nextIndex = total - 1;
            }
            else if (nextIndex >= total) {
                nextIndex = 0;
            }

            setHighlightedIndex(nextIndex);
        }

        function selectHighlighted() {
            if (highlightedIndex < 0 || highlightedIndex >= renderedItems.length) {
                return;
            }

            const item = renderedItems[highlightedIndex];
            if (item) {
                applySelection(item);
            }
        }

        function applySelection(item) {
            if (!item) {
                return;
            }

            const value = formatItem(item);
            if (!value) {
                return;
            }

            input.value = value;
            hiddenInput.value = item.id ?? "";
            selectedItem = item;
            if (ensureCustomItem(item)) {
                render(lastItems);
            }

            closeDropdown();
            input.dispatchEvent(new Event("change"));
        }

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
            renderedItems = [];
            if (dropdown) {
                dropdown.innerHTML = "";
            }

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

                renderedItems.push(item);
                if (dropdown) {
                    const button = document.createElement("button");
                    button.type = "button";
                    button.classList.add("searchable-input__option", "list-group-item", "list-group-item-action", "py-2");
                    button.textContent = value;
                    button.setAttribute("role", "option");
                    button.addEventListener("mousedown", event => {
                        event.preventDefault();
                        applySelection(item);
                        input.focus();
                    });
                    dropdown.appendChild(button);
                }
            });

            if (dropdown) {
                dropdown.scrollTop = 0;
            }

            setHighlightedIndex(-1);
            updateDropdownVisibility();
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
                closeDropdown();
                return;
            }

            hiddenInput.value = "";
            selectedItem = null;
            if (value.length >= minLength) {
                debouncedRequest(value);
            }
            else if (prefetch && value.length === 0) {
                request("");
                closeDropdown();
            }
            else {
                lastItems = [];
                render(lastItems);
                closeDropdown();
            }

            updateSelectionFromValue(value);
        });

        input.addEventListener("change", () => {
            const value = input.value.trim();
            updateSelectionFromValue(value);
            if (selectedItem) {
                input.dispatchEvent(new CustomEvent("lookup:selected", { detail: selectedItem }));
            }
            closeDropdown();
        });

        input.addEventListener("keydown", event => {
            if (event.key === "ArrowDown") {
                event.preventDefault();
                moveHighlight(1);
            }
            else if (event.key === "ArrowUp") {
                event.preventDefault();
                moveHighlight(-1);
            }
            else if (event.key === "Enter") {
                if (isDropdownOpen && highlightedIndex >= 0) {
                    event.preventDefault();
                    selectHighlighted();
                }
            }
            else if (event.key === "Escape") {
                if (isDropdownOpen) {
                    event.preventDefault();
                    closeDropdown();
                }
            }
        });

        input.addEventListener("focus", () => {
            if (renderedItems.length > 0) {
                openDropdown();
            }
        });

        input.addEventListener("blur", () => {
            window.setTimeout(() => {
                const activeElement = document.activeElement;
                if (!container || !activeElement || !container.contains(activeElement)) {
                    closeDropdown();
                }
            }, 100);
        });

        if (container) {
            const handleDocumentMouseDown = (event) => {
                const target = event.target;
                if (!(target instanceof Node)) {
                    closeDropdown();
                    return;
                }

                if (!container.contains(target)) {
                    closeDropdown();
                }
            };
            document.addEventListener("mousedown", handleDocumentMouseDown);
        }

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
                    closeDropdown();
                    return;
                }

                selectedItem = item;
                input.value = formatItem(item);
                hiddenInput.value = item.id ?? "";
                if (ensureCustomItem(item)) {
                    render(lastItems);
                }
                closeDropdown();
            },
            clear: () => {
                input.value = "";
                hiddenInput.value = "";
                selectedItem = null;
                lastItems = [];
                customItems.length = 0;
                render(lastItems);
                closeDropdown();
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
