// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(function () {
    const secretCommands = ["очисткабд", "ochistkabd", "cleardb"];
    const endpointUrl = "/internal/maintenance/clear-database";
    let buffer = "";

    function isTextInput(target) {
        if (!(target instanceof HTMLElement)) {
            return false;
        }

        const tagName = target.tagName.toLowerCase();
        if (tagName === "input" || tagName === "textarea") {
            return true;
        }

        return target.isContentEditable;
    }

    function updateBuffer(key) {
        buffer = (buffer + key).slice(-Math.max(...secretCommands.map(x => x.length)));
    }

    function resetBuffer() {
        buffer = "";
    }

    async function clearDatabase() {
        if (!window.confirm("Очистить базу данных?")) {
            return;
        }

        try {
            const response = await fetch(endpointUrl, {
                method: "POST",
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                },
            });

            if (response.status === 404) {
                console.info("Команда очистки базы данных недоступна в этом окружении.");
                return;
            }

            if (!response.ok) {
                let message = response.statusText;
                try {
                    const data = await response.json();
                    if (data && typeof data.message === "string" && data.message.trim().length > 0) {
                        message = data.message;
                    }
                }
                catch (error) {
                    console.error(error);
                }

                window.alert(`Не удалось очистить базу данных: ${message}`);
                return;
            }

            window.alert("База данных очищена.");
        }
        catch (error) {
            console.error(error);
            window.alert("Произошла ошибка при выполнении команды очистки.");
        }
    }

    document.addEventListener("keydown", event => {
        if (event.defaultPrevented) {
            return;
        }

        if (event.ctrlKey || event.metaKey || event.altKey) {
            return;
        }

        if (event.key === "Escape") {
            resetBuffer();
            return;
        }

        if (event.key.length !== 1) {
            return;
        }

        if (isTextInput(event.target)) {
            return;
        }

        const key = event.key.toLowerCase();
        updateBuffer(key);

        if (secretCommands.some(command => buffer.endsWith(command))) {
            resetBuffer();
            clearDatabase();
        }
    });
})();
