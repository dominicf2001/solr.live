<script lang="ts">
    import type { Media, Room, RoomMember } from "$lib/api";
    import { localStore } from "$lib/localStore.svelte";
    import type { HubConnection } from "@microsoft/signalr";
    import {
        HttpTransportType,
        HubConnectionBuilder,
    } from "@microsoft/signalr";
    import dayjs from "dayjs";
    import "vidstack/bundle";
    import type { MediaPlayerElement } from "vidstack/elements";

    let connection: HubConnection;
    let userId: string = $state("");

    let room: Room | undefined = $state(undefined);
    let mediaPlayer: MediaPlayerElement | undefined = $state(undefined);
    let mediaQueue = localStore<Media[]>("mediaQueue", []);

    const isInHostQueue: boolean = $derived.by(
        () => !!room?.hostQueue?.some((h) => h.id === userId),
    );

    const isHost: boolean = $derived.by(() => {
        return room?.session?.host?.id === userId;
    });

    const search = $state({
        isLoading: false,
        results: [] as Media[],
        input: "",
        clear: function () {
            this.input = "";
            this.results = [];
        },
        run: async function () {
            this.results = [];
            if (!this.input) return;

            this.isLoading = true;
            this.results = await connection.invoke(
                "YTSearch",
                this.input.trim(),
            );
            this.isLoading = false;
        },
    });

    $effect(() => {
        // WEBSOCKET CONNECTION
        connection = new HubConnectionBuilder()
            .withUrl(`${import.meta.env.VITE_API_URL}/roomHub`, {
                skipNegotiation: true,
                transport: HttpTransportType.WebSockets,
            })
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveRoom", (receivedRoom: Room) => {
            console.log("Received room", receivedRoom);

            room = receivedRoom;

            if (!mediaPlayer) return;
            mediaPlayer.src = room?.session?.media?.url ?? "";
        });

        connection.on("ReceiveOwnID", (receivedId) => {
            console.log("Received own ID", receivedId);
            userId = receivedId;
        });

        connection.on("DequeueMediaQueue", () => {
            console.log("Dequeuing media queue");
            return mediaQueue.value.shift() ?? null;
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
            if (mediaPlayer && canPlay) {
                if (paused) mediaPlayer.play();

                mediaPlayer.currentTime = room?.session
                    ? dayjs().diff(dayjs(room.session.startTime), "ms") / 1000
                    : 0;
            }
        });

        window.onbeforeunload = () => {
            // warn user when they exit
            // TODO: uncomment
            //if (isHost() || isInHostQueue()) {
            //    return true;
            //}
        };

        return () => {
            unsubPlayer?.();
            connection.stop();
            window.onbeforeunload = null;
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
                class="queue-button"
                class:join={!isInHostQueue && !isHost}
                class:leave={isInHostQueue || isHost}
            >
                {#if isInHostQueue}
                    Leave Queue
                {:else if isHost}
                    Stop DJing
                {:else}
                    Join Queue
                {/if}
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
        <form onsubmit={() => search.run()}>
            <input
                bind:value={search.input}
                placeholder="Song name"
                name="mediaName"
            />
            <button id="searchButton" disabled={search.isLoading} type="submit"
                >{search.isLoading ? "Loading..." : "Search"}</button
            >
            <div class="media-container">
                {#each search.results as media (media.id)}
                    <button
                        type="button"
                        class="search-result media-item"
                        onclick={() => {
                            mediaQueue.value.push(media);
                            search.clear();
                        }}
                    >
                        <img
                            src={media.thumbnails[0].url}
                            alt="Thumbnail"
                            class="thumbnail"
                        />
                        <div class="song-info">
                            <span class="title">{media.title}</span>
                            {#if media.duration}
                                <span class="duration">({media.duration})</span>
                            {/if}
                        </div>
                    </button>
                {/each}
            </div>
        </form>
    </div>
    <div class="item-list">
        <h2 style="margin-bottom: 10px">Your Song Queue</h2>
        <div class="media-container">
            {#each mediaQueue.value as media (media.id)}
                <button class="media-item" type="button">
                    <img
                        src={media.thumbnails[0].url}
                        alt="Thumbnail"
                        class="thumbnail"
                    />
                    <div class="song-info">
                        <span class="title">{media.title}</span>
                        {#if media.duration}
                            <span class="duration">({media.duration})</span>
                        {/if}
                    </div>
                </button>
            {/each}
        </div>
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
        max-width: 1200px;
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
        max-width: 1200px;
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

    .media-container {
        display: flex;
        flex-direction: column;
        gap: 10px;
    }

    .media-item {
        display: flex;
        align-items: center;
        width: 100%;
        padding: 10px;
        border: 1px solid #ddd;
        border-radius: 4px;
        background-color: white;
        transition: background-color 0.3s ease;
        text-align: left;
    }

    .search-result:hover {
        cursor: pointer;
        background-color: #f0f0f0;
    }

    .thumbnail {
        width: 200px;
        height: auto;
        object-fit: cover;
        margin-right: 10px;
    }

    .song-info {
        display: flex;
        flex-direction: column;
        flex-grow: 1;
    }

    .title {
        font-weight: bold;
        margin-bottom: 5px;
        color: var(--primary-color);
    }

    .duration {
        font-size: 0.8em;
        color: #666;
    } /* Form Styling */
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

    #searchButton {
        background-color: var(--primary-color);
        color: #fff;
        border: none;
        cursor: pointer;
    }

    #searchButton:hover:not(:disabled) {
        background-color: var(--secondary-color);
    }

    #searchButton:disabled {
        opacity: 50%;
        cursor: default;
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
