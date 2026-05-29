let isPlaying = false;
let queue = [];
let queueProcessing = false;
let lastPlaying = Date.now();
let lastPlayingInSeconds = 0;
let ws = null;
let wsReady = false;
let isDebug = true;

function connectWS() {
    try {
        ws = new ReconnectingWebSocket((window.location.protocol === 'https:' ? 'wss://' : 'ws://') + window.location.host + '/ws');
        ws.addEventListener('open', (event) => {
            printDebug('WebSocket connected');
            wsReady = true;
            wsStatusShow(true);
        });
        ws.addEventListener('error', (event) => {
            printDebug('WebSocket error:', true);
            printDebug(event, true);
        });
        ws.addEventListener('close', () => {
            wsReady = false;
            wsStatusShow(false);
        });
        ws.addEventListener('message', (message) => {
            try {
                const rawMessage = message.data;
                if (rawMessage === "ping") {
                    ws.send("pong");
                    return;
                }
                const jsonData = JSON.parse(rawMessage);
                if (jsonData.fishing !== undefined) {
                    queue.push(jsonData);
                }
            } catch (ex) {
                printDebug('Error parsing WebSocket message:', true);
                printDebug(ex, true);
            }
        });
    } catch (ex) {
        printDebug('WebSocket connection failed:', true);
        printDebug(ex, true);
    }
}

function printDebug(message, force) {
    if (isDebug || force) {
        console.log(message);
    }
}

async function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

setInterval(() => handleQueue(), 250);

connectWS();

async function handleQueue() {
    if (queueProcessing || queue.length === 0) {
        return;
    }

    queueProcessing = true;
    try {
        while (queue.length > 0) {
            let event = queue[0];
            if (isPlaying === false) {
                await sleep(100);
                isPlaying = true;
                lastPlaying = Date.now();

                if (event.fishing !== undefined) {
                    await handleFishingAlert(event.fishing);
                } else {
                    isPlaying = false;
                }
            } else {
                lastPlayingInSeconds = Math.floor((Date.now() - lastPlaying) / 1000);
                if (lastPlayingInSeconds > 180) {
                    printDebug('isPlaying is stuck, resetting');
                    isPlaying = false;
                }
                return;
            }
            queue.splice(0, 1);
        }
    } catch (ex) {
        console.log(ex)
        isPlaying = false;
        queue = [];
    } finally {
        queueProcessing = false;
    }
}

async function handleFishingAlert(fishingData) {
    const {
        username = 'Unknown',
        fishName = 'Unknown Catch',
        rarity = 'Common',
        stars = 0,
        weight = 0,
        gold = 0,
        imageFileName = '',
        duration = 5000
    } = fishingData ?? {};

    const normalizedRarity = normalizeRarity(rarity);
    const safeRarity = normalizedRarity.label;
    const safeRarityClass = normalizedRarity.cssClass;
    const safeStars = Math.max(0, Math.floor(toFiniteNumber(stars, 0)));
    const safeWeight = toFiniteNumber(weight, 0);
    const safeGold = toFiniteNumber(gold, 0);

    $('#username').text(`${username} caught:`);
    $('#fish-name').text(fishName);

    const fishImage = $('#fish-image');
    let audioFile = null;

    if (imageFileName && imageFileName.length > 0) {
        const imagePath = '/fishes/' + imageFileName;
        fishImage.attr('src', imagePath);
        fishImage.show();

        // Try to find matching audio file using the fish base filename.
        const dotIndex = imageFileName.lastIndexOf('.');
        const fileNameNoExt = dotIndex > 0 ? imageFileName.substring(0, dotIndex) : imageFileName;
        const baseFileName = fileNameNoExt.replace(/_(thumbnail|small|medium|large)$/i, '');
        audioFile = await findAudioFile(baseFileName);
    } else {
        fishImage.attr('src', '');
        fishImage.hide();
    }

    const rarityElement = $('#rarity');
    rarityElement.removeClass().addClass('rarity').addClass(safeRarityClass);
    rarityElement.text(safeRarity);

    let starHtml = '';
    for (let i = 0; i < safeStars; i++) {
        starHtml += '<span class="star">★</span>';
    }
    $('#stars').html(starHtml);

    $('#weight').text(`${safeWeight} kg`);
    $('#gold').text(`${safeGold} gold`);

    await sleep(100);

    const alertElement = $('#fishing-alert');
    alertElement.css('display', 'block');

    // Trigger reflow to ensure transition works
    alertElement[0].offsetHeight;

    scaleContainer();
    alertElement.addClass('show');

    // Play audio if found
    if (audioFile) {
        audioFile.play().catch(function(err) {
            console.log('Audio playback failed:', err);
        });
    }

    await sleep(duration);

    alertElement.removeClass('show');

    await sleep(300); // Wait for fade out transition

    alertElement.css('display', 'none');
    if (audioFile) {
        audioFile.pause();
        audioFile.currentTime = 0;
    }
    isPlaying = false;
}

async function findAudioFile(baseFileName) {
    const audioFormats = ['mp3', 'wav', 'ogg', 'webm'];
    const basePath = '/fishes/';

    for (const ext of audioFormats) {
        const audioPath = basePath + baseFileName + '.' + ext;
        try {
            const response = await fetch(audioPath, { method: 'HEAD' });
            if (response.ok) {
                const audio = new Audio(audioPath);
                audio.volume = 0.8;
                return audio;
            }
        } catch (err) {
            // File doesn't exist, continue
        }
    }

    return null;
}

function toFiniteNumber(value, fallback) {
    const parsed = typeof value === 'string'
        ? Number.parseFloat(value.trim())
        : Number(value);

    return Number.isFinite(parsed) ? parsed : fallback;
}

function scaleContainer() {
    const container = document.querySelector('.container');
    if (!container) return;
    container.style.transform = '';
    const naturalW = container.offsetWidth;
    const naturalH = container.offsetHeight;
    const padding = 40; // 20px body padding each side
    const vpW = window.innerWidth - padding;
    const vpH = window.innerHeight - padding;
    if (naturalW <= vpW && naturalH <= vpH) return;
    const scale = Math.min(vpW / naturalW, vpH / naturalH);
    container.style.transform = `scale(${scale})`;
}

function normalizeRarity(rarity) {
    const normalized = typeof rarity === 'string' ? rarity.trim().toLowerCase() : '';
    const allowedRarities = {
        common: 'Common',
        uncommon: 'Uncommon',
        rare: 'Rare',
        epic: 'Epic',
        legendary: 'Legendary',
        accident: 'Accident'
    };

    if (allowedRarities[normalized]) {
        return {
            label: allowedRarities[normalized],
            cssClass: normalized
        };
    }

    return {
        label: 'Common',
        cssClass: 'common'
    };
}
