<script lang="ts">
    import type { Media, Room, RoomMember } from "$lib/api";
    import type { HubConnection } from "@microsoft/signalr";
    import {
        HttpTransportType,
        HubConnectionBuilder,
    } from "@microsoft/signalr";
    import dayjs from "dayjs";
    import "vidstack/bundle";
    import type { MediaPlayerElement } from "vidstack/elements";

    function generateRandomID() {
        return Math.random().toString(36).substring(2, 9);
    }

    function isInHostQueue(): boolean {
        return (
            room?.hostQueue?.some((h) => h.id === ownRoomMember?.id) ||
            room?.session?.host.id === ownRoomMember?.id
        );
    }

    function queueMedia(event: Event) {
        event.preventDefault();
        const formElem = event.target as HTMLFormElement;
        const formData = new FormData(formElem);

        const songURL = formData.get("songURL");
        const duration = formData.get("duration");
        console.log("Queuing media", songURL, duration);
        if (songURL && duration) {
            connection.invoke("QueueMedia", {
                link: songURL,
                duration: duration,
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
        if (!accessToken) {
            accessToken = generateRandomID();
            localStorage.setItem("accessToken", accessToken);
        }

        // WEBSOCKET CONNECTION
        connection = new HubConnectionBuilder()
            .withUrl("http://192.168.4.29:5066/roomHub", {
                skipNegotiation: true,
                transport: HttpTransportType.WebSockets,
                accessTokenFactory: () => accessToken as string,
            })
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveRoom", (receivedRoom: Room) => {
            console.log("Received room", receivedRoom);

            room = receivedRoom;

            console.log(mediaPlayer);
            if (!mediaPlayer) return;
            mediaPlayer.src = room?.session?.media?.link ?? "";
        });

        connection.on("ReceiveOwnRoomMember", (receivedMember: RoomMember) => {
            console.log("Received own room member", receivedMember);
            ownRoomMember = receivedMember;
        });

        connection.on(
            "ReceiveHostQueue",
            (receivedQueue: Array<RoomMember>) => {
                console.log("Received host queue", receivedQueue);
                if (room) room.hostQueue = receivedQueue;
            },
        );

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
            if (mediaPlayer && canPlay) {
                if (paused) mediaPlayer.play();

                mediaPlayer.currentTime = room?.session
                    ? dayjs().diff(dayjs(room.session.startTime), "ms") / 1000
                    : 0;
            }
        });

        return () => {
            unsubPlayer?.();
            connection.stop();
        };
    });
</script>

<header class="room-info">
    <h1 id="roomName">{room?.name ?? "Empty Room"}</h1>
    <p class="room-host">Host: {room?.session?.host?.id ?? "None"}</p>
</header>

<section class="media-player-container">
    <media-player
        class="media-player"
        bind:this={mediaPlayer}
        preload="auto"
        muted
    >
        <media-provider></media-provider>
        <media-video-layout></media-video-layout>
    </media-player>
</section>

<section class="room-details">
    <div class="item-list">
        <h2>Members</h2>
        <ul>
            {#each Object.values(room?.members ?? {}) as member (member.id)}
                <li>{member.id}</li>
            {/each}
        </ul>
    </div>
    <div class="item-list">
        <div class="host-queue-header">
            <h2>Host Queue</h2>
            <button
                onclick={() => connection.invoke("ToggleHostQueueStatus")}
                class="queue-button {isInHostQueue() ? 'leave' : 'join'}"
            >
                {isInHostQueue() ? "Leave Queue" : "Join Queue"}
            </button>
        </div>
        <ul>
            {#each room?.hostQueue ?? [] as m (m.id)}
                <li>{m.id}</li>
            {/each}
        </ul>
    </div>
</section>

<section class="room-details">
    <div class="item-list">
        <form onsubmit={queueMedia} id="songForm">
            <input type="url" placeholder="URL" name="songURL" required />
            <input
                type="text"
                placeholder="Duration (e.g., 00:04:51)"
                name="duration"
                required
            />
            <button type="submit">Queue Song</button>
        </form>
    </div>
    <div class="item-list">
        <h2>Your Song Queue</h2>
        <ul>
            {#each ownRoomMember?.mediaQueue ?? [] as song, index (index)}
                <li>{song.link}</li>
            {/each}
        </ul>
    </div>
</section>

<style>
    :root {
        --primary-color: #2c3e50;
        --secondary-color: #34495e;
        --accent-color: #ecf0f1;
        --text-color: #333;
        --background-color: #f5f5f5;
        --font-family: "Arial", sans-serif;
    }

    h1,
    h2 {
        margin: 0;
        font-weight: bold;
    }

    h1 {
        font-size: 2rem;
        color: var(--primary-color);
    }

    h2 {
        font-size: 1.5rem;
        color: var(--secondary-color);
    }

    /* Host Queue Header */
    .host-queue-header {
        display: flex;
        align-items: center;
        margin-bottom: 1rem;
    }

    /* Queue Button Styles */
    .queue-button {
        margin-left: 1rem;
        padding: 0.5rem 1rem;
        font-size: 1rem;
        color: #ffffff;
        background-color: #3498db; /* Blue for 'Join Queue' */
        border: none;
        border-radius: 4px;
        cursor: pointer;
        transition: background-color 0.3s;
    }

    .queue-button.join:hover {
        background-color: #2980b9; /* Darker blue on hover */
    }

    .queue-button.leave {
        background-color: #e74c3c; /* Red for 'Leave Queue' */
    }

    .queue-button.leave:hover {
        background-color: #c0392b; /* Darker red on hover */
    }

    /* Room Info Section */
    .room-info {
        text-align: center;
        padding: 1rem 0;
        background-color: var(--accent-color);
        border-bottom: 1px solid #bdc3c7;
    }

    .room-host {
        font-size: 1rem;
        color: #7f8c8d;
    }

    /* Media Player Section */
    .media-player-container {
        max-width: 800px;
        margin: 1rem auto;
        padding: 0 1rem;
    }

    .media-player {
        width: 100%;
        aspect-ratio: 16/9;
        background-color: #000;
    }

    /* Room Details Section */
    .room-details {
        display: flex;
        flex-wrap: wrap;
        justify-content: space-between;
        margin: 1rem auto;
        max-width: 800px;
    }

    .item-list {
        flex: 1 1 45%;
        margin: 1rem 1rem;
    }

    .item-list ul {
        list-style: none;
        padding: 0;
    }

    .item-list li {
        padding: 0.5rem;
        margin-bottom: 0.5rem;
        background-color: #fff;
        border-radius: 4px;
    }

    /* Form Styling */
    form {
        display: flex;
        flex-direction: column;
    }

    form input,
    form button {
        padding: 0.5rem;
        margin-bottom: 0.5rem;
        font-size: 1rem;
    }

    form button {
        background-color: var(--primary-color);
        color: #fff;
        border: none;
        cursor: pointer;
    }

    form button:hover {
        background-color: var(--secondary-color);
    }

    /* Responsive Design */
    @media (max-width: 600px) {
        .room-details {
            flex-direction: column;
        }

        .item-list {
            flex: 1 1 100%;
        }
    }
</style>
