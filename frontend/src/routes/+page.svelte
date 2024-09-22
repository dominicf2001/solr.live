<script lang="ts">
    import type { SongSession } from "$lib/api";
    import { HttpTransportType, HubConnectionBuilder, type HubConnection } from "@microsoft/signalr";
    import dayjs from "dayjs";
    import "vidstack/bundle";

    let connection: HubConnection;

    $effect(()=>{  
        connection = new HubConnectionBuilder()
            .withUrl("http://localhost:5066/roomHub", {
                skipNegotiation: true,
                transport: HttpTransportType.WebSockets
            })
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveSongSession", (songSession: SongSession | null) => {
            if (!player || !songSession) return;
            const startDate = dayjs(songSession.startTime);

            player.currentTime = dayjs().diff(startDate, "seconds");
            player.src = songSession.song.link;
        });

        connection.start();

        const player = document.querySelector("media-player");
        if (player){
            return player.subscribe(({ paused, canPlay }) => {
                if (canPlay && paused){
                    player.play();
                }            
            });
        }

        return () => connection.stop();
    });
</script>

<media-player muted={true}>
  <media-provider></media-provider>
  <media-video-layout></media-video-layout>
</media-player>

<style>
</style>
