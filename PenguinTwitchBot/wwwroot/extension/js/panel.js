(function () {
    'use strict';

    var TAB_DEFS = {
        giveaway: { id: 'giveaway', label: 'Giveaway', icon: '🎁', featureKey: 'giveaway' },
        fishing: { id: 'fishing', label: 'Fishing', icon: '🎣', featureKey: 'fishing' },
        leaderboards: { id: 'leaderboards', label: 'Rankings', icon: '🏆', featureKey: 'leaderboards' },
        commands: { id: 'commands', label: 'Commands', icon: '💬', featureKey: 'commands' }
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
        botFeatures: null,
        config: null,
        refreshTimer: null,
        searchDebounceTimer: null
    };

    function showAlert(type, message, durationMs) {
        var el = document.getElementById('alert-banner');
        if (!el) return;
        el.className = 'banner show banner-' + type;
        el.textContent = message;
        setTimeout(function () {
            el.className = 'banner';
        }, durationMs || 4000);
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
        } catch (_) {}
        document.body.removeChild(input);
    }

    // --- Dynamic Navigation & Gating ---
    function renderNavigationTabs() {
        var container = document.getElementById('main-nav-tabs');
        if (!container) return;
        container.innerHTML = '';

        var order = (state.config && state.config.tabOrder) || ['giveaway', 'fishing', 'leaderboards', 'commands'];
        var visibleTabs = [];

        order.forEach(function (tabKey) {
            var def = TAB_DEFS[tabKey];
            if (!def) return;

            // Dual-layer gate:
            // 1. Bot feature enabled?
            var botEnabled = !state.botFeatures || state.botFeatures[def.featureKey] !== false;
            // 2. Streamer config enabled?
            var configEnabled = !state.config || !state.config.enabledTabs || state.config.enabledTabs[tabKey] !== false;

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
            btn.innerHTML = '<span>' + tab.icon + '</span> ' + tab.label;
            btn.addEventListener('click', function () {
                switchTab(tab.id);
            });
            container.appendChild(btn);
        });

        // If active tab is not visible, select the first visible tab
        var isActiveVisible = visibleTabs.some(function (t) { return t.id === state.activeTab; });
        if (!isActiveVisible && visibleTabs.length > 0) {
            switchTab(visibleTabs[0].id);
        }
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

        // Hide all panel elements
        ['giveaway', 'fishing', 'leaderboards', 'commands'].forEach(function (id) {
            var panel = document.getElementById('panel-' + id);
            if (panel) {
                panel.style.display = (id === tabId) ? 'block' : 'none';
            }
        });

        loadActiveTabContent();
        resetRefreshTimer();
    }

    function loadActiveTabContent() {
        switch (state.activeTab) {
            case 'giveaway':
                loadGiveaway();
                break;
            case 'fishing':
                loadFishingContent();
                break;
            case 'leaderboards':
                loadLeaderboards();
                break;
            case 'commands':
                loadCommands();
                break;
        }
    }

    // --- 1. Giveaway View ---
    async function loadGiveaway() {
        var container = document.getElementById('giveaway-content');
        if (!container) return;

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
        } catch (err) {
            container.innerHTML = '<div class="empty-state">Unable to load giveaway information.</div>';
        }
    }

    function renderGiveaway(container, g, v) {
        var html = '<div class="card giveaway-card">';

        if (g.imageUrl) {
            html += '<div class="giveaway-image-wrapper"><img src="' + escapeHtml(g.imageUrl) + '" class="giveaway-image" alt="Prize" /></div>';
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

        if (g.rules) {
            html += '<div style="font-size:10px; color:var(--text-muted); margin-bottom:8px;">' + escapeHtml(g.rules) + '</div>';
        }
        html += '</div>';

        // Entry Area
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

        container.innerHTML = html;

        // Wire Event Listeners
        var linkBtn = document.getElementById('btn-giveaway-link');
        if (linkBtn) {
            linkBtn.addEventListener('click', function () {
                TwitchExtApi.requestIdentityShare();
            });
        }

        if (v && !g.isClosed) {
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
    }

    // --- 2. Fishing View ---
    function loadFishingContent() {
        switch (state.fishingSubTab) {
            case 'catches':
                loadRecentCatches();
                break;
            case 'inventory':
                loadFishingInventory();
                break;
            case 'store':
                loadFishingStore();
                break;
        }
    }

    async function loadRecentCatches() {
        var container = document.getElementById('fishing-sub-content');
        if (!container) return;
        container.innerHTML = '<div class="loading-spinner">Loading catches...</div>';

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
        } catch (err) {
            container.innerHTML = '<div class="empty-state">Unable to load recent catches.</div>';
        }
    }

    async function loadFishingInventory() {
        var container = document.getElementById('fishing-sub-content');
        if (!container) return;

        if (!TwitchExtApi.isIdentityShared()) {
            container.innerHTML = '<div class="identity-box">' +
                '<p>Share your Twitch ID to view and manage your equipped rods, lures, and fishing gear.</p>' +
                '<button id="btn-inventory-link" class="btn-identity">Link Twitch Account</button>' +
                '</div>';
            var linkBtn = document.getElementById('btn-inventory-link');
            if (linkBtn) linkBtn.addEventListener('click', TwitchExtApi.requestIdentityShare);
            return;
        }

        container.innerHTML = '<div class="loading-spinner">Loading inventory...</div>';

        try {
            var viewer = await TwitchExtApi.getFishingViewer();
            updateHeaderUser(viewer.username, viewer.totalGold);

            var html = '<div class="inventory-summary">' +
                '<span><strong>' + escapeHtml(viewer.username) + '</strong></span>' +
                '<span class="gold-badge">🪙 ' + formatNumber(viewer.totalGold) + ' Gold</span>' +
                '</div>';

            if (!viewer.items || viewer.items.length === 0) {
                html += '<div class="empty-state">You do not own any fishing equipment yet. Visit the Fish Shop to purchase items!</div>';
                container.innerHTML = html;
                return;
            }

            viewer.items.forEach(function (item) {
                var isEquipped = item.isEquipped;
                var durabilityPct = item.currentDurability != null ? Math.round(item.currentDurability * 100) : null;

                html += '<div class="item-card">' +
                    '<div class="item-header">' +
                    '<div>' +
                    '<div class="item-name">' + escapeHtml(item.name) + (isEquipped ? ' <span style="color:var(--success); font-size:10px;">(Equipped)</span>' : '') + '</div>' +
                    '<div class="item-desc">' + escapeHtml(item.description) + '</div>' +
                    '</div>' +
                    (item.equipmentSlot ? '<span class="item-slot">' + escapeHtml(item.equipmentSlot) + '</span>' : '') +
                    '</div>';

                if (item.boostSummary) {
                    html += '<div style="font-size:10px; color:var(--primary-light);">' + escapeHtml(item.boostSummary) + '</div>';
                }

                html += '<div class="item-stats">';
                if (durabilityPct != null) {
                    html += '<span>Durability: ' + durabilityPct + '%</span>' +
                        '<div class="durability-bar"><div class="durability-fill ' + (durabilityPct < 25 ? 'durability-low' : '') + '" style="width:' + durabilityPct + '%;"></div></div>';
                }
                if (item.remainingUses >= 0) {
                    html += '<span>Uses: ' + item.remainingUses + '</span>';
                }
                html += '</div>';

                // Action buttons: Equip / Unequip
                if (item.equipmentSlot) {
                    html += '<div class="item-actions">';
                    if (isEquipped) {
                        html += '<button class="btn-sm btn-unequip btn-item-action" data-action="unequip" data-id="' + item.id + '">Unequip</button>';
                    } else {
                        html += '<button class="btn-sm btn-equip btn-item-action" data-action="equip" data-id="' + item.id + '">Equip</button>';
                    }
                    html += '</div>';
                }

                html += '</div>';
            });

            container.innerHTML = html;

            container.querySelectorAll('.btn-item-action').forEach(function (btn) {
                btn.addEventListener('click', async function () {
                    var action = btn.getAttribute('data-action');
                    var id = parseInt(btn.getAttribute('data-id'), 10);
                    btn.disabled = true;
                    try {
                        if (action === 'equip') {
                            await TwitchExtApi.equipFishingItem(id);
                            showAlert('success', 'Equipment updated!');
                        } else {
                            await TwitchExtApi.unequipFishingItem(id);
                            showAlert('success', 'Item unequipped.');
                        }
                        loadFishingInventory();
                    } catch (err) {
                        showAlert('error', err.message || 'Failed to update equipment.');
                        btn.disabled = false;
                    }
                });
            });

        } catch (err) {
            container.innerHTML = '<div class="empty-state">Unable to load fishing inventory.</div>';
        }
    }

    async function loadFishingStore() {
        var container = document.getElementById('fishing-sub-content');
        if (!container) return;
        container.innerHTML = '<div class="loading-spinner">Loading shop items...</div>';

        try {
            var items = await TwitchExtApi.getFishingStore();

            if (!items || items.length === 0) {
                container.innerHTML = '<div class="empty-state">Shop currently has no items for sale.</div>';
                return;
            }

            var html = '';
            items.forEach(function (item) {
                html += '<div class="item-card">' +
                    '<div class="item-header">' +
                    '<div>' +
                    '<div class="item-name">' + escapeHtml(item.name) + '</div>' +
                    '<div class="item-desc">' + escapeHtml(item.description) + '</div>' +
                    '</div>' +
                    (item.equipmentSlot ? '<span class="item-slot">' + escapeHtml(item.equipmentSlot) + '</span>' : '') +
                    '</div>' +
                    '<div class="item-stats">' +
                    '<span style="color:var(--warning); font-weight:700;">🪙 ' + formatNumber(item.cost) + ' Gold</span>' +
                    (item.maxUses ? '<span>Max Uses: ' + item.maxUses + '</span>' : '') +
                    '</div>' +
                    '<div class="item-actions">' +
                    '<button class="btn-sm btn-buy btn-buy-store" data-id="' + item.id + '" data-name="' + escapeHtml(item.name) + '">Buy (1)</button>' +
                    '</div>' +
                    '</div>';
            });

            container.innerHTML = html;

            container.querySelectorAll('.btn-buy-store').forEach(function (btn) {
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

        } catch (err) {
            container.innerHTML = '<div class="empty-state">Unable to load shop items.</div>';
        }
    }

    // --- 3. Leaderboards View ---
    async function loadLeaderboards() {
        var container = document.getElementById('leaderboard-content');
        var pointSelectorWrapper = document.getElementById('lb-point-type-selector-wrapper');
        if (!container) return;

        if (state.lbType === 'points') {
            if (pointSelectorWrapper) pointSelectorWrapper.style.display = 'block';
            await loadPointTypesSelector();
        } else {
            if (pointSelectorWrapper) pointSelectorWrapper.style.display = 'none';
        }

        container.innerHTML = '<div class="loading-spinner">Loading standings...</div>';

        try {
            var top = (state.config && state.config.leaderboardTopCount) || 10;

            if (state.lbType === 'tournaments') {
                var tournaments = await TwitchExtApi.getFishingTournaments(top);
                renderTournaments(container, tournaments);
                return;
            }

            var res = await TwitchExtApi.getLeaderboards(state.lbType, state.selectedPointTypeId, top);
            renderRankingsTable(container, res);
        } catch (err) {
            container.innerHTML = '<div class="empty-state">Unable to load standings.</div>';
        }
    }

    async function loadPointTypesSelector() {
        var select = document.getElementById('lb-point-type-select');
        if (!select || select.options.length > 0) return;

        try {
            var pts = await TwitchExtApi.getLeaderboardMeta();
            state.lbPointTypes = pts;
            select.innerHTML = '';
            pts.forEach(function (pt) {
                var opt = document.createElement('option');
                opt.value = pt.id;
                opt.textContent = pt.name;
                select.appendChild(opt);
            });
            if (pts.length > 0) {
                state.selectedPointTypeId = pts[0].id;
            }
        } catch (_) {}
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

        container.innerHTML = html;
    }

    // --- 4. Commands View ---
    async function loadCommands() {
        var container = document.getElementById('commands-list-content');
        if (!container) return;

        if (!state.categoriesLoaded) {
            try {
                var cats = await TwitchExtApi.getCommandCategories();
                var catSelect = document.getElementById('cmd-category-select');
                if (catSelect) {
                    cats.forEach(function (c) {
                        var opt = document.createElement('option');
                        opt.value = c;
                        opt.textContent = c;
                        catSelect.appendChild(opt);
                    });
                }
                state.categoriesLoaded = true;
            } catch (_) {}
        }

        container.innerHTML = '<div class="loading-spinner">Searching commands...</div>';

        try {
            var commands = await TwitchExtApi.getCommands(state.cmdCategory, state.cmdSearchText);

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
                    (c.userCooldown > 0 ? '<span>CD: ' + c.userCooldown + 's</span>' : '') +
                    (c.cost > 0 ? '<span style="color:var(--warning);">Cost: ' + c.cost + '</span>' : '') +
                    '</div>' +
                    '</div>';
            });

            container.innerHTML = html;

            container.querySelectorAll('.btn-copy').forEach(function (btn) {
                btn.addEventListener('click', function () {
                    var cmd = btn.getAttribute('data-cmd');
                    copyToClipboard(cmd, btn);
                });
            });

        } catch (err) {
            container.innerHTML = '<div class="empty-state">Unable to load commands list.</div>';
        }
    }

    function updateHeaderUser(username, gold) {
        var chip = document.getElementById('user-status-chip');
        var nameEl = document.getElementById('user-display-name');
        var goldEl = document.getElementById('user-header-gold');

        if (chip && nameEl) {
            nameEl.textContent = username;
            if (gold != null && goldEl) {
                goldEl.textContent = formatNumber(gold) + 'g';
            }
            chip.style.display = 'flex';
        }
    }

    function resetRefreshTimer() {
        if (state.refreshTimer) clearInterval(state.refreshTimer);
        var intervalSec = (state.config && state.config.refreshIntervalSeconds) || 30;
        state.refreshTimer = setInterval(function () {
            loadActiveTabContent();
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
                state.cmdCategory = catSelect.value;
                loadCommands();
            });
        }

        // Fetch bot features to perform feature gating
        try {
            state.botFeatures = await TwitchExtApi.getFeatures();
        } catch (e) {
            console.debug('Failed to query bot features, using default:', e);
            state.botFeatures = { fishing: true, giveaway: true, points: true, leaderboards: true, commands: true };
        }

        renderNavigationTabs();
    }

    TwitchExtApi.onConfigLoaded(function (cfg) {
        state.config = cfg;
        renderNavigationTabs();
        resetRefreshTimer();
    });

    TwitchExtApi.onReady(function () {
        init();
    });
})();

