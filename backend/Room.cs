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
            SongQueue = new Queue<Song>(new[]
            {
                new Song(new Uri("https://www.youtube.com/watch?v=_Td7JjCTfyc"), new TimeSpan(0, 0, 30)),
                new Song(new Uri("https://www.youtube.com/watch?v=d95PPykB2vE"), new TimeSpan(0, 0, 30)),
            })
        };
        room.Members.TryAdd("djBill", djBill);
        room.DJQueue.Enqueue(djBill);
        room.NextDJSession();
    }

    public override async Task OnConnectedAsync()
    {
        string userID = Context.ConnectionId;
        logger.LogInformation("User connected: {UserID}", userID);

        await Groups.AddToGroupAsync(userID, room.Name);

        RoomMember? member;
        if (!room.Members.TryGetValue(userID, out member))
            room.Members[Context.ConnectionId] = member = new RoomMember(userID);

        await Clients.Caller.SendAsync("ReceiveDJSession", room.DJSession);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string userID = Context.ConnectionId;
        logger.LogInformation("User disconnected: {UserID}", userID);

        await Groups.RemoveFromGroupAsync(userID, room.Name);
        room.Members.Remove(userID, out _);

        if (room.DJSession?.DJ.ID == userID)
            room.NextDJSession();

        await base.OnDisconnectedAsync(exception);
    }
}

public class Room
{
    private readonly ILogger logger;
    private readonly IHubContext<RoomHub> roomHubContext;

    public ConcurrentQueue<RoomMember> DJQueue { get; set; } = new();
    public DJSession? DJSession { get; private set; }

    public ConcurrentDictionary<string, RoomMember> Members { get; set; } = new();

    public string Name { get; set; } = "";
    public string Owner { get; private set; } = "";

    public void NextDJSession()
    {
        logger.LogInformation("Advancing to next DJ session...");
        TimerCallback onDJSessionTimerEnd = async (Object? state) =>
        {
            logger.LogInformation("DJ session timer ended");
            NextDJSession();
            await roomHubContext.Clients.Group(Name).SendAsync("ReceiveDJSession", DJSession);
        };

        DJSession?.Timer.Dispose();

        RoomMember? prevDJ = DJSession?.DJ;
        DJSession = null;

        // find a DJ with at least one song 
        RoomMember? nextDJ;
        while (DJQueue.TryDequeue(out nextDJ) && nextDJ.SongQueue.Count == 0)
            nextDJ = null;

        // set the new DJ
        if (nextDJ is null)
        {
            if (prevDJ?.SongQueue.Count > 0)
                DJSession = new DJSession(prevDJ, onDJSessionTimerEnd);
        }
        else
        {
            DJSession = new DJSession(nextDJ, onDJSessionTimerEnd);
            if (prevDJ != null)
                DJQueue.Enqueue(prevDJ);
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
    public Queue<Song> SongQueue { get; set; } = new();
    public string ID { get; private set; }

    public RoomMember(string id)
    {
        ID = id;
    }

    public override string ToString() => ID;
}

public class DJSession
{
    public RoomMember DJ { get; private set; }
    public DateTime StartTime { get; private set; } = DateTime.UtcNow;
    public Timer Timer { get; private set; }

    public Song Song { get; set; }
    public int Likes { get; set; } = 0;
    public int Dislikes { get; set; } = 0;

    public DJSession(RoomMember dj, TimerCallback onDJSessionEnd)
    {
        if (dj.SongQueue.Count == 0)
            throw new InvalidOperationException("DJ must have at least one song in the queue.");

        Song = dj.SongQueue.Dequeue();
        Timer = new Timer(onDJSessionEnd, null, Song.Duration, Timeout.InfiniteTimeSpan);
        DJ = dj;
    }

    public override string ToString()
    {
        return Song.Link.ToString();
    }
}

public class Song
{
    public Uri Link { get; set; }
    public TimeSpan Duration { get; private set; }

    public Song(Uri link, TimeSpan duration)
    {
        Link = link;
        Duration = duration;
    }
}
