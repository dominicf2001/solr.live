class User
{
    public string ID { get; private set; } = "";

    public User(string id)
    {
        ID = id;
    }

    public override string ToString() => ID;
}

class Song
{
    Uri Link { get; set; }

    public Song(Uri link)
    {
        Link = link;
    }
}

class SongSession
{
    private DateTime startTime = DateTime.Now;
    public Song Song { get; set; }
    public int Likes { get; set; } = 0;
    public int Dislikes { get; set; } = 0;
    public int CurrentPlaybackTime
    {
        get
        {
            TimeSpan duration = DateTime.Now - startTime;
            return (int)duration.TotalSeconds;
        }
    }

    public SongSession(Song song)
    {
        Song = song;
    }
}

class Room
{
    Queue<RoomMember> djQueue = new();

    public List<RoomMember> Members = new();
    public SongSession? SongSession { get; private set; }
    public RoomMember? DJ;

    public string Name { get; set; } = "";
    public string Owner { get; set; } = "";

    public void nextDJ()
    {
        RoomMember? prevDJ = DJ;
        DJ = null;
        SongSession = null;

        // find a DJ with at least one song 
        RoomMember? nextDJ;
        while (djQueue.TryDequeue(out nextDJ) && nextDJ.SongQueue.Count == 0)
            nextDJ = null;

        // set the new DJ
        if (nextDJ is null && prevDJ?.SongQueue.Count > 0)
        {
            DJ = prevDJ;
            SongSession = new SongSession(prevDJ.SongQueue.Dequeue());
        }
        else
        {
            DJ = nextDJ;
            SongSession = new SongSession(nextDJ!.SongQueue.Dequeue());
            if (prevDJ != null)
                djQueue.Enqueue(prevDJ);
        }
    }

    public Room(string name)
    {
        this.Name = name;
    }

    public override string ToString()
    {
        List<string> memberStrs = new();
        foreach (RoomMember member in Members)
            memberStrs.Add(member.ToString());
        return $"[{string.Join(",", memberStrs)}]";
    }
}

class RoomMember
{
    public Queue<Song> SongQueue = new();
    public User User { get; set; }

    public RoomMember(User user)
    {
        this.User = user;
    }

    public override string ToString() => User.ID;
}
