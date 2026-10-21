(function () {
    'use strict';

    const view = document.body.dataset.starshipView;
    const root = document.getElementById('starship-widget');
    const stage = document.getElementById('stage');
    const BASE_SIZE = view === 'event' ? { width: 620, height: 360 } : { width: 520, height: 180 };
    const DEFAULT_ACCENT = '#66d0c2';
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
    const tracker = window.StarshipScene.createTracker();
    const feedHandlers = [handleActivity];
    window.StarshipOverlay = { onFeed: function (handler) { feedHandlers.push(handler); } };

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
        if (item.kind !== 'CrewJoined') return;
        toastQueue.push(item);
        pulseUntil = Date.now() + PULSE_MS;
    }

    function pumpToast() {
        const toast = document.getElementById('starship-toast');
        if (!toast) return;
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

    function shipSvg(style, accent) {
        const hull = '#e8f3f0';
        const shapes = {
            arrow: '<polygon points="2,16 26,5 62,16 26,27" fill="' + hull + '"/><polygon points="22,16 40,11 52,16 40,21" fill="' + accent + '"/><rect x="0" y="13" width="6" height="6" fill="' + accent + '" opacity=".7"/>',
            hauler: '<rect x="10" y="8" width="46" height="16" rx="3" fill="' + hull + '"/><polygon points="56,8 63,16 56,24" fill="' + hull + '"/><rect x="3" y="11" width="8" height="10" fill="' + accent + '"/><rect x="38" y="11" width="14" height="10" fill="' + accent + '" opacity=".8"/>',
            scout: '<ellipse cx="34" cy="16" rx="24" ry="8" fill="' + hull + '"/><ellipse cx="42" cy="13" rx="8" ry="5" fill="' + accent + '"/><polygon points="12,16 2,5 2,27" fill="' + accent + '" opacity=".8"/>',
            cruiser: '<polygon points="4,12 54,12 63,16 54,20 4,20" fill="' + hull + '"/><rect x="20" y="4" width="14" height="8" rx="2" fill="' + hull + '"/><rect x="24" y="6" width="6" height="4" fill="' + accent + '"/><rect x="0" y="13" width="6" height="6" fill="' + accent + '" opacity=".7"/><rect x="36" y="14" width="18" height="4" fill="' + accent + '" opacity=".8"/>'
        };
        return '<svg viewBox="0 0 64 32" xmlns="http://www.w3.org/2000/svg">' + (shapes[style] || shapes.arrow) + '</svg>';
    }

    function getAccent() {
        const accent = snapshot?.ship?.accentColor;
        return typeof accent === 'string' && /^#[0-9a-fA-F]{6}$/.test(accent) ? accent : DEFAULT_ACCENT;
    }

    function renderShip() {
        const accent = getAccent();
        setVar(stage, '--ship-accent', accent);
        if (view !== 'status') return;
        const ship = snapshot?.ship;
        const hasShip = !!(ship && ship.name);
        setText('ship-name', hasShip ? ship.name : 'Starship Expedition');
        const icon = document.getElementById('ship-icon');
        const marker = document.getElementById('ship-marker');
        const markup = hasShip ? shipSvg(ship.hullStyle, accent) : '';
        icon.innerHTML = markup;
        icon.hidden = !hasShip;
        marker.innerHTML = markup;
        marker.hidden = !hasShip;
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

    function applyEnvelope(message) {
        if (!message || message.type !== 'expedition_state') return;
        const incoming = message.snapshot;
        if (incoming && incoming.updatedAtUtc) {
            const receivedAt = Date.parse(incoming.updatedAtUtc);
            if (Number.isFinite(receivedAt) && receivedAt < lastAppliedTimestamp) return;
            if (Number.isFinite(receivedAt)) lastAppliedTimestamp = receivedAt;
        }
        snapshot = incoming;
        hasSnapshot = true;
        for (const item of tracker.update(snapshot)) {
            for (const handler of feedHandlers) handler(item);
        }
        render();
    }

    async function loadInitialState() {
        try {
            const response = await fetch('/api/overlay/starship-state', { cache: 'no-store' });
            if (!response.ok) throw new Error('State request failed: ' + response.status);
            applyEnvelope(await response.json());
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
        phase.textContent = status === 'Inactive' ? 'Awaiting' : status;
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
        else if (status === 'Returned') timer = 'Voyage complete';
        else if (status === 'Aborted') timer = 'Voyage aborted';
        setText('timing', timer);

        root.dataset.faded = tracker.derive(snapshot).statusFaded && !toastEndsAt ? 'true' : 'false';
        document.getElementById('crew-block').dataset.pulse = Date.now() < pulseUntil ? 'true' : 'false';
    }

    function renderChoices(currentEvent) {
        const list = document.getElementById('choices');
        list.replaceChildren();
        for (const choice of currentEvent.choices || []) {
            const row = document.createElement('div');
            row.className = 'choice-row';
            const label = document.createElement('span');
            label.className = 'choice-label';
            label.textContent = choice.label;
            const command = document.createElement('span');
            command.className = 'choice-command';
            command.textContent = '!sv ' + (choice.number ?? '');
            const votes = document.createElement('span');
            votes.className = 'choice-votes';
            const count = currentEvent.voteCounts?.[choice.choiceId] ?? 0;
            votes.textContent = count + (count === 1 ? ' vote' : ' votes');
            row.append(label, command, votes);
            if (choice.hint) {
                const hint = document.createElement('span');
                hint.className = 'choice-hint';
                hint.textContent = choice.hint;
                row.appendChild(hint);
            }
            list.appendChild(row);
        }
    }

    function renderRewardDisclosure(currentEvent) {
        const panel = document.getElementById('reward-disclosure');
        const profile = currentEvent.rewardDisclosure;
        panel.hidden = !profile;
        const riskElement = document.getElementById('reward-risk');
        const hasRiskyChoice = (currentEvent.choices || []).some(choice => choice.isRisky);
        riskElement.hidden = !profile;
        if (!profile) return;

        setText('reward-point-type', profile.pointTypeName + ' · per eligible crew member');
        const chance = currentEvent.effectiveSuccessChancePercent ?? profile.successChancePercent;
        setText('reward-success', 'Success chance: ' + chance + '% / failure ' + (100 - chance) + '%');
        setText('reward-scope', hasRiskyChoice
            ? 'Scope: only viewers who commit to the risky option can win or lose points.'
            : 'Scope: success pays eligible crew; failure costs the whole crew.');
        const range = {
            Common: [profile.commonMinimum, profile.commonMaximum],
            Uncommon: [profile.uncommonMinimum, profile.uncommonMaximum],
            Rare: [profile.rareMinimum, profile.rareMaximum]
        }[currentEvent.revealedRarity];
        setText('reward-rarity', range
            ? 'Rarity: ' + currentEvent.revealedRarity + ' (' + range[0] + '-' + range[1] + ' ' + profile.pointTypeName + ')'
            : 'Rarity: unknown' + (hasRiskyChoice ? ' - scan to reveal it before committing' : ' until the event resolves'));
        setText('reward-caps', 'Cap: ' + profile.maximumPerEvent + ' per event · ' + profile.maximumUnbankedPerVoyage + ' unbanked per voyage');
        const riskPercent = Math.min(100, profile.riskLossPercent * (currentEvent.riskMultiplierPercent ?? 100) / 100);
        const riskCap = Math.floor(profile.maximumRiskLossPerEvent * (currentEvent.riskMultiplierPercent ?? 100) / 100);
        setText('reward-risk', 'If it fails, the whole crew loses up to ' + riskPercent + '% of unbanked points, capped at ' + riskCap + '.');
    }

    function renderStepInfo(currentEvent) {
        const note = document.getElementById('step-note');
        const text = [
            currentEvent.stepNumber > 1 ? 'Step ' + currentEvent.stepNumber : '',
            (currentEvent.path || []).length ? 'Path: ' + currentEvent.path.join(' > ') : '',
            currentEvent.stepNote || ''
        ].filter(Boolean).join(' · ');
        note.textContent = text;
        note.hidden = !text;
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
            line.textContent = payout.amount + ' ' + payout.pointTypeName + (payout.isPaid ? ' banked' : ' banking');
            container.appendChild(line);
        }
    }

    function renderEvent() {
        if (!snapshot || (snapshot.status !== 'Boarding' && snapshot.status !== 'EventOpen')) {
            const terminal = snapshot && (snapshot.status === 'Returned' || snapshot.status === 'Aborted');
            const latest = snapshot?.latestResult;
            if (!tracker.derive(snapshot).eventVisible) {
                root.hidden = true;
                return;
            }

            root.hidden = false;
            document.getElementById('boarding-panel').hidden = true;
            document.getElementById('event-panel').hidden = true;
            document.getElementById('result-panel').hidden = false;
            setText('event-kicker', terminal ? 'VOYAGE COMPLETE' : 'EVENT RESOLVED');
            setText('event-title', terminal ? 'Starship Expedition' : latest.title);
            setText('event-timer', terminal ? snapshot.status : 'RESOLVED');
            setText('event-description', '');
            setText('result-choice', latest.choice);
            setText('result-detail', latest.sector + ' · Leg ' + latest.sequence);
            renderBankedAmounts(latest);
            renderResultLines(latest);
            return;
        }

        root.hidden = false;
        document.getElementById('result-panel').hidden = true;
        const boarding = snapshot.status === 'Boarding';
        document.getElementById('boarding-panel').hidden = !boarding;
        document.getElementById('event-panel').hidden = boarding;

        if (boarding) {
            setText('event-kicker', 'CREW ASSEMBLY');
            setText('event-title', 'Boarding is open');
            setText('event-timer', formatTimeRemaining(snapshot.boardingClosesAtUtc));
            setText('event-description', 'The expedition launches when boarding closes.');
        } else {
            const currentEvent = snapshot.currentEvent;
            if (!currentEvent) {
                root.hidden = true;
                return;
            }
            setText('event-kicker', 'LEG ' + currentEvent.sequence + ' · ' + currentEvent.sector);
            setText('event-title', currentEvent.title);
            setText('event-timer', formatTimeRemaining(currentEvent.closesAtUtc));
            setText('event-description', currentEvent.description);
            setText('event-sector', currentEvent.sector + ' · Default: ' + currentEvent.defaultChoiceId);
            renderChoices(currentEvent);
            renderStepInfo(currentEvent);
            renderRewardDisclosure(currentEvent);
        }
    }

    function render() {
        if (!root) return;
        pumpToast();
        renderShip();
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
                applyEnvelope(JSON.parse(event.data));
            } catch (error) {
                // Ignore messages for other widget protocols.
            }
        });
    }

    applyScale();
    if (typeof window.addEventListener === 'function') window.addEventListener('resize', applyScale);
    render();
    loadInitialState();
    connect();
    window.setInterval(render, 250);
}());