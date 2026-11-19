(function () {
    'use strict';

    const params = new URLSearchParams(window.location.search);
    const profileParam = params.get('profile') ? params.get('profile').trim() : '';
    const sampleParam = params.get('sample') || '';
    const autoStartParam = params.get('autoStart');
    const previewMode = params.get('preview') === '1';
    const overlayKey = params.get('key') || '';
    const windowModeParam = params.get('windowMode') || '';
    const lookbackParam = params.get('lookbackMinutes') || '';
    const speedParam = Number(params.get('speed'));

    const rootEl = document.getElementById('credits-root');
    const backdropEl = document.getElementById('credits-backdrop');
    const bgImageEl = document.getElementById('credits-bg-image');
    const bgScrimEl = document.getElementById('credits-bg-scrim');
    const viewportEl = document.getElementById('credits-viewport');
    const stageEl = document.getElementById('credits-stage');
    const trackEl = document.getElementById('credits-track');
    const pagedEl = document.getElementById('credits-paged-container');
    const themeCssEl = document.getElementById('credits-theme-css');
    const profileCssEl = document.getElementById('credits-profile-css');
    const audioEl = document.getElementById('credits-audio');

    const SYSTEM_FONTS = new Set([
        'arial', 'verdana', 'tahoma', 'trebuchet ms', 'segoe ui',
        'georgia', 'times new roman', 'courier new', 'sans-serif',
        'serif', 'monospace', 'impact'
    ]);

    const loadedGoogleFonts = new Set();

    let currentPayload = null;
    let resolvedProfileName = '';
    let activeRequestId = 0;
    let runGeneration = 0;
    let rafId = null;
    let pagedTimerId = null;
    let pagedTransitionTimerId = null;
    let pagedRemainingMs = 0;
    let pagedStartedAt = 0;
    let pagedDeadlineCallback = null;
    let introTimerId = null;
    let outroTimerId = null;
    let audioFadeInterval = null;
    let isRunning = false;
    let isPaused = false;
    let speedMultiplier = Number.isFinite(speedParam) && speedParam > 0
        ? Math.max(0.25, Math.min(10, speedParam))
        : 1.0;
    let scrollY = 0;
    let endScrollY = 0;
    let lastFrameTime = 0;
    let effectiveSpeedPxPerSec = 75;
    let completionSent = false;

    function sanitizeMediaUrl(rawUrl) {
        if (!rawUrl || typeof rawUrl !== 'string') return '';
        const trimmed = rawUrl.trim();
        if (!trimmed) return '';
        if (trimmed.startsWith('/')) return trimmed;
        try {
            const parsed = new URL(trimmed);
            if (parsed.protocol === 'http:' || parsed.protocol === 'https:') {
                return parsed.href;
            }
        } catch (_) {}
        return '';
    }

    function ensureGoogleFontLoaded(fontFamily) {
        if (!fontFamily || typeof fontFamily !== 'string') return;
        const primary = fontFamily.split(',')[0].replace(/['"]/g, '').trim();
        if (!primary || SYSTEM_FONTS.has(primary.toLowerCase())) return;
        if (loadedGoogleFonts.has(primary.toLowerCase())) return;
        loadedGoogleFonts.add(primary.toLowerCase());

        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = 'https://fonts.googleapis.com/css2?family=' +
            encodeURIComponent(primary).replace(/%20/g, '+') +
            ':ital,wght@0,400;0,600;0,700;1,400;1,700&display=swap';
        document.head.appendChild(link);
    }

    function clearTimersAndAnimations() {
        if (rafId !== null) {
            cancelAnimationFrame(rafId);
            rafId = null;
        }
        if (pagedTimerId !== null) {
            clearTimeout(pagedTimerId);
            pagedTimerId = null;
        }
        if (pagedTransitionTimerId !== null) {
            clearTimeout(pagedTransitionTimerId);
            pagedTransitionTimerId = null;
        }
        pagedRemainingMs = 0;
        pagedStartedAt = 0;
        pagedDeadlineCallback = null;
        if (introTimerId !== null) {
            clearTimeout(introTimerId);
            introTimerId = null;
        }
        if (outroTimerId !== null) {
            clearTimeout(outroTimerId);
            outroTimerId = null;
        }
    }

    function schedulePagedDeadline(durationMs, callback, gen) {
        if (pagedTimerId !== null) {
            clearTimeout(pagedTimerId);
            pagedTimerId = null;
        }
        pagedRemainingMs = Math.max(0, durationMs);
        pagedDeadlineCallback = function () {
            if (gen !== runGeneration || !isRunning) return;
            pagedTimerId = null;
            pagedRemainingMs = 0;
            pagedStartedAt = 0;
            pagedDeadlineCallback = null;
            callback();
        };
        if (isPaused) {
            pagedStartedAt = 0;
            return;
        }
        pagedStartedAt = performance.now();
        pagedTimerId = setTimeout(pagedDeadlineCallback, pagedRemainingMs);
    }

    function pausePagedTimer() {
        if (pagedTimerId !== null) {
            clearTimeout(pagedTimerId);
            pagedTimerId = null;
            if (pagedStartedAt > 0) {
                const elapsed = Math.max(0, performance.now() - pagedStartedAt);
                pagedRemainingMs = Math.max(0, pagedRemainingMs - elapsed);
            }
            pagedStartedAt = 0;
        }
    }

    function resumePagedTimer() {
        if (pagedDeadlineCallback && pagedTimerId === null) {
            if (pagedRemainingMs <= 0) {
                const cb = pagedDeadlineCallback;
                pagedDeadlineCallback = null;
                cb();
                return;
            }
            pagedStartedAt = performance.now();
            pagedTimerId = setTimeout(pagedDeadlineCallback, pagedRemainingMs);
        }
    }

    function stopAudio(fadeOutSeconds) {
        if (!audioEl) return;
        if (audioFadeInterval !== null) {
            clearInterval(audioFadeInterval);
            audioFadeInterval = null;
        }
        if (audioEl.paused) return;

        const durationMs = Math.max(0, (Number(fadeOutSeconds) || 0) * 1000);
        if (durationMs <= 50 || audioEl.volume <= 0.01) {
            audioEl.pause();
            audioEl.currentTime = 0;
            return;
        }

        const startVol = audioEl.volume;
        const start = performance.now();
        audioFadeInterval = setInterval(function () {
            const t = Math.min(1, (performance.now() - start) / durationMs);
            audioEl.volume = Math.max(0, startVol * (1 - t));
            if (t >= 1) {
                clearInterval(audioFadeInterval);
                audioFadeInterval = null;
                audioEl.pause();
                audioEl.currentTime = 0;
            }
        }, 40);
    }

    function startAudio(mediaConfig) {
        if (!audioEl || !mediaConfig) return;
        const url = sanitizeMediaUrl(mediaConfig.backgroundAudioUrl);
        if (!url) {
            stopAudio(0);
            return;
        }

        if (previewMode) {
            // Avoid blaring background audio unexpectedly inside the editor preview iframe
            return;
        }

        if (audioFadeInterval !== null) {
            clearInterval(audioFadeInterval);
            audioFadeInterval = null;
        }

        const targetVol = Math.max(0, Math.min(1, Number(mediaConfig.audioVolume ?? 0.5)));
        const fadeInMs = Math.max(0, (Number(mediaConfig.audioFadeInSeconds) || 0) * 1000);

        audioEl.src = url;
        audioEl.loop = mediaConfig.audioLoop !== false;
        audioEl.volume = fadeInMs > 50 ? 0 : targetVol;
        audioEl.currentTime = 0;

        const playPromise = audioEl.play();
        if (playPromise && typeof playPromise.catch === 'function') {
            playPromise.catch(function (err) {
                console.warn('Credits audio autoplay prevented:', err);
            });
        }

        if (fadeInMs > 50) {
            const start = performance.now();
            audioFadeInterval = setInterval(function () {
                const t = Math.min(1, (performance.now() - start) / fadeInMs);
                audioEl.volume = targetVol * t;
                if (t >= 1) {
                    clearInterval(audioFadeInterval);
                    audioFadeInterval = null;
                }
            }, 40);
        }
    }

    function applyThemeAndMedia(payload) {
        const theme = payload.theme || {};
        const media = payload.media || {};
        const playback = payload.playback || {};
        const rootStyle = document.documentElement.style;

        ensureGoogleFontLoaded(theme.fontFamilyHeading);
        ensureGoogleFontLoaded(theme.fontFamilyBody);

        const bgType = (media.backgroundTypeOverride && media.backgroundTypeOverride !== 'InheritTheme')
            ? media.backgroundTypeOverride
            : (theme.backgroundType || 'Gradient');

        const bgColor = media.backgroundColorOverride || theme.backgroundColor || '#07090e';
        const bgGradient = media.backgroundGradientOverride || theme.backgroundGradient || bgColor;
        const bgImageUrl = sanitizeMediaUrl(media.backgroundImageUrlOverride || theme.backgroundImageUrl);

        backdropEl.className = 'credits-backdrop';
        bgImageEl.style.display = 'none';
        bgImageEl.style.backgroundImage = '';
        bgScrimEl.style.background = 'transparent';

        if (bgType === 'Starfield') {
            backdropEl.classList.add('credits-backdrop--starfield');
            rootStyle.setProperty('--credits-bg', '#000000');
        } else if (bgType === 'Transparent') {
            rootStyle.setProperty('--credits-bg', 'transparent');
        } else if (bgType === 'Solid') {
            rootStyle.setProperty('--credits-bg', bgColor);
        } else if (bgType === 'Image' && bgImageUrl) {
            rootStyle.setProperty('--credits-bg', bgColor || '#000000');
            bgImageEl.style.display = 'block';
            bgImageEl.style.backgroundImage = 'url("' + bgImageUrl.replace(/"/g, '%22') + '")';
            const fit = (theme.backgroundImageFit || 'cover').toLowerCase();
            if (fit === 'repeat') {
                bgImageEl.style.backgroundRepeat = 'repeat';
                bgImageEl.style.backgroundSize = 'auto';
            } else {
                bgImageEl.style.backgroundRepeat = 'no-repeat';
                bgImageEl.style.backgroundSize = fit === 'contain' ? 'contain' : 'cover';
            }
            const blurPx = Math.max(0, Number(theme.backgroundBlurPx) || 0);
            bgImageEl.style.filter = blurPx > 0 ? ('blur(' + blurPx + 'px)') : 'none';
            const scrimOpacity = Math.max(0, Math.min(1, Number(theme.backgroundScrimOpacity) || 0));
            if (scrimOpacity > 0) {
                bgScrimEl.style.background = theme.backgroundScrimColor || '#000000';
                bgScrimEl.style.opacity = String(scrimOpacity);
            } else {
                bgScrimEl.style.opacity = '0';
            }
        } else {
            rootStyle.setProperty('--credits-bg', bgGradient || bgColor);
        }

        rootStyle.setProperty('--credits-text', theme.textColor || '#f4f6fa');
        rootStyle.setProperty('--credits-secondary-text', theme.secondaryTextColor || '#b8c2d4');
        rootStyle.setProperty('--credits-accent', theme.accentColor || '#f5c451');
        rootStyle.setProperty('--credits-header-color', theme.headerColor || '#ffffff');
        rootStyle.setProperty('--credits-section-color', theme.sectionTitleColor || '#f5c451');
        rootStyle.setProperty('--credits-text-shadow', theme.textShadow || 'none');
        rootStyle.setProperty('--credits-font-heading', theme.fontFamilyHeading ? ('"' + theme.fontFamilyHeading + '", sans-serif') : 'sans-serif');
        rootStyle.setProperty('--credits-font-body', theme.fontFamilyBody ? ('"' + theme.fontFamilyBody + '", sans-serif') : 'sans-serif');
        rootStyle.setProperty('--credits-header-size', (theme.headerFontSizePx || 64) + 'px');
        rootStyle.setProperty('--credits-subtitle-size', (theme.subtitleFontSizePx || 28) + 'px');
        rootStyle.setProperty('--credits-section-title-size', (theme.sectionTitleFontSizePx || 36) + 'px');
        rootStyle.setProperty('--credits-entry-size', (theme.entryFontSizePx || 26) + 'px');
        rootStyle.setProperty('--credits-detail-size', (theme.detailFontSizePx || 18) + 'px');
        rootStyle.setProperty('--credits-footer-size', (theme.footerFontSizePx || 42) + 'px');
        rootStyle.setProperty('--credits-max-width', (theme.containerMaxWidthPx || 960) + 'px');
        rootStyle.setProperty('--credits-section-gap', (theme.sectionGapPx ?? 56) + 'px');
        rootStyle.setProperty('--credits-entry-gap', (theme.entryGapPx ?? 14) + 'px');
        rootStyle.setProperty('--credits-avatar-size', (theme.avatarSizePx || 40) + 'px');
        rootStyle.setProperty('--credits-edge-fade', (theme.edgeFadeHeightPct ?? 12) + '%');
        rootStyle.setProperty('--credits-perspective', (theme.crawlPerspectivePx || 360) + 'px');
        rootStyle.setProperty('--credits-tilt-deg', (theme.crawlTiltDegrees || 25) + 'deg');
        rootStyle.setProperty('--credits-crawl-fade-top', (theme.crawlFadeTopPct ?? 30) + '%');

        const borderWidth = Number(theme.avatarBorderWidthPx ?? 2);
        const borderColor = theme.avatarBorderColor || 'transparent';
        rootStyle.setProperty('--credits-avatar-border', borderWidth > 0 ? (borderWidth + 'px solid ' + borderColor) : 'none');

        const align = (theme.textAlign || 'center').toLowerCase();
        rootEl.style.textAlign = align;
        rootEl.classList.remove('credits-align-left', 'credits-align-center', 'credits-align-right', 'credits-heading-italic', 'credits-root--crawl3d', 'credits-root--paged');
        rootEl.classList.add('credits-align-' + (align === 'left' || align === 'right' ? align : 'center'));
        if (theme.headingItalic) {
            rootEl.classList.add('credits-heading-italic');
        }

        const animMode = playback.animationMode || theme.animationMode || 'SmoothScroll';
        if (animMode === 'Crawl3D') {
            rootEl.classList.add('credits-root--crawl3d');
        } else if (animMode === 'PagedSlideshow') {
            rootEl.classList.add('credits-root--paged');
        }

        viewportEl.classList.toggle('credits-viewport--fade', (theme.edgeFadeHeightPct ?? 12) > 0 && animMode !== 'Crawl3D');

        themeCssEl.textContent = theme.customCss || '';
        profileCssEl.textContent = payload.profileCustomCss || '';
    }

    function createSilhouetteSvg() {
        const ns = 'http://www.w3.org/2000/svg';
        const svg = document.createElementNS(ns, 'svg');
        svg.setAttribute('viewBox', '0 0 24 24');
        svg.setAttribute('aria-hidden', 'true');
        const head = document.createElementNS(ns, 'circle');
        head.setAttribute('cx', '12');
        head.setAttribute('cy', '8');
        head.setAttribute('r', '4');
        const body = document.createElementNS(ns, 'path');
        body.setAttribute('d', 'M4 20c0-4 3.6-6.5 8-6.5s8 2.5 8 6.5');
        svg.appendChild(head);
        svg.appendChild(body);
        return svg;
    }

    function buildAvatarElement(entry, section, theme) {
        const fallbackMode = theme.avatarFallbackMode || 'ColoredSilhouette';
        const sizePx = section.avatarSizePx || theme.avatarSizePx || 40;
        const shape = (theme.avatarShape || 'circle').toLowerCase();
        const safeUrl = sanitizeMediaUrl(entry.avatarUrl);
        const safeFallbackCustom = sanitizeMediaUrl(theme.avatarFallbackImageUrl);

        if (!safeUrl && fallbackMode === 'Hide') {
            return null;
        }

        const wrap = document.createElement('span');
        wrap.className = 'credits-avatar-wrap credits-avatar-wrap--' + (shape === 'rounded' || shape === 'square' ? shape : 'circle');
        wrap.style.width = sizePx + 'px';
        wrap.style.height = sizePx + 'px';

        function appendFallback() {
            wrap.innerHTML = '';
            const fb = document.createElement('span');
            fb.className = 'credits-avatar--fallback';
            fb.style.backgroundColor = entry.avatarColor || '#9146FF';
            if (fallbackMode === 'Initials') {
                const name = (entry.displayName || '?').trim();
                fb.textContent = name ? name.slice(0, 2).toUpperCase() : '?';
            } else {
                fb.appendChild(createSilhouetteSvg());
            }
            wrap.appendChild(fb);
        }

        const imgSrc = safeUrl || (fallbackMode === 'CustomImage' ? safeFallbackCustom : '');
        if (imgSrc) {
            const img = document.createElement('img');
            img.className = 'credits-avatar';
            img.alt = entry.displayName || '';
            img.src = imgSrc;
            img.addEventListener('error', function () {
                if (fallbackMode === 'Hide') {
                    wrap.style.display = 'none';
                } else {
                    appendFallback();
                }
            });
            wrap.appendChild(img);
        } else {
            appendFallback();
        }

        return wrap;
    }

    function buildHeaderNode(header) {
        if (!header || !header.enabled) return null;
        const div = document.createElement('header');
        div.className = 'credits-header';

        const bAvatar = sanitizeMediaUrl(header.broadcasterAvatarUrl);
        if (bAvatar) {
            const img = document.createElement('img');
            img.className = 'credits-broadcaster-avatar';
            img.src = bAvatar;
            img.alt = '';
            div.appendChild(img);
        }

        const logoUrl = sanitizeMediaUrl(header.imageUrl);
        if (logoUrl) {
            const logo = document.createElement('img');
            logo.className = 'credits-header-logo';
            logo.src = logoUrl;
            logo.alt = '';
            if (header.imageMaxHeightPx > 0) {
                logo.style.maxHeight = header.imageMaxHeightPx + 'px';
            }
            div.appendChild(logo);
        }

        if (header.title) {
            const h1 = document.createElement('h1');
            h1.className = 'credits-header-title';
            h1.textContent = header.title;
            div.appendChild(h1);
        }

        if (header.subtitle) {
            const sub = document.createElement('p');
            sub.className = 'credits-header-subtitle';
            sub.textContent = header.subtitle;
            div.appendChild(sub);
        }

        return div;
    }

    function buildFooterNode(footer) {
        if (!footer || !footer.enabled) return null;
        const div = document.createElement('footer');
        div.className = 'credits-footer';

        const logoUrl = sanitizeMediaUrl(footer.imageUrl);
        if (logoUrl) {
            const logo = document.createElement('img');
            logo.className = 'credits-footer-logo';
            logo.src = logoUrl;
            logo.alt = '';
            if (footer.imageMaxHeightPx > 0) {
                logo.style.maxHeight = footer.imageMaxHeightPx + 'px';
            }
            div.appendChild(logo);
        }

        if (footer.title) {
            const h2 = document.createElement('h2');
            h2.className = 'credits-footer-title';
            h2.textContent = footer.title;
            div.appendChild(h2);
        }

        if (Array.isArray(footer.lines) && footer.lines.length > 0) {
            const linesWrap = document.createElement('div');
            linesWrap.className = 'credits-footer-lines';
            footer.lines.forEach(function (lineText) {
                if (!lineText) return;
                const p = document.createElement('p');
                p.className = 'credits-footer-line';
                p.textContent = lineText;
                linesWrap.appendChild(p);
            });
            div.appendChild(linesWrap);
        }

        return div;
    }

    function layoutClassName(layout) {
        switch ((layout || 'SingleColumn').toLowerCase()) {
            case 'grid2': return 'credits-list--grid-2';
            case 'grid3': return 'credits-list--grid-3';
            case 'grid4': return 'credits-list--grid-4';
            case 'inlinepills': return 'credits-list--inline-pills';
            case 'avatarcards': return 'credits-list--avatar-cards';
            case 'classiccast': return 'credits-list--classic-cast';
            default: return 'credits-list--single';
        }
    }

    function buildSectionNode(section, theme) {
        const sec = document.createElement('section');
        sec.className = 'credits-section';
        if (Array.isArray(section.sourceTypes)) {
            section.sourceTypes.forEach(function (src) {
                if (src) {
                    sec.classList.add('credits-section--' + String(src).toLowerCase().replace(/[^a-z0-9_-]/g, ''));
                }
            });
        }
        if (section.customCssClass) {
            section.customCssClass.split(/\s+/).filter(Boolean).forEach(function (cls) {
                sec.classList.add(cls);
            });
        }

        const headerDiv = document.createElement('div');
        headerDiv.className = 'credits-section-header';

        const iconUrl = sanitizeMediaUrl(section.headerImageUrl);
        if (iconUrl) {
            const icon = document.createElement('img');
            icon.className = 'credits-section-icon';
            icon.src = iconUrl;
            icon.alt = '';
            headerDiv.appendChild(icon);
        }

        if (section.title) {
            const h2 = document.createElement('h2');
            h2.className = 'credits-section-title';
            h2.textContent = section.title;
            headerDiv.appendChild(h2);
        }

        if (section.subtitle) {
            const p = document.createElement('p');
            p.className = 'credits-section-subtitle';
            p.textContent = section.subtitle;
            headerDiv.appendChild(p);
        }

        sec.appendChild(headerDiv);

        const entries = Array.isArray(section.entries) ? section.entries : [];
        if (entries.length === 0) {
            if (section.emptyFallbackText) {
                const emptyP = document.createElement('p');
                emptyP.className = 'credits-section-empty';
                emptyP.textContent = section.emptyFallbackText;
                sec.appendChild(emptyP);
            }
            return sec;
        }

        const layout = section.layout || theme.defaultLayout || 'SingleColumn';
        const list = document.createElement('div');
        list.className = 'credits-list ' + layoutClassName(layout);

        entries.forEach(function (entry) {
            const item = document.createElement('div');
            item.className = 'credits-entry';

            if (layout === 'ClassicCast') {
                const roleSpan = document.createElement('span');
                roleSpan.className = 'credits-cast-role';
                roleSpan.textContent = entry.role || entry.detail || '';
                item.appendChild(roleSpan);

                const dashSpan = document.createElement('span');
                dashSpan.className = 'credits-cast-sep';
                dashSpan.textContent = '•';
                item.appendChild(dashSpan);

                const personSpan = document.createElement('span');
                personSpan.className = 'credits-cast-person';
                if (section.showAvatars) {
                    const av = buildAvatarElement(entry, section, theme);
                    if (av) personSpan.appendChild(av);
                }
                const txt = document.createElement('span');
                txt.className = 'credits-entry-text';
                txt.textContent = entry.displayName || entry.formattedText || '';
                personSpan.appendChild(txt);
                item.appendChild(personSpan);
            } else {
                if (section.showAvatars) {
                    const av = buildAvatarElement(entry, section, theme);
                    if (av) item.appendChild(av);
                }
                const txt = document.createElement('span');
                txt.className = 'credits-entry-text';
                txt.textContent = entry.formattedText || entry.displayName || '';
                item.appendChild(txt);
            }

            list.appendChild(item);
        });

        sec.appendChild(list);
        return sec;
    }

    function renderDom(payload) {
        trackEl.innerHTML = '';
        pagedEl.innerHTML = '';

        const headerNode = buildHeaderNode(payload.header);
        if (headerNode) trackEl.appendChild(headerNode);

        (payload.sections || []).forEach(function (section) {
            trackEl.appendChild(buildSectionNode(section, payload.theme || {}));
        });

        const footerNode = buildFooterNode(payload.footer);
        if (footerNode) trackEl.appendChild(footerNode);
    }

    function notifyCompleted(payload) {
        if (previewMode || completionSent || !overlayKey) return;
        completionSent = true;
        fetch('/api/credits/completed', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                overlayKey: overlayKey,
                profileName: payload.profileName || resolvedProfileName || profileParam || 'Default',
                totalEntries: payload.totalEntries || 0,
                totalSections: (payload.sections || []).length
            })
        }).catch(function (err) {
            console.warn('Failed reporting credits completion:', err);
        });
    }

    function handleEndReached(payload, gen) {
        if (gen !== undefined && gen !== runGeneration) return;
        const playback = payload.playback || {};
        const endBehavior = playback.endBehavior || 'FadeOutAndHide';
        const media = payload.media || {};

        if (endBehavior === 'Loop') {
            startPlayback(payload, /* skipIntroDelay */ true);
            return;
        }

        isRunning = false;
        notifyCompleted(payload);

        if (endBehavior === 'FreezeOnEnd') {
            stopAudio(media.audioFadeOutSeconds);
            return;
        }

        if (endBehavior === 'HoldFooter') {
            const holdMs = Math.max(0, (Number(payload.footer?.holdSeconds) || 4) * 1000);
            const expectedGen = runGeneration;
            outroTimerId = setTimeout(function () {
                if (expectedGen !== runGeneration) return;
                outroTimerId = null;
                stopAudio(media.audioFadeOutSeconds);
                rootEl.classList.add('credits-hidden');
            }, holdMs);
            return;
        }

        // Default: FadeOutAndHide
        stopAudio(media.audioFadeOutSeconds);
        rootEl.classList.add('credits-hidden');
    }

    function startScrollPlayback(payload, gen) {
        const viewportH = viewportEl.clientHeight || window.innerHeight || 1080;
        const trackH = trackEl.scrollHeight || trackEl.offsetHeight || 600;
        const playback = payload.playback || {};

        // Position at bottom edge of viewport
        scrollY = viewportH + 40;
        if (playback.endBehavior === 'HoldFooter' || playback.endBehavior === 'FreezeOnEnd') {
            const footerEl = trackEl.querySelector('.credits-footer');
            const footerHeight = footerEl ? footerEl.offsetHeight : 140;
            endScrollY = Math.min(-trackH + Math.max(viewportH * 0.55, footerHeight + 120), -(trackH - viewportH * 0.45));
            if (playback.endBehavior === 'HoldFooter') {
                endScrollY = -(trackH - Math.min(viewportH * 0.65, footerHeight + 160));
            }
        } else {
            endScrollY = -trackH - 80;
        }

        const totalDistance = Math.max(100, scrollY - endScrollY);
        if (playback.speedMode === 'TargetDuration') {
            const targetSec = Math.max(5, Number(playback.targetDurationSeconds) || 60);
            effectiveSpeedPxPerSec = totalDistance / targetSec;
        } else {
            effectiveSpeedPxPerSec = Math.max(10, Number(playback.scrollSpeedPxPerSec) || 75);
        }

        trackEl.style.transform = 'translate3d(0, ' + scrollY.toFixed(1) + 'px, 0)';
        lastFrameTime = performance.now();

        function step(now) {
            if (gen !== runGeneration || !isRunning) return;
            const dt = Math.min(0.1, Math.max(0, (now - lastFrameTime) / 1000));
            lastFrameTime = now;

            if (!isPaused) {
                scrollY -= effectiveSpeedPxPerSec * speedMultiplier * dt;
                trackEl.style.transform = 'translate3d(0, ' + scrollY.toFixed(1) + 'px, 0)';
                if (scrollY <= endScrollY) {
                    handleEndReached(payload, gen);
                    return;
                }
            }

            rafId = requestAnimationFrame(step);
        }

        rafId = requestAnimationFrame(step);
    }

    function buildPagedSectionSlides(section, theme, availableH) {
        const fullNode = buildSectionNode(section, theme);
        const listEl = fullNode.querySelector('.credits-list');
        if (!listEl || listEl.children.length <= 1) {
            return [fullNode];
        }

        const measureWrapper = document.createElement('div');
        measureWrapper.className = 'credits-page-slide';
        measureWrapper.style.visibility = 'hidden';
        measureWrapper.style.pointerEvents = 'none';
        measureWrapper.appendChild(fullNode);
        pagedEl.innerHTML = '';
        pagedEl.appendChild(measureWrapper);

        if (Math.max(measureWrapper.scrollHeight, fullNode.scrollHeight, fullNode.offsetHeight) <= availableH) {
            measureWrapper.removeChild(fullNode);
            pagedEl.innerHTML = '';
            return [fullNode];
        }

        const headerEl = fullNode.querySelector('.credits-section-header');
        const entryNodes = Array.from(listEl.children);
        listEl.innerHTML = '';

        function createSectionShell() {
            const shell = fullNode.cloneNode(false);
            if (headerEl) {
                shell.appendChild(headerEl.cloneNode(true));
            }
            const shellList = listEl.cloneNode(false);
            shell.appendChild(shellList);
            return { sectionEl: shell, listEl: shellList };
        }

        const sectionSlides = [];
        let current = { sectionEl: fullNode, listEl: listEl };

        function exceedsAvailableHeight() {
            return Math.max(
                measureWrapper.scrollHeight,
                current.sectionEl.scrollHeight,
                current.sectionEl.offsetHeight
            ) > availableH;
        }

        for (let i = 0; i < entryNodes.length; i++) {
            const entryNode = entryNodes[i];
            current.listEl.appendChild(entryNode);

            if (current.listEl.children.length > 1 && exceedsAvailableHeight()) {
                current.listEl.removeChild(entryNode);
                measureWrapper.removeChild(current.sectionEl);
                sectionSlides.push(current.sectionEl);

                current = createSectionShell();
                measureWrapper.appendChild(current.sectionEl);
                current.listEl.appendChild(entryNode);
            }
        }

        if (current.listEl.children.length > 0) {
            if (current.sectionEl.parentNode === measureWrapper) {
                measureWrapper.removeChild(current.sectionEl);
            }
            sectionSlides.push(current.sectionEl);
        }

        pagedEl.innerHTML = '';
        return sectionSlides.length > 0 ? sectionSlides : [fullNode];
    }

    function startPagedPlayback(payload, gen) {
        const slides = [];
        const headerNode = buildHeaderNode(payload.header);
        if (headerNode) slides.push(headerNode);

        const computedStyle = window.getComputedStyle(pagedEl);
        const padY = (parseFloat(computedStyle.paddingTop) || 0) + (parseFloat(computedStyle.paddingBottom) || 0);
        const availableH = Math.max(120, (pagedEl.clientHeight || window.innerHeight || 1080) - padY);

        (payload.sections || []).forEach(function (section) {
            const sectionSlides = buildPagedSectionSlides(section, payload.theme || {}, availableH);
            sectionSlides.forEach(function (slideNode) {
                slides.push(slideNode);
            });
        });

        const footerNode = buildFooterNode(payload.footer);
        if (footerNode) slides.push(footerNode);

        if (slides.length === 0) {
            if ((payload.playback?.endBehavior || 'FadeOutAndHide') === 'Loop') {
                stopPlayback();
            } else {
                handleEndReached(payload, gen);
            }
            return;
        }

        const baseMs = Math.max(1500, (Number(payload.playback?.pageDurationSeconds) || 6) * 1000);
        let idx = 0;

        function showSlide(i) {
            if (gen !== runGeneration || !isRunning) return;
            if (i >= slides.length) {
                handleEndReached(payload, gen);
                return;
            }

            pagedEl.innerHTML = '';
            const wrapper = document.createElement('div');
            wrapper.className = 'credits-page-slide';
            wrapper.appendChild(slides[i]);
            pagedEl.appendChild(wrapper);

            requestAnimationFrame(function () {
                if (gen !== runGeneration || !isRunning) return;
                wrapper.classList.add('credits-page-slide--active');
            });

            const durationMs = Math.max(1000, baseMs / speedMultiplier);
            schedulePagedDeadline(durationMs, function () {
                if (gen !== runGeneration || !isRunning) return;
                wrapper.classList.remove('credits-page-slide--active');
                schedulePagedDeadline(450, function () {
                    if (gen !== runGeneration || !isRunning) return;
                    idx += 1;
                    showSlide(idx);
                }, gen);
            }, gen);
        }

        showSlide(0);
    }

    function startPlayback(payload, skipIntroDelay) {
        if (!payload) return;
        activeRequestId++;
        const gen = ++runGeneration;
        clearTimersAndAnimations();
        currentPayload = payload;
        if (payload.profileName) {
            resolvedProfileName = String(payload.profileName).trim();
        }
        isPaused = false;
        isRunning = true;
        completionSent = false;

        applyThemeAndMedia(payload);
        renderDom(payload);
        rootEl.classList.remove('credits-hidden');

        if (!skipIntroDelay) {
            startAudio(payload.media);
        }

        const introMs = skipIntroDelay ? 0 : Math.max(0, (Number(payload.header?.introDelaySeconds) || 0) * 1000);
        const animMode = payload.playback?.animationMode || payload.theme?.animationMode || 'SmoothScroll';

        function beginMotion() {
            introTimerId = null;
            if (gen !== runGeneration || !isRunning) return;
            if (animMode === 'PagedSlideshow') {
                startPagedPlayback(payload, gen);
            } else {
                startScrollPlayback(payload, gen);
            }
        }

        if (introMs > 0) {
            // Put track off-screen cleanly during intro fade-in
            trackEl.style.transform = 'translate3d(0, 100vh, 0)';
            introTimerId = setTimeout(beginMotion, introMs);
        } else {
            beginMotion();
        }
    }

    function stopPlayback() {
        activeRequestId++;
        runGeneration++;
        clearTimersAndAnimations();
        isRunning = false;
        isPaused = false;
        stopAudio(currentPayload?.media?.audioFadeOutSeconds || 1.0);
        rootEl.classList.add('credits-hidden');
    }

    async function fetchAndPrepare(startImmediately, overrideSample) {
        const reqId = ++activeRequestId;
        try {
            const url = new URL('/api/credits/render', window.location.origin);
            if (profileParam) {
                url.searchParams.set('profile', profileParam);
            }
            const effSample = overrideSample !== undefined ? overrideSample : sampleParam;
            if (effSample) url.searchParams.set('sample', effSample);
            if (windowModeParam) url.searchParams.set('windowMode', windowModeParam);
            if (lookbackParam) url.searchParams.set('lookbackMinutes', lookbackParam);
            url.searchParams.set('_cb', Date.now().toString());

            const resp = await fetch(url.toString(), { cache: 'no-store' });
            if (reqId !== activeRequestId) return;
            if (!resp.ok) throw new Error('HTTP ' + resp.status);
            const payload = await resp.json();
            if (reqId !== activeRequestId) return;

            currentPayload = payload;
            if (payload && payload.profileName) {
                resolvedProfileName = String(payload.profileName).trim();
            }

            const shouldAutoStart = previewMode
                ? true
                : (autoStartParam === 'true'
                    ? true
                    : (autoStartParam === 'false'
                        ? false
                        : payload.playback?.startBehavior === 'AutoStartOnLoad'));

            if (startImmediately || shouldAutoStart) {
                startPlayback(payload, false);
            } else {
                applyThemeAndMedia(payload);
                rootEl.classList.add('credits-hidden');
            }
        } catch (err) {
            console.error('Failed loading credits payload:', err);
        }
    }

    function matchesProfileTarget(targetProfile) {
        if (!targetProfile || targetProfile === '*') return true;
        const normalizedTarget = String(targetProfile).trim().toLowerCase();
        if (profileParam && normalizedTarget === profileParam.toLowerCase()) return true;
        if (resolvedProfileName && normalizedTarget === resolvedProfileName.toLowerCase()) return true;
        return false;
    }

    function setPausedState(nextPaused) {
        if (!isRunning || isPaused === nextPaused) return;
        isPaused = nextPaused;
        lastFrameTime = performance.now();
        if (isPaused) {
            pausePagedTimer();
            if (audioEl && !audioEl.paused) {
                audioEl.pause();
            }
        } else {
            resumePagedTimer();
            if (!previewMode && audioEl && audioEl.src && audioEl.paused && currentPayload?.media?.backgroundAudioUrl) {
                audioEl.play().catch(function () {});
            }
        }
    }

    function handleCommand(msg) {
        if (!msg || typeof msg !== 'object') return;
        if (!msg.type || !String(msg.type).startsWith('credits_')) return;
        if (!matchesProfileTarget(msg.profile)) return;

        switch (msg.type) {
            case 'credits_play':
                if (msg.payload) {
                    startPlayback(msg.payload, false);
                } else {
                    fetchAndPrepare(true);
                }
                break;
            case 'credits_stop':
                stopPlayback();
                break;
            case 'credits_pause':
                if (!isRunning) return;
                setPausedState(true);
                break;
            case 'credits_resume':
                if (!isRunning) return;
                setPausedState(false);
                break;
            case 'credits_toggle_pause':
                if (!isRunning) return;
                setPausedState(!isPaused);
                break;
            case 'credits_restart':
                if (currentPayload) {
                    startPlayback(currentPayload, false);
                } else {
                    fetchAndPrepare(true);
                }
                break;
            case 'credits_speed':
                if (typeof msg.speed === 'number' && Number.isFinite(msg.speed)) {
                    speedMultiplier = Math.max(0.25, Math.min(10, msg.speed));
                }
                break;
            case 'credits_reload':
                fetchAndPrepare(previewMode || isRunning);
                break;
        }
    }

    if (previewMode) {
        window.addEventListener('message', function (event) {
            if (event.origin !== window.location.origin) return;
            const data = event.data;
            if (!data || typeof data !== 'object') return;
            if (data.type === 'credits_preview_reload') {
                fetchAndPrepare(true, data.sample);
                return;
            }
            handleCommand(data);
        });
    } else if (typeof createWsSocket === 'function') {
        const ws = createWsSocket('Credits: ' + (profileParam || 'Default'), [window.WS_TOPICS ? window.WS_TOPICS.credits : 'credits']);
        ws.addEventListener('open', function () {
            if (typeof wsStatusShow === 'function') wsStatusShow(true);
        });
        ws.addEventListener('close', function () {
            if (typeof wsStatusShow === 'function') wsStatusShow(false);
        });
        ws.addEventListener('error', function () {
            if (typeof wsStatusShow === 'function') wsStatusShow(false);
        });
        ws.addEventListener('message', function (event) {
            try {
                const raw = event.data;
                if (raw === 'ping') {
                    try { ws.send('pong'); } catch (_) {}
                    return;
                }
                const msg = JSON.parse(raw);
                handleCommand(msg);
            } catch (_) {}
        });
    }

    fetchAndPrepare(previewMode);
})();
