using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;


public class RoomManager
{
    private readonly ConcurrentDictionary<string, Room> rooms = new();
    private readonly ILogger<RoomManager> logger;
    private readonly IHubContext<RoomHub> roomHubContext;

    public RoomManager(ILogger<RoomManager> logger, IHubContext<RoomHub> roomHubContext)
    {
        this.logger = logger;
        this.roomHubContext = roomHubContext;
    }

    public Room GetRoom(string roomName)
    {
        return rooms.GetOrAdd(roomName, name => new Room(name, roomHubContext, logger));
    }

    public IEnumerable<Room> GetAllRooms() => rooms.Values;
}

public class RoomManagerBackground : BackgroundService
{
    private readonly RoomManager roomManager;
    private readonly ILogger<RoomManager> logger;
    private readonly TimeSpan timeoutPeriod = TimeSpan.FromSeconds(15);
    private readonly TimeSpan cleanupInterval = TimeSpan.FromSeconds(15);

    public RoomManagerBackground(ILogger<RoomManager> logger, RoomManager roomManager)
    {
        this.logger = logger;
        this.roomManager = roomManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            CleanupInactiveUsers();
            await Task.Delay(cleanupInterval, stoppingToken);
        }
    }

    public void CleanupInactiveUsers()
    {
        logger.LogInformation("Cleaning inactive users...");

        foreach (Room room in roomManager.GetAllRooms())
        {
            logger.LogInformation($"Cleaning room: {room.Name}");
            foreach (RoomMember member in room.Members.Values)
            {
                logger.LogInformation($"Checking user: {member.ID}. Status: {member.CurrentStatus}");
                if (member.CurrentStatus == RoomMember.Status.Connected || member.DisconnectionDate is null)
                    continue;

                TimeSpan? disconnectionPeriod = DateTime.Now - member.DisconnectionDate;
                logger.LogInformation($"Checking user: {member.ID}. Disconnection date: {member.DisconnectionDate}");
                if (disconnectionPeriod != null && disconnectionPeriod > timeoutPeriod)
                {
                    logger.LogInformation($"Cleaned up user: {member.ID} after timeout.");
                    room.RemoveMember(member.ID);
                }
            }

        }
    }
}

public class RoomHub : Hub
{
    private static bool initialized = false;
    private readonly ILogger logger;
    private RoomManager roomManager;

    public RoomHub(ILogger<RoomHub> logger, RoomManager roomManager, IHubContext<RoomHub> roomHubContext)
    {
        this.logger = logger;
        this.roomManager = roomManager;
    }

    public override async Task OnConnectedAsync()
    {
        Room room = roomManager.GetRoom("Test");
        string? userID = Context.GetHttpContext()?.Request.Query["access_token"];
        if (userID is null)
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, room.Name);

        RoomMember? member;
        if (!room.Members.TryGetValue(userID, out member))
            room.Members[userID] = member = new RoomMember(userID);

        room.Members[userID].SetStatus(RoomMember.Status.Connected);

        await Clients.Caller.SendAsync("ReceiveRoom", room);
        await Clients.Caller.SendAsync("ReceiveOwnRoomMember", member);

        await Clients.Group(room.Name).SendAsync("MemberJoined", member);

