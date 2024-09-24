<script lang="ts">
    import type { Room, RoomMember, Session } from "$lib/api";
    import { HttpTransportType, HubConnectionBuilder } from "@microsoft/signalr";
    import dayjs from "dayjs";
    import "vidstack/bundle";
    import type { MediaPlayerElement } from "vidstack/elements";

    let room: Room | undefined = $state(undefined); 
    let player: MediaPlayerElement | undefined = $state(undefined);

    $effect(()=>{  
        const connection = new HubConnectionBuilder()
            .withUrl("http://192.168.4.29:5066/roomHub", {
                skipNegotiation: true,
                transport: HttpTransportType.WebSockets
            })
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveRoom", (receivedRoom: Room) => {
            console.log("Received room", receivedRoom);

            room = receivedRoom;

            if (!player) return;
            player.src = room?.session?.media?.link ?? "";
        });

        connection.on("UserConnected", (receivedMember: RoomMember) => {
            console.log("User connected", receivedMember);
            if (room) room.members[receivedMember.id] = receivedMember;
        });

        connection.on("UserDisconnected", (memberID: string) => {
            console.log("User disconnected", memberID);
            delete room?.members?.[memberID];
        });

        connection.start();

        const unsubPlayer = player?.subscribe(({ paused, canPlay }) => {
            if (player && canPlay){
                if (paused) player.play();

                player.currentTime = room?.session ? 
                    dayjs().diff(dayjs(room.session.startTime), "ms") / 1000 :
                    0;
            } 
        });

        return () => {
            unsubPlayer?.();
            connection.stop();
        }
    });
</script>

<section class="room-info">
    <h1 id="roomName">{ room?.name ?? "Empty Room" }</h1>
    <p class="room-host">Host: {room?.session?.host?.id ?? "Unknown"}</p>
</section>

<section class="media-player-container">
    <media-player bind:this={player} preload="auto" streamType="ll-live" muted={true} class="media-player">
      <media-provider></media-provider>
      <media-video-layout></media-video-layout>
    </media-player>
</section>

<section class="room-details">
    <div class="members">
        <h2>Members</h2>
        <ul>
            {#each Object.values(room?.members ?? []) as m (m.id)}
                <li>{m.id}</li>
            {/each}
        </ul>
    </div>
    <div class="host-queue">
        <h2>Host Queue</h2>
        <ul>
            {#each Object.values(room?.hostQueue ?? []) as m (m.id)}
                <li>{m.id}</li>
            {/each}
        </ul>
    </div>
</section>

<style>
    /* General Styling */
    * {
        margin: 0;
        padding: 0;
        box-sizing: border-box;
    }

    body {
        font-family: 'Arial', sans-serif;
        background-color: #f5f5f5;
        color: #333;
        line-height: 1.6;
    }

    h1, h2 {
        font-weight: bold;
    }

    h1 {
        font-size: 2.5rem;
        color: #2c3e50;
        margin-bottom: 1rem;
    }

    h2 {
        font-size: 1.5rem;
        margin-bottom: 0.5rem;
        color: #34495e;
    }

    /* Room Info Section */
    .room-info {
        text-align: center;
        padding: 2rem 0;
        background-color: #ecf0f1;
        border-bottom: 1px solid #bdc3c7;
    }

    .room-host {
        font-size: 1.25rem;
        color: #7f8c8d;
        margin-top: 0.5rem;
    }

    /* Media Player Section */
    .media-player-container {
        margin: 2rem auto;
        max-width: 80%;
        display: flex;
        justify-content: center;
        align-items: center;
        background-color: #fff;
        box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
        border-radius: 8px;
        overflow: hidden;
        padding: 1rem;
    }

    .media-player {
        width: 100%;
        height: 500px;
    }

    /* Room Details Section */
    .room-details {
        display: flex;
        justify-content: space-around;
        margin: 2rem 0;
        padding: 2rem;
        background-color: #ecf0f1;
        border-radius: 8px;
        box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
    }

    .members, .host-queue {
        width: 45%;
    }

    ul {
        list-style-type: none;
    }

    li {
        padding: 0.5rem;
        margin: 0.25rem 0;
        background-color: #fff;
        border-radius: 4px;
        box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
        transition: transform 0.2s ease-in-out;
    }

    li:hover {
        transform: translateY(-3px);
    }

    /* Responsive Design */
    @media screen and (max-width: 768px) {
        .room-details {
            flex-direction: column;
            align-items: center;
        }

        .members, .host-queue {
            width: 90%;
            margin-bottom: 2rem;
        }

        .media-player-container {
            max-width: 100%;
        }
    }
</style>
