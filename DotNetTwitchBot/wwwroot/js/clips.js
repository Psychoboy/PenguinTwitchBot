isDebug = true;
let audioFormats = {
    maybe: [],
    probably: []
};

let videoFormats = {
    maybe: [],
    probably: []
};

queryMap = getQueryMap();

function populateSupportedAudioTypes() {
    let audio = new Audio();

    let formats = {
        wav: 'audio/wav',
        mp3: 'audio/mpeg',
        mp4: 'audio/mp4',
        m4a: 'audio/mp4',
        aac: 'audio/aac',
        opus: 'audio/ogg; codecs="opus"',
        ogg: 'audio/ogg; codecs="vorbis"',
        oga: 'audio/ogg; codecs="vorbis"',
        webm: 'audio/webm; codecs="vorbis"'
    };

    for (let x in formats) {
        let ret = audio.canPlayType(formats[x]);
        printDebug('supportsAudioType(' + x + '): ' + ret);

        if (ret === 'maybe') {
            audioFormats.maybe.push(x);
        } else if (ret === 'probably') {
            audioFormats.probably.push(x);
        }
    }
}
populateSupportedAudioTypes();

function populateSupportedVideoTypes() {
    let video = document.createElement('video');

    let formats = {
        ogg: 'video/ogg; codecs="theora"',
        ogv: 'video/ogg; codecs="theora"',
        webm: 'video/webm; codecs="vp8"',
        mp4: 'video/mp4'
    };

    for (let x in formats) {
        let ret = video.canPlayType(formats[x]);
        printDebug('supportsVideoType(' + x + '): ' + ret);

        if (ret === 'maybe') {
            videoFormats.maybe.push(x);
        } else if (ret === 'probably') {
            videoFormats.probably.push(x);
        }
    }
}
populateSupportedVideoTypes();

function getQueryMap() {
    let queryString = window.location.search, // Query string that starts with ?
        queryParts = queryString.slice(1).split('&'), // Split at each &, which is a new query.
        queryMap = new Map(); // Create a new map for save our keys and values.

    for (let i = 0; i < queryParts.length; i++) {
        let key = queryParts[i].substring(0, queryParts[i].indexOf('=')),
            value = queryParts[i].slice(queryParts[i].indexOf('=') + 1);

        if (key.length > 0 && value.length > 0) {
            queryMap.set(key, value);
        }
    }

    return queryMap;
}

