/**
 * chat.js — Chat overlay widget
 *
 * Query parameters:
 *   direction   vertical (default) | horizontal
 *   valign      top (default) | bottom  — only used in vertical mode
 *   maxMessages integer, default 30     — max messages shown before oldest removed
 *   fontSize    px integer, default 16
 *   fontFamily  CSS font string, default inherit
 *   msgColor    hex color, default #ffffff
 *   bgColor     CSS color for container background, default transparent
 *   rowBg       CSS color for each message row background, default transparent
 *   badgeSize      px integer, default 20
 *   timeout        seconds, 0 = no timeout, default 0
 *   fontWeight     CSS font-weight value, default 700
 *   hideCommands   true|false — hide messages starting with !, default false
 *   ignoredUsers   comma-separated login names to suppress, default empty
 */

(function () {
    'use strict';

    // ── Parse query params ────────────────────────────────────────────────────
    function getQueryMap() {
        const queryMap = new Map();
        const parts = window.location.search.slice(1).split('&');
        for (const part of parts) {
            const eq = part.indexOf('=');
            if (eq > 0) {
                queryMap.set(part.substring(0, eq), decodeURIComponent(part.slice(eq + 1)));
            }
        }
        return queryMap;
    }

    const qp = getQueryMap();

    const direction   = qp.get('direction')   || 'vertical';  // vertical | horizontal
    const valign      = qp.get('valign')       || 'bottom';    // top | bottom
    const maxMessages = Math.max(1, parseInt(qp.get('maxMessages') || '30', 10) || 30);
    const fontSize    = parseInt(qp.get('fontSize')    || '16', 10);
    const fontFamily  = qp.get('fontFamily')   || null;
    const msgColor    = qp.get('msgColor')     || '#ffffff';
    const bgColor     = qp.get('bgColor')      || 'transparent';
    const rowBg       = qp.get('rowBg')        || 'transparent';
    const badgeSize   = parseInt(qp.get('badgeSize')   || '20', 10);
    const timeoutSecs = parseInt(qp.get('timeout')     || '0',  10);
    const tickerSpeed   = parseInt(qp.get('tickerSpeed')  || '80',   10); // px/sec
    const fontWeight    = parseInt(qp.get('fontWeight')   || '400',  10) || 400;
    const hideCommands  = qp.get('hideCommands') === 'true';
    const ignoredUsers  = new Set(
        (qp.get('ignoredUsers') || '').split(',').map(u => u.trim().toLowerCase()).filter(Boolean)
    );

    // ── Shared state ──────────────────────────────────────────────────────────
    /** @type {Map<string, string>} "setId/versionId" → imageUrl */
    let badgeCache = new Map();
    let badgeCacheLoaded = false;

    /** @type {Map<string, string>} emoteName → imageUrl (BTTV, FFZ, 7TV) */
    let emoteCache = new Map();

    function normalizeFontFamilyForGoogle(fontFamilyValue) {
        if (!fontFamilyValue) return '';
        // If user typed a full stack, use only the first family name for fetch.
        return fontFamilyValue
            .split(',')[0]
            .trim()
            .replace(/^['\"]|['\"]$/g, '');
    }

    function loadGoogleFont(fontFamilyValue) {
        const family = normalizeFontFamilyForGoogle(fontFamilyValue);
        if (!family) return;

        // Avoid duplicate link tags when the same widget reloads.
        const existing = document.querySelector(`link[data-chat-font="${family}"]`);
        if (existing) return;

        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = `https://fonts.googleapis.com/css2?family=${encodeURIComponent(family)}:wght@100;200;300;400;500;600;700;800;900&display=swap`;
        link.setAttribute('data-chat-font', family);
        document.head.appendChild(link);
    }

    if (fontFamily) loadGoogleFont(fontFamily);

    // ── Apply global CSS vars / body styles ───────────────────────────────────
    document.documentElement.style.setProperty('--msg-color', msgColor);
    document.documentElement.style.setProperty('--chat-bg', bgColor);
    document.documentElement.style.setProperty('--row-bg', rowBg);
    document.documentElement.style.setProperty('--badge-size', badgeSize + 'px');
    document.documentElement.style.setProperty('--font-size', fontSize + 'px');
    if (fontFamily) {
        document.documentElement.style.setProperty('--font-family', fontFamily);
    }
    document.body.style.fontSize   = fontSize + 'px';
    document.body.style.fontWeight  = String(fontWeight);
    document.body.style.color       = msgColor;
    if (fontFamily) document.body.style.fontFamily = fontFamily;

    // ── Container setup ───────────────────────────────────────────────────────
    const container = document.getElementById('chat-container');

    if (direction === 'horizontal') {
        container.classList.add('horizontal');
        // Message list anchored bottom-right; measurement div off-screen
        const ul = document.createElement('ul');
        ul.id = 'hchat-list';
        container.appendChild(ul);
        const measureDiv = document.createElement('div');
        measureDiv.id = 'hchat-measure';
        measureDiv.classList.add('horizontal'); // so .horizontal .chat-message styles apply
        document.body.appendChild(measureDiv);
    } else {
        container.classList.add('vertical');
        container.classList.add(valign === 'top' ? 'valign-top' : 'valign-bottom');
    }

    // ── Badge loading ─────────────────────────────────────────────────────────
    async function loadBadges() {
        try {
            const resp = await fetch('/api/chat/badges');
            if (!resp.ok) throw new Error('Badge fetch failed: ' + resp.status);
            const data = await resp.json();
            if (data && data.badges) {
                badgeCache = new Map(Object.entries(data.badges));
            }
        } catch (e) {
            console.warn('[chat] Could not load badge map:', e);
        } finally {
            badgeCacheLoaded = true;
        }
    }

    // ── Third-party emote loading (BTTV, FFZ, 7TV) ───────────────────────────
    async function loadEmotes() {
        try {
            const resp = await fetch('/api/chat/emotes');
            if (!resp.ok) throw new Error('Emote fetch failed: ' + resp.status);
            const data = await resp.json();
            if (data && data.emotes) {
                emoteCache = new Map(Object.entries(data.emotes));
                console.log('[chat] Loaded ' + emoteCache.size + ' third-party emotes');
            }
        } catch (e) {
            console.warn('[chat] Could not load third-party emotes:', e);
        }
    }

    // ── Emote text helpers ────────────────────────────────────────────────────
    /** Split a text fragment by whitespace and replace emote words with <img>. */
    function buildTextWithEmotes(text, parentEl) {
        // Split on whitespace, keeping the whitespace tokens so spacing is preserved
        const tokens = text.split(/(\s+)/);
        for (const token of tokens) {
            const url = emoteCache.get(token);
            if (url) {
                const img = document.createElement('img');
                img.src = url;
                img.alt = token;
                img.title = token;
                img.classList.add('fragment-emote');
                img.style.height = (fontSize * 1.5) + 'px';
                img.style.width = 'auto';
                img.style.verticalAlign = 'middle';
                parentEl.appendChild(img);
            } else {
                parentEl.appendChild(document.createTextNode(token));
            }
        }
    }

    // ── DOM helpers ───────────────────────────────────────────────────────────
    function buildBadgeImg(setId, versionId) {
        const key = `${setId}/${versionId}`;
        const url = badgeCache.get(key);
        if (!url) return null;
        const img = document.createElement('img');
        img.src = url;
        img.alt = setId;
        img.title = setId;
        img.width = badgeSize;
        img.height = badgeSize;
        img.style.width = badgeSize + 'px';
        img.style.height = badgeSize + 'px';
        img.style.verticalAlign = 'middle';
        return img;
    }

    function buildMessageElement(msg) {
        const row = document.createElement('div');
        row.classList.add('chat-message');
        row.dataset.msgId = msg.id;

        // Badges
        if (msg.badges && msg.badges.length > 0) {
            const badgesEl = document.createElement('span');
            badgesEl.classList.add('badges');
            for (const badge of msg.badges) {
                const img = buildBadgeImg(badge.setId, badge.id);
                if (img) badgesEl.appendChild(img);
            }
            if (badgesEl.childNodes.length > 0) row.appendChild(badgesEl);
        }

        // Username
        const nameEl = document.createElement('span');
        nameEl.classList.add('username');
        nameEl.textContent = msg.displayName;
        nameEl.style.color = msg.color || '#ffffff';
        row.appendChild(nameEl);

        // Separator
        const sep = document.createElement('span');
        sep.classList.add('separator');
        sep.textContent = ':';
        sep.style.color = msgColor;
        row.appendChild(sep);

        // Fragments
        const fragmentsEl = document.createElement('span');
        fragmentsEl.classList.add('fragments');
        fragmentsEl.style.color = msgColor;

        for (const frag of (msg.fragments || [])) {
            if ((frag.type === 'emote' || frag.type === 'cheermote') && frag.emoteUrl) {
                const img = document.createElement('img');
                img.src = frag.emoteUrl;
                img.alt = frag.text;
                img.title = frag.text;
                img.classList.add('fragment-emote');
                img.style.height = (fontSize * 1.5) + 'px';
                img.style.width = 'auto';
                img.style.verticalAlign = 'middle';
                fragmentsEl.appendChild(img);
            } else {
                if (frag.type === 'cheermote' && frag.cheerAmount) {
                    // Cheermote with no image still shows the amount in its colour
                    const span = document.createElement('span');
                    span.textContent = frag.text;
                    if (frag.cheerColor) span.style.color = frag.cheerColor;
                    fragmentsEl.appendChild(span);
                } else {
                    // Plain text: scan word-by-word for third-party emotes
                    buildTextWithEmotes(frag.text, fragmentsEl);
                }
            }
        }

        // Fall back to raw text if no fragments
        if (!msg.fragments || msg.fragments.length === 0) {
            fragmentsEl.textContent = msg.message || '';
        }

        row.appendChild(fragmentsEl);
        return row;
    }

    // ── Vertical mode message management ─────────────────────────────────────
    /** @type {Map<string, {el: HTMLElement, timerId: number|null}>} */
    const messageMap = new Map();

    function addMessageVertical(msg) {
        const el = buildMessageElement(msg);

        if (valign === 'top') {
            container.insertBefore(el, container.firstChild);
        } else {
            container.appendChild(el);
        }

        let timerId = null;
        if (timeoutSecs > 0) {
            timerId = setTimeout(() => removeMessage(msg.id), timeoutSecs * 1000);
        }
        messageMap.set(msg.id, { el, timerId, userId: msg.userId });

        // Trim oldest if over limit
        while (messageMap.size > maxMessages) {
            const oldest = messageMap.keys().next().value;
            removeMessage(oldest);
        }
    }

    function removeMessage(id) {
        const entry = messageMap.get(id);
        if (!entry) return;
        if (entry.timerId) clearTimeout(entry.timerId);
        entry.el.remove();
        messageMap.delete(id);
    }

    function removeMessagesByUser(userId) {
        const ids = [...messageMap.keys()].filter(id => messageMap.get(id).userId === userId);
        ids.forEach(removeMessage);
    }

    // ── Horizontal (push-left) mode message management ─────────────────────────
    // New messages appear at the right and slide in (max-width transition),
    // pushing all existing messages to the left. The list has no max-width — it
    // grows naturally leftward from right:0. #chat-container's overflow:hidden
    // clips old messages that drift off the left edge. DOM pruning is just cleanup.
    const hList = document.getElementById('hchat-list');
    const hMeasure = document.getElementById('hchat-measure');

    /** @type {Map<string, {el: HTMLLIElement, timerId: number|null, width: number}>} */
    const hChatMap = new Map();

    function addMessageHorizontal(msg) {
        if (!hList || !hMeasure) return;
        const content = buildMessageElement(msg);

        // Measure natural width in hidden off-screen div (forces layout reflow)
        hMeasure.appendChild(content);
        const measuredWidth = content.offsetWidth;
        hMeasure.removeChild(content);

        // Build list item — starts collapsed (max-width: 0 via CSS)
        const li = document.createElement('li');
        li.dataset.msgId = msg.id;
        li.appendChild(content);
        hList.appendChild(li);

        // Two rAF frames ensure the browser has painted max-width:0 before the
        // transition starts, so the expand animation always plays.
        requestAnimationFrame(() => requestAnimationFrame(() => {
            li.style.maxWidth = measuredWidth + 'px';
            // Release constraint after transition so content can breathe
            setTimeout(() => { li.style.maxWidth = 'none'; }, 900);
        }));

        let timerId = null;
        if (timeoutSecs > 0) {
            timerId = setTimeout(() => removeHMessage(msg.id), timeoutSecs * 1000);
        }
        hChatMap.set(msg.id, { el: li, timerId, width: measuredWidth, userId: msg.userId });

        // Prune by hard message count first
        while (hChatMap.size > maxMessages) {
            removeHMessageInstant(hChatMap.keys().next().value);
        }

        // Then prune anything whose accumulated width exceeds the container
        pruneHorizontal();
    }

    /** Remove oldest messages only once they are fully past the left edge. */
    function pruneHorizontal() {
        const limit = container.clientWidth;
        while (hChatMap.size > 1) {
            const oldestId = hChatMap.keys().next().value;
            const oldestWidth = hChatMap.get(oldestId).width;
            // Read the actual rendered list width each iteration — messages that are
            // still transitioning in have max-width:0 in the DOM, so they don't
            // inflate the total and cause premature pruning of partially-visible messages.
            if (hList.offsetWidth < limit + oldestWidth) break;
            removeHMessageInstant(oldestId);
        }
    }

    /** Instantly remove a message (used for overflow pruning). */
    function removeHMessageInstant(id) {
        const entry = hChatMap.get(id);
        if (!entry) return;
        if (entry.timerId) clearTimeout(entry.timerId);
        hChatMap.delete(id);
        entry.el.remove();
    }

    /** Animated removal (timeout expiry or chat_delete). */
    function removeHMessage(id) {
        const entry = hChatMap.get(id);
        if (!entry) return;
        if (entry.timerId) clearTimeout(entry.timerId);
        hChatMap.delete(id);
        // Re-apply explicit width so the CSS transition can collapse back to 0
        entry.el.style.maxWidth = entry.width + 'px';
        requestAnimationFrame(() => requestAnimationFrame(() => {
            entry.el.style.maxWidth = '0';
            setTimeout(() => entry.el.remove(), 900);
        }));
    }

    function removeHMessagesByUser(userId) {
        const ids = [...hChatMap.keys()].filter(id => hChatMap.get(id).userId === userId);
        ids.forEach(removeHMessage);
    }

    // ── WebSocket ─────────────────────────────────────────────────────────────
    function getWebSocket() {
        const scheme = window.location.protocol === 'https:' ? 'wss' : 'ws';
        const socketUri = scheme + '://' + window.location.host + '/ws';
        return new ReconnectingWebSocket(socketUri, null, { reconnectInterval: 5000 });
    }

    const socket = getWebSocket();

    socket.onopen = function () {
        if (typeof wsStatusShow === 'function') wsStatusShow(true);
    };
    socket.onclose = function () {
        if (typeof wsStatusShow === 'function') wsStatusShow(false);
    };
    socket.onerror = function () {
        if (typeof wsStatusShow === 'function') wsStatusShow(false);
    };

    socket.onmessage = function (event) {
        try {
            const raw = event.data;
            if (raw === 'ping') { socket.send('pong'); return; }

            const msg = JSON.parse(raw);

            if (msg.type === 'chat_message') {
                // Filter: ignored users
                if (ignoredUsers.size > 0 && ignoredUsers.has((msg.login || '').toLowerCase())) return;
                // Filter: hide commands (messages whose first text fragment starts with !)
                if (hideCommands) {
                    const firstText = (msg.fragments || []).find(f => f.type === 'text');
                    if (firstText && firstText.text.trimStart().startsWith('!')) return;
                }
                if (direction === 'horizontal') {
                    addMessageHorizontal(msg);
                } else {
                    addMessageVertical(msg);
                }
            } else if (msg.type === 'chat_delete') {
                if (direction === 'horizontal') {
                    removeHMessage(msg.id);
                } else {
                    removeMessage(msg.id);
                }
            } else if (msg.type === 'chat_user_banned') {
                if (direction === 'horizontal') {
                    removeHMessagesByUser(msg.userId);
                } else {
                    removeMessagesByUser(msg.userId);
                }
            }
        } catch (e) {
            console.warn('[chat] Failed to parse WS message:', e);
        }
    };

    // ── Bootstrap ─────────────────────────────────────────────────────────────
    loadBadges();
    loadEmotes();
}());
