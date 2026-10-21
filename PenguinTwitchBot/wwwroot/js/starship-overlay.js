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
    let revealCounter = 0;
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
        if (view === 'event') {
            const eventShip = document.getElementById('event-ship');
            const hasEventShip = !!(snapshot?.ship && snapshot.ship.name);
            eventShip.innerHTML = hasEventShip ? shipSvg(snapshot.ship.hullStyle, accent) : '';
            eventShip.hidden = !hasEventShip;
        }
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
        const counts = currentEvent.voteCounts || {};
        const totalVotes = Object.keys(counts).reduce(function (sum, key) { return sum + counts[key]; }, 0);
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
            list.appendChild(row);
        }
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

    const OBJECT_ART = {
        wreck: [
            '<polygon points="14,40 56,30 62,46 50,58 18,56" fill="%H"/><polygon points="70,44 108,38 104,58 76,62" fill="%H"/><polygon points="56,30 62,46 50,58" fill="%D"/><circle cx="36" cy="46" r="5" fill="%A"/><polygon points="64,64 70,70 60,72" fill="%H"/><polygon points="84,24 90,30 80,32" fill="%H"/>',
            '<rect x="22" y="36" width="70" height="24" rx="6" fill="%H" transform="rotate(-12 57 48)"/><rect x="44" y="40" width="16" height="12" fill="%D" transform="rotate(-12 57 48)"/><line x1="86" y1="30" x2="100" y2="14" stroke="%A" stroke-width="2"/><circle cx="101" cy="13" r="3" fill="%A"/><polygon points="30,62 24,74 40,66" fill="%H"/>',
            '<polygon points="10,46 70,26 100,46 70,66" fill="%H"/><circle cx="68" cy="46" r="11" fill="%D"/><circle cx="68" cy="46" r="6" fill="%A" opacity=".8"/><rect x="100" y="40" width="10" height="12" fill="%H" transform="rotate(18 105 46)"/><polygon points="20,70 34,64 30,78" fill="%H" opacity=".7"/>'
        ],
        beacon: [
            '<rect x="56" y="30" width="8" height="46" fill="%H"/><circle cx="60" cy="26" r="7" fill="%A"/><circle cx="60" cy="26" r="14" fill="none" stroke="%A" stroke-width="1.5" opacity=".7"/><circle cx="60" cy="26" r="22" fill="none" stroke="%A" stroke-width="1.5" opacity=".4"/><rect x="46" y="74" width="28" height="6" fill="%H"/>',
            '<circle cx="60" cy="45" r="16" fill="%D" stroke="%H" stroke-width="2"/><circle cx="60" cy="45" r="8" fill="%A"/><ellipse cx="60" cy="45" rx="32" ry="9" fill="none" stroke="%A" stroke-width="1.5" opacity=".6"/><ellipse cx="60" cy="45" rx="44" ry="14" fill="none" stroke="%A" stroke-width="1" opacity=".35"/>',
            '<polygon points="40,64 80,64 70,76 50,76" fill="%H"/><path d="M34 44 Q60 70 86 44 Z" fill="%H"/><line x1="60" y1="52" x2="60" y2="24" stroke="%A" stroke-width="2"/><circle cx="60" cy="22" r="4" fill="%A"/><path d="M42 18 Q60 4 78 18" fill="none" stroke="%A" stroke-width="1.5" opacity=".6"/>'
        ],
        debris: [
            '<polygon points="20,50 38,34 54,44 48,64 26,66" fill="#7f8f8b"/><polygon points="64,30 84,24 94,42 78,52 62,46" fill="#9aa9a5"/><polygon points="70,62 92,60 98,74 76,78" fill="#6c7b77"/><polygon points="40,14 50,12 48,22" fill="#9aa9a5"/>',
            '<polygon points="30,70 40,22 52,66" fill="#9fd3e8"/><polygon points="50,72 66,14 80,70" fill="#bfe6f3"/><polygon points="76,70 90,34 100,68" fill="#8fc4da"/><circle cx="24" cy="30" r="3" fill="#bfe6f3"/>',
            '<rect x="18" y="30" width="34" height="20" fill="%H" transform="rotate(-18 35 40)"/><rect x="62" y="22" width="28" height="16" fill="%D" stroke="%H" transform="rotate(22 76 30)"/><rect x="50" y="56" width="40" height="12" fill="%H" transform="rotate(8 70 62)"/><circle cx="26" cy="64" r="5" fill="%A"/>'
        ],
        station: [
            '<circle cx="60" cy="45" r="30" fill="none" stroke="%H" stroke-width="7"/><circle cx="60" cy="45" r="10" fill="%A"/><line x1="60" y1="15" x2="60" y2="75" stroke="%H" stroke-width="3"/><line x1="30" y1="45" x2="90" y2="45" stroke="%H" stroke-width="3"/>',
            '<polygon points="56,10 64,10 68,62 52,62" fill="%H"/><rect x="38" y="62" width="44" height="8" fill="%H"/><rect x="46" y="72" width="28" height="6" fill="%D"/><circle cx="60" cy="24" r="5" fill="%A"/><rect x="28" y="40" width="64" height="5" fill="%H" opacity=".8"/>',
            '<rect x="48" y="33" width="24" height="24" fill="%H"/><rect x="18" y="39" width="26" height="12" fill="%H"/><rect x="76" y="39" width="26" height="12" fill="%H"/><rect x="52" y="8" width="16" height="22" fill="%H"/><rect x="52" y="60" width="16" height="22" fill="%H"/><circle cx="60" cy="45" r="6" fill="%A"/>'
        ],
        fork: [
            '<path d="M60 80 L60 52 L30 20" stroke="%H" stroke-width="6" fill="none"/><path d="M60 52 L90 20" stroke="%A" stroke-width="6" fill="none"/><polygon points="22,26 34,14 38,28" fill="%H"/><polygon points="98,26 86,14 82,28" fill="%A"/>',
            '<ellipse cx="60" cy="42" rx="16" ry="12" fill="#7f8f8b"/><path d="M60 82 Q24 66 24 34" stroke="%H" stroke-width="5" fill="none"/><path d="M60 82 Q96 66 96 34" stroke="%A" stroke-width="5" fill="none"/><circle cx="24" cy="28" r="5" fill="%H"/><circle cx="96" cy="28" r="5" fill="%A"/>',
            '<rect x="20" y="20" width="10" height="46" fill="%H"/><rect x="90" y="20" width="10" height="46" fill="%A"/><rect x="20" y="16" width="80" height="6" fill="%H" opacity=".5"/><path d="M60 82 L60 50 M60 50 L34 30 M60 50 L86 30" stroke="%H" stroke-width="4" fill="none"/><circle cx="60" cy="50" r="5" fill="%A"/>'
        ]
    };

    function objectSvg(kind, variant, accent) {
        const set = OBJECT_ART[kind];
        if (!set) return '';
        const index = Math.abs(parseInt(variant, 10) || 0) % set.length;
        const body = set[index].split('%H').join('#cfdcd9').split('%D').join('#17343a').split('%A').join(accent);
        return '<svg viewBox="0 0 120 90" xmlns="http://www.w3.org/2000/svg">' + body + '</svg>';
    }

    function renderArt(markup, caption) {
        const art = document.getElementById('scene-object');
        if (art.dataset.art !== markup) {
            art.dataset.art = markup;
            art.innerHTML = markup;
        }
        setText('scene-subject', caption);
    }

    function shipArt() {
        const ship = snapshot?.ship;
        return ship && ship.name ? shipSvg(ship.hullStyle, getAccent()).replace('viewBox="0 0 64 32"', 'viewBox="-4 -20 72 72"') : '';
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
            line.textContent = payout.amount + ' ' + payout.pointTypeName + (payout.isPaid ? ' banked' : ' banking');
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
        const scene = tracker.derive(snapshot);
        root.dataset.scene = scene.name;
        if (!snapshot || (snapshot.status !== 'Boarding' && snapshot.status !== 'EventOpen')) {
            const terminal = snapshot && (snapshot.status === 'Returned' || snapshot.status === 'Aborted');
            const latest = snapshot?.latestResult;
            if (!scene.eventVisible) {
                root.hidden = true;
                return;
            }

            root.hidden = false;
            document.getElementById('boarding-panel').hidden = true;
            document.getElementById('event-panel').hidden = true;
            document.getElementById('result-panel').hidden = false;
            document.getElementById('event-description').hidden = true;
            const shipName = snapshot?.ship?.name || 'Starship Expedition';
            setText('event-kicker', terminal ? 'VOYAGE COMPLETE' : 'EVENT RESOLVED');
            setText('event-title', terminal ? shipName : latest.title);
            setText('event-timer', terminal ? 'END' : latest.outcome === 'Success' ? '\u2713' : latest.outcome === 'Failure' ? '\u2717' : '\u2013');
            document.getElementById('ring-fg').style.strokeDashoffset = '0';
            setText('event-description', '');
            const banner = document.getElementById('result-banner');
            const outcome = terminal ? snapshot.status : (latest.outcome || '');
            banner.dataset.outcome = outcome;
            banner.textContent = terminal ? (outcome === 'Aborted' ? 'Voyage aborted' : 'Voyage complete') : outcome;
            banner.hidden = !banner.textContent;
            setText('result-choice', terminal ? '' : latest.choice);
            setText('result-detail', terminal ? '' : latest.sector + ' \u00b7 Leg ' + latest.sequence);
            document.getElementById('result-choice').hidden = terminal;
            document.getElementById('result-detail').hidden = terminal;
            setTextAnimated('result-story', latest.story || '');
            document.getElementById('result-story').hidden = !latest.story;
            renderBankedAmounts(latest);
            renderResultLines(latest);
            renderTimeline(terminal ? snapshot.timeline : []);
            const objectMarkup = !terminal && latest.objectKind ? objectSvg(latest.objectKind, latest.objectVariant, getAccent()) : '';
            renderArt(objectMarkup || shipArt(), !terminal && latest.subject ? latest.subject : shipName);
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
            renderArt(shipArt(), snapshot.ship?.name || '');
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
            setText('event-sector', ((currentEvent.path || []).length ? 'Path: ' + currentEvent.path.join(' > ') : currentEvent.sector) +
                ' · Default: ' + currentEvent.defaultChoiceId);
            renderArt(objectSvg(currentEvent.objectKind, currentEvent.objectVariant, getAccent()) || shipArt(), currentEvent.subject || '');
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