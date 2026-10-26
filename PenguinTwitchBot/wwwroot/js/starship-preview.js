// Preview mode for the streamer editor: shows sample voyage data and applies the appearance posted by the editor page.
(function () {
    'use strict';

    const params = new URLSearchParams(window.location.search);
    const sample = params.get('sample') || 'event';
    let appearance = null;

    function build() {
        const now = Date.now();
        const iso = function (seconds) { return new Date(now + seconds * 1000).toISOString(); };
        const ship = { name: 'Rockhopper Voyager', hullStyle: 'cruiser', accentColor: '#f4bd68', variant: 0 };
        const profile = {
            pointTypeName: 'Salvage Credits', successChancePercent: 80, commonWeightPercent: 60, commonMinimum: 5, commonMaximum: 12,
            uncommonWeightPercent: 30, uncommonMinimum: 15, uncommonMaximum: 25, rareWeightPercent: 10, rareMinimum: 30, rareMaximum: 50,
            riskLossPercent: 25, maximumRiskLossPerEvent: 40, maximumPerEvent: 100, maximumUnbankedPerVoyage: 500,
            tiers: [
                { name: 'Common', chancePercent: 60, rewardText: '5-12 Credits' },
                { name: 'Uncommon', chancePercent: 30, rewardText: '15-25 Credits' },
                { name: 'Rare', chancePercent: 10, rewardText: '30-50 Credits' }
            ]
        };
        const choice = function (id, label, number, extra) {
            return Object.assign({ choiceId: id, label: label, isRisky: false, number: number, hint: '' }, extra || {});
        };
        const base = {
            status: 'Cruising', updatedAtUtc: iso(0), serverNowUtc: iso(0), currentLeg: 2, legCount: 6,
            currentSector: 'Ternion Belt', currentRoute: 'Frontier Route', crewCount: 7, boardingClosesAtUtc: null,
            nextEventAtUtc: iso(600), currentEvent: null, latestResult: null, ship: ship, stationLegs: [3, 5], activity: [], timeline: [],
            shipSystems: { hull: 72, maximumHull: 100, supplies: 43, pendingSuccessBonusPercent: 0, activeSuccessBonusPercent: 0 }
        };

        if (sample === 'status') {
            return Object.assign(base, { statusForcedUntilUtc: iso(3600) });
        }

        if (sample === 'boarding') {
            return Object.assign(base, { status: 'Boarding', currentLeg: 0, boardingClosesAtUtc: iso(60) });
        }

        if (sample === 'result') {
            return Object.assign(base, {
                latestResult: {
                    sequence: 3, title: 'Derelict Salvage', sector: 'Ternion Belt', choice: 'Recover salvage', resolvedAtUtc: iso(0),
                    bankedAmounts: [], outcome: 'Success', rarity: 'Rare',
                    lines: ['SUCCESS - Rare find.', 'Earned 16 Salvage Credits average per eligible crew member.'],
                    story: 'Pilot boards the ISV Quiet Meridian and starts cutting toward the cargo hold. The hatch gives way and the crew hauls out an intact cryo-vault.',
                    subject: 'the ISV Quiet Meridian', objectKind: 'wreck', objectVariant: 1, objectRoll: 1
                }
            });
        }

        if (sample === 'end') {
            return Object.assign(base, {
                status: 'Returned', currentLeg: 6, crewCount: 9,
                latestResult: {
                    sequence: 6, title: 'Navigation Hazard', sector: 'Vela Expanse', choice: 'Maneuver around it', resolvedAtUtc: iso(0),
                    bankedAmounts: [{ pointTypeId: 1, pointTypeName: 'Salvage Credits', amount: 140, averagePerCrewMember: 16, isPaid: true }], outcome: 'Success', rarity: 'Common',
                    lines: ['Voyage completed: 6 events, 3 successful, 1 failed, 9 crew.', 'Earned 23 Salvage Credits average per eligible crew member.'],
                    story: '', subject: '', objectKind: '', objectVariant: 0, objectRoll: 0
                },
                timeline: [
                    { sequence: 1, title: 'Choose a Route', outcome: '', rarity: '', objectKind: 'fork' },
                    { sequence: 2, title: 'Derelict Salvage', outcome: 'Success', rarity: 'Rare', objectKind: 'wreck' },
                    { sequence: 3, title: 'Space Station', outcome: '', rarity: '', objectKind: 'station' },
                    { sequence: 4, title: 'Unidentified Signal', outcome: 'Failure', rarity: 'Uncommon', objectKind: 'beacon' }
                ]
            });
        }

        return Object.assign(base, {
            status: 'EventOpen',
            currentEvent: {
                sequence: 3, eventType: 'SalvageSite', title: 'Derelict Salvage', sector: 'Ternion Belt',
                description: 'A damaged vessel, the ISV Quiet Meridian, is drifting near the route. Scan it, secure the area, then recover the salvage - or pass.',
                openedAtUtc: iso(-25), closesAtUtc: iso(35), subject: 'the ISV Quiet Meridian', objectKind: 'wreck', objectVariant: 1, objectRoll: 1,
                choices: [
                    choice('scan', 'Scan the wreck', 1, { continuesEvent: true, revealsRarity: true }),
                    choice('secure', 'Secure the area', 2, { continuesEvent: true, successModifierPercent: 20, rewardMultiplierPercent: 80, riskMultiplierPercent: 50 }),
                    choice('salvage', 'Recover salvage', 3),
                    choice('pass', 'Pass it by', 4, { endsWithoutReward: true })
                ],
                defaultChoiceId: 'pass', voteCounts: { scan: 3, secure: 1, salvage: 2 }, stepNumber: 1, path: [], revealedRarity: null, stepNote: '',
                shipDisclosure: { choices: [
                    { choiceId: 'scan', available: true, successChancePercent: 80, suppliesCost: 3 },
                    { choiceId: 'secure', available: true, successChancePercent: 100, suppliesCost: 4 },
                    { choiceId: 'salvage', available: true, successChancePercent: 80 },
                    { choiceId: 'pass', available: true }
                ] },
                effectiveSuccessChancePercent: 80, rewardMultiplierPercent: 100, riskMultiplierPercent: 100, rewardDisclosure: profile
            }
        });
    }

    function push() {
        window.StarshipOverlay.applyEnvelope({ type: 'expedition_state', snapshot: build(), appearance: appearance });
    }

    window.addEventListener('message', function (event) {
        if (event.origin !== window.location.origin) return;
        if (event.data && event.data.type === 'starship_preview_appearance') {
            appearance = event.data.appearance;
            push();
        }
    });

    push();
    window.setInterval(push, 15000);
    if (window.parent !== window) window.parent.postMessage({ type: 'starship_preview_ready' }, window.location.origin);
}());
