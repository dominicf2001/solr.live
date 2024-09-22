using Microsoft.AspNetCore.SignalR;

public class RoomHub : Hub
{
    private readonly ILogger<RoomHub> logger;

    private static bool testDataInitialized = false;
    static Room room = new("Test Room");

    public RoomHub(ILogger<RoomHub> logger)
    {
        this.logger = logger;
        if (!testDataInitialized)
        {
            InitializeTestData();
            testDataInitialized = true;
        }
    }

    private void InitializeTestData()
    {
        Uri testSongUrl = new Uri("https://www.youtube.com/watch?v=G9M3GVejHNE");
        RoomMember djBill = new RoomMember("DJ Bill")
        {
            SongQueue = new Queue<Song>(new[]
            {
                new Song(testSongUrl)
            })
        };
        room.Members.TryAdd("djBill", djBill);
        room.DJQueue.Enqueue(djBill);
        room.nextDJ();
    }

    public override async Task OnConnectedAsync()
    {
        string userID = Context.ConnectionId;
        logger.LogInformation("User connected: {UserID}", userID);

        await Groups.AddToGroupAsync(userID, room.Name);

        RoomMember? member;
        if (!room.Members.TryGetValue(userID, out member))
            room.Members[Context.ConnectionId] = member = new RoomMember(userID);

        await Clients.Caller.SendAsync("ReceiveSongSession", room.SongSession);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string userID = Context.ConnectionId;
        logger.LogInformation("User disconnected: {UserID}", userID);

        await Groups.RemoveFromGroupAsync(userID, room.Name);
        room.Members.Remove(userID);

        if (room.DJ?.ID == userID)
            room.nextDJ();

        await base.OnDisconnectedAsync(exception);
    }
}

public class Song
{
    public Uri Link { get; set; }

    public Song(Uri link)
    {
        Link = link;
    }
}

public class SongSession
{
    public DateTime StartTime { get; private set; } = DateTime.Now;
    public Song Song { get; set; }
    public int Likes { get; set; } = 0;
    public int Dislikes { get; set; } = 0;

    public SongSession(Song song)
    {
        Song = song;
    }

    public override string ToString()
    {
        return Song.Link.ToString();
    }
}

public class Room
{
    public Queue<RoomMember> DJQueue { get; set; } = new();

    public Dictionary<string, RoomMember> Members { get; set; } = new();
    public SongSession? SongSession { get; private set; }
    public RoomMember? DJ { get; private set; }

    public string Name { get; set; } = "";
    public string Owner { get; private set; } = "";

    public void nextDJ()
    {
        RoomMember? prevDJ = DJ;
        DJ = null;
        SongSession = null;

        // find a DJ with at least one song 
        RoomMember? nextDJ;
        while (DJQueue.TryDequeue(out nextDJ) && nextDJ.SongQueue.Count == 0)
            nextDJ = null;

        // set the new DJ
        if (nextDJ is null)
        {
            if (prevDJ?.SongQueue.Count > 0)
            {

                DJ = prevDJ;
                SongSession = new SongSession(prevDJ.SongQueue.Dequeue());
            }
        }
        else
        {
            DJ = nextDJ;
            SongSession = new SongSession(nextDJ.SongQueue.Dequeue());
            if (prevDJ != null)
                DJQueue.Enqueue(prevDJ);
        }
    }

    public Room(string name)
    {
        this.Name = name;
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
