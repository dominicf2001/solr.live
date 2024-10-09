using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using YoutubeExplode.Common;
using YoutubeExplode.Search;

public class RoomHub : Hub
{
    private readonly ILogger logger;
    private readonly RoomManager roomManager;
    private readonly YoutubeExplode.YoutubeClient yt;

    public RoomHub(ILogger<RoomHub> logger, RoomManager roomManager, YoutubeExplode.YoutubeClient youtubeClient)
    {
        this.logger = logger;
        this.roomManager = roomManager;
        this.yt = youtubeClient;
    }

    public override async Task OnConnectedAsync()
    {
        Room room = roomManager.GetRoom("Test");
        string userID = Context.UserIdentifier ?? throw new InvalidOperationException("User identifier is unexpectedly null");

        await Groups.AddToGroupAsync(Context.ConnectionId, room.ID);

        RoomMember? member;
        if (!room.Members.TryGetValue(userID, out member))
        {
            string username = RoomUtils.GenRandomUsername();
            int duplicates = room.Members.Values.Where(m => m.Username.StartsWith(username)).Count();
            if (duplicates > 0)
                username += duplicates;

            string avatar = RoomUtils.GenRandomAvatar();

            room.Members[userID] = member = new RoomMember(userID, username, avatar);
        }

        await Clients.Caller.SendAsync("ReceiveOwnRoomMember", member);
        Task[] tasks = [
            Clients.Caller.SendAsync("ReceiveRoom", room),
            Clients.Group(room.ID).SendAsync("MemberJoined", member),
        ];

        await Task.WhenAll(tasks);

        logger.LogInformation("User connected: {UserID}", userID);
        await base.OnConnectedAsync();
    }

    public async Task SendChatMessage(string content)
    {
        Room room = roomManager.GetRoom("Test");
        string userID = Context.UserIdentifier ?? throw new InvalidOperationException("User identifier is unexpectedly null");
        logger.LogInformation($"Sending chat message: {content} from {userID}");

        ChatMessage chatMessage = room.Chat.Send(userID, content);
        await Clients.Group(room.ID).SendAsync("ReceiveChatMessage", chatMessage);
    }

    public async Task ToggleSkip()
    {
        Room room = roomManager.GetRoom("Test");
        string userID = Context.UserIdentifier ?? throw new InvalidOperationException("User identifier is unexpectedly null");
        logger.LogInformation($"Attempt from {userID} to toggle a skip");
        if (room?.Session is null) return;

        bool alreadySkipped = room.Session.Skips.Add(userID);
        if (!alreadySkipped)
        {
            room.Session.Skips.Remove(userID);
            logger.LogInformation($"{userID} removed a skip");
        }
        else
        {
            logger.LogInformation($"{userID} added a skip");
        }

        if (room.Session.CanSkip(room))
        {
            logger.LogInformation($"Skipping the session");
            await room.NextSession();
        }
        else
        {
            await Clients.Group(room.ID).SendAsync("ReceiveRoom", room);
        }
    }

    public async Task ToggleLike()
    {
        Room room = roomManager.GetRoom("Test");
        string userID = Context.UserIdentifier ?? throw new InvalidOperationException("User identifier is unexpectedly null");
        logger.LogInformation($"Attempt from {userID} to toggle a like");
        if (room?.Session is null) return;

        bool alreadyLiked = room.Session.Likes.Add(userID);
        if (!alreadyLiked)
        {
            room.Session.Likes.Remove(userID);
            logger.LogInformation($"{userID} removed a like");
        }
        else
        {
            logger.LogInformation($"{userID} added a like");
        }

        await Clients.Group(room.ID).SendAsync("ReceiveRoom", room);
    }

    public async Task ToggleHostQueueStatus()
    {
        Room room = roomManager.GetRoom("Test");
        string userID = Context.UserIdentifier ?? throw new InvalidOperationException("User identifier is unexpectedly null");

        logger.LogInformation("User attempting to join host queue: {UserID}", userID);

        room.Members.TryGetValue(userID, out RoomMember? member);
        if (member is null)
            return;

        bool inHostQueue = room.HostQueue.FirstOrDefault(h => h.ID == userID) is not null;
        bool isHost = room.Session?.Host.ID == userID;
        if (isHost)
        {
            await room.NextSession(preventHostRequeue: true);
        }
        else if (inHostQueue)
        {
            room.RemoveFromHostQueue(userID);
        }
        else
        {
            logger.LogInformation("User joined host queue: {UserID}", userID);
            room.HostQueue.Enqueue(member);

            if (room.Session is null)
            {
                logger.LogInformation("No room session, setting {UserID} as host", userID);
                await room.NextSession();
            }
        }

        await Clients.Group(room.ID).SendAsync("ReceiveRoom", room);
    }

