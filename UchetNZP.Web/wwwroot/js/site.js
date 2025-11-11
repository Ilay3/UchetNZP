// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(function () {
    const endpointUrl = "/internal/maintenance/clear-database";
    const secretCommandHandlers = [
        {
            keywords: ["очисткабд", "ochistkabd", "cleardb"],
            action: clearDatabase,
        },
        {
            keywords: ["admin"],
            action: openAdminPanel,
        },
        {
            keywords: ["edit"],
            action: openAdminRemnants,
        },
    ];
    const maxCommandLength = Math.max(
        0,
        ...secretCommandHandlers.flatMap(handler => handler.keywords.map(keyword => keyword.length)),
    );
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
        buffer = (buffer + key).slice(-maxCommandLength);
    }

    function resetBuffer() {
        buffer = "";
    }

    function confirmAndRedirect(in_message, in_url) {
        if (!window.confirm(in_message)) {
            return;
        }

        window.location.assign(in_url);
    }

    function openAdminPanel() {
        confirmAndRedirect("Открыть страницу администрирования?", "/admin");
    }

    function openAdminRemnants() {
        confirmAndRedirect("Открыть страницу администрирования остатков?", "/admin/wip");
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

        const command = secretCommandHandlers.find(handler =>
            handler.keywords.some(keyword => buffer.endsWith(keyword)));

        if (command) {
            resetBuffer();
            command.action();
        }
    });

    function animateGlassPanels() {
        const panels = Array.from(document.querySelectorAll(".glass-panel"));
        if (panels.length === 0) {
            return;
        }

        panels.forEach(panel => panel.classList.add("glass-panel--animated"));

        const prefersReducedMotion = typeof window.matchMedia === "function"
            && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        if (!("IntersectionObserver" in window) || prefersReducedMotion) {
            panels.forEach(panel => panel.classList.add("glass-panel--visible"));
            return;
        }

        const observer = new IntersectionObserver((in_entries, in_observer) => {
            in_entries.forEach(in_entry => {
                if (!in_entry.isIntersecting) {
                    return;
                }

                in_entry.target.classList.add("glass-panel--visible");
                in_observer.unobserve(in_entry.target);
            });
        }, { threshold: 0.15 });

        panels.forEach(panel => observer.observe(panel));
    }

    window.addEventListener("DOMContentLoaded", animateGlassPanels);
})();
