"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/ythub").withAutomaticReconnect().build();
connection.start().then(function () {

}).catch(function (err) {
    return console.error(err.toString());
});
function playNextVideo() {
    connection.invoke("PlayNextVideo").catch(function (err) {
        return console.error(err.toString());
    });
}

function updateState(state) {
    connection.invoke("UpdateState", state).catch(function (err) {
        return console.error(err.toString());
    });
}

function loadNextSong() {
    connection.invoke("LoadNextSong").catch(function (err) {
        return console.error(err.toString());
    });
}

function songError(event) {
    connection.invoke("SongError", event.data).catch(function (err) {
        return console.error(err.toString());
    });
}

connection.on("PlayVideo", function (ytId) {
    player.loadVideoById(ytId, 0);
});

connection.on("Play", function () {
    player.playVideo();
});

connection.on("Pause", function () {
    player.pauseVideo();
});

connection.on("LoadVideo", function (ytId) {
    player.cueVideoByUrl(ytId);
});

connection.onreconnecting(error => {
    console.log("Reconnecting");
});


var tag = document.createElement('script');

tag.src = "https://www.youtube.com/iframe_api";
var firstScriptTag = document.getElementsByTagName('script')[0];
firstScriptTag.parentNode.insertBefore(tag, firstScriptTag);
var player;
function onYouTubeIframeAPIReady() {
    player = new YT.Player('player', {
        height: '390',
        width: '640',
        videoId: 'PHSDrTWfy9k',
        playerVars: {
            'playsinline': 1
        },
        events: {
            'onReady': onPlayerReady,
            'onStateChange': onPlayerStateChange,
            'onError': songError

        }
    });
}

function onPlayerReady(event) {
    // player.playVideo();
    loadNextSong();
}

var done = false;
function onPlayerStateChange(event) {
    updateState(event.data);
}
function stopVideo() {
    player.stopVideo();
}