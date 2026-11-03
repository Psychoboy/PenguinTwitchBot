(function (global) {
    'use strict';

    global.FireworksEventHandler = {
        getLaunchDelay: function (settings, elapsedSinceLaunchMs, configuredMinGapMs) {
            const configuredGap = settings.isLegacy
                ? Math.max(0, Number(configuredMinGapMs) - Math.max(0, Number(elapsedSinceLaunchMs) || 0))
                : 0;
            return Math.max(Number(settings.delayBeforeMs) || 0, configuredGap, 0);
        },

        getLaunchMinimumGap: function (settings, configuredMinGapMs) {
            return settings.isLegacy ? Math.max(0, Number(configuredMinGapMs) || 0) : 0;
        },

        createLaunchQueue: function (launch, getDelay, timerApi, onChange) {
            const queue = [];
            let timerPending = false;
            const timers = timerApi && typeof timerApi.setTimeout === 'function' ? timerApi : global;

            function notifyChange() {
                if (typeof onChange === 'function') onChange(queue.length);
            }

            function scheduleNext() {
                if (timerPending || queue.length === 0) return;

                const delay = Math.max(0, Number(getDelay(queue[0])) || 0);
                timerPending = true;
                timers.setTimeout(function () {
                    timerPending = false;
                    try {
                        if (queue.length > 0 && launch(queue[0]) !== false) {
                            queue.shift();
                            notifyChange();
                        }
                    } catch (error) {
                        if (queue.length > 0) {
                            queue.shift();
                            notifyChange();
                        }
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
                    notifyChange();
                    scheduleNext();
                },
                enqueueJob: function (settings, maxPending) {
                    const count = Math.max(1, Math.min(100, parseInt(settings.count, 10) || 1));
                    const startDelayMs = Math.max(0, Math.min(60000, parseInt(settings.startDelayMs, 10) || 0));
                    const delayMs = Math.max(0, Math.min(60000, parseInt(settings.delayMs, 10) || 0));
                    const delayVarianceMs = Math.max(0, Math.min(60000, parseInt(settings.delayVarianceMs, 10) || 0));
                    const isLegacy = settings.isLegacy === true;
                    const jobId = isLegacy ? null : String(settings.jobId || '');
                    if (!isLegacy && !jobId) return { accepted: 0, rejected: count };

                    const managedPending = queue.reduce(function (total, entry) {
                        return total + (entry.jobId ? 1 : 0);
                    }, 0);
                    const available = isLegacy
                        ? count
                        : Math.max(0, Math.max(1, Number(maxPending) || 1) - managedPending);
                    const accepted = Math.min(count, available);
                    for (let index = 0; index < accepted; index++) {
                        const minimumDelayMs = Math.max(0, delayMs - delayVarianceMs);
                        const maximumDelayMs = Math.min(60000, delayMs + delayVarianceMs);
                        const randomizedDelayMs = minimumDelayMs + Math.floor(Math.random() * (maximumDelayMs - minimumDelayMs + 1));
                        queue.push({
                            jobId: jobId,
                            isLegacy: isLegacy,
                            shell: settings.shell || '',
                            size: settings.size,
                            delayBeforeMs: index === 0 ? startDelayMs : randomizedDelayMs
                        });
                    }
                    if (accepted > 0) {
                        notifyChange();
                        scheduleNext();
                    }
                    return { accepted: accepted, rejected: count - accepted };
                },
                clearManaged: function (jobId) {
                    let removed = 0;
                    for (let index = queue.length - 1; index >= 0; index--) {
                        if (queue[index].jobId && (!jobId || queue[index].jobId === jobId)) {
                            queue.splice(index, 1);
                            removed++;
                        }
                    }
                    if (removed > 0) notifyChange();
                    return removed;
                },
                clear: function () {
                    const pendingCount = queue.length;
                    queue.length = 0;
                    if (pendingCount > 0) notifyChange();
                    return pendingCount;
                },
                getManagedPendingCount: function () {
                    return queue.reduce(function (total, entry) {
                        return total + (entry.jobId ? 1 : 0);
                    }, 0);
                },
                getJobs: function () {
                    const jobs = [];
                    queue.forEach(function (entry) {
                        if (!entry.jobId) return;
                        const existing = jobs.find(function (job) { return job.jobId === entry.jobId; });
                        if (existing) existing.pending++;
                        else jobs.push({ jobId: entry.jobId, pending: 1 });
                    });
                    return jobs;
                },
                getPendingCount: function () {
                    return queue.length;
                }
            };
        },

        handle: function (message, launch, cancelPending, widgetId) {
            if (!message || typeof launch !== 'function') return false;

            if (message.event && (message.event.type === 'FireworksClearQueue' || message.event.type === 'FireworksCancelJob')) {
                const control = message.data || {};
                if (control.widgetId && control.widgetId !== widgetId) return true;
                if (typeof cancelPending === 'function') {
                    cancelPending(message.event.type === 'FireworksCancelJob' ? control.jobId : null);
                }
                return true;
            }

            if (message.event && message.event.type === 'Fireworks') {
                const data = message.data || {};
                if (data.widgetId && data.widgetId !== widgetId) return true;
                launch({
                    count: data.count,
                    startDelayMs: data.startDelayMs ?? 0,
                    delayMs: data.delayMs,
                    delayVarianceMs: data.delayVarianceMs ?? 0,
                    shell: data.shell || '',
                    size: data.size || '',
                    jobId: data.jobId || 'client-' + Date.now() + '-' + Math.random().toString(36).slice(2)
                });
                return true;
            }

            // LEGACY: Keep the old action-name trigger during the migration period. Do not add behavior here.
            if (message.event && message.event.type && message.data && message.data.name === 'Fireworks') {
                launch({ count: 1, delayMs: 0, shell: '', size: '', isLegacy: true });
                return true;
            }

            return false;
        }
    };
}(window));