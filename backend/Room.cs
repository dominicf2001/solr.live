using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

public class RoomHub : Hub
{
    private readonly ILogger<RoomHub> logger;

    private static bool initialized = false;
    static Room room;

    public RoomHub(ILogger<RoomHub> logger, IHubContext<RoomHub> roomHubContext)
    {
        this.logger = logger;
        if (!initialized)
        {
            room = new Room("Test Room", roomHubContext, logger);
            InitializeTestData();
            initialized = true;
        }
    }

    private void InitializeTestData()
    {
        logger.LogInformation("Initializing test data...");
        RoomMember djBill = new RoomMember("DJ Bill");
        /*{*/
        /*    MediaQueue = new Queue<Media>(new[]*/
        /*    {*/
        /*        new Media(new Uri("https://www.youtube.com/watch?v=5IsSpAOD6K8"), new TimeSpan(0, 3, 44)),*/
        /*        new Media(new Uri("https://www.youtube.com/watch?v=_3eC35LoF4U"), new TimeSpan(0, 3, 53)),*/
        /*    })*/
        /*};*/
        room.Members.TryAdd("djBill", djBill);
        room.HostQueue.Enqueue(djBill);
        room.NextSession();
    }

    public override async Task OnConnectedAsync()
    {
        string userID = Context.ConnectionId;
        logger.LogInformation("User connected: {UserID}", userID);

        await Groups.AddToGroupAsync(userID, room.Name);

        RoomMember? member;
        if (!room.Members.TryGetValue(userID, out member))
            room.Members[Context.ConnectionId] = member = new RoomMember(userID);

        await Clients.Caller.SendAsync("ReceiveRoom", room);
        await Clients.Caller.SendAsync("ReceiveThisRoomMember", member);

        await Clients.Group(room.Name).SendAsync("UserConnected", member);

        await base.OnConnectedAsync();
    }

    public async Task ToggleHostQueueStatus()
    {
        string userID = Context.ConnectionId;
        logger.LogInformation("User attempting to join host queue: {UserID}", userID);

        room.Members.TryGetValue(userID, out RoomMember? member);
        if (member is null)
            return;

        bool inHostQueue = room.HostQueue.FirstOrDefault(h => h.ID == userID) is not null;
        bool isHost = room.Session?.Host.ID == userID;
        if (isHost)
        {
            room.NextSession(preventRequeuePrevHost: true);
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
        string userID = Context.ConnectionId;
        logger.LogInformation("User: {UserID} attempting to queue media {MediaLink}", userID, media.Link);
        if (room.Members.TryGetValue(userID, out RoomMember? member))
        {
            logger.LogInformation("User: {UserID} queuing media {MediaLink}", userID, media.Link);
            member.MediaQueue.Enqueue(media);
            await Clients.Caller.SendAsync("ReceiveThisRoomMember", member);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string userID = Context.ConnectionId;
        logger.LogInformation("User disconnected: {UserID}", userID);

        if (room.Session?.Host.ID == userID)
            room.NextSession();

        if (room.HostQueue.FirstOrDefault(h => h.ID == userID) is not null)
        {
            room.RemoveFromHostQueue(userID);
            await Clients.Group(room.Name).SendAsync("ReceiveHostQueue", room.HostQueue);
        }

        await Groups.RemoveFromGroupAsync(userID, room.Name);
        await Clients.Group(room.Name).SendAsync("UserDisconnected", userID);
        room.Members.Remove(userID, out _);

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
    public string Owner { get; private set; } = "";

    public void RemoveFromHostQueue(string userID)
    {
        List<RoomMember> remainingMembers = new List<RoomMember>();
        while (HostQueue.TryDequeue(out RoomMember? member))
            if (member.ID != userID)
                remainingMembers.Add(member);

        foreach (var remainingMember in remainingMembers)
            HostQueue.Enqueue(remainingMember);
    }

    public async void NextSession(bool preventRequeuePrevHost = false)
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
            if (prevHost != null)
                HostQueue.Enqueue(prevHost);
        }
        else
        {
            // fallback to previous host, if allowed
            if (!preventRequeuePrevHost && prevHost?.MediaQueue.Count > 0)
                Session = new Session(prevHost, onSessionTimerEnd);
        }

        await roomHubContext.Clients.Group(Name).SendAsync("ReceiveRoom", this);
        if (Session != null)
        {
            logger.LogInformation("Notifying new host: {HostID} of changes", Session.Host.ID);
            await roomHubContext.Clients.Client(Session.Host.ID).SendAsync("ReceiveThisRoomMember", Session.Host);
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
    public Queue<Media> MediaQueue { get; set; } = new();
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
