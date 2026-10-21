(function (global) {
    'use strict';

    const RESULT_WINDOW_MS = 14000;
    const EVENT_END_WINDOW_MS = 20000;
    const STATUS_END_WINDOW_MS = 30000;
    const CRUISE_HOLD_MS = 30000;
    const STEP_WINDOW_MS = 2500;
    const REPLAY_WINDOW_MS = 10000;

    // Scenes: Idle, Boarding, Cruising, EventVoting, StepTransition, Result, VoyageEnd (Resolving is reserved for the reveal animation).
    function createTracker() {
        let offsetMs = 0;
        let lastFeedId = 0;
        let seenFeed = false;
        let expeditionId = null;
        let resultKey = '';
        let resultUntil = 0;
        let stepKey = '';
        let stepUntil = 0;

        function now() {
            return Date.now() + offsetMs;
        }

        function parse(value) {
            const parsed = value ? Date.parse(value) : NaN;
            return Number.isFinite(parsed) ? parsed : 0;
        }

        // Returns activity items that should be animated now (each id is returned once).
        function update(snapshot) {
            const fresh = [];
            if (!snapshot) return fresh;

            const nextExpeditionId = snapshot.expeditionId ?? null;
            if (nextExpeditionId !== expeditionId) {
                expeditionId = nextExpeditionId;
                lastFeedId = 0;
                seenFeed = false;
            }

            const serverNow = parse(snapshot.serverNowUtc);
            if (serverNow) offsetMs = serverNow - Date.now();

            const latest = snapshot.latestResult;
            const key = latest ? latest.sequence + ':' + latest.choice + ':' + (latest.resolvedAtUtc || '') : '';
            if (key && key !== resultKey) {
                resultKey = key;
                const resolvedAt = parse(latest.resolvedAtUtc);
                resultUntil = (resolvedAt || now()) + RESULT_WINDOW_MS;
            }

            const event = snapshot.currentEvent;
            if (event) {
                const eventPart = String(event.sequence);
                const stepPart = String(event.stepNumber ?? 1);
                if (stepKey && stepKey.split(':')[0] === eventPart && stepKey.split(':')[1] !== stepPart) {
                    stepUntil = now() + STEP_WINDOW_MS;
                }
                stepKey = eventPart + ':' + stepPart;
            }

            const items = (snapshot.activity || []).slice().sort(function (a, b) { return a.id - b.id; });
            for (const item of items) {
                if (item.id <= lastFeedId) continue;
                lastFeedId = item.id;
                const age = now() - parse(item.atUtc);
                if (seenFeed || age <= REPLAY_WINDOW_MS) fresh.push(item);
            }
            seenFeed = true;
            return fresh;
        }

        function derive(snapshot) {
            const t = now();
            const status = snapshot?.status ?? 'Inactive';
            const terminal = status === 'Returned' || status === 'Aborted';
            const updatedAt = parse(snapshot?.updatedAtUtc);
            const forced = parse(snapshot?.statusForcedUntilUtc) > t;
            const resolvedAt = parse(snapshot?.latestResult?.resolvedAtUtc);
            const holding = status === 'Cruising' && resolvedAt > 0 && t < resolvedAt + CRUISE_HOLD_MS;
            const resultVisible = !!snapshot?.latestResult && t <= resultUntil &&
                !(terminal && t > updatedAt + EVENT_END_WINDOW_MS);

            let name = 'Idle';
            let eventVisible = false;
            if (status === 'Boarding') {
                name = 'Boarding';
                eventVisible = true;
            } else if (status === 'EventOpen') {
                name = t <= stepUntil ? 'StepTransition' : 'EventVoting';
                eventVisible = true;
            } else if (resultVisible) {
                name = terminal ? 'VoyageEnd' : 'Result';
                eventVisible = true;
            } else if (status === 'Cruising') {
                name = 'Cruising';
            }

            return {
                name: name,
                eventVisible: eventVisible,
                forced: forced,
                statusFaded: !forced && !holding && (status === 'Cruising' || (terminal && t > updatedAt + STATUS_END_WINDOW_MS))
            };
        }

        return { now: now, update: update, derive: derive };
    }

    global.StarshipScene = { createTracker: createTracker };
}(window));
