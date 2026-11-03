(function (global) {
    'use strict';

    global.FireworksEventHandler = {
        createLaunchQueue: function (launch, getDelay, timerApi) {
            const queue = [];
            let timerPending = false;
            const timers = timerApi || global;

            function scheduleNext() {
                if (timerPending || queue.length === 0) return;

                const delay = Math.max(0, Number(getDelay(queue[0])) || 0);
                timerPending = true;
                timers.setTimeout(function () {
                    timerPending = false;
                    try {
                        if (queue.length > 0 && launch(queue[0]) !== false) {
                            queue.shift();
                        }
                    } catch (error) {
                        if (queue.length > 0) queue.shift();
                        if (global.console && typeof global.console.error === 'function') {
                            global.console.error('Fireworks launch failed', error);
                        }
                    } finally {
                        scheduleNext();
                    }
                }, delay);
            }

            return {
                enqueue: function (entry) {
                    queue.push(entry);
                    scheduleNext();
                },
                getPendingCount: function () {
                    return queue.length;
                }
            };
        },

        handle: function (message, launch) {
            if (!message || typeof launch !== 'function') return false;

            if (message.event && message.event.type === 'Fireworks') {
                const data = message.data || {};
                launch({
                    count: data.count,
                    delayMs: data.delayMs,
                    shell: data.shell || '',
                    size: data.size || ''
                });
                return true;
            }

            // LEGACY: Keep the old action-name trigger during the migration period. Do not add behavior here.
            if (message.event && message.event.type && message.data && message.data.name === 'Fireworks') {
                launch({ count: 1, delayMs: 0, shell: '', size: '' });
                return true;
            }

            return false;
        }
    };
}(window));