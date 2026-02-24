(function () {
    const defaultConfig = {
        enabled: true,
        bubbleCount: 18,
        bubbleCountMobile: 8,
        bubbleCountLowPower: 6,
        sizeRangePx: [36, 140],
        speedRangeSeconds: [14, 32],
        speedRangeSecondsMobile: [20, 42],
        speedRangeSecondsLowPower: [24, 48],
        opacityRange: [0.12, 0.32],
        opacityRangeReduced: [0.08, 0.18],
        palette: [
            "rgba(37, 99, 235, 0.5)",
            "rgba(14, 165, 233, 0.45)",
            "rgba(56, 189, 248, 0.4)",
            "rgba(167, 139, 250, 0.4)",
        ],
        labelsApiUrl: "/api/background-bubbles/labels?in_limit=50",
        labelsRefreshIntervalMs: 180000,
        maxLabelLength: 42,
        mobileBreakpointPx: 768,
        reducedMotionMode: "minimal",
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
            this.motionMediaQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
            this.environmentProfile = this.resolveEnvironmentProfile();
            this.onReducedMotionChanged = () => {
                this.environmentProfile = this.resolveEnvironmentProfile();
                this.renderBubbles();
            };
            this.onViewportChanged = () => {
                this.environmentProfile = this.resolveEnvironmentProfile();
                this.renderBubbles();
            };
        }

        async init() {
            if (!(this.rootElement instanceof HTMLElement) || this.config.enabled === false) {
                this.rootElement?.replaceChildren();
                return;
            }

            this.bindEnvironmentListeners();
            await this.refreshLabelPool();
            this.renderBubbles();
            this.startLabelPoolRefresh();
        }

        bindEnvironmentListeners() {
            if (typeof this.motionMediaQuery?.addEventListener === "function") {
                this.motionMediaQuery.addEventListener("change", this.onReducedMotionChanged);
                return;
            }

            if (typeof this.motionMediaQuery?.addListener === "function") {
                this.motionMediaQuery.addListener(this.onReducedMotionChanged);
            }

            window.addEventListener("resize", this.onViewportChanged, { passive: true });
        }

        resolveEnvironmentProfile() {
            const viewportWidth = window.innerWidth || document.documentElement.clientWidth || 0;
            const isMobile = viewportWidth > 0 && viewportWidth < this.config.mobileBreakpointPx;
            const prefersReducedMotion = this.motionMediaQuery?.matches === true;
            const deviceMemory = typeof navigator.deviceMemory === "number" ? navigator.deviceMemory : null;
            const hardwareConcurrency = typeof navigator.hardwareConcurrency === "number" ? navigator.hardwareConcurrency : null;
            const isLikelyLowPower =
                (typeof deviceMemory === "number" && deviceMemory <= 4) ||
                (typeof hardwareConcurrency === "number" && hardwareConcurrency <= 4);

            return {
                isMobile,
                prefersReducedMotion,
                isLikelyLowPower,
            };
        }

        renderBubbles() {
            const profile = this.environmentProfile;
            const performanceMode = profile.prefersReducedMotion || profile.isLikelyLowPower || profile.isMobile
                ? "reduced"
                : "full";
            this.rootElement.setAttribute("data-performance-mode", performanceMode);

            if (profile.prefersReducedMotion && this.config.reducedMotionMode === "off") {
                this.rootElement.replaceChildren();
                return;
            }

            const fragment = document.createDocumentFragment();
            const bubbleCount = this.resolveBubbleCount(profile);

            for (let index = 0; index < bubbleCount; index += 1) {
                const bubble = document.createElement("span");
                bubble.className = "background-bubbles__item";

                const sizePx = this.getRandomInRange(this.config.sizeRangePx);
                const speedSeconds = this.getRandomInRange(this.resolveSpeedRange(profile));
                const opacity = this.getRandomInRange(this.resolveOpacityRange(profile));
                const leftPercent = this.getRandomInRange([0, 100]);
                const driftPx = this.getRandomInRange(profile.prefersReducedMotion ? [-30, 30] : [-80, 80]);
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

        resolveBubbleCount(profile) {
            if (profile.prefersReducedMotion) {
                return Math.max(0, Math.floor(this.config.bubbleCountLowPower / 2));
            }

            if (profile.isLikelyLowPower) {
                return Math.max(0, this.config.bubbleCountLowPower);
            }

            if (profile.isMobile) {
                return Math.max(0, this.config.bubbleCountMobile);
            }

            return Math.max(0, this.config.bubbleCount);
        }

        resolveSpeedRange(profile) {
            if (profile.prefersReducedMotion) {
                return this.config.speedRangeSecondsLowPower;
            }

            if (profile.isLikelyLowPower) {
                return this.config.speedRangeSecondsLowPower;
            }

            if (profile.isMobile) {
                return this.config.speedRangeSecondsMobile;
            }

            return this.config.speedRangeSeconds;
        }

        resolveOpacityRange(profile) {
            if (profile.prefersReducedMotion) {
                return this.config.opacityRangeReduced;
            }

            return this.config.opacityRange;
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
