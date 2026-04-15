// SignalR connection for real-time fishing catch updates
let fishingConnection = null;
let dotNetHelper = null;

export async function setupFishingConnection(dotNetReference) {
    dotNetHelper = dotNetReference;

    try {
        // Create connection to the Main SignalR hub
        fishingConnection = new signalR.HubConnectionBuilder()
            .withUrl("/mainhub")
            .withAutomaticReconnect()
            .build();

        // Subscribe to fish catch events
        fishingConnection.on("ReceiveFishCatch", (catchData) => {
            console.log("New fish caught:", catchData);

            // Notify the Blazor component
            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync(
                    'OnFishCaught',
                    catchData.userId,
                    catchData.username,
                    catchData.fishName,
                    catchData.fishRarity,
                    catchData.fishImageFileName,
                    catchData.stars,
                    catchData.weight,
                    catchData.goldEarned
                );
            }
        });

        // Start the connection
        await fishingConnection.start();
        console.log("SignalR fishing connection established");

    } catch (err) {
        console.error("Error establishing SignalR fishing connection:", err);
    }
}

export async function disconnectFishing() {
    if (fishingConnection) {
        try {
            await fishingConnection.stop();
            console.log("SignalR fishing connection closed");
        } catch (err) {
            console.error("Error closing SignalR fishing connection:", err);
        }
        fishingConnection = null;
    }
    dotNetHelper = null;
}
