(function () {
    const defaultConfig = {
        bubbleCount: 18,
        sizeRangePx: [36, 140],
        speedRangeSeconds: [14, 32],
        opacityRange: [0.12, 0.32],
        palette: [
            "rgba(37, 99, 235, 0.5)",
            "rgba(14, 165, 233, 0.45)",
            "rgba(56, 189, 248, 0.4)",
            "rgba(167, 139, 250, 0.4)",
        ],
        labelsApiUrl: "/api/background-bubbles/labels?in_limit=50",
        labelsRefreshIntervalMs: 180000,
        maxLabelLength: 42,
    };

    class BackgroundBubbles {
        constructor(rootElement, config) {
            this.rootElement = rootElement;
            this.config = {
                ...defaultConfig,
                ...config,
            };
            this.labelPool = [];
            this.refreshTimerId = null;
        }

        async init() {
            if (!(this.rootElement instanceof HTMLElement)) {
                return;
            }

            await this.refreshLabelPool();
            this.renderBubbles();
            this.startLabelPoolRefresh();
        }

        renderBubbles() {
            const fragment = document.createDocumentFragment();
            const bubbleCount = Math.max(0, this.config.bubbleCount);

            for (let index = 0; index < bubbleCount; index += 1) {
                const bubble = document.createElement("span");
                bubble.className = "background-bubbles__item";

                const sizePx = this.getRandomInRange(this.config.sizeRangePx);
                const speedSeconds = this.getRandomInRange(this.config.speedRangeSeconds);
                const opacity = this.getRandomInRange(this.config.opacityRange);
                const leftPercent = this.getRandomInRange([0, 100]);
                const driftPx = this.getRandomInRange([-80, 80]);
                const delaySeconds = -this.getRandomInRange([0, speedSeconds]);
                const color = this.getPaletteColor(index);

                bubble.style.setProperty("--bubble-size", `${sizePx}px`);
                bubble.style.setProperty("--bubble-duration", `${speedSeconds}s`);
                bubble.style.setProperty("--bubble-opacity", `${opacity}`);
                bubble.style.setProperty("--bubble-left", `${leftPercent}%`);
                bubble.style.setProperty("--bubble-drift", `${driftPx}px`);
                bubble.style.setProperty("--bubble-delay", `${delaySeconds}s`);
                bubble.style.setProperty("--bubble-color", color);
                bubble.textContent = this.pickRandomLabel();

                fragment.appendChild(bubble);
            }

            this.rootElement.replaceChildren(fragment);
        }

        async refreshLabelPool() {
            try {
                const response = await fetch(this.config.labelsApiUrl, {
                    method: "GET",
                    headers: {
                        Accept: "application/json",
                    },
                    cache: "no-store",
                });

                if (!response.ok) {
                    return;
                }

                const payload = await response.json();
                const items = Array.isArray(payload?.items) ? payload.items : [];
                const normalized = items
                    .map((item) => this.normalizeLabel(item))
                    .filter((item) => item.length > 0);

                if (normalized.length > 0) {
                    this.labelPool = normalized;
                    this.renderBubbles();
                }
            } catch {
                // Ignore network errors, keep existing pool.
            }
        }

        startLabelPoolRefresh() {
            const interval = Number(this.config.labelsRefreshIntervalMs);
            if (!Number.isFinite(interval) || interval < 60_000) {
                return;
            }

            this.refreshTimerId = window.setInterval(() => {
                this.refreshLabelPool();
            }, interval);
        }

        pickRandomLabel() {
            if (!Array.isArray(this.labelPool) || this.labelPool.length === 0) {
                return "";
            }

            const index = Math.floor(Math.random() * this.labelPool.length);
            return this.labelPool[index] ?? "";
        }

        normalizeLabel(value) {
            if (typeof value !== "string") {
                return "";
            }

            const trimmed = value.trim();
            if (!trimmed) {
                return "";
            }

            if (trimmed.length <= this.config.maxLabelLength) {
                return trimmed;
            }

            return `${trimmed.slice(0, this.config.maxLabelLength - 1)}…`;
        }

        getPaletteColor(index) {
            const palette = Array.isArray(this.config.palette) ? this.config.palette : [];
            if (palette.length === 0) {
                return defaultConfig.palette[0];
            }

            return palette[index % palette.length];
        }

        getRandomInRange(range) {
            if (!Array.isArray(range) || range.length !== 2) {
                return 0;
            }

            const [min, max] = range;
            const low = Math.min(min, max);
            const high = Math.max(min, max);
            return Math.random() * (high - low) + low;
        }
    }

    const appConfig = window.backgroundBubblesConfig || {};
    const bubblesRoot = document.getElementById("backgroundBubblesRoot");

    new BackgroundBubbles(bubblesRoot, appConfig).init();
})();
