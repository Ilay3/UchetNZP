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
    }) {
        if (!input || !datalist || !hiddenInput || !fetchUrl) {
            throw new Error("searchable input requires input, datalist, hiddenInput and fetchUrl");
        }

        let lastItems = [];
        let selectedItem = null;

        function render(items) {
            datalist.innerHTML = "";
            items.forEach(item => {
                const option = document.createElement("option");
                option.value = formatItem(item);
                datalist.appendChild(option);
            });
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
            }
            catch (error) {
                console.error(error);
            }
        }

        const debouncedRequest = debounce(request, 200);

        input.addEventListener("input", () => {
            hiddenInput.value = "";
            selectedItem = null;
            const value = input.value.trim();
            if (value.length >= minLength) {
                debouncedRequest(value);
            }
            else {
                datalist.innerHTML = "";
                lastItems = [];
            }
        });

        input.addEventListener("change", () => {
            const value = input.value.trim();
            const match = lastItems.find(item => formatItem(item) === value);
            if (match) {
                hiddenInput.value = match.id ?? "";
                selectedItem = match;
                input.dispatchEvent(new CustomEvent("lookup:selected", { detail: match }));
            }
            else {
                hiddenInput.value = "";
                selectedItem = null;
            }
        });

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
            },
            clear: () => {
                input.value = "";
                hiddenInput.value = "";
                selectedItem = null;
                datalist.innerHTML = "";
                lastItems = [];
            },
            refresh: (term) => request(term ?? input.value.trim()),
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
