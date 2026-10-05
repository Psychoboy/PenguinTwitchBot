/* global signalR */
(async function () {
    const containerEl = document.getElementById("counters-container");
    const statusEl = document.getElementById("counter-status");

    function setStatus(message, connected) {
        if (!statusEl) return;
        statusEl.textContent = message;
        statusEl.classList.toggle("connected", connected);
        statusEl.classList.toggle("hidden", connected);
    }

    const currentUrlParams = new URLSearchParams(window.location.search);

    let items = [];
    let settings = {};

    function parseSettings(sourceParams) {
        function p(name, fallback) {
            const value = sourceParams.get(name);
            return value === null || value.trim() === "" ? fallback : value;
        }

        return {
            fontFamily: p("fontFamily", '"Segoe UI", sans-serif'),
            fontSize: parseInt(p("fontSize", "32"), 10) || 32,
            fontWeight: p("fontWeight", "700"),
            color: p("color", "#ffffff"),
            labelColor: p("labelColor", "#ffb74d"),
            bgColor: p("bgColor", "transparent"),
            itemBgColor: p("itemBgColor", "rgba(0, 0, 0, 0.6)"),
            textShadow: p("textShadow", "1px 1px 3px #000000"),
            letterSpacing: parseInt(p("letterSpacing", "0"), 10) || 0,
            borderRadius: parseInt(p("borderRadius", "0"), 10) || 0,
            padding: parseInt(p("padding", "8"), 10) || 8,
            gap: parseInt(p("gap", "8"), 10) || 8,
            layout: p("layout", "horizontal")
        };
    }

    function parseItems(sourceParams) {
        let parsedItems = [];
        const rawItems = sourceParams.get("counters") || sourceParams.get("items");
        if (rawItems) {
            try {
                parsedItems = JSON.parse(rawItems);
            } catch (e) {
                try {
                    parsedItems = JSON.parse(decodeURIComponent(rawItems));
                } catch (e2) {
                    console.warn("Failed to parse counters param as JSON:", e, rawItems);
                    if (typeof rawItems === "string" && rawItems.includes(",")) {
                        parsedItems = rawItems.split(",").map(s => s.trim()).filter(Boolean).map(name => ({
                            counterType: name.toLowerCase() === "death" ? "death" : "generic",
                            counterName: name
                        }));
                    }
                }
            }
        }

        // Fallback: single counter via direct params
        if (!Array.isArray(parsedItems) || parsedItems.length === 0) {
            const singleName = sourceParams.get("counterName") || sourceParams.get("name") || "";
            const singleType = sourceParams.get("counterType") || "generic";
            const singleLabel = sourceParams.get("customLabel") || sourceParams.get("label") || "";
            if (singleName || singleType.toLowerCase() === "death") {
                parsedItems = [{
                    counterType: singleType,
                    counterName: singleName,
                    customLabel: singleLabel
                }];
            }
        }

        if (Array.isArray(parsedItems)) {
            return parsedItems.map(i => ({
                counterType: (i.counterType || i.CounterType || i.type || i.Type || "generic").toLowerCase(),
                counterName: (i.counterName || i.CounterName || i.name || i.Name || "").trim(),
                customLabel: (i.customLabel ?? i.CustomLabel ?? i.label ?? i.Label ?? null),
                prefix: (i.prefix ?? i.Prefix ?? ""),
                suffix: (i.suffix ?? i.Suffix ?? ""),
                customColor: (i.customColor ?? i.CustomColor ?? null),
                customLabelColor: (i.customLabelColor ?? i.CustomLabelColor ?? null),
                customItemBgColor: (i.customItemBgColor ?? i.CustomItemBgColor ?? null)
            })).filter(i => i.counterType === "death" || Boolean(i.counterName));
        }

        return [];
    }

    settings = parseSettings(currentUrlParams);
    items = parseItems(currentUrlParams);

    // If no counters were found in current query params, attempt to load from overlay layout config (fallback)
    if (items.length === 0) {
        try {
            const layout = currentUrlParams.get("layout") || "";
            const cfgUrl = layout
                ? `/api/overlay/config?layout=${encodeURIComponent(layout)}`
                : "/api/overlay/config";
            const res = await fetch(cfgUrl);
            if (res.ok) {
                const cfg = await res.json();
                const counterWidget = (cfg.widgets || []).find(w => w.widgetType === "counter" && w.isEnabled);
                if (counterWidget && counterWidget.sourcePath) {
                    const subUrl = new URL(counterWidget.sourcePath, window.location.origin);
                    settings = parseSettings(subUrl.searchParams);
                    items = parseItems(subUrl.searchParams);
                }
            }
        } catch (err) {
            console.warn("Fallback overlay config check failed:", err);
        }
    }

    function applyStyles() {
        if (!containerEl) return;
        const root = containerEl.style;
        const isMulti = items.length > 1;
        const isVertical = settings.layout === "vertical";

        let justify = "flex-start";
        let align = "flex-start";
        if (settings.align === "center") {
            justify = "center";
            align = "center";
        } else if (settings.align === "end") {
            justify = "flex-end";
            align = "flex-end";
        } else if (!isMulti && !settings.align) {
            justify = "center";
            align = "center";
        }

        root.setProperty("--counter-layout", isVertical ? "column" : "row");
        root.setProperty("--counter-wrap", "wrap");
        root.setProperty("--counter-justify", justify);
        root.setProperty("--counter-align-items", align);
        root.setProperty("--counter-align-content", justify);
        root.setProperty("--counter-gap", `${settings.gap}px`);
        root.setProperty("--counter-bg", settings.bgColor);
        root.setProperty("--counter-radius", `${settings.borderRadius}px`);
        root.setProperty("--counter-padding", `${settings.padding}px`);
        root.setProperty("--counter-item-padding", `${Math.max(2, Math.round(settings.padding * 0.75))}px ${Math.max(4, Math.round(settings.padding * 1.5))}px`);
        root.setProperty("--counter-font-family", settings.fontFamily);
        root.setProperty("--counter-font-size", `${settings.fontSize}px`);
        root.setProperty("--counter-font-weight", settings.fontWeight);
        root.setProperty("--counter-color", settings.color);
        root.setProperty("--counter-label-color", settings.labelColor);
        root.setProperty("--counter-item-bg", settings.itemBgColor);
        root.setProperty("--counter-text-shadow", settings.textShadow);
        root.setProperty("--counter-letter-spacing", `${settings.letterSpacing}px`);
    }

    // Map of element references: key -> { el, valEl }
    const renderedItems = [];

    function renderSkeleton() {
        if (!containerEl) return;
        containerEl.innerHTML = "";
        renderedItems.length = 0;

        items.forEach((item) => {
            const type = (item.counterType || "generic").toLowerCase();
            const name = item.counterName || "";
            const label = item.customLabel || (type === "death" ? "Deaths" : name);

            const itemEl = document.createElement("div");
            itemEl.className = "counter-item";
            itemEl.dataset.type = type;
            itemEl.dataset.name = name.toLowerCase();

            if (item.customItemBgColor) {
                itemEl.style.setProperty("--item-custom-bg", item.customItemBgColor);
            }

            const labelEl = document.createElement("span");
            labelEl.className = "counter-label";
            labelEl.textContent = label ? `${label}:` : "";
            if (item.customLabelColor) {
                labelEl.style.setProperty("--item-custom-label-color", item.customLabelColor);
            }

            const valWrapEl = document.createElement("span");
            valWrapEl.className = "counter-val-wrapper";

            if (item.prefix) {
                const prefixEl = document.createElement("span");
                prefixEl.className = "counter-prefix";
                prefixEl.textContent = item.prefix;
                if (item.customColor) prefixEl.style.setProperty("--item-custom-color", item.customColor);
                valWrapEl.appendChild(prefixEl);
            }

            const valEl = document.createElement("span");
            valEl.className = "counter-value";
            valEl.textContent = "0";
            if (item.customColor) {
                valEl.style.setProperty("--item-custom-color", item.customColor);
            }
            valWrapEl.appendChild(valEl);

            if (item.suffix) {
                const suffixEl = document.createElement("span");
                suffixEl.className = "counter-suffix";
                suffixEl.textContent = item.suffix;
                if (item.customColor) suffixEl.style.setProperty("--item-custom-color", item.customColor);
                valWrapEl.appendChild(suffixEl);
            }

            if (label) {
                itemEl.appendChild(labelEl);
            }
            itemEl.appendChild(valWrapEl);
            containerEl.appendChild(itemEl);

            renderedItems.push({
                type,
                name: name.toLowerCase(),
                valEl
            });
        });
    }

    function updateValue(type, name, value) {
        const cleanType = (type || "").toLowerCase();
        const cleanName = (name || "").toLowerCase();

        renderedItems.forEach(item => {
            let matches = false;
            if (cleanType === "death" && item.type === "death") {
                matches = true;
            } else if (cleanType === "generic" && item.type === "generic" && item.name === cleanName) {
                matches = true;
            }

            if (matches && item.valEl) {
                item.valEl.textContent = value.toString();
                item.valEl.classList.remove("bump");
                // Trigger reflow to restart css animation
                void item.valEl.offsetWidth;
                item.valEl.classList.add("bump");
                setTimeout(() => item.valEl.classList.remove("bump"), 160);
            }
        });
    }

    async function fetchInitialState() {
        const hasDeath = items.some(i => (i.counterType || "").toLowerCase() === "death");
        const genericNames = items
            .filter(i => (i.counterType || "generic").toLowerCase() !== "death" && i.counterName)
            .map(i => i.counterName);

        try {
            if (genericNames.length > 0) {
                const res = await fetch(`/api/overlay/counter-state?names=${encodeURIComponent(genericNames.join(","))}`);
                if (res.ok) {
                    const data = await res.json();
                    if (Array.isArray(data)) {
                        data.forEach(c => {
                            const cName = c.name || c.Name;
                            const cVal = c.amount ?? c.Amount ?? 0;
                            updateValue("generic", cName, cVal);
                        });
                    }
                }
            }

            if (hasDeath) {
                const res = await fetch("/api/overlay/death-counter-state");
                if (res.ok) {
                    const data = await res.json();
                    if (data) {
                        const count = data.count ?? data.amount ?? data.Count ?? data.Amount ?? 0;
                        updateValue("death", "", count);
                    }
                }
            }
        } catch (err) {
            console.error("Failed to load initial counter state:", err);
        }
    }

    function initSignalR() {
        if (typeof signalR === "undefined") {
            setStatus("SignalR not loaded", false);
            return;
        }

        setStatus("Connecting...", false);

        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/mainHub")
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .build();

        connection.on("CounterUpdated", counter => {
            if (counter) {
                const cName = counter.counterName || counter.CounterName;
                const cAmount = counter.amount ?? counter.Amount ?? 0;
                if (cName) {
                    updateValue("generic", cName, cAmount);
                }
            }
        });

        connection.on("DeathCounterUpdated", data => {
            if (data) {
                const count = data.count ?? data.amount ?? data.Count ?? data.Amount;
                if (typeof count !== "undefined") {
                    updateValue("death", "", count);
                }
            }
        });

        connection.onreconnecting(() => {
            setStatus("Reconnecting...", false);
        });

        connection.onreconnected(() => {
            setStatus("Connected", true);
            fetchInitialState();
        });

        connection.onclose(() => {
            setStatus("Disconnected", false);
        });

        connection.start()
            .then(() => {
                setStatus("Connected", true);
                fetchInitialState();
            })
            .catch(err => {
                console.error("SignalR Connection Error:", err);
                setStatus("Connection Error", false);
            });
    }

    applyStyles();
    renderSkeleton();
    await fetchInitialState();
    initSignalR();
})();
