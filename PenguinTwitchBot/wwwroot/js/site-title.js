(function () {
    function getConfiguredTitle() {
        var meta = document.querySelector('meta[name="application-title"]');
        var value = meta && meta.getAttribute("content");
        if (!value || !value.trim()) {
            return "Penguin Twitch Bot";
        }

        return value.trim();
    }

    function rewriteTitle(baseTitle) {
        if (!document || !document.title) {
            return;
        }

        var current = document.title;
        var updated = current.replace(/^Penguin Twitch Bot/, baseTitle);
        if (updated !== current) {
            document.title = updated;
        }
    }

    function startTitleSync() {
        var configuredTitle = getConfiguredTitle();
        rewriteTitle(configuredTitle);

        var titleElement = document.querySelector("title");
        if (!titleElement || typeof MutationObserver === "undefined") {
            return;
        }

        var observer = new MutationObserver(function () {
            rewriteTitle(configuredTitle);
        });

        observer.observe(titleElement, {
            childList: true,
            characterData: true,
            subtree: true
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", startTitleSync);
    } else {
        startTitleSync();
    }
})();
