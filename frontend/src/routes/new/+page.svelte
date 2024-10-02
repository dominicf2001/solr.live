<script lang="ts">
    import type { ChatMessage, Media, Room, RoomMember } from "$lib/api";
    import { localStore } from "$lib/localStore.svelte";
    import { formatDate, scrollToBottom } from "$lib/util";
    import type { HubConnection } from "@microsoft/signalr";
    import {
        HttpTransportType,
        HubConnectionBuilder,
    } from "@microsoft/signalr";
    import dayjs from "dayjs";
    import { onMount } from "svelte";
    import "vidstack/bundle";
    import type { MediaPlayerElement } from "vidstack/elements";
    import { Button, Drawer, Input, Popover, Spinner } from "flowbite-svelte";
    import { Toolbar, ToolbarButton, ToolbarGroup } from "flowbite-svelte";
    import {
        CirclePlusSolid,
        CloseCircleSolid,
        ListMusicSolid,
        PlaySolid,
        TrashBinSolid,
        YoutubeSolid,
    } from "flowbite-svelte-icons";
    import { sineIn } from "svelte/easing";

    // CONNECTION
    let connection: HubConnection;
    let preferredUsername = localStore<string>("preferredUsername", "");
    let preferredAvatar = localStore<string>("preferredAvatar", "");

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
    let mediaQueue = $state({
        value: localStore<Media[]>("mediaQueue", []).value,
        enqueue: (media: Media) => mediaQueue.value.push(media),
        dequeue: () => mediaQueue.value.shift() ?? null,
        remove: (media: Media) => {
            const index = mediaQueue.value.findIndex((m) => media.id === m.id);
            mediaQueue.value.splice(index, 1);
        },
        hidden: false,
        transitionParams: {
            y: 0,
            duration: 200,
            easing: sineIn,
        },
        searchMode: false,
    });

    const search = $state({
        isLoading: false,
        results: [] as Media[],
        input: "",
        clear: () => {
            search.input = "";
            search.results = [];
        },
        run: async () => {
            console.log("Running search");
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

    const disableHostQueueButton = $derived(
        !mediaQueue.value.length && !isInHostQueue && !isHost,
    );

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

    function deriveSkipsNeeded(room: Room) {
        return Math.ceil(Object.values(room.members).length);
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
                        await connection.invoke(
                            "ChangeUsernameAndAvatar",
                            preferredUsername.value,
                            preferredAvatar.value,
                        );
                    } catch (error) {
                        console.error(
                            "Error changing username or avatar:",
                            error,
                        );
                    }
                } else {
                    preferredUsername.value = ownRoomMember.username;
                    preferredAvatar.value = ownRoomMember.avatar;
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

<main class="flex w-screen h-screen">
    <section id="stage" class="flex flex-col">
        <div class="w-full flex flex-grow justify-center p-4">
            <div class="mt-20 w-full max-w-screen-md">
                <media-player
                    class="w-full h-96 aspect-video rounded-lg shadow-lg overflow-hidden bg-background-dark"
                    bind:this={mediaPlayer}
                    muted
                >
                    <media-provider></media-provider>
                    <media-video-layout></media-video-layout>
                </media-player>
            </div>
        </div>
        <Drawer
            backdrop={false}
            placement="bottom"
            transitionType="fly"
            transitionParams={mediaQueue.transitionParams}
            bind:hidden={mediaQueue.hidden}
            id="sidebar1"
            class="mb-14 h-2/5 bg-background-dark text-gray-400"
        >
            <div class="items-center bg-background-dark">
                <h5
                    id="drawer-label"
                    class="w-full inline-flex items-center mb-4 text-base font-semibold"
                >
                    {#if !mediaQueue.searchMode}
                        <ListMusicSolid class="w-6 h-6 me-2.5" />Song Queue
                        <CirclePlusSolid
                            onclick={() => (mediaQueue.searchMode = true)}
                            class="hover:opacity-80 cursor-pointer ml-auto w-6 h-6 me-2.5"
                        />
                    {:else}
                        <div class="w-6 h-6 flex items-center justify-center">
                            {#if search.isLoading}
                                <Spinner size="6" color="blue" />
                            {:else}
                                <YoutubeSolid class="w-6 h-6" />
                            {/if}
                        </div>
                        <Input
                            class="ml-2.5 text-white font-extrabold rounded-sm bg-background-darker border-opacity-25 h-6 w-5/6"
                            size="sm"
                            type="text"
                            placeholder="Search"
                            onkeydown={(e) => e.key === "Enter" && search.run()}
                            let:props
                        >
                            <input {...props} bind:value={search.input} />
                        </Input>

                        <CloseCircleSolid
                            onclick={() => (mediaQueue.searchMode = false)}
                            class="hover:opacity-80 cursor-pointer ml-auto w-6 h-6 me-2.5"
                        />
                    {/if}
                </h5>

                <div class="h-full overflow-y-auto pr-2">
                    <div class="space-y-1">
                        {#if mediaQueue.searchMode}
                            {#each search.results as video, index (video.id)}
                                <button
                                    class="w-full flex items-center space-x-2 p-1 rounded hover:bg-gray-800 transition-colors duration-200"
                                    onclick={() => {
                                        mediaQueue.enqueue(video);
                                        mediaQueue.searchMode = false;
                                        search.clear();
                                    }}
                                >
                                    <img
                                        src={video.thumbnail?.url}
                                        alt="Video thumbnail"
                                        class="w-16 h-12 object-cover rounded"
                                    />
                                    <div class="flex-1 min-w-0">
                                        <h3
                                            class="text-xs text-left font-medium text-gray-200 truncate"
                                        >
                                            {video.title}
                                        </h3>
                                        <p
                                            class="text-xs text-gray-400 truncate"
                                        >
                                            {video.author.channelTitle}
                                        </p>
                                        <div
                                            class="flex items-center text-xs text-gray-500"
                                        >
                                            <span>{video.duration}</span>
                                        </div>
                                    </div>
                                </button>
                                {#if index < 4}
                                    <hr class="border-gray-700" />
                                {/if}
                            {/each}
                        {:else}
                            {#each mediaQueue.value as video, index (video.id)}
                                <div
                                    class="w-full flex items-center space-x-2 p-1 rounded hover:bg-gray-800 transition-colors duration-200 group"
                                >
                                    <div
                                        class="flex-shrink-0 w-6 flex items-center justify-center"
                                    >
                                        <span
                                            class="text-xs text-gray-500 font-medium"
                                            >{index + 1}</span
                                        >
                                    </div>
                                    <img
                                        src={video.thumbnail?.url}
                                        alt="Video thumbnail"
                                        class="w-16 h-12 object-cover rounded"
                                    />
                                    <div class="flex-1 min-w-0">
                                        <h3
                                            class="text-xs text-left font-medium text-gray-200 truncate"
                                        >
                                            {video.title}
                                        </h3>
                                        <p
                                            class="text-xs text-gray-400 truncate"
                                        >
                                            {video.author.channelTitle}
                                        </p>
                                        <div
                                            class="flex items-center text-xs text-gray-500"
                                        >
                                            <span>{video.duration}</span>
                                        </div>
                                    </div>
                                    <Button
                                        size="md"
                                        class="opacity-0 group-hover:opacity-100 transition-opacity duration-200"
                                        onclick={() => mediaQueue.remove(video)}
                                    >
                                        <TrashBinSolid class="w-4 h-4" />
                                    </Button>
                                </div>
                                {#if index < mediaQueue.value.length - 1}
                                    <hr class="border-gray-700" />
                                {/if}
                            {/each}
                        {/if}
                    </div>
                </div>
            </div>
        </Drawer>

        <footer>
            <Toolbar class="rounded-none w-full bg-background-dark h-14">
                <ToolbarGroup class="bg-background-dark">
                    <ToolbarButton
                        on:click={() =>
                            (mediaQueue.hidden = !mediaQueue.hidden)}
                        class="hover:bg-background-darker {!mediaQueue.hidden
                            ? 'bg-background-darker'
                            : ''}"
                    >
                        <ListMusicSolid
                            class="w-8 h-8 {!mediaQueue.hidden
                                ? 'bg-background-darker'
                                : ''}"
                        />
                    </ToolbarButton>
                </ToolbarGroup>
                {#if disableHostQueueButton}
                    <Popover
                        class="w-64 text-sm font-light "
                        triggeredBy="#joinHostQueueButton"
                        >Add a song to your queue first!</Popover
                    >
                {/if}
                <ToolbarButton
                    id="joinHostQueueButton"
                    disabled={disableHostQueueButton}
                    slot="end"
                    on:click={() => connection.invoke("ToggleHostQueueStatus")}
                    class="flex items-center px-4 py-2 rounded-full transition-all duration-300 {disableHostQueueButton
                        ? 'bg-gray-600 text-gray-400 hover:bg-gray-600'
                        : isHost || isInHostQueue
                          ? 'bg-red-600 hover:bg-red-700 text-white'
                          : 'bg-blue-600 hover:bg-blue-700 text-white'}"
                >
                    <PlaySolid
                        class="w-5 h-5 mr-1 {disableHostQueueButton
                            ? 'opacity-50'
                            : ''}"
                    />
                    {#if isHost}
                        Quit DJing
                    {:else if isInHostQueue}
                        Leave Queue
                    {:else}
                        Play a song!
                    {/if}
                </ToolbarButton>
            </Toolbar>
        </footer>
    </section>
    <aside
        class="border-l border-l-tertiary-light flex flex-col bg-background-dark ml-auto h-screen w-[32rem]"
        id="chat"
    >
        <ol
            bind:this={chatMessage.container}
            class="flex-grow overflow-y-scroll p-4"
        >
            {#each room?.chat.messages ?? [] as chatMessage (chatMessage.date)}
                <li class="flex items-start space-x-4 mb-4">
                    <div class="w-10 h-10">
                        <img
                            class="rounded-full"
                            src={chatMessage.avatarAtDate}
                            alt="User avatar"
                        />
                    </div>
                    <div class="text-sm">
                        <div class="flex items-center space-x-2">
                            <span class="text-blue-500 font-bold">
                                {chatMessage.usernameAtDate}
                            </span>
                            <span class="text-gray-400 text-xs"
                                >{formatDate(chatMessage.date)}</span
                            >
                        </div>
                        <p class="text-white">
                            {chatMessage.content}
                        </p>
                    </div>
                </li>
            {/each}
        </ol>
        <form
            class="p-2"
            onsubmit={(e) => {
                e.preventDefault();
                chatMessage.send();
            }}
        >
            <Input
                class="text-white font-extrabold rounded-sm bg-background-darker border-opacity-25"
                type="text"
                placeholder="Send a message"
                let:props
            >
                <input {...props} bind:this={chatMessage.input} />
            </Input>
        </form>
    </aside>
</main>

<style>
    #stage {
        background-image: url("/aurora.jpg");
        background-size: cover;
        background-position: center;
        background-repeat: no-repeat;
        height: 100vh;
        width: 100vw;
    }
</style>