async function handleQueue() {
    if (queueProcessing || queue.length === 0) {
        return;
    }

    queueProcessing = true;
    try {
        while (queue.length > 0) {
            let event = queue[0];
            if (isPlaying === false || event.stopclip !== undefined) {
                await sleep(100);
                isPlaying = true;
                lastPlaying = Date.now();

                if (event.clip !== undefined) {
                    handleClipAlert(event);
                } else if (event.stopclip !== undefined) {
                    handleStopClip();
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

async function handleClipAlert(json) {
    // Make sure we can allow alerts.
    let defaultPath = '/clips/',
        clipData = json.clip,
        clipDuration = 3000,
        clipVolume = '0.8',
        clipFile = '',
        clipText = '',
        clipAvatar = '',
        clipGameImage = '',
        clipStreamer = '',
        htmlObj,
        audio,
        isVideo = false,
        hasAudio = false;

    // If a comma is found, that means there are custom settings.
    if (clipData.indexOf(',') !== -1) {
        let clipSettingParts = clipData.split(',');

        // Loop through each setting and set it if found.
        clipSettingParts.forEach(function (value, index) {
            switch (index) {
                case 0:
                    clipFile = value;
                    break;
                case 1:
                    clipDuration = (parseInt(value) * 1000);
                    break;
                case 2:
                    clipStreamer = value;
                    break;
                case 3:
                    clipAvatar = value;
                    break;
                case 4:
                    clipGameImage = value;
                default:
                    clipText = clipText + ',' + value;
                    break;
            }
        });
    } else {
        clipFile = clipData;
    }

    if (clipStreamer.trim().length === 0) {
        clipText = '';
    } else {
        clipText = 'Checkout ' + clipStreamer.trim() + ' and follow!';
    }

    htmlObj = $('<video/>', {
        'src': defaultPath + clipFile,
        'preload': 'auto',
        'width': 854,
        'height': 480,
        'id':'clipplayer'
    });

    htmlObj.prop('volume', clipVolume);
    isVideo = true;

    let ext = clipFile.substring(clipFile.lastIndexOf('.') + 1);
    if (!videoFormats.probably.includes(ext) && !videoFormats.maybe.includes(ext)) {
        printDebug('Video format ' + ext + ' was not supported by the browser!', true);
    }

    // p object to hold custom clip alert text and style
    textObj = $('<p/>', {
        'id': 'textObj'
    }).html(clipText);

    await sleep(500);

    if (isVideo) {
        let isReady = false;
        htmlObj[0].oncanplay = (event) => {
            isReady = true;
        };
        htmlObj[0].oncanplaythrough = (event) => {
            isReady = true;
        };
        const videoIsReady = () => {
            return isReady;
        };
        try {
            htmlObj[0].load();
            await promisePoll(() => videoIsReady(), { pollIntervalMs: 250 });
        } catch (err) { }
    }

    avatarObj = $('<img/>', {
        'src': clipAvatar,
        'id':'avatarObj'
    });

    gameImageObj = $('<img/>', {
        'src': clipGameImage,
        'id':'gameImageObj'
    });

    await sleep(500);

    // Append the custom text object to the page
    $('#alert-text').append(textObj).fadeIn(1e2).delay(clipDuration)
    .fadeOut(1e2, function () { //Remove the text with a fade out.
        let t = $(this);

        // Remove the p tag
        t.find('p').remove();
    });

    // Append a new the image.
    $('#alert').append(htmlObj).fadeIn(1e2, async function () {// Set the volume.
        if (isVideo) {
            // Play the sound.
            htmlObj[0].play().catch(function () {
                // Ignore.
            });
        }
        if (hasAudio) {
            audio.play().catch(function () {
                // Ignore.
            });
        }
    }).delay(clipDuration) // Wait this time before removing this image.
    .fadeOut(1e2, function () { // Remove the image with a fade out.
        let t = $(this);

        // Remove the video.
        t.find('video').remove();
        htmlObj[0].pause();
        htmlObj[0].currentTime = 0;
        // Mark as done playing.
        isPlaying = false;
    });
    $('#avatar').append(avatarObj).fadeIn(1e2).delay(clipDuration)
    .fadeOut(1e2, function () { //Remove the text with a fade out.
        let t = $(this);

        // Remove the p tag
        t.find('img').remove();
    });

    $('#box-art').append(gameImageObj).fadeIn(1e2).delay(clipDuration)
    .fadeOut(1e2, function () { //Remove the text with a fade out.
        let t = $(this);

        // Remove the p tag
        t.find('img').remove();
    });

}

function handleStopClip() {
    $("#clipplayer").remove();
    $("#gameImageObj").remove();
    $("#textObj").remove();
    $("#avatarObj").remove();
    isPlaying = false;
}

function getOptionSetting(option, def) {
    if (queryMap.has(option)) {
        return queryMap.get(option);
    } else {
        return def;
    }
}

function findFirstFile(filePath, fileName, extensions) {
    let ret = '';

    if (!filePath.endsWith('/')) {
        filePath = filePath + '/';
    }

    for (let x in extensions) {
        if (ret.length > 0) {
            return ret;
        }

        $.ajax({
            async: false,
            method: 'HEAD',
            url: filePath + fileName + '.' + extensions[x],
            success: function () {
                ret = filePath + fileName + '.' + extensions[x];
            }
        });
    }

    return ret;
}

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

promisePoll = (promiseFunction, { pollIntervalMs = 2000 } = {}) => {
    try {
        const startPoll = async resolve => {
            const startTime = new Date();
            const result = await promiseFunction();

            if (result) {
                return resolve();
            }

            const timeUntilNext = Math.max(pollIntervalMs - (new Date() - startTime), 0);
            setTimeout(() => startPoll(resolve), timeUntilNext);
        };
        return new Promise(startPoll).catch(error => { console.log('Caught Promise Error: ' + error) });
    } catch (err) {
        return null;
    }
};

function handleBrowserInteraction() {
    const audio = new Audio();

    // Try to play to see if we can interact.
    audio.play().catch(function (err) {
        // User need to interact with the page.
        if (err.toString().startsWith('NotAllowedError')) {
            $('.main-alert').append($('<button/>', {
                'html': 'Click me to activate audio hooks.',
                'style': 'top: 50%; position: absolute; font-size: 30px; font-weight: 30; cursor: pointer;'
            }).on('click', function () {
                $(this).remove();
            }));
        }
    });
}

function getWebSocket() {
    let socketUri = ((window.location.protocol === 'https:' ? 'wss://' : 'ws://') + window.location.host + '/ws'), // URI of the socket.
        reconnectInterval = 5000; // How often in milliseconds we should try reconnecting.

    return new ReconnectingWebSocket(socketUri, null, {
        reconnectInterval: reconnectInterval
    });
}

var socket;

var scheme = document.location.protocol === "https:" ? "wss" : "ws";
var port = document.location.port ? (":" + document.location.port) : "";
let queue = [];
let queueProcessing = false;
let isPlaying = false;
let lastPlaying = 0;
let playingAudioFiles = [];

connectionUrl = scheme + "://" + document.location.hostname + port + "/ws";

socket = getWebSocket();
socket.onopen = function (event) {
    updateState();
};
socket.onclose = function (event) {
    updateState();
};
socket.onerror = updateState;
socket.onmessage = function (event) {
    try {
        handleBrowserInteraction();
        let rawMessage = event.data;
        if (rawMessage == "ping") {
            socket.send("pong");
            return;
        }
        let message = JSON.parse(rawMessage);
        queue.push(message);
    } catch (ex) {
        console.log('Failed to parse socket message [' + event.data + ']: ' + event.stack)
    }
};

setInterval(handleQueue, 500);

function updateState() {

}

function htmlEscape(str) {
    return str.toString()
        .replace(/&/g, '&amp;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}

function printDebug(message, force) {
    if (isDebug || force) {
        console.log('%c[PenguinBot Log]', 'color: #6441a5; font-weight: 900;', message);
    }
}

