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
            if (isPlaying === false) {
                await sleep(100);
                isPlaying = true;
                lastPlaying = Date.now();

                if (event.alert_image !== undefined) {
                    handleGifAlert(event);
                } else if (event.audio_panel_hook !== undefined) {
                    handleAudioHook(event);
                } else {
                    console.log('Something bad happened 100');
                    isPlaying = false;
                }
            } else {
                lastPlayingInSeconds = Math.floor((Date.now() - lastPlaying) / 1000);
                if (lastPlayingInSeconds > 60) {
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

async function handleGifAlert(json) {
    // Make sure we can allow alerts.
    let defaultPath = '/gifs/',
        gifData = json.alert_image,
        gifDuration = 3000,
        gifVolume = '0.8',
        gifFile = '',
        gifCss = '',
        gifText = '',
        htmlObj,
        audio,
        isVideo = false,
        hasAudio = false;

    // If a comma is found, that means there are custom settings.
    if (gifData.indexOf(',') !== -1) {
        let gifSettingParts = gifData.split(',');

        // Loop through each setting and set it if found.
        gifSettingParts.forEach(function (value, index) {
            switch (index) {
                case 0:
                    gifFile = value;
                    break;
                case 1:
                    gifDuration = (parseInt(value) * 1000);
                    break;
                case 2:
                    gifVolume = value;
                    break;
                case 3:
                    gifCss = value;
                    break;
                case 4:
                    gifText = value;
                    break;
                default:
                    gifText = gifText + ',' + value;
                    break;
            }
        });
    } else {
        gifFile = gifData;
    }

    // Check if the file is a gif, or video.
    if (gifFile.match(/\.(webm|mp4|ogg|ogv)$/) !== null) {
        htmlObj = $('<video/>', {
            'src': defaultPath + gifFile,
            'style': gifCss,
            'preload': 'auto'
        });

        htmlObj.prop('volume', gifVolume);
        isVideo = true;

        let ext = gifFile.substring(gifFile.lastIndexOf('.') + 1);
        if (!videoFormats.probably.includes(ext) && !videoFormats.maybe.includes(ext)) {
            printDebug('Video format ' + ext + ' was not supported by the browser!', true);
        }
    } else {
        htmlObj = $('<img/>', {
            'src': defaultPath + gifFile,
            'style': gifCss,
            'alt': "Video"
        });
        await htmlObj[0].decode();
    }

    let audioPath = getAudioFile(gifFile.slice(0, gifFile.indexOf('.')), defaultPath);

    if (audioPath.length > 0 && gifFile.substring(gifFile.lastIndexOf('.') + 1) !== audioPath.substring(audioPath.lastIndexOf('.') + 1)) {
        hasAudio = true;
        audio = new Audio(audioPath);
    }

    // p object to hold custom gif alert text and style
    textObj = $('<p/>', {
        'style': gifCss
    }).html(gifText);

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
    if (hasAudio) {
        let isReady = false;
        audio.oncanplay = (event) => {
            isReady = true;
        };
        audio.oncanplaythrough = (event) => {
            isReady = true;
        };
        const audioIsReady = () => {
            return isReady;
        };

        audio.load();
        await promisePoll(() => audioIsReady(), { pollIntervalMs: 250 });
        audio.volume = gifVolume;
    }

    await sleep(500);

    // Append the custom text object to the page
    $('#alert-text').append(textObj).fadeIn(1e2).delay(gifDuration)
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
    }).delay(gifDuration) // Wait this time before removing this image.
        .fadeOut(1e2, function () { // Remove the image with a fade out.
            let t = $(this);

            // Remove either the img tag or video tag.
            if (!isVideo) {
                // Remove the image.
                t.find('img').remove();
            } else {
                // Remove the video.
                t.find('video').remove();
            }

            if (hasAudio) {
                // Stop the audio.
                audio.pause();
                // Reset the duration.
                audio.currentTime = 0;
            }
            if (isVideo) {
                htmlObj[0].pause();
                htmlObj[0].currentTime = 0;
            }
            // Mark as done playing.
            isPlaying = false;
        });

}

function getAudioFile(name, path) {
    let defaultPath = '/audio/',
        fileName = '';

    if (path !== undefined) {
        defaultPath = path;
    }

    fileName = findFirstFile(defaultPath, name, audioFormats.probably);

    if (fileName.length === 0) {
        fileName = findFirstFile(defaultPath, name, audioFormats.maybe);
    }

    if (fileName.length === 0) {
        printDebug(`Could not find a supported audio file for ${name}.`, true);
    }

    if (getOptionSetting('enableDebug', getOptionSetting('show-debug', 'false')) === 'true' && fileName.length === 0) {
        $('.main-alert').append('<br />getAudioFile(' + name + '): Unable to find file in a supported format');
    }

    return fileName;
}

