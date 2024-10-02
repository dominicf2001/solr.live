<script lang="ts">
    import type { ChatMessage, Media, Room, RoomMember } from "$lib/api";
    import { localStore } from "$lib/localStore.svelte";
    import { scrollToBottom } from "$lib/util";
    import type { HubConnection } from "@microsoft/signalr";
    import {
        HttpTransportType,
        HubConnectionBuilder,
    } from "@microsoft/signalr";
    import dayjs from "dayjs";
    import { onMount } from "svelte";
    import "vidstack/bundle";
    import type { MediaPlayerElement } from "vidstack/elements";

    // CONNECTION
    let connection: HubConnection;
    let preferredUsername = localStore<string>("preferredUsername", "");

    // ROOM
    let room: Room | undefined = $state(undefined);
    let ownRoomMember: RoomMember | undefined = $state(undefined);
    let skipsNeeded = $derived(room ? deriveSkipsNeeded(room) : 0);

    const isInHostQueue: boolean = $derived.by(
        () => !!room?.hostQueue?.some((h) => h.id === ownRoomMember?.id),
    );

    const isHost: boolean = $derived.by(
        () => room?.session?.host?.id === ownRoomMember?.id,
    );

    // MEDIA
    let mediaPlayer: MediaPlayerElement | undefined = $state(undefined);
    let mediaQueue = {
        value: localStore<Media[]>("mediaQueue", []).value,
        enqueue: (media: Media) => mediaQueue.value.push(media),
        dequeue: () => mediaQueue.value.shift() ?? null,
        remove: (media: Media) => {
            const index = mediaQueue.value.findIndex((m) => media.id === m.id);
            mediaQueue.value.splice(index, 1);
        },
    };

    function deriveSkipsNeeded(room: Room) {
        return Math.ceil(Object.values(room.members).length);
    }

    const search = $state({
        isLoading: false,
        results: [] as Media[],
        input: "",
        clear: () => {
            search.input = "";
            search.results = [];
        },
        run: async () => {
            search.results = [];
            if (!search.input) return;

            search.isLoading = true;
            search.results = await connection.invoke(
                "YTSearch",
                search.input.trim(),
            );
            search.isLoading = false;
        },
    });

    let chatMessage = $state({
        shouldScroll: false,
        container: null as HTMLElement | null,
        input: null as HTMLInputElement | null,
        send: () => {
            if (!chatMessage.container || !chatMessage.input?.value) return;
            connection.invoke("SendChatMessage", chatMessage.input.value);

            chatMessage.input.value = "";
            chatMessage.input.focus();
            chatMessage.input.select();
        },
    });

    function calculateCurrentTime(): number {
        if (room?.session) {
            return dayjs().diff(dayjs(room.session.startTime), "ms") / 1000;
        } else {
            return 0;
        }
    }

    onMount(() => {
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

        connection.on(
            "ReceiveOwnRoomMember",
            async (receivedMember: RoomMember) => {
                console.log("Received own room member", receivedMember);
                ownRoomMember = receivedMember;

                if (
                    preferredUsername.value !== "" &&
                    preferredUsername.value !== ownRoomMember.username
                ) {
                    try {
                        const usernameChanged = await connection.invoke(
                            "ChangeUsername",
                            preferredUsername.value,
                        );
                        if (usernameChanged) {
                            ownRoomMember.username = preferredUsername.value;
                        }
                    } catch (error) {
                        console.error("Error changing username:", error);
                    }
                } else {
                    preferredUsername.value = ownRoomMember.username;
                }
            },
        );

        connection.on("DequeueMediaQueue", () => {
            console.log("Dequeuing media queue");
            return mediaQueue.dequeue();
        });

        connection.on("MemberJoined", (receivedMember: RoomMember) => {
            console.log("Member joined", receivedMember);
            if (room) room.members[receivedMember.id] = receivedMember;
        });

        connection.on(
            "ReceiveChatMessage",
            (receivedChatMessage: ChatMessage) => {
                if (receivedChatMessage.authorID == ownRoomMember?.id)
                    chatMessage.shouldScroll = true;
                room?.chat.messages.push(receivedChatMessage);
            },
        );

        connection.on("MemberLeft", (memberID: string) => {
            console.log("Member left", memberID);
            delete room?.members?.[memberID];
        });

        connection.start();

        // MEDIA PLAYER
        const unsubPlayer = mediaPlayer?.subscribe(
            ({ paused, canPlay, autoPlay }) => {
                if (!mediaPlayer) return;

                if (canPlay) {
                    if (paused || autoPlay) mediaPlayer.play();

                    mediaPlayer.currentTime = calculateCurrentTime();
                }
            },
        );

        //window.onbeforeunload = () => {
        //    // warn user when they exit
        //    return isHost || isInHostQueue;
        //};

        return () => {
            unsubPlayer?.();
            connection.stop();
            window.onbeforeunload = null;
        };
    });

    $effect(() => {
        if (
            chatMessage.shouldScroll &&
            (room?.chat?.messages?.length ?? 0) > 0
        ) {
            scrollToBottom(chatMessage.container);
            chatMessage.shouldScroll = false;
        }
    });
