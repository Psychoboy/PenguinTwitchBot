"use strict";

var connection = null;
var tag = document.createElement('script');

tag.src = "https://www.youtube.com/iframe_api";
var firstScriptTag = document.getElementsByTagName('script')[0];
firstScriptTag.parentNode.insertBefore(tag, firstScriptTag);
function playNextVideo() {
    console.log("playNextVideo");
    window.connection.invoke("PlayNextVideo").catch(function (err) {
        return console.error(err.toString());
    });
}

function updateState(state) {
    console.log("updateState");
    window.connection.invoke("UpdateState", state).catch(function (err) {
        return console.error(err.toString());
    });
}

function loadNextSong() {
    console.log("loadNextSong");
    window.connection.invoke("LoadNextSong").catch(function (err) {
        return console.error(err.toString());
    });
}

function songError(event) {
    //connection.invoke("SongError", event.data).catch(function (err) {
    //    return console.error(err.toString());
    //});
    console.log("Error");
    console.log(event);
}




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
    console.log("onYouTubeIframeAPIReady");
}

function onPlayerReady(event) {
    console.log("onPlayerReady");
     //player.playVideo();
    window.connection = new signalR.HubConnectionBuilder().withUrl("/ythub").withAutomaticReconnect().build();
    window.connection.start().then(function () {

        //var tag = document.createElement('script');

        //tag.src = "https://www.youtube.com/iframe_api";
        //var firstScriptTag = document.getElementsByTagName('script')[0];
        //firstScriptTag.parentNode.insertBefore(tag, firstScriptTag);
        loadNextSong();
    }).catch(function (err) {
        return console.error(err.toString());
    });

    window.connection.on("PlayVideo", function (ytId) {
        player.loadVideoById(ytId, 0);
    });

    window.connection.on("Play", function () {
        player.playVideo();
    });

    window.connection.on("Pause", function () {
        player.pauseVideo();
    });

    window.connection.on("LoadVideo", function (ytId) {
        player.cueVideoByUrl(ytId);
    });

    window.connection.onreconnecting(error => {
        console.log("Reconnecting");
    });

    
}

var done = false;
function onPlayerStateChange(event) {
    console.log("onPlayerStateChange");
    updateState(event.data);
}
function stopVideo() {
    console.log("stopVideo");
    player.stopVideo();
}