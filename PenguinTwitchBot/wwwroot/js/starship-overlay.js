(function () {
    'use strict';

    const view = document.body.dataset.starshipView;
    const root = document.getElementById('starship-widget');
    const stage = document.getElementById('stage');
    const BASE_SIZE = view === 'event' ? { width: 780, height: 460 } : { width: 520, height: 180 };
    const DEFAULT_ACCENT = '#66d0c2';
    const FONTS = ['Barlow Condensed', 'Arial Narrow', 'Segoe UI', 'Verdana', 'Georgia', 'Trebuchet MS', 'Impact', 'Courier New'];
    const DEFAULT_APPEARANCE = {
        panelColor: '#0a181d', panelOpacityPercent: 94, textColor: '#edf5ef', mutedColor: '#a8c1bd',
        accentColor: DEFAULT_ACCENT, highlightColor: '#f4bd68', warningColor: '#ef866f', useShipAccent: true,
        fontFamily: 'Barlow Condensed', cornerRadiusPx: 7, animationsEnabled: true, overlayDisplayMode: 'AutomaticNoOverlap',
        showEffectChips: true, showVoteBars: true, showOddsPanel: true, showPath: true, showRouteMap: true, showToasts: true,
        useBuiltInShipArt: true, shipImages: [], useBuiltInObjectArt: true, objectImages: {}
    };
    const previewMode = !!(window.location && /[?&]preview=/.test(window.location.search || ''));
    const TOAST_MS = 3500;
    const PULSE_MS = 700;
    let socket;
    let snapshot = null;
    let hasSnapshot = false;
    let lastAppliedTimestamp = -Infinity;
    let toastQueue = [];
    let toastEndsAt = 0;
    let pulseUntil = 0;
    let routeSignature = '';
    let revealCounter = 0;
    let allowRealtimeSounds = false;
    let soundManager = null;
    const playedSoundKeys = new Set();
    let appearance = Object.assign({}, DEFAULT_APPEARANCE);
    const tracker = window.StarshipScene.createTracker();
    const feedHandlers = [handleActivity];
    window.StarshipOverlay = {
        onFeed: function (handler) { feedHandlers.push(handler); },
        applyEnvelope: function (message) { applyEnvelope(message, false); },
        applyAppearance: function (value) { applyAppearance(value); }
    };

    function isHex(value) {
        return typeof value === 'string' && /^#[0-9a-fA-F]{6}$/.test(value);
    }

    function hexToRgba(hex, alpha) {
        return 'rgba(' + parseInt(hex.slice(1, 3), 16) + ', ' + parseInt(hex.slice(3, 5), 16) + ', ' + parseInt(hex.slice(5, 7), 16) + ', ' + alpha + ')';
    }

    function isSafeImageUrl(url) {
        return typeof url === 'string' && url.length <= 500 && /^(https?:\/\/|\/(?!\/))[^\s"'<>\\]+$/.test(url);
    }

    // The streamer's saved look; anything invalid is ignored so a bad value can never break the overlay.
    function applyAppearance(next) {
        appearance = Object.assign({}, DEFAULT_APPEARANCE, next || {});
        const a = appearance;
        const panelAlpha = Math.min(100, Math.max(0, Number(a.panelOpacityPercent))) / 100;
        if (isHex(a.panelColor)) setVar(stage, '--panel', hexToRgba(a.panelColor, Number.isFinite(panelAlpha) ? panelAlpha : 0.94));
        if (isHex(a.textColor)) setVar(stage, '--text', a.textColor);
        if (isHex(a.mutedColor)) setVar(stage, '--muted', a.mutedColor);
        if (isHex(a.accentColor)) {
            setVar(stage, '--teal', a.accentColor);
            setVar(stage, '--panel-line', hexToRgba(a.accentColor, 0.42));
        }
        if (isHex(a.highlightColor)) setVar(stage, '--amber', a.highlightColor);
        if (isHex(a.warningColor)) setVar(stage, '--coral', a.warningColor);
        const radius = Number(a.cornerRadiusPx);
        setVar(stage, '--radius', (Number.isFinite(radius) ? Math.min(24, Math.max(0, radius)) : 7) + 'px');
        const font = FONTS.indexOf(a.fontFamily) >= 0 ? a.fontFamily : FONTS[0];
        setVar(stage, '--font', "'" + font + "', 'Arial Narrow', sans-serif");
        if (stage && stage.dataset) {
            stage.dataset.animations = a.animationsEnabled === false ? 'off' : 'on';
            stage.dataset.chips = a.showEffectChips === false ? 'off' : 'on';
            stage.dataset.votebars = a.showVoteBars === false ? 'off' : 'on';
            stage.dataset.odds = a.showOddsPanel === false ? 'off' : 'on';
            stage.dataset.path = a.showPath === false ? 'off' : 'on';
            stage.dataset.routemap = a.showRouteMap === false ? 'off' : 'on';
        }
        render();
    }

    function stableIndex(value, count) {
        return count > 0 ? Math.abs(Math.trunc(Number(value) || 0)) % count : 0;
    }

    function setVar(element, name, value) {
        if (!element) return;
        if (element.style && typeof element.style.setProperty === 'function') element.style.setProperty(name, value);
        else if (element.style) element.style[name] = value;
    }

    // Layouts are designed at the default browser-source size and scaled to whatever size the streamer uses.
    function applyScale() {
        const width = window.innerWidth;
        const height = window.innerHeight;
        if (!(width > 0 && height > 0)) return;
        const scale = Math.min(4, Math.max(0.4, Math.min(width / BASE_SIZE.width, height / BASE_SIZE.height)));
        setVar(stage, '--ui-scale', String(scale));
    }

    function handleActivity(item) {
        if (allowRealtimeSounds && view === 'event' && item.kind === 'BoardingOpened') {
            playSound(snapshot?.boardingOpenSound, 'boarding-open:' + snapshot?.expeditionId);
        } else if (allowRealtimeSounds && view === 'event' && item.kind === 'EventOpened') {
            playSound(snapshot?.currentEvent?.openSound, 'event-open:' + snapshot?.expeditionId + ':' + snapshot?.currentEvent?.sequence);
        } else if (allowRealtimeSounds && view === 'event' && item.kind === 'VoyageEnded' && snapshot?.shipSystems?.returnReason === 'HullDepleted') {
            playSound(snapshot.emergencyReturnSound, 'emergency-return:' + snapshot.expeditionId);
        }
        if (item.kind !== 'CrewJoined') return;
        toastQueue.push(item);
        pulseUntil = Date.now() + PULSE_MS;
    }

    function playSound(sound, key) {
        if (!sound || sound.enabled !== true || !sound.url || !window.DropGamesAudio || previewMode) return;
        if (playedSoundKeys.has(key)) return;
        playedSoundKeys.add(key);
        if (!soundManager) soundManager = window.DropGamesAudio.createSoundManager();
        soundManager.updateBundle({ maxConcurrentSounds: 1 });
        soundManager.play(sound, key, true);
    }

    function pumpToast() {
        const toast = document.getElementById('starship-toast');
        if (!toast) return;
        if (appearance.showToasts === false) {
            toastQueue = [];
            toast.hidden = true;
            toastEndsAt = 0;
            return;
        }
        const nowMs = Date.now();
        if (toastEndsAt && nowMs >= toastEndsAt) {
            toast.hidden = true;
            toastEndsAt = 0;
        }
        if (toastEndsAt || toastQueue.length === 0) return;

        let text;
        if (toastQueue.length > 2) {
            const aboard = /(\d+) aboard/.exec(toastQueue[toastQueue.length - 1].text || '');
            text = toastQueue[0].actor + ' and ' + (toastQueue.length - 1) + ' others joined the crew' +
                (aboard ? ' - ' + aboard[1] + ' aboard' : '');
            toastQueue = [];
        } else {
            text = toastQueue.shift().text;
        }
        toast.textContent = text;
        toast.hidden = false;
        toastEndsAt = nowMs + TOAST_MS;
    }

    function getAccent() {
        const shipAccent = snapshot?.ship?.accentColor;
        if (appearance.useShipAccent !== false && isHex(shipAccent)) return shipAccent;
        return isHex(appearance.accentColor) ? appearance.accentColor : DEFAULT_ACCENT;
    }

    function setArt(element, spec) {
        const key = spec ? (spec.image ? 'img:' + spec.image : 'svg:' + spec.svg) : '';
        if (element.dataset.art === key) return;
        element.dataset.art = key;
        element.innerHTML = '';
        if (spec && spec.image) {
            const image = document.createElement('img');
            image.className = 'art-image';
            image.alt = '';
            image.src = spec.image;
            element.appendChild(image);
        } else if (spec && spec.svg) {
            element.innerHTML = spec.svg;
        }
    }

    // Custom media-library images share a pool with the built-in art unless the streamer turned the built-in art off.
    function shipSpec(large) {
        const ship = snapshot?.ship;
        if (!(ship && ship.name)) return null;
        const images = (appearance.shipImages || []).filter(isSafeImageUrl);
        const pool = (appearance.useBuiltInShipArt !== false || images.length === 0 ? [null] : []).concat(images);
        const picked = pool[stableIndex(ship.variant, pool.length)];
        if (picked) return { image: picked };
        const svg = window.StarshipArt.shipSvg(ship.hullStyle, getAccent());
        return { svg: large ? svg.replace('viewBox="0 0 64 32"', 'viewBox="-4 -20 72 72"') : svg };
    }

    function objectSpec(kind, variant, roll, eventImages) {
        const custom = (eventImages || []).filter(isSafeImageUrl);
        if (custom.length) return { image: custom[stableIndex(roll ?? variant, custom.length)] };
        const images = ((appearance.objectImages || {})[kind] || []).filter(isSafeImageUrl);
        if (images.length === 0) {
            const svg = window.StarshipArt.objectSvg(kind, variant, getAccent());
            return svg ? { svg: svg } : null;
        }

        const pool = [];
        if (appearance.useBuiltInObjectArt !== false) {
            for (let index = 0; index < window.StarshipArt.variantCount(kind); index++) pool.push({ svg: window.StarshipArt.objectSvg(kind, index, getAccent()) });
        }
        images.forEach(function (url) { pool.push({ image: url }); });
        return pool[stableIndex(roll ?? variant, pool.length)];
    }

    function renderShip() {
        const accent = getAccent();
        setVar(stage, '--ship-accent', accent);
        if (view === 'event') {
            const eventShip = document.getElementById('event-ship');
            const eventSpec = shipSpec(false);
            setArt(eventShip, eventSpec);
            eventShip.hidden = !eventSpec;
        }
        if (view !== 'status') return;
        const ship = snapshot?.ship;
        const hasShip = !!(ship && ship.name);
        setText('ship-name', hasShip ? ship.name : 'Starship Expedition');
        const icon = document.getElementById('ship-icon');
        const marker = document.getElementById('ship-marker');
        const spec = shipSpec(false);
        setArt(icon, spec);
        icon.hidden = !spec;
        setArt(marker, spec);
        marker.hidden = !spec;
    }

    function renderRouteMap(status, legCount, completed) {
        const nodes = document.getElementById('route-nodes');
        const stations = snapshot?.stationLegs || [];
        const signature = legCount + ':' + stations.join(',');
        if (signature !== routeSignature) {
            routeSignature = signature;
            nodes.replaceChildren();
            for (let leg = 1; leg <= legCount; leg++) {
                const node = document.createElement('span');
                node.dataset.leg = String(leg);
                node.style.left = (leg / legCount * 100) + '%';
                nodes.appendChild(node);
            }
        }

        const active = status === 'Cruising' || status === 'EventOpen' || status === 'Boarding';
        Array.prototype.forEach.call(nodes.children || [], function (node) {
            const leg = Number(node.dataset.leg);
            node.className = 'leg-node' + (stations.indexOf(leg) >= 0 ? ' station' : '') +
                (leg <= completed ? ' done' : '') + (active && leg === completed + 1 ? ' current' : '');
        });

        const progress = legCount ? Math.min(100, (completed + (status === 'EventOpen' ? 0.5 : 0)) / legCount * 100) : 0;
        document.getElementById('progress-fill').style.width = progress + '%';
        document.getElementById('ship-marker').style.left = progress + '%';
    }

    function applyEnvelope(message, isRealtime) {
        if (!message || message.type !== 'expedition_state') return;
        if (message.appearance) applyAppearance(message.appearance);
        const incoming = message.snapshot;
        if (incoming && incoming.updatedAtUtc) {
            const receivedAt = Date.parse(incoming.updatedAtUtc);
            if (Number.isFinite(receivedAt) && receivedAt < lastAppliedTimestamp) return;
            if (Number.isFinite(receivedAt)) lastAppliedTimestamp = receivedAt;
        }
        snapshot = incoming;
        hasSnapshot = true;
        allowRealtimeSounds = !!isRealtime && !previewMode;
        for (const item of tracker.update(snapshot)) {
            for (const handler of feedHandlers) handler(item);
        }
        render();
    }

    async function loadInitialState() {
        try {
            const response = await fetch('/api/overlay/starship-state', { cache: 'no-store' });
            if (!response.ok) throw new Error('State request failed: ' + response.status);
            applyEnvelope(await response.json(), false);
        } catch (error) {
            if (!hasSnapshot) root.dataset.connection = 'waiting';
        }
    }

    function formatTimeRemaining(timestamp) {
        if (!timestamp) return '--:--';
        const remaining = Math.max(0, Math.ceil((Date.parse(timestamp) - tracker.now()) / 1000));
        return String(Math.floor(remaining / 60)).padStart(2, '0') + ':' + String(remaining % 60).padStart(2, '0');
    }

    function setText(id, value) {
        const element = document.getElementById(id);
        if (element) element.textContent = value ?? '';
    }

    function renderStatus() {
        const status = snapshot?.status ?? 'Inactive';
        const phase = document.getElementById('phase');
        phase.textContent = snapshot?.shipSystems?.returnReason === 'HullDepleted' ? 'Emergency return' : status === 'Inactive' ? 'Awaiting' : status;
        phase.dataset.status = status;
        setText('sector', snapshot?.currentSector || (hasSnapshot ? 'No active voyage' : 'Connecting to ship'));
        setText('route', snapshot?.currentRoute && snapshot.currentRoute !== 'Uncharted' ? snapshot.currentRoute : 'Route not yet selected');
        setText('crew-count', snapshot?.crewCount ?? 0);

        const legCount = snapshot?.legCount ?? 0;
        const completed = Math.max(0, snapshot?.currentLeg ?? 0);
        const shownLeg = status === 'Boarding' ? 0 : Math.min(legCount, completed + (status === 'EventOpen' ? 1 : 0));
        setText('leg-count', 'Leg ' + shownLeg + ' / ' + legCount);
        renderRouteMap(status, legCount, completed);

        let timer = 'Awaiting launch';
        if (status === 'Boarding') timer = 'Takeoff in ' + formatTimeRemaining(snapshot.boardingClosesAtUtc);
        else if (status === 'EventOpen') timer = 'Vote closes in ' + formatTimeRemaining(snapshot.currentEvent?.closesAtUtc);
        else if (status === 'Cruising') timer = 'Next signal ' + formatTimeRemaining(snapshot.nextEventAtUtc);
        else if (status === 'Returned') timer = snapshot?.shipSystems?.returnReason === 'HullDepleted' ? 'Returned early: hull depleted' : 'Voyage complete';
        else if (status === 'Aborted') timer = 'Voyage aborted';
        setText('timing', timer);

        const scene = tracker.derive(snapshot, appearance.overlayDisplayMode);
        root.hidden = appearance.overlayDisplayMode === 'AutomaticNoOverlap' && scene.eventVisible;
        root.dataset.faded = scene.statusFaded ? 'true' : 'false';
        document.getElementById('crew-block').dataset.pulse = Date.now() < pulseUntil ? 'true' : 'false';
    }

    function renderChoices(currentEvent) {
        const list = document.getElementById('choices');
        list.replaceChildren();
        const disclosure = currentEvent.shipDisclosure?.choices || [];
        const choices = (currentEvent.choices || []).slice();
        disclosure.filter(details => details.available === false).forEach(details => {
            if (!choices.some(choice => choice.choiceId === details.choiceId)) choices.push({ choiceId: details.choiceId, label: details.label, unavailable: true });
        });
        list.dataset.compact = choices.length > 2 ? 'true' : 'false';
        root.dataset.dense = choices.length >= 4 && disclosure.length ? 'true' : 'false';
        const counts = currentEvent.voteCounts || {};
        const totalVotes = Object.keys(counts).reduce(function (sum, key) { return sum + counts[key]; }, 0);
        for (const choice of choices) {
            const details = disclosure.find(item => item.choiceId === choice.choiceId);
            const unavailable = choice.unavailable || details?.available === false;
            const row = document.createElement('div');
            row.className = 'choice-row';
            row.dataset.unavailable = unavailable ? 'true' : 'false';
            const label = document.createElement('span');
            label.className = 'choice-label';
            label.textContent = choice.label;
            const command = document.createElement('span');
            command.className = 'choice-command';
            command.textContent = unavailable ? 'Unavailable' : '!sv ' + (choice.number ?? '');
            const votes = document.createElement('span');
            votes.className = 'choice-votes';
            const count = counts[choice.choiceId] ?? 0;
            votes.textContent = count + (count === 1 ? ' vote' : ' votes');
            row.append(label, command, votes);

            const chips = document.createElement('span');
            chips.className = 'chip-row';
            for (const chip of choiceChips(choice)) {
                const element = document.createElement('span');
                element.className = 'chip';
                element.dataset.tone = chip[1];
                element.textContent = chip[0];
                chips.appendChild(element);
            }
            row.appendChild(chips);

            const bar = document.createElement('span');
            bar.className = 'vote-bar';
            const fill = document.createElement('span');
            fill.style.width = (totalVotes > 0 ? count / totalVotes * 100 : 0) + '%';
            bar.appendChild(fill);
            row.appendChild(bar);
            if (details) {
                const effects = document.createElement('span');
                effects.className = 'ship-choice-effects';
                effects.textContent = shipChoiceText(details);
                row.appendChild(effects);
            }
            list.appendChild(row);
        }
    }

    function signed(value) { return value > 0 ? '+' + value : String(value); }

    function shipChoiceText(details) {
        if (details.available === false) return 'Requires ' + details.suppliesCost + ' supplies';
        const parts = [];
        if (details.hullDelta) parts.push('Hull ' + signed(details.hullDelta));
        if (details.suppliesDelta) parts.push('Supplies net ' + signed(details.suppliesDelta));
        if (details.suppliesCost) parts.push('cost ' + details.suppliesCost + ' supplies');
        if (details.successChancePercent != null) parts.push(details.successChancePercent + '% success');
        if (details.nextEventSuccessBonusPercent) parts.push('Next event +' + details.nextEventSuccessBonusPercent + 'pp success');
        if (details.continuesEvent) parts.push('Next step');
        const success = [];
        if (details.successHullDelta) success.push('hull ' + signed(details.successHullDelta));
        if (details.successSuppliesDelta) success.push('supplies ' + signed(details.successSuppliesDelta));
        if (success.length) parts.push('Success: ' + success.join(', '));
        const failure = [];
        if (details.failureHullDelta) failure.push('hull ' + signed(details.failureHullDelta));
        if (details.failureSuppliesDelta) failure.push('supplies ' + signed(details.failureSuppliesDelta));
        if (failure.length) parts.push('Failure: ' + failure.join(', '));
        return parts.join(' | ') || 'No shared resource effect';
    }

    function renderShipResources() {
        const panel = document.getElementById('ship-systems');
        if (!panel) return;
        const ship = snapshot?.shipSystems;
        panel.hidden = !ship;
        root.dataset.ship = ship ? 'on' : 'off';
        if (!ship) return;
        const maximumHull = Math.max(1, Math.min(100, Number(ship.maximumHull) || 100));
        const hull = Math.min(maximumHull, Math.max(0, Number(ship.hull) || 0));
        const supplies = Math.min(100, Math.max(0, Number(ship.supplies) || 0));
        setText('ship-hull-text', 'Hull ' + hull + '/' + maximumHull);
        setText('ship-supplies-text', 'Supplies ' + supplies + '/100');
        const hullFill = document.getElementById('ship-hull-fill');
        const suppliesFill = document.getElementById('ship-supplies-fill');
        hullFill.style.width = (hull / maximumHull * 100) + '%';
        suppliesFill.style.width = supplies + '%';
        panel.dataset.hull = hull <= 25 ? 'low' : 'normal';
        panel.dataset.supplies = supplies === 0 ? 'empty' : 'normal';
        const bonus = [];
        if (ship.pendingSuccessBonusPercent) bonus.push('Next +' + ship.pendingSuccessBonusPercent + 'pp');
        if (ship.activeSuccessBonusPercent) bonus.push('Event +' + ship.activeSuccessBonusPercent + 'pp');
        if (supplies === 0) bonus.push('Empty supplies');
        setText('ship-bonus', bonus.join(' | '));
    }

    function choiceChips(choice) {
        const chips = [];
        const success = choice.successModifierPercent ?? 0;
        const reward = choice.rewardMultiplierPercent ?? 100;
        const risk = choice.riskMultiplierPercent ?? 100;
        if (success) chips.push([(success > 0 ? '+' : '') + success + '% success', success > 0 ? 'good' : 'bad']);
        if (reward !== 100) chips.push(['x' + reward / 100 + ' reward', reward > 100 ? 'good' : 'bad']);
        if (risk !== 100) chips.push(['x' + risk / 100 + ' loss', risk < 100 ? 'good' : 'bad']);
        if (choice.revealsRarity) chips.push(['reveals rarity', 'info']);
        if (choice.endsWithoutReward) chips.push(['no reward', 'neutral']);
        return chips;
    }

    // Each text change restarts a CSS reveal by alternating between two identical animations.
    function setTextAnimated(id, value) {
        const element = document.getElementById(id);
        if (!element) return;
        const text = value ?? '';
        if (element.textContent === text) return;
        element.textContent = text;
        if (text) {
            revealCounter++;
            element.dataset.reveal = String(revealCounter % 2);
        }
    }

    function renderArt(spec, caption) {
        setArt(document.getElementById('scene-object'), spec);
        setText('scene-subject', caption);
    }

    function renderRing(currentEvent) {
        const ring = document.getElementById('ring-fg');
        const total = (Date.parse(currentEvent.closesAtUtc) - Date.parse(currentEvent.openedAtUtc)) / 1000;
        const remaining = (Date.parse(currentEvent.closesAtUtc) - tracker.now()) / 1000;
        const fraction = total > 0 ? Math.min(1, Math.max(0, remaining / total)) : 0;
        ring.style.strokeDashoffset = String(106.8 * (1 - fraction));
    }

    function renderRarityBadge(currentEvent) {
        const badge = document.getElementById('rarity-badge');
        const known = currentEvent.revealedRarity || '';
        badge.hidden = !currentEvent.rewardDisclosure;
        badge.dataset.rarity = known;
        const knownTier = (((currentEvent.rewardDisclosure || {}).tiers) || []).find(tier => tier.name === known);
        const color = currentEvent.revealedRarityColor || (knownTier ? knownTier.color : '');
        setVar(badge, '--rarity-color', known && /^#[0-9a-fA-F]{6}$/.test(color) ? color : '');
        badge.textContent = known ? known + ' find' : 'rarity unknown';
    }

    function renderRewardDisclosure(currentEvent) {
        const panel = document.getElementById('reward-disclosure');
        const profile = currentEvent.rewardDisclosure;
        panel.hidden = !profile;
        const riskElement = document.getElementById('reward-risk');
        const hasRiskyChoice = (currentEvent.choices || []).some(choice => choice.isRisky);
        riskElement.hidden = !profile;
        if (!profile) return;

        const currencies = profile.currencies && profile.currencies.length ? profile.currencies : [profile];
        setText('reward-point-type', currencies.map(currency => currency.pointTypeName).join(' / ') + ' · per eligible crew member');
        const chance = currentEvent.effectiveSuccessChancePercent ?? profile.successChancePercent;
        setText('reward-success', 'Success chance: ' + chance + '% / failure ' + (100 - chance) + '%');
        setText('reward-scope', hasRiskyChoice
            ? 'Scope: only viewers who commit to the risky option can win or lose points.'
            : 'Scope: success pays eligible crew; failure costs the whole crew.');
        const tiers = profile.tiers || [];
        const knownTier = tiers.find(tier => tier.name === currentEvent.revealedRarity);
        if (knownTier) {
            setText('reward-rarity', 'Rarity: ' + knownTier.name + ' (' + knownTier.rewardText + ')');
        } else {
            const odds = tiers.map(tier => tier.name + ' ' + tier.chancePercent + '%' + (tier.rewardText ? ' (' + tier.rewardText + ')' : '')).join(' · ');
            setText('reward-rarity', 'Rarity: unknown' + (hasRiskyChoice ? ' - scan to reveal it before committing' : ' until the event resolves') +
                (odds ? ' · Odds: ' + odds : ''));
        }
        setText('reward-caps', currencies.map(currency => currency.pointTypeName + ': cap ' + (currency.maximumPerEvent ?? 'Unlimited') + '/event, ' + (currency.maximumUnbankedPerVoyage ?? 'Unlimited') + '/voyage').join(' · '));
        const risk = currencies.map(currency => {
            const riskPercent = Math.min(100, currency.riskLossPercent * (currentEvent.riskMultiplierPercent ?? 100) / 100);
            const lossLimit = currency.maximumRiskLossPerEvent == null ? 'no amount cap' : 'capped at ' + Math.floor(currency.maximumRiskLossPerEvent * (currentEvent.riskMultiplierPercent ?? 100) / 100);
            return currency.pointTypeName + ': ' + riskPercent + '% unbanked, ' + lossLimit;
        }).join(' · ');
        setText('reward-risk', 'Failure loss: ' + risk + '.');
    }

    function renderStepInfo(currentEvent) {
        const text = currentEvent.stepNote || '';
        setTextAnimated('step-note', text);
        document.getElementById('step-note').hidden = !text;
    }

    function renderResultLines(latest) {
        const container = document.getElementById('result-lines');
        container.replaceChildren();
        const lines = latest.lines || [];
        container.hidden = lines.length === 0;
        for (const text of lines) {
            const line = document.createElement('span');
            line.textContent = text;
            container.appendChild(line);
        }
    }

    function renderBankedAmounts(latest) {
        const container = document.getElementById('result-banked');
        container.replaceChildren();
        const amounts = latest.bankedAmounts || [];
        container.hidden = amounts.length === 0;
        for (const payout of amounts) {
            const line = document.createElement('span');
            const amount = payout.averagePerCrewMember != null
                ? payout.averagePerCrewMember
                : payout.amount;
            line.textContent = Number(amount).toLocaleString(undefined, { maximumFractionDigits: 2 }) + ' ' + payout.pointTypeName + (payout.isPaid ? ' banked' : ' banking') +
                (payout.averagePerCrewMember != null ? ' average per banked crew member' : ' crew total');
            container.appendChild(line);
        }
    }

    function renderTimeline(entries) {
        const container = document.getElementById('result-timeline');
        container.replaceChildren();
        container.hidden = !entries || entries.length === 0;
        for (const entry of entries || []) {
            const pill = document.createElement('span');
            pill.className = 'timeline-entry';
            pill.dataset.outcome = entry.outcome || '';
            const mark = entry.outcome === 'Success' ? ' ✓' : entry.outcome === 'Failure' ? ' ✗' : '';
            pill.textContent = entry.sequence + ' ' + entry.title + (entry.rarity ? ' · ' + entry.rarity : '') + mark;
            container.appendChild(pill);
        }
    }

    function renderEvent() {
        const scene = tracker.derive(snapshot, appearance.overlayDisplayMode);
        root.dataset.scene = scene.name;
        if (!scene.eventVisible) {
            root.hidden = true;
            return;
        }
        if (!snapshot || (snapshot.status !== 'Boarding' && snapshot.status !== 'EventOpen')) {
            const terminal = snapshot && (snapshot.status === 'Returned' || snapshot.status === 'Aborted');
            const latest = snapshot?.latestResult;
            root.hidden = false;
            document.getElementById('boarding-panel').hidden = true;
            document.getElementById('event-panel').hidden = true;
            document.getElementById('result-panel').hidden = false;
            document.getElementById('event-description').hidden = true;
            const shipName = snapshot?.ship?.name || 'Starship Expedition';
            setText('event-kicker', terminal ? 'VOYAGE COMPLETE' : latest ? 'EVENT RESOLVED' : 'EVENT WIDGET');
            setText('event-title', terminal ? shipName : latest?.title || shipName);
            setText('event-timer', terminal ? 'END' : latest?.outcome === 'Success' ? '\u2713' : latest?.outcome === 'Failure' ? '\u2717' : snapshot?.status || 'SHOWING');
            document.getElementById('ring-fg').style.strokeDashoffset = '0';
            setText('event-description', '');
            const banner = document.getElementById('result-banner');
            const outcome = terminal ? snapshot.status : (latest?.outcome || '');
            banner.dataset.outcome = outcome;
            banner.textContent = terminal ? (snapshot.shipSystems?.returnReason === 'HullDepleted' ? 'Emergency return: hull depleted' : outcome === 'Aborted' ? 'Voyage aborted' : 'Voyage complete') : outcome;
            banner.hidden = !banner.textContent;
            setText('result-choice', terminal ? '' : latest?.choice || 'Event overlay triggered');
            setText('result-detail', terminal ? '' : latest ? latest.sector + ' \u00b7 Leg ' + latest.sequence : '');
            document.getElementById('result-choice').hidden = terminal;
            document.getElementById('result-detail').hidden = terminal;
            setTextAnimated('result-story', latest?.story || '');
            document.getElementById('result-story').hidden = !latest?.story;
            renderBankedAmounts(latest || {});
            renderResultLines(latest || {});
            renderTimeline(terminal ? snapshot.timeline : []);
            const resultSpec = !terminal && latest?.objectKind ? objectSpec(latest.objectKind, latest.objectVariant, latest.objectRoll, latest.objectImages) : null;
            renderArt(resultSpec || shipSpec(true), !terminal && latest?.subject ? latest.subject : shipName);
            return;
        }

        root.hidden = false;
        document.getElementById('result-panel').hidden = true;
        const boarding = snapshot.status === 'Boarding';
        document.getElementById('boarding-panel').hidden = !boarding;
        document.getElementById('event-panel').hidden = boarding;

        if (boarding) {
            setText('event-kicker', 'CREW ASSEMBLY');
            setText('event-title', snapshot.ship?.name || 'Boarding is open');
            setText('event-timer', formatTimeRemaining(snapshot.boardingClosesAtUtc));
            document.getElementById('ring-fg').style.strokeDashoffset = '0';
            document.getElementById('event-description').hidden = false;
            setText('event-description', 'Boarding is open. The expedition launches when it closes.');
            renderArt(shipSpec(true), snapshot.ship?.name || '');
        } else {
            const currentEvent = snapshot.currentEvent;
            if (!currentEvent) {
                root.hidden = true;
                return;
            }
            setText('event-kicker', 'LEG ' + currentEvent.sequence + (currentEvent.stepNumber > 1 ? ' · STEP ' + currentEvent.stepNumber : '') + ' · ' + currentEvent.sector);
            setText('event-title', currentEvent.title);
            setText('event-timer', formatTimeRemaining(currentEvent.closesAtUtc));
            renderRing(currentEvent);
            document.getElementById('event-description').hidden = currentEvent.stepNumber > 1;
            setTextAnimated('event-description', currentEvent.description);
            const defaultIndex = (currentEvent.choices || []).findIndex(choice => choice.choiceId === currentEvent.defaultChoiceId);
            const defaultChoice = defaultIndex >= 0 ? currentEvent.choices[defaultIndex] : null;
            setText('event-sector', ((currentEvent.path || []).length ? 'Path: ' + currentEvent.path.join(' > ') : currentEvent.sector) +
                (defaultChoice ? ' · Default: ' + (defaultChoice.number ?? defaultIndex + 1) + ') ' + defaultChoice.label : ''));
            renderArt(objectSpec(currentEvent.objectKind, currentEvent.objectVariant, currentEvent.objectRoll, currentEvent.objectImages) || shipSpec(true), currentEvent.subject || '');
            renderChoices(currentEvent);
            renderStepInfo(currentEvent);
            renderRarityBadge(currentEvent);
            renderRewardDisclosure(currentEvent);
        }
    }

    function render() {
        if (!root) return;
        pumpToast();
        renderShip();
        renderShipResources();
        if (view === 'status') renderStatus();
        else renderEvent();
    }

    function connect() {
        const topics = window.WS_TOPICS?.starship;
        if (!topics || typeof window.createWsSocket !== 'function') return;
        socket = window.createWsSocket('Starship ' + view, [topics]);
        socket.addEventListener('open', function () {
            if (typeof window.wsStatusShow === 'function') window.wsStatusShow(true);
            loadInitialState();
        });
        socket.addEventListener('close', function () {
            if (typeof window.wsStatusShow === 'function') window.wsStatusShow(false);
        });
        socket.addEventListener('message', function (event) {
            if (event.data === 'ping') {
                if (socket.readyState === WebSocket.OPEN) socket.send('pong');
                return;
            }
            try {
                applyEnvelope(JSON.parse(event.data), true);
            } catch (error) {
                // Ignore messages for other widget protocols.
            }
        });
    }

    applyScale();
    if (typeof window.addEventListener === 'function') window.addEventListener('resize', applyScale);
    render();
    if (previewMode) {
        document.body.dataset.preview = 'true';
        const script = document.createElement('script');
        script.src = '/js/starship-preview.js';
        document.body.appendChild(script);
    } else {
        loadInitialState();
        connect();
    }
    window.setInterval(render, 250);
}());