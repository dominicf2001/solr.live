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

/**/
/*public class RoomManagerBackground : BackgroundService*/
/*{*/
/*    private readonly RoomManager roomManager;*/
/*    private readonly ILogger<RoomManager> logger;*/
/**/
/*    public RoomManagerBackground(ILogger<RoomManager> logger, RoomManager roomManager)*/
/*    {*/
/*        this.logger = logger;*/
/*        this.roomManager = roomManager;*/
/*    }*/
/**/
/*    protected override async Task ExecuteAsync(CancellationToken stoppingToken)*/
/*    {*/
/*        while (!stoppingToken.IsCancellationRequested)*/
/*        {*/
/*            // await Task.Delay(, stoppingToken);*/
/*        }*/
/*    }*/
/*}*/
