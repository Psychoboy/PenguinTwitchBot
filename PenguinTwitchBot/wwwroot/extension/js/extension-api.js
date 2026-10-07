/**
 * Penguin Twitch Bot - Twitch Extension API Client
 * Encapsulates Twitch Extension Helper communication, JWT handling,
 * Configuration Service, and API requests to the self-hosted bot backend.
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.TwitchExtApi = factory();
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    var jwt = null;
    var channelId = null;
    var userId = null;
    var isIdentityShared = false;
    var broadcasterConfig = null;
    var readyCallbacks = [];
    var configCallbacks = [];
    var isReady = false;

    // Default configuration if broadcaster has not set one
    var DEFAULT_CONFIG = {
        botBaseUrl: '',
        tabOrder: ['giveaway', 'fishing', 'leaderboards', 'commands'],
        enabledTabs: {
            giveaway: true,
            fishing: true,
            leaderboards: true,
            commands: true
        },
        leaderboardTopCount: 10,
        recentCatchesCount: 15,
        refreshIntervalSeconds: 30
    };

    function parseBroadcasterConfig() {
        if (window.twitch && window.twitch.ext && window.twitch.ext.configuration && window.twitch.ext.configuration.broadcaster) {
            try {
                var content = window.twitch.ext.configuration.broadcaster.content;
                if (content) {
                    var parsed = JSON.parse(content);
                    broadcasterConfig = Object.assign({}, DEFAULT_CONFIG, parsed);
                    return broadcasterConfig;
                }
            } catch (err) {
                console.warn('Failed to parse broadcaster configuration:', err);
            }
        }

        // Fallback to localStorage for local development / testing outside Twitch iFrame
        try {
            var local = localStorage.getItem('ptb_extension_config');
            if (local) {
                broadcasterConfig = Object.assign({}, DEFAULT_CONFIG, JSON.parse(local));
                return broadcasterConfig;
            }
        } catch (_) {}

        broadcasterConfig = Object.assign({}, DEFAULT_CONFIG);
        return broadcasterConfig;
    }

    function getBaseUrl() {
        var cfg = getConfig();
        if (cfg && cfg.botBaseUrl && cfg.botBaseUrl.trim().length > 0) {
            return cfg.botBaseUrl.trim().replace(/\/+$/, '');
        }
        // Fallback to current host
        return '';
    }

    function getConfig() {
        if (!broadcasterConfig) {
            parseBroadcasterConfig();
        }
        return broadcasterConfig;
    }

    function saveConfig(configObj) {
        return new Promise(function (resolve, reject) {
            var payload = JSON.stringify(configObj);
            broadcasterConfig = Object.assign({}, DEFAULT_CONFIG, configObj);

            // Also persist to localStorage for easy local dev testing
            try {
                localStorage.setItem('ptb_extension_config', payload);
            } catch (_) {}

            if (window.twitch && window.twitch.ext && window.twitch.ext.configuration) {
                try {
                    window.twitch.ext.configuration.set('broadcaster', '1.0', payload);
                    resolve(broadcasterConfig);
                } catch (err) {
                    reject(err);
                }
            } else {
                resolve(broadcasterConfig);
            }
        });
    }

    function onReady(cb) {
        if (isReady) {
            cb();
        } else {
            readyCallbacks.push(cb);
        }
    }

    function onConfigLoaded(cb) {
        configCallbacks.push(cb);
        if (broadcasterConfig) {
            cb(broadcasterConfig);
        }
    }

    function triggerReady() {
        if (isReady) return;
        isReady = true;
        for (var i = 0; i < readyCallbacks.length; i++) {
            try { readyCallbacks[i](); } catch (e) { console.error(e); }
        }
    }

    function triggerConfigChanged(cfg) {
        for (var i = 0; i < configCallbacks.length; i++) {
            try { configCallbacks[i](cfg); } catch (e) { console.error(e); }
        }
    }

    // Initialize Twitch Extension helper hooks
    if (typeof window !== 'undefined' && window.twitch && window.twitch.ext) {
        window.twitch.ext.onAuthorized(function (auth) {
            jwt = auth.token;
            channelId = auth.channelId;
            userId = auth.userId;
            isIdentityShared = Boolean(userId && !userId.startsWith('A'));
            triggerReady();
        });

        if (window.twitch.ext.configuration) {
            window.twitch.ext.configuration.onChanged(function () {
                var cfg = parseBroadcasterConfig();
                triggerConfigChanged(cfg);
            });
        }
    }

    // Fallback timer for local development outside Twitch iFrame
    setTimeout(function () {
        if (!isReady) {
            parseBroadcasterConfig();
            triggerReady();
        }
    }, 1200);

    function requestIdentityShare() {
        if (window.twitch && window.twitch.ext && window.twitch.ext.actions && window.twitch.ext.actions.requestIdShare) {
            window.twitch.ext.actions.requestIdShare();
        } else {
            alert('Identity share requested. In production Twitch, this opens a Twitch account sharing prompt.');
        }
    }

    async function request(endpoint, options) {
        options = options || {};
        var headers = options.headers || {};

        if (jwt) {
            headers['Authorization'] = 'Bearer ' + jwt;
        }
        headers['Accept'] = 'application/json';

        if (options.body && typeof options.body === 'object' && !(options.body instanceof FormData)) {
            headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(options.body);
        }

        var fullUrl = getBaseUrl() + endpoint;
        var fetchOptions = Object.assign({}, options, { headers: headers });

        var response = await fetch(fullUrl, fetchOptions);

        if (!response.ok) {
            var errorBody = null;
            try {
                errorBody = await response.json();
            } catch (_) {}

            var err = new Error(errorBody && (errorBody.message || errorBody.error) ? (errorBody.message || errorBody.error) : 'HTTP ' + response.status);
            err.status = response.status;
            err.data = errorBody;
            throw err;
        }

        return await response.json();
    }

    return {
        onReady: onReady,
        onConfigLoaded: onConfigLoaded,
        getConfig: getConfig,
        saveConfig: saveConfig,
        requestIdentityShare: requestIdentityShare,
        isIdentityShared: function () { return isIdentityShared; },
        getUserId: function () { return userId; },
        getChannelId: function () { return channelId; },

        // API Methods
        getFeatures: function () {
            return request('/api/twitch-extension/features');
        },
        getLeaderboards: function (type, pointTypeId, top) {
            var url = '/api/twitch-extension/leaderboards?type=' + encodeURIComponent(type || 'points') + '&top=' + (top || 10);
            if (pointTypeId) {
                url += '&pointTypeId=' + encodeURIComponent(pointTypeId);
            }
            return request(url);
        },
        getLeaderboardMeta: function () {
            return request('/api/twitch-extension/leaderboards/meta');
        },
        getGiveaway: function () {
            return request('/api/twitch-extension/giveaway');
        },
        getGiveawayViewer: function () {
            return request('/api/twitch-extension/giveaway/viewer');
        },
        enterGiveaway: function (amount) {
            return request('/api/twitch-extension/giveaway/enter', {
                method: 'POST',
                body: { amount: parseInt(amount, 10) || 1 }
            });
        },
        getFishingTournaments: function (top) {
            return request('/api/twitch-extension/fishing/tournaments?top=' + (top || 5));
        },
        getRecentCatches: function (count) {
            return request('/api/twitch-extension/fishing/recent-catches?count=' + (count || 15));
        },
        getFishingStore: function () {
            return request('/api/twitch-extension/fishing/store');
        },
        getFishingViewer: function () {
            return request('/api/twitch-extension/fishing/viewer');
        },
        buyFishingItem: function (shopItemId, quantity) {
            return request('/api/twitch-extension/fishing/store/buy', {
                method: 'POST',
                body: { shopItemId: shopItemId, quantity: quantity || 1 }
            });
        },
        equipFishingItem: function (userBoostId) {
            return request('/api/twitch-extension/fishing/inventory/equip', {
                method: 'POST',
                body: { userBoostId: userBoostId }
            });
        },
        unequipFishingItem: function (userBoostId) {
            return request('/api/twitch-extension/fishing/inventory/unequip', {
                method: 'POST',
                body: { userBoostId: userBoostId }
            });
        },
        getCommands: function (category, search) {
            var url = '/api/twitch-extension/commands?';
            if (category && category !== 'all') {
                url += 'category=' + encodeURIComponent(category) + '&';
            }
            if (search) {
                url += 'search=' + encodeURIComponent(search);
            }
            return request(url);
        },
        getCommandCategories: function () {
            return request('/api/twitch-extension/commands/categories');
        }
    };
});