</script>

<header class="room-info">
    <h1 id="roomName">{room?.name ?? "Empty Room"}</h1>
    <p class="room-host">Host: {room?.session?.host?.username ?? "None"}</p>
</header>

<main>
    <section id="mediaContainer">
        <section class="media-player-container">
            <media-player class="media-player" bind:this={mediaPlayer} muted>
                <media-provider></media-provider>
                <media-video-layout></media-video-layout>
            </media-player>
            {#if room?.session}
                <div style="display: flex; align-items: center;">
                    <p style="margin: none;" class="room-host">
                        Skips: {room?.session?.skips.length ?? 0} / {skipsNeeded}
                    </p>
                    <button
                        disabled={room.session.skips.some(
                            (id) => id === ownRoomMember?.id,
                        )}
                        onclick={() => connection.invoke("SendSkip")}
                        style="height: 50%; margin-left: 1%;"
                        class="input-button">Skip</button
                    >
                </div>
            {/if}
        </section>

        <section class="room-details">
            <div class="item-list">
                <h2>Members</h2>
                <ul>
                    {#each Object.values(room?.members ?? {}).sort( (a, b) => a.username.localeCompare(b.username), ) as member (member.id)}
                        <li>
                            {member.username}
                            {member.username === ownRoomMember?.username
                                ? "(You)"
                                : ""}
                        </li>
                    {/each}
                </ul>
            </div>
            <div class="item-list">
                <div class="host-queue-header">
                    <h2>Host Queue</h2>
                    <button
                        onclick={() =>
                            connection.invoke("ToggleHostQueueStatus")}
                        class="queue-button"
                        class:join={!isInHostQueue && !isHost}
                        class:leave={isInHostQueue || isHost}
                        disabled={!isInHostQueue &&
                            !isHost &&
                            !mediaQueue.value.length}
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
                        <li>{m.username}</li>
                    {/each}
                </ul>
            </div>
        </section>

        <section class="room-details">
            <div class="item-list">
                <form
                    onsubmit={(e) => {
                        e.preventDefault();
                        search.run();
                    }}
                >
                    <input
                        bind:value={search.input}
                        placeholder="Song name"
                        name="mediaName"
                    />
                    <button
                        class="input-button"
                        disabled={search.isLoading}
                        type="submit"
                        >{search.isLoading ? "Loading..." : "Search"}</button
                    >
                    <div class="media-container">
                        {#each search.results as media (media.id)}
                            <button
                                type="button"
                                class="search-result media-item"
                                onclick={() => {
                                    mediaQueue.enqueue(media);
                                    search.clear();
                                }}
                            >
                                {#if media.thumbnail}
                                    <img
                                        src={media.thumbnail.url}
                                        alt="thumbnail"
                                        class="thumbnail"
                                    />
                                {/if}
                                <div class="song-info">
                                    <span class="title">{media.title}</span>
                                    {#if media.duration}
                                        <span class="duration"
                                            >({media.duration})</span
                                        >
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
                        <div class="media-item">
                            {#if media.thumbnail}
                                <img
                                    src={media.thumbnail.url}
                                    alt="Thumbnail"
                                    class="thumbnail"
                                />
                            {/if}
                            <div class="song-info">
                                <span class="title">{media.title}</span>
                                {#if media.duration}
                                    <span class="duration"
                                        >({media.duration})</span
                                    >
                                {/if}
                                <button
                                    onclick={() => mediaQueue.remove(media)}
                                    class="remove-song-button">Remove</button
                                >
                            </div>
                        </div>
                    {/each}
                </div>
            </div>
        </section>
    </section>

    <section id="chatContainer">
        <ol bind:this={chatMessage.container}>
            {#each room?.chat.messages ?? [] as chatMessage (chatMessage.date)}
                <li>
                    <h4>
                        {room?.members[chatMessage.authorID]?.username ??
                            chatMessage.usernameAtDate ??
                            "Unknown user"}
                        <span
                            >{chatMessage.date
                                .split(".")[0]
                                .replace("T", " ")}</span
                        >
                    </h4>
                    <p>{chatMessage.content}</p>
                </li>
            {/each}
        </ol>
        <form
            onsubmit={(e) => {
                e.preventDefault();
                chatMessage.send();
            }}
        >
            <input
                bind:this={chatMessage.input}
                name="chatMessage"
                placeholder="Message"
            />
            <button class="input-button" type="submit">Send Message</button>
        </form>
    </section>
</main>

<style>
    :root {
        --primary-color: #2c3e50;
        --secondary-color: #34495e;
        --accent-color: #ecf0f1;
        --text-color: #333;
        --background-color: #f5f5f5;
        --font-family: "Arial", sans-serif;
    }

    main {
        display: flex;
        gap: 2rem;
    }

    #mediaContainer {
        flex: 3;
    }

    #chatContainer {
        margin: 1rem auto;
        padding: 0 1rem;
        flex: 1;
        display: flex;
        flex-direction: column;
        background-color: #f9f9f9;
        padding: 1rem;
        border-radius: 8px;
        box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
        height: 100%;
        min-width: 400px;
    }

    #chatContainer ol {
        list-style: none;
        padding: 0;
        margin: 0;
        flex-grow: 1;
        overflow-y: auto; /* Allow scrolling for long chat histories */
        margin-bottom: 1rem;
        max-height: 70vh; /* Adjust the height as needed */
    }

    #chatContainer li {
        margin-bottom: 1rem;
        padding: 0.5rem;
        background-color: white;
        border-radius: 5px;
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
    }

    #chatContainer li h4 {
        margin: 0;
        font-size: 1rem;
        font-weight: bold;
        display: flex;
        justify-content: space-between;
        align-items: center;
        color: #2c3e50;
    }

    #chatContainer li h4 span {
        font-size: 0.75rem;
        color: #7f8c8d; /* A lighter color for the timestamp */
    }

    #chatContainer li p {
        margin: 0.25rem 0 0 0;
        color: #34495e;
        font-size: 0.875rem;
    }

    #chatContainer form {
        display: flex;
        flex-direction: row;
        align-items: center;
        gap: 0.5rem;
    }

    #chatContainer input {
        flex: 1;
        padding: 0.5rem;
        border-radius: 4px;
        border: 1px solid #ddd;
        font-size: 1rem;
    }

    #chatContainer button {
        padding: 0.5rem 1rem;
        border-radius: 4px;
        background-color: var(--primary-color);
        color: white;
        border: none;
        cursor: pointer;
        font-size: 1rem;
    }

    #chatContainer button:hover {
        background-color: var(--secondary-color);
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

    .queue-button:disabled {
        opacity: 50%;
        cursor: default;
    }

    .queue-button.join:hover:not(:disabled) {
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
        margin-bottom: 10px;
    }

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

    .remove-song-button {
        background-color: var(--primary-color);
        color: #fff;
        border: none;
        cursor: pointer;
    }

    .remove-song-button:hover {
        background-color: var(--secondary-color);
    }

    .input-button {
        background-color: var(--primary-color);
        color: #fff;
        border: none;
        cursor: pointer;
    }

    .input-button:hover:not(:disabled) {
        background-color: var(--secondary-color);
    }

    .input-button:disabled {
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

        #chatContainer li h4 {
            flex-direction: column;
            align-items: flex-start;
        }

        #chatContainer ol {
            max-height: 50vh;
        }
    }
</style>
