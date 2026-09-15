(function () {
    var jwt = null;
    var channelId = null;
    var channelName = null;
    var authorized = false;

    function getJwt() {
        return jwt;
    }

    function getChannelId() {
        return channelId;
    }

    function getChannelName() {
        return channelName;
    }

    async function fetchWithAuth(url, options) {
        var headers = options?.headers || {};
        if (jwt) {
            headers['Authorization'] = 'Bearer ' + jwt;
        }
        headers['Accept'] = 'application/json';

        var response = await fetch(url, Object.assign({}, options, { headers: headers }));
        if (!response.ok) {
            throw new Error('HTTP ' + response.status);
        }
        return response;
    }

    async function getConfig() {
        var result = await twitch.ext.configuration.get();
        return {
            tournamentCount: result.tournamentCount || 5,
            catchCount: result.catchCount || 10,
            refreshInterval: result.refreshInterval || 30
        };
    }

    async function setConfig(config) {
        await twitch.ext.configuration.set(config);
    }

    function onReady() {
        if (window._onExtensionReady) {
            window._onExtensionReady();
        }
    }

    if (window.twitch && window.twitch.ext) {
        window.twitch.ext.onAuthorized(function (auth) {
            authorized = true;
            jwt = auth.token;
            channelId = auth.channelId;
            channelName = auth.channel.name;
            onReady();
        });
    }

    setTimeout(function () {
        if (!authorized) {
            onReady();
        }
    }, 1500);

    window._extJwt = getJwt;
    window._extChannelId = getChannelId;
    window._extChannelName = getChannelName;
    window._extFetch = fetchWithAuth;
    window._extGetConfig = getConfig;
    window._extSetConfig = setConfig;
})();