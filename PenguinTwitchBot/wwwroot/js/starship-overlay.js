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
            command.textContent = '!starship vote ' + choice.choiceId;
            const votes = document.createElement('span');
            votes.className = 'choice-votes';
            const count = currentEvent.voteCounts?.[choice.choiceId] ?? 0;
            votes.textContent = count + (count === 1 ? ' vote' : ' votes');
            row.append(label, command, votes);
            list.appendChild(row);
        }
    }

    function renderEvent() {
        if (!snapshot || (snapshot.status !== 'Boarding' && snapshot.status !== 'EventOpen')) {
            const terminal = snapshot && (snapshot.status === 'Returned' || snapshot.status === 'Aborted');
            const latest = snapshot?.latestResult;
            const key = latest ? latest.sequence + ':' + latest.choice : '';
            if (key && key !== resultKey) {
                resultKey = key;
                resultVisibleUntil = Date.now() + 9000;
            }
            if (!latest || Date.now() > resultVisibleUntil || (terminal && Date.now() > Date.parse(snapshot.updatedAtUtc) + 12000)) {
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