        logger.LogInformation("User connected: {UserID}", userID);
        await base.OnConnectedAsync();
    }

    public async Task ToggleHostQueueStatus()
    {
        Room room = roomManager.GetRoom("Test");
        string? userID = Context.GetHttpContext()?.Request.Query["access_token"];
        if (userID is null)
            return;

        logger.LogInformation("User attempting to join host queue: {UserID}", userID);

        room.Members.TryGetValue(userID, out RoomMember? member);
        if (member is null)
            return;

        bool inHostQueue = room.HostQueue.FirstOrDefault(h => h.ID == userID) is not null;
        bool isHost = room.Session?.Host.ID == userID;
        if (isHost)
        {
            room.NextSession(preventRequeue: true);
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
                room.NextSession();
            }
        }

        await Clients.Group(room.Name).SendAsync("ReceiveHostQueue", room.HostQueue);
    }

    public async Task QueueMedia(Media media)
    {
        Room room = roomManager.GetRoom("Test");
        string? userID = Context.GetHttpContext()?.Request.Query["access_token"];
        if (userID is null)
            return;

        logger.LogInformation("User: {UserID} attempting to queue media {MediaLink}", userID, media.Link);
        if (room.Members.TryGetValue(userID, out RoomMember? member))
        {
            member.MediaQueue.Enqueue(media);
            logger.LogInformation("User: {UserID} queued media {MediaLink}", userID, media.Link);
            await Clients.Caller.SendAsync("ReceiveOwnRoomMember", member);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Room room = roomManager.GetRoom("Test");
        string? userID = Context.GetHttpContext()?.Request.Query["access_token"];
        if (userID is null)
            return;

        logger.LogInformation("User disconnected: {UserID}", userID);

        room.Members[userID].SetStatus(RoomMember.Status.Disconnected);

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
        List<RoomMember> remainingMembers = new List<RoomMember>();
        while (HostQueue.TryDequeue(out RoomMember? member))
            if (member.ID != userID)
                remainingMembers.Add(member);

        foreach (var remainingMember in remainingMembers)
            HostQueue.Enqueue(remainingMember);
    }

    public async void RemoveMember(string userID)
    {
        if (Session?.Host.ID == userID)
            NextSession();

        if (HostQueue.FirstOrDefault(h => h.ID == userID) is not null)
        {
            RemoveFromHostQueue(userID);
            await roomHubContext.Clients.Group(Name).SendAsync("ReceiveHostQueue", HostQueue);
        }

        Members.Remove(userID, out _);
        await roomHubContext.Clients.Group(Name).SendAsync("MemberLeft", userID);
    }

    public async void NextSession(bool preventRequeue = false)
    {
        logger.LogInformation("Advancing to next session...");
        TimerCallback onSessionTimerEnd = (Object? state) =>
        {
            logger.LogInformation("Session timer ended");
            NextSession();
        };

        Session?.Timer.Dispose();

        RoomMember? prevHost = Session?.Host;
        Session = null;

        // find a host with at least one media 
        RoomMember? nextHost;
        while (HostQueue.TryDequeue(out nextHost) && nextHost.MediaQueue.Count == 0)
            nextHost = null;

        // set the new host 
        if (nextHost != null)
        {
            Session = new Session(nextHost, onSessionTimerEnd);
            if (!preventRequeue && prevHost != null)
                HostQueue.Enqueue(prevHost);
        }
        else
        {
            // fallback to previous host, if allowed
            if (!preventRequeue && prevHost?.MediaQueue.Count > 0)
                Session = new Session(prevHost, onSessionTimerEnd);
        }

        await roomHubContext.Clients.Group(Name).SendAsync("ReceiveRoom", this);
        if (Session != null)
        {
            logger.LogInformation("Notifying new host: {HostID} of changes", Session.Host.ID);
            await roomHubContext.Clients.Users(Session.Host.ID).SendAsync("ReceiveOwnRoomMember", Session.Host);
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
    public enum Status
    {
        Connected,
        Disconnected
    }

    public Status CurrentStatus { get; private set; }
    public DateTime? DisconnectionDate { get; private set; }

    public Queue<Media> MediaQueue { get; set; } = new();
    public string ID { get; private set; }

    public void SetStatus(Status newStatus)
    {
        CurrentStatus = newStatus;
        DisconnectionDate = newStatus is Status.Disconnected ?
            DateTime.Now :
            null;
    }

    public RoomMember(string id)
    {
        CurrentStatus = Status.Disconnected;
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

    public Session(RoomMember host, TimerCallback onSessionEnd)
    {
        if (host.MediaQueue.Count == 0)
            throw new InvalidOperationException("Host must have at least one media in the queue.");

        Media = host.MediaQueue.Dequeue();
        Timer = new Timer(onSessionEnd, null, Media.Duration, Timeout.InfiniteTimeSpan);
        Host = host;
    }

    public override string ToString()
    {
        return Media.Link.ToString();
    }
}

public class Media
{
    public Uri Link { get; set; }
    public TimeSpan Duration { get; private set; }

    public Media(Uri link, TimeSpan duration)
    {
        Link = link;
        Duration = duration;
    }
}

public class NameUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.GetHttpContext()?.Request.Query["access_token"];
    }
}
