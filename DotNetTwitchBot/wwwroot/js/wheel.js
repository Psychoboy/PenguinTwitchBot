async function handleQueue() {
    if (queueProcessing || queue.length === 0) {
        return;
    }

    queueProcessing = true;
    try {
        while (queue.length > 0) {
            let event = queue[0];
            if (event.wheel !== undefined) {
                handleWheel(event);
            }
            queue.splice(0, 1);
        }
    } catch (ex) {
        console.log('Failed to process queue: ' + ex);
        queue = [];
    } finally {
        queueProcessing = false;
    }
}

function handleWheel(event) {
    switch (event.wheel) {
        case "show":
            showWheel(event);
            break;
        case "hide":
            hideWheel(event);
            break;
        case "spin":
            wheelSpin(event);
            break;
    }
}

function showWheel(event) {
    hideWheel(event);
    const container = document.querySelector('.wheel-container');
    const i = new Image();
    i.src = './img/wheel.svg'
    const props = {
        items: event.items,
        itemLabelColors: ['#fff'],
        itemLabelBaselineOffset: -0.07,
        radius: 0.84,
        itemLabelRadius: 0.93,
        itemLabelRadiusMax: 0.35,
        itemBackgroundColors: ['#ffc93c', '#66bfbf', '#a2d5f2', '#515070', '#43658b', '#ed6663', '#d54062'],
        rotationSpeedMax: 500,
        rotationResistance: -100,
        lineWidth: 1,
        lineColor: '#fff',
        itemLabelFont: 'Rubik',
        overlayImage: i
    };
    window.wheel = new spinWheel.Wheel(container, props);
    window.wheel.onRest = function (item) {
        console.log('Wheel stopped at item: ' + item.currentIndex);
        socket.send(JSON.stringify({ wheel: 'stop', item: item.currentIndex }));
    }
    window.wheel.onCurrentIndexChange = function (event) {
        audio.play();
    }
}

function hideWheel(event) {
    const container = document.querySelector('.wheel-container');
    while (container.firstChild) {
        container.removeChild(container.firstChild);
    }
}

function wheelSpin(event) {
    window.wheel.spinToItem(event.spinTo, 10000, false, 5);
}

function getWebSocket() {
    let socketUri = ((window.location.protocol === 'https:' ? 'wss://' : 'ws://') + window.location.host + '/ws'), // URI of the socket.
        reconnectInterval = 5000; // How often in milliseconds we should try reconnecting.

    return new ReconnectingWebSocket(socketUri, null, {
        reconnectInterval: reconnectInterval
    });
}

var socket;
let queueProcessing = false;
var scheme = document.location.protocol === "https:" ? "wss" : "ws";
var port = document.location.port ? (":" + document.location.port) : "";
let queue = [];
connectionUrl = scheme + "://" + document.location.hostname + port + "/ws";

socket = getWebSocket();
socket.onmessage = function (event) {
    try {
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
let audio = new Audio("./sfx/wheel.ogg")
setInterval(handleQueue, 500);

//window.onload = () => {

//    const props = {
//        items: [
//            {
//                label: 'one',
//            },
//            {
//                label: 'two',
//            },
//            {
//                label: 'three',
//            },
//        ]
//    };

//    const container = document.querySelector('.wheel-container');

//    window.wheel = new spinWheel.Wheel(container, props);
//}