function handleAudioHook(json) {
    // Make sure we can allow audio hooks.
    let audioFile = getAudioFile(json.audio_panel_hook),
        audio;

    if (audioFile.length === 0) {
        printDebug('Failed to find audio file.', true);
        isPlaying = false;
        return;
    }

    // Create a new audio file.
    audio = new Audio(audioFile);
    // Set the volume.
    audio.volume = getOptionSetting('audioHookVolume', getOptionSetting('audio-hook-volume', '1'));

    if (json.hasOwnProperty("audio_panel_volume") && json.audio_panel_volume >= 0.0) {
        audio.volume = json.audio_panel_volume;
    }
    // Add an event handler.
    $(audio).on('ended', function () {
        audio.currentTime = 0;
        isPlaying = false;
    });
    playingAudioFiles.push(audio);
    // Play the audio.
    audio.play().catch(function (err) {
        console.log(err);
    });
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

// var connectionUrl = document.getElementById("connectionUrl");
// var connectButton = document.getElementById("connectButton");
// var stateLabel = document.getElementById("stateLabel");
// var sendMessage = document.getElementById("sendMessage");
// var sendButton = document.getElementById("sendButton");
var testBody = document.getElementById("testBody");
//var closeButton = document.getElementById("closeButton");
var socket;

var scheme = document.location.protocol === "https:" ? "wss" : "ws";
var port = document.location.port ? (":" + document.location.port) : "";
let queue = [];
let queueProcessing = false;
let isPlaying = false;
let lastPlaying = 0;
let playingAudioFiles = [];

connectionUrl = scheme + "://" + document.location.hostname + port + "/ws";

//stateLabel.innerHTML = "Connecting";
//socket = new WebSocket(connectionUrl);
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
        let rawMessage = event.data,
            message = JSON.parse(rawMessage);
        queue.push(message);
    } catch (ex) {
        console.log('Failed to parse socket message [' + event.data + ']: ' + event.stack)
    }
};

setInterval(handleQueue, 500);

function updateState() {
    // function disable() {
    //     sendMessage.disabled = true;
    //     sendButton.disabled = true;
    //     closeButton.disabled = true;
    // }
    // function enable() {
    //     sendMessage.disabled = false;
    //     sendButton.disabled = false;
    //     closeButton.disabled = false;
    // }

    // connectionUrl.disabled = true;
    // connectButton.disabled = true;

    // if (!socket) {
    //     disable();
    // } else {
    //     switch (socket.readyState) {
    //         case WebSocket.CLOSED:
    //             // stateLabel.innerHTML = "Closed";
    //             disable();
    //             // connectionUrl.disabled = false;
    //             // connectButton.disabled = false;
    //             break;
    //         case WebSocket.CLOSING:
    //             // stateLabel.innerHTML = "Closing...";
    //             disable();
    //             break;
    //         case WebSocket.CONNECTING:
    //             // stateLabel.innerHTML = "Connecting...";
    //             disable();
    //             break;
    //         case WebSocket.OPEN:
    //             // stateLabel.innerHTML = "Open";
    //             enable();
    //             break;
    //         default:
    //             // stateLabel.innerHTML = "Unknown WebSocket State: " + htmlEscape(socket.readyState);
    //             disable();
    //             break;
    //     }
    // }
}

// closeButton.onclick = function () {
//     if (!socket || socket.readyState !== WebSocket.OPEN) {
//         alert("socket not connected");
//     }
//     socket.close(1000, "Closing from client");
// };

// sendButton.onclick = function () {
//     if (!socket || socket.readyState !== WebSocket.OPEN) {
//         alert("socket not connected");
//     }
//     var data = sendMessage.value;
//     socket.send(data);
// };

// connectButton.onclick = function() {
//     stateLabel.innerHTML = "Connecting";
//     socket = new WebSocket(connectionUrl.value);
//     socket.onopen = function (event) {
//         updateState();
//     };
//     socket.onclose = function (event) {
//         updateState();
//     };
//     socket.onerror = updateState;
//     socket.onmessage = function (event) {
//         testBody.innerHTML = event.data;
//     };
// };

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

