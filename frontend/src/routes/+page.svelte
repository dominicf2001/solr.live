<script lang="ts">
    import type { SongSession } from "$lib/api";
    import { HttpTransportType, HubConnectionBuilder, type HubConnection } from "@microsoft/signalr";
    import dayjs from "dayjs";
    import "vidstack/bundle";

    let connection: HubConnection;
    let songSession: SongSession | undefined; 

    $effect(()=>{  
        connection = new HubConnectionBuilder()
            .withUrl("http://192.168.4.29:5066/roomHub", {
                skipNegotiation: true,
                transport: HttpTransportType.WebSockets
            })
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveSongSession", (receivedSongSession: SongSession | undefined) => {
            if (!player || !receivedSongSession) return;

            songSession = receivedSongSession;
            player.src = songSession.song.link;
        });

        connection.start();

        const player = document.querySelector("media-player");
        if (player){
            return player.subscribe(({ paused, canPlay }) => {
                if (canPlay){
                    if (paused) player.play();

                    player.currentTime = songSession ? 
                        dayjs().diff(dayjs(songSession.startTime), "ms") / 1000 :
                        0;
                }            
            });
        }

        return () => connection.stop();
    });
</script>

<media-player preload="auto" streamType="ll-live" muted={true}>
  <media-provider></media-provider>
  <media-video-layout></media-video-layout>
</media-player>

<style>
</style>
