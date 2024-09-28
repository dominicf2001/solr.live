using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using YoutubeExplode.Common;

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

        await Groups.AddToGroupAsync(Context.ConnectionId, room.Name);

        RoomMember? member;
        if (!room.Members.TryGetValue(userID, out member))
            room.Members[userID] = member = new RoomMember(userID);

        await Clients.Caller.SendAsync("ReceiveRoom", room);
        await Clients.Caller.SendAsync("ReceiveOwnID", userID);
        await Clients.Group(room.Name).SendAsync("MemberJoined", member);

        logger.LogInformation("User connected: {UserID}", userID);
        await base.OnConnectedAsync();
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

        await Clients.Group(room.Name).SendAsync("ReceiveRoom", room);
    }

    public async Task<IEnumerable<YoutubeExplode.Search.VideoSearchResult>> YTSearch(string query)
    {
        string userID = Context.UserIdentifier ?? throw new InvalidOperationException("User identifier is unexpectedly null");
        logger.LogInformation($"{userID} searching for {query}");
        return await yt.Search.GetVideosAsync(query).CollectAsync(5);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Room room = roomManager.GetRoom("Test");
        string userID = Context.UserIdentifier ?? throw new InvalidOperationException("User identifier is unexpectedly null");

        logger.LogInformation("User disconnected: {UserID}", userID);

        await room.RemoveMember(userID);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, room.Name);

        await base.OnDisconnectedAsync(exception);
    }
}

public class Room
{
    private readonly ILogger logger;
    private readonly IHubContext<RoomHub> roomHubContext;

    public ConcurrentQueue<RoomMember> HostQueue { get; set; } = new();
    public Session? Session { get; private set; }

    public ConcurrentDictionary<string, RoomMember> Members { get; set; } = new();

    public string Name { get; set; } = "";

    public void RemoveFromHostQueue(string userID)
    {
        HostQueue = new ConcurrentQueue<RoomMember>(HostQueue.Where(member => member.ID != userID));
    }

    public async Task RemoveMember(string userID)
    {
        if (Session?.Host.ID == userID)
            await NextSession();

        if (HostQueue.FirstOrDefault(h => h.ID == userID) is not null)
        {
            RemoveFromHostQueue(userID);
            await roomHubContext.Clients.Group(Name).SendAsync("ReceiveRoom", this);
        }

        Members.Remove(userID, out _);
        await roomHubContext.Clients.Group(Name).SendAsync("MemberLeft", userID);
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

            await roomHubContext.Clients.Group(Name).SendAsync("ReceiveRoom", this);
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
    }

    public override string ToString()
    {
        List<string> memberStrs = new();
        foreach (RoomMember member in Members.Values)
            memberStrs.Add(member.ToString());
        return $"[{string.Join(",", memberStrs)}]";
    }
}

public class RoomMember
{
    public string ID { get; private set; }

    public RoomMember(string id)
    {
        ID = id;
    }

    public override string ToString() => ID;
}

public class Session
{
    public RoomMember Host { get; private set; }
    public DateTime StartTime { get; private set; } = DateTime.UtcNow;
    public Timer Timer { get; private set; }

    public Media Media { get; set; }
    public int Likes { get; set; } = 0;
    public int Dislikes { get; set; } = 0;

    public Session(RoomMember host, Media media, TimerCallback onSessionEnd)
    {
        Media = media;
        Timer = new Timer(onSessionEnd, null, Media.Duration, Timeout.InfiniteTimeSpan);
        Host = host;
    }

    public override string ToString()
    {
        return Media.URL.ToString();
    }
}

// TODO: just use video result class?
public class Media
{
    public Uri URL { get; set; }
    public TimeSpan Duration { get; private set; }

    public Media(Uri url, TimeSpan duration)
    {
        this.URL = url;
        Duration = duration;
    }
}

public class NameUserIdProvider : IUserIdProvider
{
    public string GetUserId(HubConnectionContext connection)
    {
        return connection.ConnectionId;
    }
}
