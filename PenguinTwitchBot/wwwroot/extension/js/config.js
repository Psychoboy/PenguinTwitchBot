(function () {
    'use strict';

    var TAB_NAMES = {
        giveaway: '🎁 Giveaway',
        fishing: '🎣 Fishing System',
        leaderboards: '🏆 Leaderboards & Tournaments',
        commands: '💬 Commands Lookup'
    };

    var currentTabOrder = ['giveaway', 'fishing', 'leaderboards', 'commands'];

    function showAlert(type, message) {
        var el = document.getElementById('config-alert');
        if (!el) return;
        el.className = 'banner show banner-' + type;
        el.textContent = message;
        setTimeout(function () {
            el.className = 'banner';
        }, 5000);
    }

    function renderTabOrderList() {
        var list = document.getElementById('tab-order-list');
        if (!list) return;
        list.innerHTML = '';

        currentTabOrder.forEach(function (tabKey, index) {
            var li = document.createElement('li');
            li.className = 'order-item';

            var nameSpan = document.createElement('span');
            nameSpan.textContent = (index + 1) + '. ' + (TAB_NAMES[tabKey] || tabKey);
            li.appendChild(nameSpan);

            var actions = document.createElement('div');
            actions.className = 'order-actions';

            var upBtn = document.createElement('button');
            upBtn.type = 'button';
            upBtn.className = 'btn-order';
            upBtn.textContent = '▲';
            upBtn.title = 'Move Up';
            upBtn.disabled = (index === 0);
            upBtn.addEventListener('click', function () {
                moveTab(index, -1);
            });
            actions.appendChild(upBtn);

            var downBtn = document.createElement('button');
            downBtn.type = 'button';
            downBtn.className = 'btn-order';
            downBtn.textContent = '▼';
            downBtn.title = 'Move Down';
            downBtn.disabled = (index === currentTabOrder.length - 1);
            downBtn.addEventListener('click', function () {
                moveTab(index, 1);
            });
            actions.appendChild(downBtn);

            li.appendChild(actions);
            list.appendChild(li);
        });
    }

    function moveTab(fromIndex, delta) {
        var toIndex = fromIndex + delta;
        if (toIndex < 0 || toIndex >= currentTabOrder.length) return;
        var temp = currentTabOrder[fromIndex];
        currentTabOrder[fromIndex] = currentTabOrder[toIndex];
        currentTabOrder[toIndex] = temp;
        renderTabOrderList();
    }

    function loadFormValues() {
        var config = TwitchExtApi.getConfig();
        if (!config) return;

        var urlInput = document.getElementById('botBaseUrl');
        if (urlInput && config.botBaseUrl != null) {
            urlInput.value = config.botBaseUrl;
        }

        var enabled = config.enabledTabs || {};
        setChecked('tab-enable-giveaway', enabled.giveaway !== false);
        setChecked('tab-enable-fishing', enabled.fishing !== false);
        setChecked('tab-enable-leaderboards', enabled.leaderboards !== false);
        setChecked('tab-enable-commands', enabled.commands !== false);

        if (Array.isArray(config.tabOrder) && config.tabOrder.length > 0) {
            // Reconcile known tabs
            var set = new Set(config.tabOrder);
            ['giveaway', 'fishing', 'leaderboards', 'commands'].forEach(function (k) {
                if (!set.has(k)) config.tabOrder.push(k);
            });
            currentTabOrder = config.tabOrder.slice();
        }

        renderTabOrderList();

        setInputValue('leaderboardTopCount', config.leaderboardTopCount || 10);
        setInputValue('recentCatchesCount', config.recentCatchesCount || 15);
        setInputValue('refreshIntervalSeconds', config.refreshIntervalSeconds || 30);
    }

    function setChecked(id, val) {
        var el = document.getElementById(id);
        if (el) el.checked = Boolean(val);
    }

    function setInputValue(id, val) {
        var el = document.getElementById(id);
        if (el) el.value = val;
    }

    function getChecked(id) {
        var el = document.getElementById(id);
        return el ? el.checked : true;
    }

    function getInputValue(id, fallback) {
        var el = document.getElementById(id);
        return el ? el.value : fallback;
    }

    async function handleSave(e) {
        e.preventDefault();
        var saveBtn = document.getElementById('btn-save-config');
        if (saveBtn) {
            saveBtn.disabled = true;
            saveBtn.textContent = 'Saving...';
        }

        var botUrl = (getInputValue('botBaseUrl', '') || '').trim();
        var topCount = parseInt(getInputValue('leaderboardTopCount', 10), 10) || 10;
        var catchesCount = parseInt(getInputValue('recentCatchesCount', 15), 10) || 15;
        var intervalSec = parseInt(getInputValue('refreshIntervalSeconds', 30), 10) || 30;

        var configToSave = {
            botBaseUrl: botUrl,
            tabOrder: currentTabOrder,
            enabledTabs: {
                giveaway: getChecked('tab-enable-giveaway'),
                fishing: getChecked('tab-enable-fishing'),
                leaderboards: getChecked('tab-enable-leaderboards'),
                commands: getChecked('tab-enable-commands')
            },
            leaderboardTopCount: Math.clamp ? Math.clamp(topCount, 1, 50) : Math.max(1, Math.min(topCount, 50)),
            recentCatchesCount: Math.max(1, Math.min(catchesCount, 50)),
            refreshIntervalSeconds: Math.max(10, Math.min(intervalSec, 300))
        };

        try {
            await TwitchExtApi.saveConfig(configToSave);
            showAlert('success', 'Extension settings saved successfully!');
        } catch (err) {
            showAlert('error', 'Failed to save settings: ' + (err.message || 'Unknown error'));
        } finally {
            if (saveBtn) {
                saveBtn.disabled = false;
                saveBtn.textContent = 'Save Extension Settings';
            }
        }
    }

    function init() {
        loadFormValues();
        var form = document.getElementById('extension-config-form');
        if (form) {
            form.addEventListener('submit', handleSave);
        }
    }

    TwitchExtApi.onConfigLoaded(function () {
        loadFormValues();
    });

    TwitchExtApi.onReady(function () {
        init();
    });
})();

