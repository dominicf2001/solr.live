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
    import {
        Avatar,
        Button,
        Drawer,
        Input,
        Progressbar,
        Spinner,
        TabItem,
        Tabs,
        Tooltip,
    } from "flowbite-svelte";
    import {
        AngleLeftOutline,
        ChevronDownOutline,
        ChevronUpOutline,
        CirclePlusOutline,
        CloseCircleSolid,
        ForwardStepSolid,
        ListMusicOutline,
        ListMusicSolid,
        MessagesSolid,
        PlaySolid,
        StopSolid,
        ThumbsDownSolid,
        ThumbsUpSolid,
        TrashBinSolid,
        UserCircleSolid,
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
    let skips = $derived.by(() => {
        return {
            needed: Math.ceil(Object.values(room?.members ?? []).length),
            amount: room?.session?.skips?.length ?? 0,
            alreadySkipped: room?.session
                ? !!room.session.skips.find((id) => id === ownRoomMember?.id)
                : false,
            toggle: () => connection.send("ToggleSkip"),
        };
    });
    let likes = $derived.by(() => {
        return {
            percentage:
                !room || !room.session
                    ? 0
                    : (room.session.likes.length /
                          Object.values(room.members).length) *
                      100,
            alreadyLiked: room?.session
                ? !!room.session.likes.find((id) => id === ownRoomMember?.id)
                : false,
            toggle: () => connection.send("ToggleLike"),
        };
    });

    const isInHostQueue: boolean = $derived.by(
        () => !!room?.hostQueue?.some((h) => h.id === ownRoomMember?.id),
    );

    const isHost: boolean = $derived.by(() => {
        if (!room?.session?.host || !ownRoomMember) return false;
        return room.session.host.id === ownRoomMember.id;
    });

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
        hidden: true,
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

    let chat = $state({
        shouldScroll: false,
        container: null as HTMLElement | null,
        input: null as HTMLInputElement | null,
        send: () => {
            if (!chat.container || !chat.input?.value) return;
            connection.invoke("SendChatMessage", chat.input.value);

            chat.input.value = "";
            chat.input.focus();
            chat.input.select();
        },
    });

    let tabs = {
        classes: {
            active: "inline-block text-sm font-medium text-center disabled:cursor-not-allowed p-2 text-gray-200 border-b-1 active",
            inactive:
                "inline-block text-sm font-medium text-center disabled:cursor-not-allowed p-2 border-b-1 border-transparent hover:text-gray-300 text-gray-500 hover:border-gray-300",
        },
    };

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

                console.log("After own: ", ownRoomMember);
                if (
                    preferredUsername.value !== "" &&
                    preferredUsername.value !== ownRoomMember.username
                ) {
                    try {
                        console.log("A");
                        await connection.invoke(
                            "ChangeUsernameAndAvatar",
                            preferredUsername.value,
                            preferredAvatar.value,
                        );
                        ownRoomMember.username = preferredUsername.value;
                        ownRoomMember.avatar = preferredAvatar.value;
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
                    chat.shouldScroll = true;
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
        if (chat.shouldScroll && (room?.chat?.messages?.length ?? 0) > 0) {
            scrollToBottom(chat.container);
            chat.shouldScroll = false;
        }
    });
</script>

<main class="flex w-screen h-screen">
    <section id="stage" class="flex flex-2 flex-col">
        <div class="w-full flex flex-grow justify-center px-5">
            <div class="mt-20 w-full min-w-[575px] max-w-screen-md">
                <media-player
                    class="aspect-video rounded-md shadow-lg overflow-hidden bg-background-dark"
                    bind:this={mediaPlayer}
                    streamType="live"
                    muted
                >
                    <media-provider></media-provider>
                    <media-video-layout></media-video-layout>
                </media-player>
                {#if room?.session}
                    <div
                        class="w-1/5 bg-background-dark p-4 rounded-b-lg shadow-md"
                    >
                        <div class="w-full">
                            <div class="flex items-center justify-between mb-2">
                                <div class="flex items-center">
                                    <ThumbsUpSolid
                                        class="mr-2 text-green-600"
                                        size="sm"
                                    />
                                    <span
                                        class="text-sm font-semibold text-gray-400"
                                        >{likes.percentage}%</span
                                    >
                                </div>
                            </div>
                            <Progressbar
                                progress={likes.percentage}
                                size="h-1"
                                color="green"
                            />
                        </div>
                    </div>
                {/if}
            </div>
        </div>
        <Drawer
            id="mediaQueue"
            backdrop={false}
            placement="bottom"
            transitionType="fade"
            transitionParams={mediaQueue.transitionParams}
            bind:hidden={mediaQueue.hidden}
            class="bottom-20 left-3 overflow-y-hidden rounded-md h-2/5 bg-background-dark text-gray-400"
        >
            <div class="items-center h-full bg-background-dark">
                <h5
                    id="drawer-label"
                    class="w-full inline-flex items-center mb-4 text-base font-semibold"
                >
                    {#if !mediaQueue.searchMode}
                        <ListMusicOutline class="w-6 h-6 me-2.5" />My Queue
                        <CirclePlusOutline
                            onclick={() => (mediaQueue.searchMode = true)}
                            class="hover:opacity-80 cursor-pointer ml-auto w-6 h-6 me-2.5"
                        />
                    {:else}
                        <div class="w-6 h-6 flex items-center justify-center">
                            <YoutubeSolid class="w-6 h-6" />
                        </div>
                        <Input
                            class="ml-2.5 text-gray-200 font-extrabold rounded-sm bg-background-darker border-opacity-25 h-6 w-5/6"
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

                {#if mediaQueue.searchMode}
                    {#if !search.isLoading}
                        <div class="pb-8 h-full overflow-y-scroll">
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
                            {/each}
                        </div>
                    {:else}
                        <div
                            class="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2"
                        >
                            <Spinner size="9" color="blue" />
                        </div>
                    {/if}
                {:else if mediaQueue.value.length}
                    {#each mediaQueue.value as video, index (video.id)}
                        <div
                            class="w-full flex items-center space-x-2 p-1 rounded hover:bg-gray-800 transition-colors duration-200 group"
                        >
                            <div
                                class="flex-shrink-0 w-6 flex items-center justify-center"
                            >
                                <span class="text-xs text-gray-500 font-medium"
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
                                <p class="text-xs text-gray-400 truncate">
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
                    {/each}
                {:else}
                    <div
                        class="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2"
                    >
                        <p>No songs</p>
                    </div>
                {/if}
            </div>
        </Drawer>

        <footer>
            <div
                class="p-2 items-center flex rounded-none w-full bg-background-dark"
            >
                <button
                    onclick={() => {
                        const mediaQueueElem =
                            document.getElementById("mediaQueue");
                        mediaQueue.hidden = mediaQueueElem
                            ? !mediaQueueElem.classList.contains("hidden")
                            : false;
                    }}
                    class="flex items-center rounded-sm h-full p-1.5 hover:bg-background-darker transition-colors duration-200 {!mediaQueue.hidden
                        ? 'bg-background-darker'
                        : ''}"
                >
                    <div
                        class="relative flex-shrink-0 hover:bg-background-darker"
                    >
                        <Avatar
                            rounded
                            src={ownRoomMember?.avatar}
                            class="w-10 h-10 rounded-full bg-background-darker"
                        />
                    </div>
                    <div class="ml-3">
                        <p class="text-sm font-semibold text-gray-100 truncate">
                            {ownRoomMember?.username ?? "Loading..."}
                        </p>
                        <div
                            class="flex items-center text-left text-xs text-gray-400 truncate"
                        >
                            <ListMusicSolid />
                            <p class="ml-1">My Queue</p>
                        </div>
                    </div>
                    {#if mediaQueue.hidden}
                        <ChevronDownOutline class="ml-2 text-gray-400" />
                    {:else}
                        <ChevronUpOutline class="ml-2 text-gray-400" />
                    {/if}
                </button>
                <div class="flex items-center ml-auto mr-auto text-gray-400">
                    <button
                        disabled={!room?.session}
                        class="mr-2 {skips.alreadySkipped
                            ? 'text-gray-200'
                            : 'hover:text-gray-200'} disabled:opacity-50 disabled:hover:text-gray-400 disabled:cursor-not-allowed"
                        onclick={skips.toggle}
                    >
                        <ForwardStepSolid size="xl" />
                    </button>
                    <Tooltip class="z-50 text-background-darker bg-gray-200">
                        {#if room?.session}
                            Skip song ({skips.amount} / {skips.needed})
                        {:else}
                            No song to skip
                        {/if}
                    </Tooltip>
                    <button
                        disabled={!room?.session}
                        class="mr-2 {likes.alreadyLiked
                            ? 'text-green-400'
                            : 'hover:text-gray-200'} disabled:opacity-50 disabled:hover:text-gray-400 disabled:cursor-not-allowed"
                        onclick={likes.toggle}
                    >
                        <ThumbsUpSolid size="xl" />
                    </button>
                    <Tooltip class="z-50 text-background-darker bg-gray-200">
                        {#if room?.session}
                            Like song
                        {:else}
                            No song to like
                        {/if}
                    </Tooltip>
                </div>
                {#if disableHostQueueButton}
                    <Tooltip
                        class="text-background-darker bg-gray-200"
                        triggeredBy="#joinHostQueueButton"
                        >Add a song to your queue first</Tooltip
                    >
                {/if}
                <Button
                    id="joinHostQueueButton"
                    disabled={disableHostQueueButton}
                    class="w-36"
                    onclick={() => connection.invoke("ToggleHostQueueStatus")}
                    color={isHost || isInHostQueue ? "red" : "blue"}
                >
                    {#if isHost}
                        <StopSolid />
                        Quit DJing
                    {:else if isInHostQueue}
                        <AngleLeftOutline />
                        Leave Queue
                    {:else}
                        <PlaySolid />
                        Play a song!
                    {/if}
                </Button>
            </div>
        </footer>
    </section>
    <aside
        class="border-l border-l-tertiary-light flex flex-col bg-background-dark ml-auto h-screen sm:w-0 md:w-[32rem] transition-all duration-300 ease-in-out"
        id="chat"
    >
        <Tabs contentClass="bg-none h-full" tabStyle="full">
            <TabItem
                activeClasses={tabs.classes.active}
                inactiveClasses={tabs.classes.inactive}
                divClass="h-full flex flex-col"
                class="h-full"
                open
            >
                <div slot="title" class="flex items-center gap-2">
                    <MessagesSolid size="md" />
                    Chat
                </div>
                {#if !room?.chat?.messages?.length}
                    <div class="h-full flex items-center justify-center">
                        <p class="text-gray-400">No messages</p>
                    </div>
                {/if}
                <ol
                    bind:this={chat.container}
                    class="flex-grow overflow-y-scroll p-4"
                >
                    {#each room?.chat.messages ?? [] as chatMessage (chatMessage.date)}
                        <li class="flex items-start space-x-4 mb-1">
                            <div class="w-14 h-14">
                                <Avatar
                                    class="rounded-full bg-transparent"
                                    src={chatMessage.avatarAtDate}
                                    alt="User avatar"
                                />
                            </div>
                            <div class="lg:text-sm md:text-xs w-full">
                                <div class="flex items-center">
                                    <span class="text-blue-500 font-bold">
                                        {chatMessage.usernameAtDate}
                                    </span>
                                    <span class="ml-auto text-gray-400"
                                        >{formatDate(chatMessage.date)}</span
                                    >
                                </div>
                                <p class="text-gray-200">
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
                        chat.send();
                    }}
                >
                    <Input
                        class="text-gray-200 font-extrabold rounded-sm bg-background-darker border-opacity-25"
                        type="text"
                        placeholder="Send a message"
                        let:props
                    >
                        <input {...props} bind:this={chat.input} />
                    </Input>
                </form>
            </TabItem>
            <TabItem
                activeClasses={tabs.classes.active}
                inactiveClasses={tabs.classes.inactive}
            >
                <div slot="title" class="flex items-center gap-2">
                    <UserCircleSolid size="md" />
                    Members
                </div>
                <div class="flex-grow overflow-y-auto p-4">
                    {#if room?.session?.host}
                        <h3 class="text-sm font-semibold text-gray-400 mb-2">
                            DJ
                        </h3>
                        <ol class="mb-2">
                            <li
                                class="flex items-center space-x-3 p-2 rounded-md bg-primary-900 bg-opacity-50"
                            >
                                <div class="relative">
                                    <Avatar
                                        class="w-10 h-10 rounded-full bg-transparent"
                                        src={room.session.host.avatar}
                                        alt={room.session.host.username}
                                    />
                                </div>
                                <div>
                                    <p
                                        class="text-sm font-semibold text-gray-200"
                                    >
                                        {room.session.host.username}
                                    </p>
                                </div>
                            </li>
                        </ol>
                    {/if}
                    {#if room?.hostQueue?.length}
                        <h3 class="text-sm font-semibold text-gray-400 mb-2">
                            DJ Queue
                        </h3>
                        <ol class="mb-2">
                            {#each room.hostQueue as member, index (member.id)}
                                <li
                                    class="flex items-center space-x-3 p-2 rounded-md hover:bg-gray-800"
                                >
                                    <div class="relative">
                                        <Avatar
                                            class="w-10 h-10 rounded-full bg-transparent"
                                            src={member.avatar}
                                            alt={member.username}
                                        />
                                        <div
                                            class="absolute -top-1 -left-1 bg-gray-700 text-gray-200 text-xs font-bold rounded-full w-5 h-5 flex items-center justify-center"
                                        >
                                            {index + 1}
                                        </div>
                                    </div>
                                    <div>
                                        <p
                                            class="text-sm font-semibold text-gray-200"
                                        >
                                            {member.username}
                                        </p>
                                    </div>
                                </li>
                            {/each}
                        </ol>
                    {/if}

                    {#if room}
                        {@const listeners = Object.values(
                            room.members ?? [],
                        ).filter(
                            (m) =>
                                !room?.hostQueue?.find((h) => h.id === m.id) &&
                                room?.session?.host?.id !== m.id,
                        )}
                        {#if listeners.length}
                            <h3
                                class="text-sm font-semibold text-gray-400 mb-2"
                            >
                                Listeners
                            </h3>
                            <ul class="space-y-2">
                                {#each listeners as member (member.id)}
                                    {#if !room.hostQueue.find((m) => m.id === member.id) && room.session?.host?.id !== member.id}
                                        <li
                                            class="flex items-center space-x-3 p-2 rounded-md hover:bg-gray-800"
                                        >
                                            <Avatar
                                                class="w-10 h-10 rounded-full bg-transparent"
                                                src={member.avatar}
                                                alt={member.username}
                                            />
                                            <p
                                                class="text-sm font-semibold text-gray-200"
                                            >
                                                {member.username}
                                            </p>
                                        </li>
                                    {/if}
                                {/each}
                            </ul>
                        {/if}
                    {/if}
                </div>
            </TabItem>
        </Tabs>
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
