(function () {
    'use strict';

    const view = document.body.dataset.starshipView;
    const root = document.getElementById('starship-widget');
    let socket;
    let snapshot = null;
    let hasSnapshot = false;
    let lastAppliedTimestamp = -Infinity;
    let resultKey = '';
    let resultVisibleUntil = 0;

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
        const remaining = Math.max(0, Math.ceil((Date.parse(timestamp) - Date.now()) / 1000));
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
        const fill = document.getElementById('progress-fill');
        fill.style.width = legCount ? Math.min(100, (completed / legCount) * 100) + '%' : '0%';

        let timer = 'Awaiting launch';
        if (status === 'Boarding') timer = 'Takeoff in ' + formatTimeRemaining(snapshot.boardingClosesAtUtc);
        else if (status === 'EventOpen') timer = 'Vote closes in ' + formatTimeRemaining(snapshot.currentEvent?.closesAtUtc);
        else if (status === 'Cruising') timer = 'Next signal ' + formatTimeRemaining(snapshot.nextEventAtUtc);
        else if (status === 'Returned') timer = 'Voyage complete';
        else if (status === 'Aborted') timer = 'Voyage aborted';
        setText('timing', timer);

        const forced = (snapshot?.statusForcedUntilUtc ? Date.parse(snapshot.statusForcedUntilUtc) : 0) > Date.now();
        const terminal = status === 'Returned' || status === 'Aborted';
        const endedLongAgo = terminal && Date.now() > Date.parse(snapshot.updatedAtUtc) + 30000;
        root.dataset.faded = !forced && (status === 'Cruising' || endedLongAgo) ? 'true' : 'false';
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
            const key = latest ? latest.sequence + ':' + latest.choice : '';
            if (key && key !== resultKey) {
                resultKey = key;
                resultVisibleUntil = Date.now() + 14000;
            }
            if (!latest || Date.now() > resultVisibleUntil || (terminal && Date.now() > Date.parse(snapshot.updatedAtUtc) + 20000)) {
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

    render();
    loadInitialState();
    connect();
    window.setInterval(render, 250);
}());