    public async Task<IEnumerable<Media>> YTSearch(string query)
    {
        string userID = Context.UserIdentifier ?? throw new InvalidOperationException("User identifier is unexpectedly null");
        logger.LogInformation($"{userID} searching for {query}");
        List<string> musicKeywords = RoomUtils.GetMusicKeywords();

        var videos = await yt.Search.GetVideosAsync(query).CollectAsync(30);

        var filteredVideos = videos
            .Where(v => v.Duration < TimeSpan.FromMinutes(8))
            .OrderByDescending(v =>
            {
                int score = 0;
                foreach (string keyword in musicKeywords)
                    if (v.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        ++score;
                return score;
            })
            .Take(10)
            .Select(v => new Media(v));

        return filteredVideos;
    }

    public async Task<string> GenerateRandomAvatar()
    {
        return await Task.FromResult(RoomUtils.GenRandomAvatar());
    }

    public async Task ChangeUsernameAndAvatar(string newUsername, string newAvatar)
    {
        Room room = roomManager.GetRoom("Test");
        string userID = Context.UserIdentifier ?? throw new InvalidOperationException("User identifier is unexpectedly null");

        logger.LogInformation($"User: {userID} attempting to change their username to {newUsername} and avatar to {newAvatar}");

        if (room.Members.TryGetValue(userID, out RoomMember? member))
        {
            member.Username = newUsername;
            member.Avatar = newAvatar;

            foreach (ChatMessage message in room.Chat.Messages)
                if (message.AuthorID == userID)
                {
                    message.CachedAvatar = newAvatar;
                    message.CachedUsername = newUsername;
                }

            logger.LogInformation($"User: {userID} changed their username to {newUsername} and avatar to {newAvatar}");
            await Clients.Group(room.ID).SendAsync("ReceiveRoom", room);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Room room = roomManager.GetRoom("Test");
        string userID = Context.UserIdentifier ?? throw new InvalidOperationException("User identifier is unexpectedly null");

        logger.LogInformation("User disconnected: {UserID}", userID);

        await room.RemoveMember(userID);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, room.ID);

        await base.OnDisconnectedAsync(exception);
    }
}

public class Room
{
    private readonly ILogger logger;
    private readonly IHubContext<RoomHub> roomHubContext;

    public ConcurrentQueue<RoomMember> HostQueue { get; private set; } = new();
    public Session? Session { get; private set; }
    public Chat Chat { get; private set; }

    public ConcurrentDictionary<string, RoomMember> Members { get; set; } = new();

    public string Name { get; set; } = "";
    public string ID { get; } = Guid.NewGuid().ToString();

    public void RemoveFromHostQueue(string userID)
    {
        HostQueue = new ConcurrentQueue<RoomMember>(HostQueue.Where(member => member.ID != userID));
    }

    public async Task RemoveMember(string userID)
    {
        if (Session?.Host.ID == userID)
            await NextSession();

        bool shouldReceiveRoom = false;
        if (HostQueue.FirstOrDefault(h => h.ID == userID) is not null)
        {
            RemoveFromHostQueue(userID);
            shouldReceiveRoom = true;
        }

        if (Session != null)
        {
            if (Session.Skips.Contains(userID))
            {
                Session.Skips.Remove(userID);
                shouldReceiveRoom = true;
            }

            if (Session.Likes.Contains(userID))
            {
                Session.Likes.Remove(userID);
                shouldReceiveRoom = true;
            }
        }

        if (shouldReceiveRoom)
            await roomHubContext.Clients.Group(ID).SendAsync("ReceiveRoom", this);

        Members.Remove(userID, out _);
        await roomHubContext.Clients.Group(ID).SendAsync("MemberLeft", userID);
    }

