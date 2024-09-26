<script lang="ts">
    import type { Media, Room, RoomMember } from "$lib/api";
    import type { HubConnection } from "@microsoft/signalr";
    import { HttpTransportType, HubConnectionBuilder } from "@microsoft/signalr";
    import dayjs from "dayjs";
    import "vidstack/bundle";
    import type { MediaPlayerElement } from "vidstack/elements";

    function generateRandomID() {
        return Math.random().toString(36).substring(2, 9);
    }

    function queueMedia(event: Event){
        event.preventDefault(); 
        const formElem = event.target as HTMLFormElement;
        const formData = new FormData(formElem); 

        const songURL = formData.get("songURL");
        const duration = formData.get("duration");
        console.log("Queuing media", songURL, duration);
        if (songURL && duration){
            connection.invoke("QueueMedia", {
                link: songURL,
                duration: duration
            } as Media);
            formElem.reset();
        }
    }

    let connection: HubConnection;
    let accessToken: string | null;

    let room: Room | undefined = $state(undefined); 
    let mediaPlayer: MediaPlayerElement | undefined = $state(undefined);

    let ownRoomMember: RoomMember | undefined = $state(undefined);

    $effect(() => {  
        accessToken = localStorage.getItem("accessToken");
        if (!accessToken){
            accessToken = generateRandomID();
            localStorage.setItem("accessToken", accessToken);
        }

        // WEBSOCKET CONNECTION
        connection = new HubConnectionBuilder()
            .withUrl("http://192.168.4.29:5066/roomHub", {
                skipNegotiation: true,
                transport: HttpTransportType.WebSockets,
                accessTokenFactory: () => accessToken as string
            })
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveRoom", (receivedRoom: Room) => {
            console.log("Received room", receivedRoom);

            room = receivedRoom;

            if (!mediaPlayer) return;
            mediaPlayer.src = room?.session?.media?.link ?? "";
        });

        connection.on("ReceiveOwnRoomMember", (receivedMember: RoomMember) => {
            console.log("Received own room member", receivedMember);
            ownRoomMember = receivedMember;
        });

        connection.on("ReceiveHostQueue", (receivedQueue: Array<RoomMember>) => {
            console.log("Received host queue", receivedQueue);
            if (room) room.hostQueue = receivedQueue;
        });

        connection.on("MemberJoined", (receivedMember: RoomMember) => {
            console.log("Member joined", receivedMember);
            if (room) room.members[receivedMember.id] = receivedMember;
        });

        connection.on("MemberLeft", (memberID: string) => {
            console.log("Member left", memberID);
            delete room?.members?.[memberID];
        });

        connection.start();

        // MEDIA PLAYER
        const unsubPlayer = mediaPlayer?.subscribe(({ paused, canPlay }) => {
            if (mediaPlayer && canPlay){
                if (paused) mediaPlayer.play();

                mediaPlayer.currentTime = room?.session ? 
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
    <p class="room-host">Host: {room?.session?.host?.id ?? "None"}</p>
</section>

<section class="media-player-container">
    <media-player bind:this={mediaPlayer} preload="auto" streamType="ll-live" muted={true} class="media-player">
      <media-provider></media-provider>
      <media-video-layout></media-video-layout>
    </media-player>
</section>

<section class="room-details">
    <div class="item-list">
        <h2>Members</h2>
        <ul>
            {#each Object.values(room?.members ?? []) as m (m.id)}
                <li>{m.id}</li>
            {/each}
        </ul>
    </div>
    <div class="item-list">
        <div style="display: flex;">
            <h2>Host Queue</h2>
            <button onclick={() => connection.invoke("ToggleHostQueueStatus")} id="joinQueueBtn">
                {(room?.hostQueue?.find(h => h.id == ownRoomMember?.id) || room?.session?.host.id == ownRoomMember?.id) ? "Leave queue" : "Join queue"}
            </button>
        </div>
        <ul>
            {#each Object.values(room?.hostQueue ?? []) as m (m.id)}
                <li>{m.id}</li>
            {/each}
        </ul>
    </div>
</section>

<section class="room-details">
    <div class="item-list">
        <form onsubmit={queueMedia} id="songForm">
            <input value="https://www.youtube.com/watch?v=Fb2q141rMNE" placeholder="URL" name="songURL" required />
            <input value="00:04:51" placeholder="Duration" name="duration" required />
            <button>Queue song</button>
        </form>
    </div>
    <div class="item-list">
        <h2>Song Queue</h2>
        <ul>
            {#each ownRoomMember?.mediaQueue ?? [] as song, index (index)}
                <li>{song.link}</li>
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

    #songForm input, button  {
        height: 40px;
    }
 
    #joinQueueBtn {
        margin-left: 1rem;
        padding: 5px;
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

    .item-list, .host-queue {
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

        .item-list {
            width: 90%;
            margin-bottom: 2rem;
        }

        .media-player-container {
            max-width: 100%;
        }
    }
</style>
