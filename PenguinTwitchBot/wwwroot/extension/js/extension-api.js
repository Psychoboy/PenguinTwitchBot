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
    var authCallbacks = [];
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

    function getTwitchExt() {
        if (typeof window !== 'undefined') {
            if (window.twitch && window.twitch.ext) return window.twitch.ext;
            if (window.Twitch && window.Twitch.ext) {
                window.twitch = window.Twitch;
                return window.Twitch.ext;
            }
        }
        return null;
    }

    function parseBroadcasterConfig() {
        var ext = getTwitchExt();
        if (ext && ext.configuration && ext.configuration.broadcaster) {
            try {
                var content = ext.configuration.broadcaster.content;
                if (content && content.trim().length > 0) {
                    var parsed = JSON.parse(content);
                    broadcasterConfig = Object.assign({}, DEFAULT_CONFIG, parsed);
                    return broadcasterConfig;
                }
            } catch (err) {
                console.warn('[TwitchExt] Failed to parse broadcaster configuration:', err);
            }
        }

        // Fallback to localStorage
        try {
            var local = localStorage.getItem('ptb_extension_config');
            if (local && local.trim().length > 0) {
                var localParsed = JSON.parse(local);
                if (localParsed && (localParsed.botBaseUrl || localParsed.tabOrder)) {
                    broadcasterConfig = Object.assign({}, DEFAULT_CONFIG, localParsed);
                    return broadcasterConfig;
                }
            }
        } catch (_) { /* ignore storage errors */ }

        if (!broadcasterConfig) {
            broadcasterConfig = Object.assign({}, DEFAULT_CONFIG);
        }
        return broadcasterConfig;
    }

    function getBaseUrl() {
        var cfg = getConfig();
        if (cfg && cfg.botBaseUrl && cfg.botBaseUrl.trim().length > 0) {
            return cfg.botBaseUrl.trim().replace(/\/+$/, '');
        }
        return '';
    }

    function getConfig() {
        var ext = getTwitchExt();
        if (ext && ext.configuration && ext.configuration.broadcaster && ext.configuration.broadcaster.content) {
            return parseBroadcasterConfig();
        }
        if (!broadcasterConfig || !broadcasterConfig.botBaseUrl) {
            return parseBroadcasterConfig();
        }
        return broadcasterConfig;
    }

    function saveConfig(configObj) {
        return new Promise(function (resolve, reject) {
            var payload = JSON.stringify(configObj);
            broadcasterConfig = Object.assign({}, DEFAULT_CONFIG, configObj);

            // Persist to localStorage immediately
            try {
                localStorage.setItem('ptb_extension_config', payload);
            } catch (lsErr) {
                console.warn('[TwitchExt] Failed to write localStorage:', lsErr);
            }

            var ext = getTwitchExt();
            if (ext && ext.configuration) {
                try {
                    var version = ext.version || '0.0.1';
                    ext.configuration.set('broadcaster', version, payload);
                    resolve(broadcasterConfig);
                } catch (err) {
                    console.error('[TwitchExt] configuration.set failed:', err);
                    reject(err);
                }
            } else {
                console.warn('[TwitchExt] Twitch configuration service not available in ext; saved to localStorage.');
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
            try { cb(broadcasterConfig); } catch (e) { console.error('[TwitchExt] onConfigLoaded error:', e); }
        }
    }

    function onAuthorized(cb) {
        authCallbacks.push(cb);
        if (jwt) {
            try {
                cb({ token: jwt, channelId: channelId, userId: userId, isIdentityShared: isIdentityShared });
            } catch (e) {
                console.error('[TwitchExt] onAuthorized error:', e);
            }
        }
    }

    function triggerReady() {
        if (isReady) return;
        isReady = true;
        for (var i = 0; i < readyCallbacks.length; i++) {
            try { readyCallbacks[i](); } catch (e) { console.error('[TwitchExt] Ready listener error:', e); }
        }
    }

    function triggerConfigChanged(cfg) {
        for (var i = 0; i < configCallbacks.length; i++) {
            try { configCallbacks[i](cfg); } catch (e) { console.error('[TwitchExt] Config listener error:', e); }
        }
    }

    function triggerAuthorized(auth) {
        for (var i = 0; i < authCallbacks.length; i++) {
            try { authCallbacks[i](auth); } catch (e) { console.error('[TwitchExt] Auth listener error:', e); }
        }
    }

    function initTwitchHooks() {
        var ext = getTwitchExt();
        if (ext) {
            ext.onAuthorized(function (auth) {
                jwt = auth.token;
                channelId = auth.channelId;
                userId = auth.userId;
                isIdentityShared = Boolean(userId && !userId.startsWith('A'));

                var cfg = parseBroadcasterConfig();
                if (cfg && cfg.botBaseUrl) {
                    triggerConfigChanged(cfg);
                }
                triggerAuthorized(auth);
                triggerReady();
            });

            if (ext.configuration) {
                ext.configuration.onChanged(function () {
                    var cfg = parseBroadcasterConfig();
                    triggerConfigChanged(cfg);
                });
            }
            return true;
        }
        return false;
    }

    // Attempt immediate binding, then poll if SDK is still loading asynchronously
    if (!initTwitchHooks()) {
        var pollAttempts = 0;
        var maxPollAttempts = 40; // 40 * 100ms = 4 seconds
        var pollInterval = setInterval(function () {
            pollAttempts++;
            if (initTwitchHooks()) {
                clearInterval(pollInterval);
            } else if (pollAttempts >= maxPollAttempts) {
                clearInterval(pollInterval);
                if (!isReady) {
                    parseBroadcasterConfig();
                    triggerReady();
                }
            }
        }, 100);
    }

    function requestIdentityShare() {
        var ext = getTwitchExt();
        if (ext && ext.actions && ext.actions.requestIdShare) {
            ext.actions.requestIdShare();
        } else {
            alert('Identity share requested. In production Twitch, this opens a Twitch account sharing prompt.');
        }
    }

    async function extractError(response) {
        var errorBody = null;
        try {
            errorBody = await response.json();
        } catch (_) { /* ignore */ }

        var message = (errorBody && (errorBody.message || errorBody.error)) || ('HTTP ' + response.status);
        var err = new Error(message);
        err.status = response.status;
        err.data = errorBody;
        return err;
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

        var baseUrl = getBaseUrl();
        if (!baseUrl) {
            console.warn('[TwitchExt] Warning: botBaseUrl is not configured! Attempting request to relative path:', endpoint);
        }

        var fullUrl = baseUrl + endpoint;
        var fetchOptions = Object.assign({}, options, { headers: headers });

        var response;
        try {
            response = await fetch(fullUrl, fetchOptions);
        } catch (fetchErr) {
            console.error('[TwitchExt] Network/CORS Fetch Error for ' + fullUrl + ':', fetchErr);
            throw fetchErr;
        }

        if (!response.ok) {
            var err = await extractError(response);
            console.error('[TwitchExt] HTTP Error ' + response.status + ' from ' + fullUrl + ':', err.data || err.message);
            throw err;
        }

        return await response.json();
    }

    return {
        onReady: onReady,
        onConfigLoaded: onConfigLoaded,
        onAuthorized: onAuthorized,
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