    public async Task NextSession(bool preventHostRequeue = false)
    {
        logger.LogInformation("Advancing to next session...");
        TimerCallback onSessionTimerEnd = async (Object? state) =>
        {
            logger.LogInformation("Session timer ended");
            await NextSession();
        };

        Session?.Timer.Dispose();

        RoomMember? prevHost = Session?.Host;
        Session = null;

        try
        {
            // find a host with at least one media 
            RoomMember? nextHost = null;
            Media? nextHostMedia = null;
            while (HostQueue.TryDequeue(out RoomMember? potentialHost))
            {
                Media potentialMedia = await roomHubContext.Clients.Client(potentialHost.ID).InvokeAsync<Media>("DequeueMediaQueue", CancellationToken.None);
                if (potentialMedia != null)
                {
                    Console.WriteLine($"URL: {potentialMedia.Url}");
                    nextHost = potentialHost;
                    nextHostMedia = potentialMedia;
                    break;
                }
            }

            // set the new host 
            if (nextHost != null && nextHostMedia != null)
            {
                Session = new Session(nextHost, nextHostMedia, onSessionTimerEnd);
                if (!preventHostRequeue && prevHost != null)
                    HostQueue.Enqueue(prevHost);
            }
            else if (prevHost != null && !preventHostRequeue)
            {
                // fallback to previous host, if they have another media
                Media? prevHostMedia = await roomHubContext.Clients.Client(prevHost.ID).InvokeAsync<Media?>("DequeueMediaQueue", CancellationToken.None);
                if (prevHostMedia != null)
                    Session = new Session(prevHost, prevHostMedia, onSessionTimerEnd);
            }

            await roomHubContext.Clients.Group(ID).SendAsync("ReceiveRoom", this);
            logger.LogInformation("Advanced to next session");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred when advancing to next session");
        }
    }

    public Room(string name, IHubContext<RoomHub> roomHubContext, ILogger logger)
    {
        this.roomHubContext = roomHubContext;
        this.logger = logger;
        Name = name;
        Chat = new Chat(this);
    }

    public override string ToString()
    {
        List<string> memberStrs = new();
        foreach (RoomMember member in Members.Values)
            memberStrs.Add(member.ToString());
        return $"[{string.Join(",", memberStrs)}]";
    }
}

public record RoomMember
{
    public string ID { get; }
    public string Username { get; set; }
    public string Avatar { get; set; } = "";

    public RoomMember(string id, string username, string avatar)
    {
        ID = id;
        Username = username;
        Avatar = avatar;
    }

    public override string ToString() => Username;
}

public class Session
{
    public RoomMember Host { get; private set; }
    public DateTime StartTime { get; private set; } = DateTime.UtcNow;
    public Timer Timer { get; private set; }

    public Media Media { get; set; }
    public HashSet<string> Likes { get; set; } = new();
    public HashSet<string> Skips { get; set; } = new();

    public bool CanSkip(Room room)
    {
        // TODO: figure out best equation for this. Probably should be a config
        int skipsNeeded = (int)Math.Ceiling(room.Members.Count / 1.0);
        return Skips.Count >= skipsNeeded;
    }

    public Session(RoomMember host, Media media, TimerCallback onSessionEnd)
    {
        Media = media;
        Timer = new Timer(onSessionEnd, null, media.Duration.GetValueOrDefault(), Timeout.InfiniteTimeSpan);
        Host = host;
    }
}

public class Media
{
    public string ID { get; set; } = default!;
    public Uri Url { get; set; } = default!;
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public TimeSpan? Duration { get; set; } = TimeSpan.Zero;
    public Thumbnail? Thumbnail { get; set; }

    public Media()
    {
    }

    public Media(string id, Uri url)
    {
        ID = id;
        Url = url;
    }

    public Media(VideoSearchResult videoSearchResult)
    {
        Url = new Uri(videoSearchResult.Url);
        Title = videoSearchResult.Title;
        Author = videoSearchResult.Author.ChannelTitle;
        Duration = videoSearchResult.Duration;
        ID = videoSearchResult.Id;
        Thumbnail = videoSearchResult.Thumbnails[0];
    }
}

public class Chat
{
    public List<ChatMessage> Messages { get; private set; } = new();
    private Room room;

    public ChatMessage Send(string authorID, string content)
    {
        string username = room.Members[authorID]?.Username ?? "";
        string avatar = room.Members[authorID]?.Avatar ?? "";
        ChatMessage newChatMessage = new(authorID, username, avatar, content, DateTime.UtcNow);
        Messages.Add(newChatMessage);
        return newChatMessage;
    }

    public Chat(Room room)
    {
        this.room = room;
    }
}

public class ChatMessage
{
    public string Content { get; }
    public string AuthorID { get; }
    public string CachedUsername { get; set; }
    public string CachedAvatar { get; set; }
    public DateTime Date { get; }

    public ChatMessage(string authorID, string username, string avatar, string content, DateTime date)
    {
        Date = date;
        AuthorID = authorID;
        Content = content;
        CachedAvatar = avatar;
        CachedUsername = username;
    }
}

public class NameUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        Console.WriteLine("test");
        return connection.GetHttpContext()?.Request.Query["access_token"];
    }
}
