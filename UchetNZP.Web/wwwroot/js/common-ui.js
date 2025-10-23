(function () {
    const globalNamespace = window.UchetNZP || (window.UchetNZP = {});

    function defaultFormat(item) {
        if (!item) {
            return "";
        }

        return item.code ? `${item.name} (${item.code})` : item.name;
    }

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
