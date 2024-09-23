<script lang="ts">
    import type { DJSession } from "$lib/api";
    import { HttpTransportType, HubConnectionBuilder, type HubConnection } from "@microsoft/signalr";
    import dayjs from "dayjs";
    import "vidstack/bundle";
    import type { MediaPlayerElement } from "vidstack/elements";

    let djSession: DJSession | undefined; 
    let player: MediaPlayerElement | undefined;

    $effect(()=>{  
        const connection = new HubConnectionBuilder()
            .withUrl("http://192.168.4.29:5066/roomHub", {
                skipNegotiation: true,
                transport: HttpTransportType.WebSockets
            })
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveDJSession", (receivedDJSession: DJSession | undefined) => {
            console.log("Received DJ session");
            console.log(receivedDJSession);
            if (!player || !receivedDJSession) return;

            djSession = receivedDJSession;
            player.src = djSession.song.link;
        });

        connection.start();

        const unsubPlayer = player?.subscribe(({ paused, canPlay }) => {
            if (player && canPlay){
                if (paused) player.play();

                player.currentTime = djSession ? 
                    dayjs().diff(dayjs(djSession.startTime), "ms") / 1000 :
                    0;
            } 
        });

        return () => {
            unsubPlayer?.();
            connection.stop();
        }
    });
</script>

<media-player bind:this={player} preload="auto" streamType="ll-live" muted={true}>
  <media-provider></media-provider>
  <media-video-layout></media-video-layout>
</media-player>

<style>
</style>
