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
        });
        ws.addEventListener('error', (event) => {
            printDebug('WebSocket error:', true);
            printDebug(event, true);
        });
        ws.addEventListener('message', (message) => {
            let jsonData = JSON.parse(message.data);
            if (jsonData.fishing !== undefined) {
                queue.push(jsonData);
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

async function promisePoll(fn, { pollIntervalMs = 2000 } = {}) {
    const endTime = Date.now() + pollIntervalMs;
    const checkCondition = async (resolve, reject) => {
        try {
            const result = await fn();
            if (result) {
                resolve();
            } else if (Date.now() < endTime) {
                setTimeout(checkCondition, pollIntervalMs, resolve, reject);
            } else {
                reject(new Error('Exceeded max polling time'));
            }
        } catch (error) {
            reject(error);
        }
    };
    return new Promise(checkCondition);
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
        username,
        fishName,
        rarity,
        stars,
        weight,
        gold,
        imageFileName,
        duration = 5000
    } = fishingData;

    $('#username').text(`${username} caught:`);
    $('#fish-name').text(fishName);

    const fishImage = $('#fish-image');
    let audioFile = null;

    if (imageFileName && imageFileName.length > 0) {
        const imagePath = '/fishes/' + imageFileName;
        fishImage.attr('src', imagePath);
        fishImage.show();

        // Try to find matching audio file
        const baseFileName = imageFileName.substring(0, imageFileName.lastIndexOf('.'));
        audioFile = await findAudioFile(baseFileName);
    } else {
        fishImage.hide();
    }

    const rarityElement = $('#rarity');
    rarityElement.removeClass().addClass('rarity').addClass(rarity.toLowerCase());
    rarityElement.text(rarity);

    let starHtml = '';
    for (let i = 0; i < stars; i++) {
        starHtml += '<span class="star">★</span>';
    }
    $('#stars').html(starHtml);

    $('#weight').text(`${weight} kg`);
    $('#gold').text(`${gold} gold`);

    await sleep(100);

    const alertElement = $('#fishing-alert');
    alertElement.css('display', 'block');

    // Trigger reflow to ensure transition works
    alertElement[0].offsetHeight;

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
