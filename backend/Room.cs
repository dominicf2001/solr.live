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
        RoomMember djBill = new RoomMember("DJ Bill")
        {
            MediaQueue = new Queue<Media>(new[]
            {
                new Media(new Uri("https://www.youtube.com/watch?v=5IsSpAOD6K8"), new TimeSpan(0, 3, 44)),
                new Media(new Uri("https://www.youtube.com/watch?v=_3eC35LoF4U"), new TimeSpan(0, 3, 53)),
            })
        };
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
        await Clients.Group(room.Name).SendAsync("UserConnected", member);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string userID = Context.ConnectionId;
        logger.LogInformation("User disconnected: {UserID}", userID);


        await Groups.RemoveFromGroupAsync(userID, room.Name);
        await Clients.Group(room.Name).SendAsync("UserDisconnected", userID);
        room.Members.Remove(userID, out _);

        if (room.Session?.Host.ID == userID)
            room.NextSession();

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

    public void NextSession()
    {
        logger.LogInformation("Advancing to next session...");
        TimerCallback onSessionTimerEnd = async (Object? state) =>
        {
            logger.LogInformation("Session timer ended");
            NextSession();
            await roomHubContext.Clients.Group(Name).SendAsync("ReceiveRoom", this);
        };

        Session?.Timer.Dispose();

        RoomMember? prevHost = Session?.Host;
        Session = null;

        // find a host with at least one media 
        RoomMember? nextHost;
        while (HostQueue.TryDequeue(out nextHost) && nextHost.MediaQueue.Count == 0)
            nextHost = null;

        // set the new host 
        if (nextHost is null)
        {
            if (prevHost?.MediaQueue.Count > 0)
                Session = new Session(prevHost, onSessionTimerEnd);
        }
        else
        {
            Session = new Session(nextHost, onSessionTimerEnd);
            if (prevHost != null)
                HostQueue.Enqueue(prevHost);
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
