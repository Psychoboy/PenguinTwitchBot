/* global TwitchExtApi */
/* eslint-disable xss/no-mixed-html, no-unsanitized/property */
(function () {
    'use strict';

    var TAB_DEFS = {
        giveaway: { id: 'giveaway', label: 'Giveaway', icon: '', featureKey: 'giveaway' },
        fishing: { id: 'fishing', label: 'Fishing', icon: '', featureKey: 'fishing' },
        leaderboards: { id: 'leaderboards', label: 'Rankings', icon: '', featureKey: 'leaderboards' },
        starship: { id: 'starship', label: 'Starship', icon: '', featureKey: 'starship' },
        commands: { id: 'commands', label: 'Commands', icon: '', featureKey: 'commands' }
    };

    var state = {
        activeTab: null,
        fishingSubTab: 'catches',
        lbType: 'points',
        lbPointTypes: [],
        selectedPointTypeId: null,
        cmdSearchText: '',
        cmdCategory: 'all',
        categoriesLoaded: false,
        shopSearchText: '',
        shopCategory: 'all',
        fishingStoreItems: [],
        fishingViewerData: null,
        botFeatures: null,
        config: null,
        refreshTimer: null,
        searchDebounceTimer: null,
        shopSearchDebounceTimer: null
    };

    function getDurabilityPct(item) {
        if (!item || item.currentDurability == null) return null;
        var pct;
        if (item.maxDurability != null && item.maxDurability > 0) {
            pct = Math.round((item.currentDurability / item.maxDurability) * 100);
        } else if (item.currentDurability <= 1.0) {
            pct = Math.round(item.currentDurability * 100);
        } else {
            pct = Math.round(item.currentDurability);
        }
        return Math.max(0, Math.min(100, pct));
    }

    function showAlert(type, message, durationMs) {
        var el = document.getElementById('alert-banner');
        if (!el) return;
        el.className = 'banner show banner-' + type;
        el.textContent = message;
        setTimeout(function () {
            el.className = 'banner';
        }, durationMs || 4000);
    }

    function isValidHttpsUrl(urlString) {
        if (!urlString || typeof urlString !== 'string') return false;
        var trimmed = urlString.trim();
        try {
            var parsed = new URL(trimmed);
            return parsed.protocol === 'https:';
        } catch (_) {
            return false;
        }
    }

    function resolveImageUrl(url) {
        if (!url || typeof url !== 'string') return '';
        var trimmed = url.trim();
        if (!trimmed) return '';
        if (/^https:\/\//i.test(trimmed)) {
            return trimmed;
        }
        if (trimmed.indexOf('//') === 0) {
            return 'https:' + trimmed;
        }
        if (/^http:\/\//i.test(trimmed)) {
            try {
                var parsed = new URL(trimmed);
                parsed.protocol = 'https:';
                return parsed.toString();
            } catch (err) {
                console.debug('[TwitchExt] Error parsing http image URL:', err);
                return trimmed.replace(/^http:\/\//i, 'https://');
            }
        }
        var baseUrl = TwitchExtApi.getBaseUrl();
        if (baseUrl) {
            if (trimmed.charAt(0) !== '/') {
                trimmed = '/' + trimmed;
            }
            return baseUrl + trimmed;
        }
        return trimmed;
    }

    function formatNumber(val) {
        if (!Number.isFinite(val)) return '0';
        return val.toLocaleString();
    }

    function copyToClipboard(text, btnEl) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(function () {
                var old = btnEl.textContent;
                btnEl.textContent = 'Copied!';
                setTimeout(function () { btnEl.textContent = old; }, 1500);
            }).catch(function () {
                fallbackCopy(text, btnEl);
            });
        } else {
            fallbackCopy(text, btnEl);
        }
    }

    function fallbackCopy(text, btnEl) {
        var input = document.createElement('textarea');
        input.value = text;
        document.body.appendChild(input);
        input.select();
        try {
            document.execCommand('copy');
            var old = btnEl.textContent;
            btnEl.textContent = 'Copied!';
            setTimeout(function () { btnEl.textContent = old; }, 1500);
        } catch (_) { /* ignore clipboard errors */ }
        document.body.removeChild(input);
    }

    // --- Dynamic Navigation & Gating ---
    function renderNavigationTabs() {
        var container = document.getElementById('main-nav-tabs');
        if (!container) return;
        container.innerHTML = '';

        var order = (state.config && state.config.tabOrder) || ['giveaway', 'fishing', 'leaderboards', 'starship', 'commands'];
        var visibleTabs = [];

        order.forEach(function (tabKey) {
            var def = Object.prototype.hasOwnProperty.call(TAB_DEFS, tabKey) ? TAB_DEFS[tabKey] : null;
            if (!def) return;

            // Dual-layer gate:
            // 1. Bot feature enabled?
            var botEnabled = !state.botFeatures || !Object.prototype.hasOwnProperty.call(state.botFeatures, def.featureKey) || state.botFeatures[def.featureKey] !== false;
            // 2. Streamer config enabled?
            var configEnabled = !state.config || !state.config.enabledTabs || !Object.prototype.hasOwnProperty.call(state.config.enabledTabs, tabKey) || state.config.enabledTabs[tabKey] !== false;

            if (botEnabled && configEnabled) {
                visibleTabs.push(def);
            }
        });

        if (visibleTabs.length === 0) {
            container.innerHTML = '<div class="empty-state">No tabs currently enabled.</div>';
            return;
        }

        visibleTabs.forEach(function (tab) {
            var btn = document.createElement('button');
            btn.className = 'nav-tab-btn' + (state.activeTab === tab.id ? ' active' : '');
            btn.setAttribute('data-tab', tab.id);
            btn.innerHTML = tab.label;
            btn.addEventListener('click', function () {
                switchTab(tab.id);
            });
            container.appendChild(btn);
        });

        // If active tab is not visible, select the first visible tab
        var isActiveVisible = visibleTabs.some(function (t) { return t.id === state.activeTab; });
        if (!isActiveVisible && visibleTabs.length > 0) {
            state.activeTab = visibleTabs[0].id;
        }

        // Synchronize tab buttons and panel visibility without re-triggering active tab content load
        var buttons = container.querySelectorAll('.nav-tab-btn');
        buttons.forEach(function (btn) {
            if (btn.getAttribute('data-tab') === state.activeTab) {
                btn.classList.add('active');
            } else {
                btn.classList.remove('active');
            }
        });

        ['giveaway', 'fishing', 'leaderboards', 'starship', 'commands'].forEach(function (id) {
            var panel = document.getElementById('panel-' + id);
            if (panel) {
                var isActive = (id === state.activeTab);
                panel.style.display = isActive ? 'flex' : 'none';
                if (isActive) {
                    panel.classList.add('active');
                } else {
                    panel.classList.remove('active');
                }
            }
        });
    }

    function switchTab(tabId) {
        state.activeTab = tabId;

        // Update nav button active states
        var buttons = document.querySelectorAll('.nav-tab-btn');
        buttons.forEach(function (btn) {
            if (btn.getAttribute('data-tab') === tabId) {
                btn.classList.add('active');
            } else {
                btn.classList.remove('active');
            }
        });

        // Hide all panel elements, show active as flex
        ['giveaway', 'fishing', 'leaderboards', 'starship', 'commands'].forEach(function (id) {
            var panel = document.getElementById('panel-' + id);
            if (panel) {
                var isActive = (id === tabId);
                panel.style.display = isActive ? 'flex' : 'none';
                if (isActive) {
                    panel.classList.add('active');
                } else {
                    panel.classList.remove('active');
                }
            }
        });

        loadActiveTabContent();
        resetRefreshTimer();
    }

    function getActiveScrollContainer() {
        switch (state.activeTab) {
            case 'giveaway':
                return document.getElementById('giveaway-content');
            case 'fishing':
                return document.getElementById('fishing-sub-content');
            case 'leaderboards':
                return document.getElementById('leaderboard-content');
            case 'starship':
                return document.getElementById('starship-content');
            case 'commands':
                return document.getElementById('commands-list-content');
            default:
                return null;
        }
    }

    function showUnconfiguredState() {
        var unconfiguredHtml = '<div class="empty-state" style="padding: 32px 16px; text-align: center;">' +
            '<div style="font-size: 28px; margin-bottom: 10px;">⚙️</div>' +
            '<div style="font-weight: 700; font-size: 13px; margin-bottom: 6px; color: var(--text-primary);">Extension Setup Required</div>' +
            '<p style="color: var(--text-secondary); font-size: 11px; line-height: 1.5; margin: 0 auto; max-width: 240px;">Broadcaster: Please configure your Bot API Base URL (HTTPS) in the Twitch Extension Configuration settings to enable panel features.</p>' +
            '</div>';
        ['giveaway-content', 'fishing-sub-content', 'leaderboard-content', 'starship-content', 'commands-list-content'].forEach(function (id) {
            var el = document.getElementById(id);
            if (el) el.innerHTML = unconfiguredHtml;
        });
        renderNavigationTabs();
    }

    function loadActiveTabContent(isBackgroundRefresh) {
        var cfg = TwitchExtApi.getConfig();
        if (!cfg || !cfg.botBaseUrl || !isValidHttpsUrl(cfg.botBaseUrl)) {
            showUnconfiguredState();
            return;
        }

        if (isBackgroundRefresh) {
            var activeEl = document.activeElement;
            if (activeEl && (activeEl.tagName === 'INPUT' || activeEl.tagName === 'SELECT' || activeEl.tagName === 'TEXTAREA')) {
                // User is actively interacting with an input/filter; avoid disturbing input focus or scrolling
                return;
            }
        }

        var scrollContainer = getActiveScrollContainer();
        var savedScrollTop = scrollContainer ? scrollContainer.scrollTop : 0;

        var promise;
        switch (state.activeTab) {
            case 'giveaway':
                promise = loadGiveaway(isBackgroundRefresh);
                break;
            case 'fishing':
                promise = loadFishingContent(isBackgroundRefresh);
                break;
            case 'leaderboards':
                promise = loadLeaderboards(isBackgroundRefresh);
                break;
            case 'starship':
                promise = loadStarshipContent(isBackgroundRefresh);
                break;
            case 'commands':
                promise = loadCommands(isBackgroundRefresh);
                break;
        }

        if (promise && typeof promise.then === 'function') {
            promise.then(function () {
                if (scrollContainer && isBackgroundRefresh && savedScrollTop > 0) {
                    scrollContainer.scrollTop = savedScrollTop;
                }
            }).catch(function (err) {
                console.debug('[TwitchExt] Background tab load error:', err);
            });
        }
    }

    // --- 1. Giveaway View ---
    async function loadGiveaway(isBackgroundRefresh) {
        var container = document.getElementById('giveaway-content');
        if (!container) return;
        var savedScrollTop = isBackgroundRefresh ? container.scrollTop : 0;

        try {
            var giveawayData = await TwitchExtApi.getGiveaway();
            var viewerData = null;

            if (TwitchExtApi.isIdentityShared()) {
                try {
                    viewerData = await TwitchExtApi.getGiveawayViewer();
                    updateHeaderUser(viewerData.username);
                } catch (e) {
                    console.debug('Viewer balance unavailable:', e);
                }
            }

            renderGiveaway(container, giveawayData, viewerData);
            if (isBackgroundRefresh && savedScrollTop > 0) {
                container.scrollTop = savedScrollTop;
            }
        } catch (err) {
            if (!isBackgroundRefresh) {
                container.innerHTML = '<div class="empty-state">Unable to load giveaway information.</div>';
            }
        }
    }

    function buildGiveawayHtml(g, v) {
        var html = '<div class="card giveaway-card">';

        var resolvedImageUrl = resolveImageUrl(g.imageUrl);
        if (resolvedImageUrl) {
            html += '<div class="giveaway-image-wrapper"><img src="' + escapeHtml(resolvedImageUrl) + '" class="giveaway-image" alt="Prize" onerror="this.parentElement.style.display=\'none\';" /></div>';
        }

        html += '<div class="prize-title">' + escapeHtml(g.prize || 'No Active Giveaway') + '</div>';

        if (g.isClosed) {
            html += '<span class="badge-status badge-closed">Closed</span>';
        } else {
            html += '<span class="badge-status badge-open">Active / Open</span>';
        }

        html += '<div class="giveaway-stats-grid">' +
            '<div class="stat-box"><div class="stat-val">' + formatNumber(g.pointsPerEntry) + '</div><div class="stat-lbl">Pts/Ticket</div></div>' +
            '<div class="stat-box"><div class="stat-val">' + formatNumber(g.entrantsCount) + '</div><div class="stat-lbl">Entrants</div></div>' +
            '<div class="stat-box"><div class="stat-val">' + formatNumber(g.entriesCount) + '</div><div class="stat-lbl">Entries</div></div>' +
            '</div>';

        html += '<div class="giveaway-disclaimer">' +
            'This is a promotion by the channel broadcaster. Twitch does not sponsor or endorse this promotion and is not responsible for it.' +
            '</div>';
        html += '</div>';

        if (!g.isClosed) {
            if (!TwitchExtApi.isIdentityShared()) {
                html += '<div class="identity-box">' +
                    '<p>Share your Twitch ID to view your tickets and enter this giveaway!</p>' +
                    '<button id="btn-giveaway-link" class="btn-identity">Link Twitch Account</button>' +
                    '</div>';
            } else if (v) {
                html += '<div class="entry-form">' +
                    '<div class="card-header">' +
                    '<span class="card-title">Enter Tickets</span>' +
                    '<span class="card-subtitle">Tickets: ' + formatNumber(v.userTickets) + ' | Entered: ' + formatNumber(v.userEntries) + '</span>' +
                    '</div>' +
                    '<div class="input-group">' +
                    '<input type="number" id="giveaway-amount-input" class="input-number" min="1" max="' + Math.max(1, v.maxAffordableEntries) + '" value="1" />' +
                    '</div>' +
                    '<div class="quick-chips">' +
                    '<button class="quick-chip" data-pct="1">+1</button>' +
                    '<button class="quick-chip" data-pct="0.25">25%</button>' +
                    '<button class="quick-chip" data-pct="0.5">50%</button>' +
                    '<button class="quick-chip" data-pct="1.0">Max</button>' +
                    '</div>' +
                    '<button id="btn-enter-giveaway" class="btn-primary" ' + (v.maxAffordableEntries < 1 ? 'disabled' : '') + '>' +
                    (v.maxAffordableEntries < 1 ? 'Insufficient Tickets' : 'Enter Giveaway') +
                    '</button>' +
                    '</div>';
            }
        }
        return html;
    }

    function wireGiveawayListeners(container, g, v) {
        var linkBtn = document.getElementById('btn-giveaway-link');
        if (linkBtn) {
            linkBtn.addEventListener('click', function () {
                TwitchExtApi.requestIdentityShare();
            });
        }

        if (!v || g.isClosed) return;

        var amountInput = document.getElementById('giveaway-amount-input');
        var chips = container.querySelectorAll('.quick-chip');
        chips.forEach(function (chip) {
            chip.addEventListener('click', function () {
                var pct = parseFloat(chip.getAttribute('data-pct'));
                if (pct === 1 && chip.textContent === '+1') {
                    amountInput.value = Math.min(parseInt(amountInput.value || 0, 10) + 1, v.maxAffordableEntries);
                } else {
                    var calculated = Math.floor(v.maxAffordableEntries * pct);
                    amountInput.value = Math.max(1, Math.min(calculated, v.maxAffordableEntries));
                }
            });
        });

        var enterBtn = document.getElementById('btn-enter-giveaway');
        if (enterBtn) {
            enterBtn.addEventListener('click', async function () {
                var amount = parseInt(amountInput.value, 10);
                if (!amount || amount < 1) return;
                enterBtn.disabled = true;
                enterBtn.textContent = 'Entering...';
                try {
                    var res = await TwitchExtApi.enterGiveaway(amount);
                    showAlert('success', res.message || 'Entered successfully!');
                    loadGiveaway();
                } catch (err) {
                    showAlert('error', err.message || 'Failed to enter giveaway.');
                    enterBtn.disabled = false;
                    enterBtn.textContent = 'Enter Giveaway';
                }
            });
        }
    }

    function renderGiveaway(container, g, v) {
        container.innerHTML = buildGiveawayHtml(g, v);
        wireGiveawayListeners(container, g, v);
    }

    // --- 2. Fishing View ---
    function loadFishingContent(isBackgroundRefresh) {
        switch (state.fishingSubTab) {
            case 'catches':
                loadRecentCatches(isBackgroundRefresh);
                break;
            case 'inventory':
                loadFishingInventory(isBackgroundRefresh);
                break;
            case 'store':
                loadFishingStore(isBackgroundRefresh);
                break;
        }
    }

    async function loadRecentCatches(isBackgroundRefresh) {
        var container = document.getElementById('fishing-sub-content');
        if (!container) return;
        var savedScrollTop = isBackgroundRefresh ? container.scrollTop : 0;
        if (!isBackgroundRefresh) {
            container.innerHTML = '<div class="loading-spinner">Loading catches...</div>';
        }

        try {
            var count = (state.config && state.config.recentCatchesCount) || 15;
            var catches = await TwitchExtApi.getRecentCatches(count);

            if (!Array.isArray(catches) || catches.length === 0) {
                container.innerHTML = '<div class="empty-state">No recent fish catches yet.</div>';
                return;
            }

            var html = '<div class="card" style="padding:0;">';
            catches.forEach(function (c) {
                var weightStr = Number(c.weight).toFixed(2);
                html += '<div class="catch-row">' +
                    '<span class="catch-user" title="' + escapeHtml(c.username) + '">' + escapeHtml(c.username) + '</span>' +
                    '<span class="catch-fish">' + escapeHtml(c.fishName) + '</span>' +
                    '<span class="catch-weight">' + weightStr + ' kg</span>' +
                    '</div>';
            });
            html += '</div>';
            container.innerHTML = html;

            if (isBackgroundRefresh && savedScrollTop > 0) {
                container.scrollTop = savedScrollTop;
            }

            if (TwitchExtApi.isIdentityShared()) {
                TwitchExtApi.getFishingViewer().then(function (viewer) {
                    if (viewer) {
                        updateHeaderUser(viewer.username);
                        updateFishingGold(viewer.totalGold);
                    }
                }).catch(function (err) {
                    console.debug('[TwitchExt] Background fishing viewer error:', err);
                });
            } else {
                updateFishingGold(null);
            }
        } catch (err) {
            if (!isBackgroundRefresh) {
                container.innerHTML = '<div class="empty-state">Unable to load recent catches.</div>';
            }
        }
    }

    async function loadFishingInventory(isBackgroundRefresh) {
        var container = document.getElementById('fishing-sub-content');
        if (!container) return;
        var savedScrollTop = isBackgroundRefresh ? container.scrollTop : 0;

        if (!TwitchExtApi.isIdentityShared()) {
            container.innerHTML = '<div class="identity-box">' +
                '<p>Share your Twitch ID to view and manage your equipped rods, lures, and fishing gear.</p>' +
                '<button id="btn-inventory-link" class="btn-identity">Link Twitch Account</button>' +
                '</div>';
            var linkBtn = document.getElementById('btn-inventory-link');
            if (linkBtn) linkBtn.addEventListener('click', TwitchExtApi.requestIdentityShare);
            updateFishingGold(null);
            return;
        }

        if (!isBackgroundRefresh) {
            container.innerHTML = '<div class="loading-spinner">Loading inventory...</div>';
        }

        try {
            var viewer = await TwitchExtApi.getFishingViewer();
            updateHeaderUser(viewer.username);
            updateFishingGold(viewer.totalGold);

            if (!viewer.items || viewer.items.length === 0) {
                container.innerHTML = '<div class="empty-state">You do not own any fishing equipment yet. Visit the Fish Shop to purchase items!</div>';
                return;
            }

            // Equipped items show first
            viewer.items.sort(function (a, b) {
                var aEq = a.isEquipped ? 1 : 0;
                var bEq = b.isEquipped ? 1 : 0;
                if (aEq !== bEq) return bEq - aEq;
                return (a.name || '').localeCompare(b.name || '');
            });

            var html = '';
            viewer.items.forEach(function (item) {
                var isEquipped = item.isEquipped;
                var durabilityPct = getDurabilityPct(item);
                if (item.isBroken) {
                    durabilityPct = 0;
                }

                html += '<div class="item-card">' +
                    '<div class="item-header">' +
                    '<div>' +
                    '<div class="item-name">' + escapeHtml(item.name) + (isEquipped ? ' <span style="color:var(--success); font-size:10px;">(Equipped)</span>' : '') + '</div>' +
                    '<div class="item-desc">' + escapeHtml(item.description) + '</div>' +
                    '</div>' +
                    (item.equipmentSlot ? '<span class="item-gear-type">' + escapeHtml(item.equipmentSlot) + '</span>' : '') +
                    '</div>';

                if (item.boostSummary) {
                    html += '<div style="font-size:10px; color:var(--primary-light);">' + escapeHtml(item.boostSummary) + '</div>';
                }

                html += '<div class="item-stats">';
                if (durabilityPct != null) {
                    var durLabel = item.isBroken ? 'Broken (0%)' : (durabilityPct + '%');
                    html += '<span>Durability: ' + durLabel + '</span>' +
                        '<div class="durability-bar"><div class="durability-fill ' + ((durabilityPct < 25 || item.isBroken) ? 'durability-low' : '') + '" style="width:' + durabilityPct + '%;"></div></div>';
                }
                if (item.remainingUses >= 0) {
                    html += '<span>Uses: ' + item.remainingUses + '</span>';
                }
                html += '</div>';

                // Action buttons: Equip / Unequip / Repair
                var hasActions = item.equipmentSlot || item.canRepair;
                if (hasActions) {
                    html += '<div class="item-actions">';
                    if (item.equipmentSlot) {
                        if (isEquipped) {
                            html += '<button class="btn-sm btn-unequip btn-item-action" data-action="unequip" data-id="' + item.id + '">Unequip</button>';
                        } else {
                            html += '<button class="btn-sm btn-equip btn-item-action" data-action="equip" data-id="' + item.id + '">Equip</button>';
                        }
                    }
                    if (item.canRepair && item.repairCost != null) {
                        var notEnoughGold = (viewer.totalGold < item.repairCost);
                        html += '<button class="btn-sm btn-repair btn-item-action" data-action="repair" data-id="' + item.id + '"' +
                            (notEnoughGold ? ' disabled title="Not enough gold to repair"' : '') +
                            '>Repair (' + formatNumber(item.repairCost) + 'g)</button>';
                    }
                    html += '</div>';
                }

                html += '</div>';
            });

            container.innerHTML = html;
            if (isBackgroundRefresh && savedScrollTop > 0) {
                container.scrollTop = savedScrollTop;
            }

            container.querySelectorAll('.btn-item-action').forEach(function (btn) {
                btn.addEventListener('click', async function () {
                    var action = btn.getAttribute('data-action');
                    var id = parseInt(btn.getAttribute('data-id'), 10);
                    btn.disabled = true;
                    try {
                        if (action === 'equip') {
                            await TwitchExtApi.equipFishingItem(id);
                            showAlert('success', 'Equipment updated!');
                        } else if (action === 'unequip') {
                            await TwitchExtApi.unequipFishingItem(id);
                            showAlert('success', 'Item unequipped.');
                        } else if (action === 'repair') {
                            var repairRes = await TwitchExtApi.repairFishingItem(id);
                            var paidMsg = (repairRes && repairRes.goldPaid != null) ? (' for ' + formatNumber(repairRes.goldPaid) + ' gold') : '';
                            showAlert('success', 'Item repaired' + paidMsg + '!');
                        }
                        loadFishingInventory();
                    } catch (err) {
                        showAlert('error', err.message || 'Action failed.');
                        btn.disabled = false;
                    }
                });
            });

        } catch (err) {
            if (!isBackgroundRefresh) {
                container.innerHTML = '<div class="empty-state">Unable to load fishing inventory.</div>';
            }
        }
    }

    async function loadFishingStore(isBackgroundRefresh) {
        var container = document.getElementById('fishing-sub-content');
        if (!container) return;
        var savedScrollTop = isBackgroundRefresh ? container.scrollTop : 0;

        if (!isBackgroundRefresh && (!state.fishingStoreItems || state.fishingStoreItems.length === 0)) {
            container.innerHTML = '<div class="loading-spinner">Loading shop items...</div>';
        }

        try {
            var items = await TwitchExtApi.getFishingStore();
            state.fishingStoreItems = items || [];

            var isAuth = TwitchExtApi.isIdentityShared();
            var viewerData = null;

            if (isAuth) {
                try {
                    viewerData = await TwitchExtApi.getFishingViewer();
                    updateHeaderUser(viewerData.username);
                    updateFishingGold(viewerData.totalGold);
                } catch (err) {
                    console.debug('[TwitchExt] Fishing viewer data unavailable:', err);
                }
            } else {
                updateFishingGold(null);
            }
            state.fishingViewerData = viewerData;

            if (!items || items.length === 0) {
                container.innerHTML = '<div class="empty-state">Shop currently has no items for sale.</div>';
                return;
            }

            var searchInput = document.getElementById('shop-search-input');
            var categorySelect = document.getElementById('shop-category-select');

            if (!searchInput || !categorySelect) {
                var shellHtml = '';
                if (!isAuth) {
                    shellHtml += '<div class="identity-box" style="margin-bottom: 8px;">' +
                        '<p>Share your Twitch ID to purchase items from the shop.</p>' +
                        '<button id="btn-shop-link" class="btn-identity">Link Twitch Account</button>' +
                        '</div>';
                }

                shellHtml += '<div class="shop-filter-bar">' +
                    '<input type="text" id="shop-search-input" class="search-input" placeholder="Search shop..." value="' + escapeHtml(state.shopSearchText) + '" />' +
                    '<select id="shop-category-select" class="category-select">' +
                    '<option value="all">All</option>' +
                    '</select>' +
                    '</div>' +
                    '<div id="shop-items-list"></div>';

                container.innerHTML = shellHtml;

                var linkBtn = document.getElementById('btn-shop-link');
                if (linkBtn) {
                    linkBtn.addEventListener('click', function () {
                        TwitchExtApi.requestIdentityShare();
                    });
                }

                searchInput = document.getElementById('shop-search-input');
                categorySelect = document.getElementById('shop-category-select');

                if (searchInput) {
                    searchInput.addEventListener('input', function () {
                        clearTimeout(state.shopSearchDebounceTimer);
                        state.shopSearchDebounceTimer = setTimeout(function () {
                            state.shopSearchText = searchInput.value;
                            renderStoreItemsList();
                        }, 250);
                    });
                }

                if (categorySelect) {
                    categorySelect.addEventListener('change', function () {
                        state.shopCategory = categorySelect.value;
                        renderStoreItemsList();
                    });
                }
            }

            if (categorySelect) {
                var slots = [];
                items.forEach(function (it) {
                    var s = it.equipmentSlot ? it.equipmentSlot.trim() : '';
                    if (s && slots.indexOf(s) === -1) slots.push(s);
                });
                slots.sort();

                var currentVal = state.shopCategory || 'all';
                categorySelect.innerHTML = '<option value="all">All</option>';
                slots.forEach(function (slot) {
                    var opt = document.createElement('option');
                    opt.value = slot;
                    opt.textContent = slot;
                    if (currentVal.toLowerCase() === slot.toLowerCase()) {
                        opt.selected = true;
                    }
                    categorySelect.appendChild(opt);
                });
            }

            renderStoreItemsList();

            if (isBackgroundRefresh && savedScrollTop > 0) {
                container.scrollTop = savedScrollTop;
            }

        } catch (err) {
            if (!isBackgroundRefresh) {
                container.innerHTML = '<div class="empty-state">Unable to load shop items.</div>';
            }
        }
    }

    function renderStoreItemsList() {
        var listEl = document.getElementById('shop-items-list');
        if (!listEl) return;

        var items = state.fishingStoreItems || [];
        var search = (state.shopSearchText || '').trim().toLowerCase();
        var cat = (state.shopCategory || 'all').toLowerCase();

        var filtered = items.filter(function (it) {
            if (cat !== 'all') {
                var itCat = (it.equipmentSlot || '').toLowerCase();
                if (itCat !== cat) return false;
            }
            if (search) {
                var nameMatch = it.name && it.name.toLowerCase().indexOf(search) !== -1;
                var descMatch = it.description && it.description.toLowerCase().indexOf(search) !== -1;
                var slotMatch = it.equipmentSlot && it.equipmentSlot.toLowerCase().indexOf(search) !== -1;
                var fishMatch = it.targetFishName && it.targetFishName.toLowerCase().indexOf(search) !== -1;
                if (!nameMatch && !descMatch && !slotMatch && !fishMatch) return false;
            }
            return true;
        });

        if (filtered.length === 0) {
            listEl.innerHTML = '<div class="empty-state">No shop items match your filter.</div>';
            return;
        }

        var isAuth = TwitchExtApi.isIdentityShared();
        var viewerData = state.fishingViewerData;

        var html = '';
        filtered.forEach(function (item) {
            var isDisabled = false;
            var buttonTitle = '';

            if (!isAuth) {
                isDisabled = true;
                buttonTitle = 'Please link your Twitch account to buy items';
            } else if (viewerData && viewerData.totalGold < item.cost) {
                isDisabled = true;
                buttonTitle = 'Not enough gold (costs ' + formatNumber(item.cost) + ' Gold)';
            }

            var disabledAttr = isDisabled ? ' disabled' : '';
            var titleAttr = buttonTitle ? ' title="' + escapeHtml(buttonTitle) + '"' : '';

            var fullDesc = item.description || item.name;
            if (item.boostType && item.boostAmount) {
                fullDesc += ' • ' + item.boostType + ': +' + item.boostAmount + '%';
            }

            var metaBadgesHtml = '';
            if (item.boostType && item.boostAmount) {
                metaBadgesHtml += '<span class="badge-boost" title="' + escapeHtml(item.boostType + ': +' + item.boostAmount + '%') + '">' + escapeHtml(item.boostType) + '</span>';
            }
            if (item.maxDurability) {
                metaBadgesHtml += '<span title="Max Durability: ' + item.maxDurability + '">' + item.maxDurability + ' dur</span>';
            } else if (item.maxUses) {
                metaBadgesHtml += '<span title="Max Uses: ' + item.maxUses + '">' + item.maxUses + ' uses</span>';
            }

            html += '<div class="shop-card-compact">' +
                '<div class="shop-card-top">' +
                '<div class="shop-card-name-group">' +
                (item.equipmentSlot ? '<span class="item-gear-type">' + escapeHtml(item.equipmentSlot) + '</span>' : '') +
                '<span class="shop-card-name" title="' + escapeHtml(item.name) + '">' + escapeHtml(item.name) + '</span>' +
                '</div>' +
                '<div class="shop-card-actions">' +
                '<span class="shop-card-cost">🪙 ' + formatNumber(item.cost) + '</span>' +
                '<button class="btn-sm btn-buy btn-buy-store" data-id="' + item.id + '" data-name="' + escapeHtml(item.name) + '"' + disabledAttr + titleAttr + '>Buy</button>' +
                '</div>' +
                '</div>' +
                '<div class="shop-card-bottom">' +
                '<span class="shop-card-desc" title="' + escapeHtml(fullDesc) + '">' + escapeHtml(item.description || 'No description') + '</span>' +
                '<div class="shop-card-meta">' + metaBadgesHtml + '</div>' +
                '</div>' +
                '</div>';
        });

        listEl.innerHTML = html;

        listEl.querySelectorAll('.btn-buy-store').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                if (!TwitchExtApi.isIdentityShared()) {
                    TwitchExtApi.requestIdentityShare();
                    return;
                }
                var id = parseInt(btn.getAttribute('data-id'), 10);
                var name = btn.getAttribute('data-name');
                btn.disabled = true;
                try {
                    await TwitchExtApi.buyFishingItem(id, 1);
                    showAlert('success', 'Purchased ' + name + '!');
                    loadFishingContent();
                } catch (err) {
                    showAlert('error', err.message || 'Failed to purchase item.');
                    btn.disabled = false;
                }
            });
        });
    }

    // --- 3. Leaderboards View ---
    async function loadLeaderboards(isBackgroundRefresh) {
        var container = document.getElementById('leaderboard-content');
        var pointSelectorWrapper = document.getElementById('lb-point-type-selector-wrapper');
        if (!container) return;
        var savedScrollTop = isBackgroundRefresh ? container.scrollTop : 0;

        if (state.lbType === 'points') {
            if (pointSelectorWrapper) pointSelectorWrapper.style.display = 'block';
            await loadPointTypesSelector();
        } else {
            if (pointSelectorWrapper) pointSelectorWrapper.style.display = 'none';
        }

        if (!isBackgroundRefresh) {
            container.innerHTML = '<div class="loading-spinner">Loading standings...</div>';
        }

        try {
            var top = (state.config && state.config.leaderboardTopCount) || 10;

            if (state.lbType === 'tournaments') {
                var tournaments = await TwitchExtApi.getFishingTournaments(top);
                renderTournaments(container, tournaments);
            } else {
                var res = await TwitchExtApi.getLeaderboards(state.lbType, state.selectedPointTypeId, top);
                renderRankingsTable(container, res);
            }

            if (isBackgroundRefresh && savedScrollTop > 0) {
                container.scrollTop = savedScrollTop;
            }
        } catch (err) {
            if (!isBackgroundRefresh) {
                container.innerHTML = '<div class="empty-state">Unable to load standings.</div>';
            }
        }
    }

    async function loadPointTypesSelector() {
        var select = document.getElementById('lb-point-type-select');
        if (!select || select.options.length > 0) return;

        try {
            var pts = await TwitchExtApi.getLeaderboardMeta();
            var filteredPts = (pts || []).filter(function (pt) {
                return !isProhibitedCategory(pt.name);
            });
            state.lbPointTypes = filteredPts;
            select.innerHTML = '';
            filteredPts.forEach(function (pt) {
                var opt = document.createElement('option');
                opt.value = pt.id;
                opt.textContent = pt.name;
                select.appendChild(opt);
            });
            if (filteredPts.length > 0) {
                state.selectedPointTypeId = filteredPts[0].id;
            }
        } catch (err) {
            console.debug('[TwitchExt] Point types load error:', err);
        }
    }

    function renderRankingsTable(container, data) {
        if (!data || !data.entries || data.entries.length === 0) {
            container.innerHTML = '<div class="empty-state">No rankings available yet.</div>';
            return;
        }

        var html = '<div class="card" style="padding:0;">' +
            '<table class="table-compact">' +
            '<thead><tr><th>#</th><th>Player</th><th style="text-align:right;">' + escapeHtml(data.scoreLabel || 'Score') + '</th></tr></thead>' +
            '<tbody>';

        data.entries.forEach(function (e) {
            var rankClass = e.rank <= 3 ? 'rank-' + e.rank : '';
            html += '<tr>' +
                '<td><span class="rank-badge ' + rankClass + '">#' + e.rank + '</span></td>' +
                '<td class="user-col" title="' + escapeHtml(e.username) + '">' + escapeHtml(e.username) + '</td>' +
                '<td class="score-col">' + escapeHtml(e.formattedScore || formatNumber(e.score)) + '</td>' +
                '</tr>';
        });

        html += '</tbody></table></div>';
        container.innerHTML = html;
    }

    function renderTournaments(container, tournaments) {
        if (!tournaments || tournaments.length === 0) {
            container.innerHTML = '<div class="empty-state">No active tournaments right now.</div>';
            return;
        }

        var html = '';
        tournaments.forEach(function (t) {
            html += '<div class="card" style="padding:6px;">' +
                '<div class="card-header">' +
                '<span class="card-title">' + escapeHtml(t.name) + '</span>' +
                '<span class="card-subtitle">' + escapeHtml(t.scoreCategory) + '</span>' +
                '</div>' +
                '<table class="table-compact">' +
                '<thead><tr><th>#</th><th>Player</th><th style="text-align:right;">Score</th></tr></thead>' +
                '<tbody>';

            if (!t.standings || t.standings.length === 0) {
                html += '<tr><td colspan="3" class="empty-state">No catches recorded</td></tr>';
            } else {
                t.standings.forEach(function (s) {
                    var rankClass = s.rank <= 3 ? 'rank-' + s.rank : '';
                    html += '<tr>' +
                        '<td><span class="rank-badge ' + rankClass + '">#' + s.rank + '</span></td>' +
                        '<td class="user-col">' + escapeHtml(s.username) + '</td>' +
                        '<td class="score-col">' + Number(s.score).toFixed(2) + ' (' + s.catchCount + ')</td>' +
                        '</tr>';
                });
            }

            html += '</tbody></table></div>';
        });

        html += '<div style="font-size:10px; color:var(--text-muted); text-align:center; margin-top:8px; padding:4px 8px;">' +
            'Fishing tournaments are broadcast-organized community events. Twitch does not sponsor or endorse channel competitions.' +
            '</div>';

        container.innerHTML = html;
    }

    // --- 4. Starship Expedition View ---
    async function loadStarshipContent(isBackgroundRefresh) {
        var container = document.getElementById('starship-content');
        if (!container) return;
        var savedScrollTop = isBackgroundRefresh ? container.scrollTop : 0;

        if (!isBackgroundRefresh) {
            container.innerHTML = '<div class="loading-spinner">Loading Starship Expedition...</div>';
        }

        try {
            var statePromise = TwitchExtApi.getStarshipState();
            var viewerPromise = TwitchExtApi.isIdentityShared()
                ? TwitchExtApi.getStarshipViewer().catch(function () { return null; })
                : Promise.resolve(null);

            var results = await Promise.all([statePromise, viewerPromise]);
            var res = results[0];
            var viewer = results[1];
            var snapshot = res ? res.snapshot : null;
            renderStarship(container, snapshot, viewer);
            if (isBackgroundRefresh && savedScrollTop > 0) {
                container.scrollTop = savedScrollTop;
            }
        } catch (err) {
            if (!isBackgroundRefresh) {
                container.innerHTML = '<div class="empty-state">Unable to load Starship Expedition data.</div>';
            }
        }
    }

    function renderStarship(container, snapshot, viewer) {
        if (!snapshot || snapshot.status === 'Idle' || snapshot.status === 'Completed') {
            container.innerHTML = '<div class="card starship-card" style="text-align: center; padding: 24px 14px;">' +
                '<div style="font-size: 28px; margin-bottom: 8px;">🚀</div>' +
                '<div class="starship-title" style="justify-content: center; margin-bottom: 6px;">Starship Docked</div>' +
                '<p style="color: var(--text-secondary); font-size: 11.5px; line-height: 1.4;">No starship expedition is currently in progress. Prepare your crew for the next launch!</p>' +
                '</div>';
            return;
        }

        var shipName = (snapshot.ship && snapshot.ship.name) ? snapshot.ship.name : 'SS Expedition';
        var statusBadge = '';
        var statusLower = (snapshot.status || '').toLowerCase();
        if (statusLower === 'boarding') {
            statusBadge = '<span class="badge-status badge-open">🧑‍🚀 Boarding Open</span>';
        } else if (statusLower === 'stationdocked') {
            statusBadge = '<span class="badge-status" style="background:var(--warning-bg);color:var(--warning);border:1px solid var(--warning);">⛽ Station Docked</span>';
        } else if (statusLower === 'emergencyreturn') {
            statusBadge = '<span class="badge-status badge-closed">⚠️ Emergency Return</span>';
        } else {
            statusBadge = '<span class="badge-status badge-open">🚀 ' + escapeHtml(snapshot.status) + '</span>';
        }

        var html = '';

        if (!TwitchExtApi.isIdentityShared()) {
            html += '<div class="identity-box" style="margin-bottom: 10px;">' +
                '<p>Link your Twitch account to board the starship and vote directly from this extension!</p>' +
                '<button id="btn-starship-link" class="btn-identity">Link Twitch Account</button>' +
                '</div>';
        }

        html += '<div class="card starship-card">' +
            '<div class="starship-header">' +
            '<div class="starship-title">🚀 ' + escapeHtml(shipName) + '</div>' +
            statusBadge +
            '</div>';

        html += '<div class="starship-stats-grid">' +
            '<div class="stat-box"><div class="stat-val">' + formatNumber(snapshot.crewCount || 0) + '</div><div class="stat-lbl">Crew</div></div>' +
            '<div class="stat-box"><div class="stat-val">' + (snapshot.currentLeg || 0) + ' / ' + (snapshot.legCount || 0) + '</div><div class="stat-lbl">Progress</div></div>' +
            '</div>';

        if (snapshot.currentSector || snapshot.currentRoute) {
            html += '<div style="font-size: 11px; color: var(--text-secondary); margin-bottom: 8px;">' +
                '<span>Sector: <strong style="color:var(--text-primary);">' + escapeHtml(snapshot.currentSector || 'Deep Space') + '</strong></span>' +
                (snapshot.currentRoute ? ' | <span>Route: <strong style="color:var(--text-primary);">' + escapeHtml(snapshot.currentRoute) + '</strong></span>' : '') +
                '</div>';
        }

        if (snapshot.shipSystems) {
            var hull = snapshot.shipSystems.hull != null ? snapshot.shipSystems.hull : 100;
            var maxHull = snapshot.shipSystems.maximumHull || 100;
            var hullPct = Math.max(0, Math.min(100, Math.round((hull / maxHull) * 100)));
            var supplies = snapshot.shipSystems.supplies != null ? snapshot.shipSystems.supplies : 0;

            html += '<div class="starship-resource-bar">' +
                '<div class="resource-label-row"><span>Hull Integrity</span><span>' + hull + ' / ' + maxHull + ' (' + hullPct + '%)</span></div>' +
                '<div class="resource-track"><div class="resource-fill hull" style="width:' + hullPct + '%;"></div></div>' +
                '</div>' +
                '<div class="starship-resource-bar">' +
                '<div class="resource-label-row"><span>Supplies Remaining</span><span>' + formatNumber(supplies) + '</span></div>' +
                '<div class="resource-track"><div class="resource-fill supplies" style="width:' + Math.min(100, Math.max(5, supplies)) + '%;"></div></div>' +
                '</div>';
        }

        if (statusLower === 'boarding') {
            if (viewer && viewer.hasJoined) {
                html += '<div style="background:rgba(0,230,153,0.1);border:1px solid var(--accent);border-radius:var(--radius-sm);padding:8px;text-align:center;color:var(--accent);font-size:11.5px;font-weight:700;margin-top:8px;">' +
                    '✓ You are aboard the crew!' +
                    '</div>';
            } else {
                html += '<div class="identity-box" style="margin-top: 8px;">' +
                    '<p>🧑‍🚀 Boarding is open! Join the crew to participate:</p>' +
                    '<div style="display: flex; gap: 8px; justify-content: center; align-items: center; margin-top: 6px;">' +
                    (TwitchExtApi.isIdentityShared() ? '<button id="btn-starship-join" class="btn-primary" style="padding: 4px 12px; font-size: 11px;">🧑‍🚀 Board Starship</button>' : '') +
                    '<button class="btn-copy-vote" data-cmd="!sj" title="Shortcut for !starship join">Copy !sj</button>' +
                    '</div>' +
                    '</div>';
            }
        }

        if (snapshot.currentEvent) {
            var ev = snapshot.currentEvent;

            if (viewer && viewer.hasPendingRisk) {
                var maxLoss = viewer.maximumRiskLoss || 0;
                var ptName = viewer.riskPointTypeName || 'points';
                html += '<div class="starship-risk-banner" style="margin-top:8px;">' +
                    '<span>⚠️ <strong>Risk Check:</strong> Choice risks up to ' + formatNumber(maxLoss) + ' ' + escapeHtml(ptName) + '.</span>' +
                    '<button class="btn-confirm-risk" data-choice="' + escapeHtml(viewer.pendingRiskChoiceId || '') + '">Confirm &amp; Vote</button>' +
                    '</div>';
            }

            html += '<div class="starship-event-box">' +
                '<div class="starship-event-title">⚡ Encounter: ' + escapeHtml(ev.title || 'Unknown Event') + '</div>' +
                (ev.sector ? '<div style="font-size:10px; color:var(--text-muted); margin-bottom:4px;">Sector: ' + escapeHtml(ev.sector) + '</div>' : '') +
                '<div class="starship-event-desc">' + escapeHtml(ev.stepNote || ev.description || '') + '</div>';

            if (Array.isArray(ev.choices) && ev.choices.length > 0) {
                var totalVotes = 0;
                var voteCounts = ev.voteCounts || {};
                ev.choices.forEach(function (c) {
                    totalVotes += (voteCounts[c.choiceId] || 0);
                });

                html += '<div class="starship-choices-list">';
                ev.choices.forEach(function (c) {
                    var votes = voteCounts[c.choiceId] || 0;
                    var votePct = totalVotes > 0 ? Math.round((votes / totalVotes) * 100) : 0;
                    var voteCmd = '!sv ' + c.number;
                    var isUserVote = viewer && viewer.hasVoted && viewer.votedChoiceId === c.choiceId;

                    html += '<div class="starship-choice-item">' +
                        '<div class="choice-header">' +
                        '<span class="choice-label">' + c.number + '. ' + escapeHtml(c.label || c.choiceId) + (c.isRisky ? ' <span style="color:var(--danger);font-size:9.5px;">(Risky)</span>' : '') + '</span>' +
                        '<div style="display:flex;align-items:center;gap:6px;">' +
                        (isUserVote ? '<span class="voted-badge">✓ Your Vote</span>' : '') +
                        '<span class="choice-votes">' + votes + ' vote' + (votes === 1 ? '' : 's') + ' (' + votePct + '%)</span>' +
                        '</div>' +
                        '</div>' +
                        '<div class="choice-vote-bar"><div class="choice-vote-fill" style="width:' + votePct + '%;"></div></div>' +
                        (c.hint ? '<div style="font-size:10px;color:var(--text-secondary);margin-bottom:4px;">' + escapeHtml(c.hint) + '</div>' : '') +
                        '<div class="choice-action-row">';

                    if (!viewer || !viewer.hasVoted) {
                        if (TwitchExtApi.isIdentityShared()) {
                            html += '<button class="btn-vote-active btn-cast-vote" data-choice="' + escapeHtml(c.choiceId) + '" data-num="' + c.number + '" data-risky="' + (c.isRisky ? 1 : 0) + '">Vote #' + c.number + '</button>';
                        }
                    }

                    html += '<button class="btn-copy-vote" data-cmd="' + escapeHtml(voteCmd) + '" title="Shortcut for !starship vote ' + c.number + '">Copy ' + escapeHtml(voteCmd) + '</button>' +
                        '</div>' +
                        '</div>';
                });
                html += '</div>';
            }
            html += '</div>';
        } else if (snapshot.latestResult) {
            html += '<div class="starship-event-box">' +
                '<div class="starship-event-title">Recent Event: ' + escapeHtml(snapshot.latestResult.title || '') + '</div>' +
                '<div class="starship-event-desc">' + escapeHtml(snapshot.latestResult.outcomeNarrative || snapshot.latestResult.outcomeResult || '') + '</div>' +
                '</div>';
        }

        html += '</div>';
        container.innerHTML = html;
        wireStarshipListeners(container);
    }

    function wireStarshipListeners(container) {
        var linkBtn = container.querySelector('#btn-starship-link');
        if (linkBtn) {
            linkBtn.addEventListener('click', function () {
                TwitchExtApi.requestIdentityShare();
            });
        }

        var joinBtn = container.querySelector('#btn-starship-join');
        if (joinBtn) {
            joinBtn.addEventListener('click', async function () {
                joinBtn.disabled = true;
                try {
                    var res = await TwitchExtApi.joinStarship();
                    showAlert('success', res.message || 'You have boarded the starship!');
                    await loadStarshipContent(false);
                } catch (err) {
                    showAlert('error', err.message || 'Failed to board starship.');
                    joinBtn.disabled = false;
                }
            });
        }

        container.querySelectorAll('.btn-cast-vote').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                var choiceId = btn.getAttribute('data-choice');
                var num = btn.getAttribute('data-num');

                container.querySelectorAll('.btn-cast-vote').forEach(function (b) { b.disabled = true; });

                try {
                    var res = await TwitchExtApi.voteStarship(choiceId, false);
                    if (res && res.requiresConfirmation) {
                        showAlert('warning', res.message || 'Risky decision: please confirm risk to cast vote!');
                    } else {
                        showAlert('success', res.message || 'Vote for Choice #' + num + ' recorded!');
                    }
                    await loadStarshipContent(false);
                } catch (err) {
                    showAlert('error', err.message || 'Failed to record vote.');
                    container.querySelectorAll('.btn-cast-vote').forEach(function (b) { b.disabled = false; });
                }
            });
        });

        var confirmBtn = container.querySelector('.btn-confirm-risk');
        if (confirmBtn) {
            confirmBtn.addEventListener('click', async function () {
                var choiceId = confirmBtn.getAttribute('data-choice');
                confirmBtn.disabled = true;
                try {
                    var res = await TwitchExtApi.voteStarship(choiceId, true);
                    showAlert('success', res.message || 'Risky vote confirmed and cast!');
                    await loadStarshipContent(false);
                } catch (err) {
                    showAlert('error', err.message || 'Failed to confirm risk vote.');
                    confirmBtn.disabled = false;
                }
            });
        }

        container.querySelectorAll('.btn-copy-vote').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var cmd = btn.getAttribute('data-cmd');
                copyToClipboard(cmd, btn);
            });
        });
    }

    // --- Content Policy Filters (Gambling & Music prohibitions) ---
    function isProhibitedCategory(catName) {
        if (!catName) return false;
        var lower = String(catName).toLowerCase().trim();
        var prohibitedWords = [
            'music', 'musicplayer', 'song', 'songs', 'audio', 'sound', 'soundeffect', 'playlist', 'playlists', 'spotify', 'dj',
            'roulette', 'gamble', 'gambling', 'slot', 'slots', 'bet', 'betting', 'casino', 'jackpot', 'lottery', 'wager', 'heist', 'dice', 'poker', 'blackjack'
        ];
        return prohibitedWords.some(function (w) { return lower === w || lower.indexOf(w) !== -1; });
    }

    function isProhibitedCommand(cmd) {
        if (!cmd) return false;
        var rawName = String(cmd.command || '').toLowerCase().trim();
        var name = rawName.replace(/^[!/#]/, '');
        var cat = String(cmd.category || '').toLowerCase().trim();
        var desc = String(cmd.description || '').toLowerCase();

        if (isProhibitedCategory(cat)) return true;

        var prohibitedNames = [
            'roulette', 'gamble', 'slots', 'slot', 'bet', 'wager', 'casino', 'jackpot', 'lottery', 'heist', 'dice', 'poker', 'blackjack',
            'sr', 'song', 'songs', 'songrequest', 'currentsong', 'lastsong', 'nextsong',
            'skip', 'voteskip', 'veto', 'pause', 'wrongsong', 'wrong', 'priority', 'stealsong', 'importpl', 'loadpl',
            'playlist', 'music', 'volume'
        ];
        for (var i = 0; i < prohibitedNames.length; i++) {
            if (name === prohibitedNames[i] || name.indexOf(prohibitedNames[i]) === 0) return true;
        }

        if (/\b(roulette|gamble|gambling|slots|wager|jackpot|casino|dice|poker|blackjack|place a bet|bet tickets|bet points)\b/i.test(desc)) {
            return true;
        }
        if (/\b(request a song|song request|current song|play song|youtube song|music player|audio track)\b/i.test(desc)) {
            return true;
        }

        return false;
    }

    // --- 5. Commands View ---
    async function loadCommands(isBackgroundRefresh) {
        var container = document.getElementById('commands-list-content');
        if (!container) return;
        var savedScrollTop = isBackgroundRefresh ? container.scrollTop : 0;

        if (!state.categoriesLoaded) {
            try {
                var cats = await TwitchExtApi.getCommandCategories();
                var catSelect = document.getElementById('cmd-category-select');
                if (catSelect) {
                    (cats || []).filter(function (c) {
                        return !isProhibitedCategory(c);
                    }).forEach(function (c) {
                        var opt = document.createElement('option');
                        opt.value = c;
                        opt.textContent = c;
                        catSelect.appendChild(opt);
                    });
                }
                state.categoriesLoaded = true;
            } catch (err) {
                console.debug('[TwitchExt] Command categories load error:', err);
            }
        }

        if (!isBackgroundRefresh) {
            container.innerHTML = '<div class="loading-spinner">Searching commands...</div>';
        }

        try {
            var rawCommands = await TwitchExtApi.getCommands(state.cmdCategory, state.cmdSearchText);
            var commands = (rawCommands || []).filter(function (c) {
                return !isProhibitedCommand(c);
            });

            if (!commands || commands.length === 0) {
                container.innerHTML = '<div class="empty-state">No commands found matching criteria.</div>';
                return;
            }

            var html = '';
            commands.forEach(function (c) {
                html += '<div class="command-card">' +
                    '<div class="cmd-top">' +
                    '<span class="cmd-trigger">' + escapeHtml(c.command) + '</span>' +
                    '<button class="btn-copy" data-cmd="' + escapeHtml(c.command) + '">Copy</button>' +
                    '</div>' +
                    (c.description ? '<div class="cmd-desc">' + escapeHtml(c.description) + '</div>' : '') +
                    '<div class="cmd-meta">' +
                    '<span>Category: ' + escapeHtml(c.category) + '</span>' +
                    (c.rank ? '<span class="cmd-rank">Rank: ' + escapeHtml(c.rank) + '</span>' : '') +
                    (c.userCooldown > 0 ? '<span>CD: ' + c.userCooldown + 's</span>' : '') +
                    (c.cost > 0 ? '<span style="color:var(--warning);">Cost: ' + c.cost + '</span>' : '') +
                    '</div>' +
                    '</div>';
            });

            container.innerHTML = html;

            if (isBackgroundRefresh && savedScrollTop > 0) {
                container.scrollTop = savedScrollTop;
            }

            container.querySelectorAll('.btn-copy').forEach(function (btn) {
                btn.addEventListener('click', function () {
                    var cmd = btn.getAttribute('data-cmd');
                    copyToClipboard(cmd, btn);
                });
            });

        } catch (err) {
            if (!isBackgroundRefresh) {
                container.innerHTML = '<div class="empty-state">Unable to load commands list.</div>';
            }
        }
    }

    function updateHeaderUser(username) {
        var chip = document.getElementById('user-status-chip');
        var nameEl = document.getElementById('user-display-name');

        if (chip && nameEl && username) {
            nameEl.textContent = username;
            chip.style.display = 'flex';
        }
    }

    function updateFishingGold(gold) {
        var bar = document.getElementById('fishing-gold-bar');
        var amount = document.getElementById('fishing-gold-amount');
        if (gold != null && bar && amount) {
            amount.textContent = '🪙 ' + formatNumber(gold) + ' Gold';
            bar.style.display = 'flex';
        } else if (bar) {
            bar.style.display = 'none';
        }
    }

    function resetRefreshTimer() {
        if (state.refreshTimer) clearInterval(state.refreshTimer);
        var intervalSec = (state.config && state.config.refreshIntervalSeconds) || 30;
        state.refreshTimer = setInterval(function () {
            loadActiveTabContent(true);
        }, intervalSec * 1000);
    }

    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    // --- Initialization ---
    async function init() {
        state.config = TwitchExtApi.getConfig();

        // Wire sub-nav pills
        document.querySelectorAll('#panel-fishing .sub-pill').forEach(function (btn) {
            btn.addEventListener('click', function () {
                document.querySelectorAll('#panel-fishing .sub-pill').forEach(function (b) { b.classList.remove('active'); });
                btn.classList.add('active');
                state.fishingSubTab = btn.getAttribute('data-sub');
                loadFishingContent();
            });
        });

        document.querySelectorAll('#panel-leaderboards .sub-pill').forEach(function (btn) {
            btn.addEventListener('click', function () {
                document.querySelectorAll('#panel-leaderboards .sub-pill').forEach(function (b) { b.classList.remove('active'); });
                btn.classList.add('active');
                state.lbType = btn.getAttribute('data-lb');
                loadLeaderboards();
            });
        });

        var pointTypeSelect = document.getElementById('lb-point-type-select');
        if (pointTypeSelect) {
            pointTypeSelect.addEventListener('change', function () {
                state.selectedPointTypeId = parseInt(pointTypeSelect.value, 10);
                loadLeaderboards();
            });
        }

        // Commands search & filter
        var searchInput = document.getElementById('cmd-search-input');
        if (searchInput) {
            searchInput.addEventListener('input', function () {
                clearTimeout(state.searchDebounceTimer);
                state.searchDebounceTimer = setTimeout(function () {
                    state.cmdSearchText = searchInput.value;
                    loadCommands();
                }, 350);
            });
        }

        var catSelect = document.getElementById('cmd-category-select');
        if (catSelect) {
            catSelect.addEventListener('change', function () {
                if (isProhibitedCategory(catSelect.value)) {
                    state.cmdCategory = 'all';
                    catSelect.value = 'all';
                } else {
                    state.cmdCategory = catSelect.value;
                }
                loadCommands();
            });
        }

        // Fetch bot features and load initial tab
        await refreshExtensionData();
    }

    var isRefreshing = false;
    var refreshQueued = false;

    async function refreshExtensionData() {
        if (isRefreshing) {
            refreshQueued = true;
            return;
        }
        isRefreshing = true;

        try {
            var cfg = TwitchExtApi.getConfig();
            state.config = cfg;

            if (!cfg || !cfg.botBaseUrl || !cfg.botBaseUrl.trim() || !isValidHttpsUrl(cfg.botBaseUrl)) {
                console.warn('[TwitchExt] Bot API Base URL is not configured or invalid in broadcaster settings!');
                showAlert('error', '⚠️ Bot URL not configured. Broadcaster: Please set Bot API URL in Twitch Creator Dashboard.', 12000);
                showUnconfiguredState();
                return;
            }

            try {
                state.botFeatures = await TwitchExtApi.getFeatures();
            } catch (e) {
                console.error('[TwitchExt] Failed to query bot features from ' + cfg.botBaseUrl + ':', e);
                showAlert('error', '⚠️ Failed to connect to bot: ' + (e.message || 'Network error'), 8000);
                state.botFeatures = { fishing: true, giveaway: true, points: true, leaderboards: true, starship: true, commands: true };
            }

            renderNavigationTabs();
            await loadActiveTabContent();
        } finally {
            isRefreshing = false;
            if (refreshQueued) {
                refreshQueued = false;
                refreshExtensionData();
            }
        }
    }

    function applyTheme(theme) {
        var resolvedTheme = theme === 'light' ? 'light' : 'dark';
        if (typeof document !== 'undefined') {
            try {
                if (document.documentElement) document.documentElement.setAttribute('data-theme', resolvedTheme);
                if (document.body) document.body.setAttribute('data-theme', resolvedTheme);
            } catch (_) { /* ignore DOM attribute errors */ }
        }
    }

    // Default theme detection before onContext fires
    try {
        if (window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches) {
            applyTheme('light');
        } else {
            applyTheme('dark');
        }
    } catch (err) {
        console.debug('[TwitchExt] matchMedia error:', err);
        applyTheme('dark');
    }

    TwitchExtApi.onContext(function (context) {
        if (context && context.theme) {
            applyTheme(context.theme);
        }
    });

    var isInitialized = false;

    async function startExtension() {
        if (!isInitialized) {
            isInitialized = true;
            await init();
        } else {
            await refreshExtensionData();
        }
    }

    TwitchExtApi.onConfigLoaded(function (cfg) {
        state.config = cfg;
        startExtension();
        resetRefreshTimer();
    });

    TwitchExtApi.onAuthorized(function () {
        startExtension();
    });

    TwitchExtApi.onReady(function () {
        startExtension();
    });
})();

