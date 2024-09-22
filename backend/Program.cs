var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

Room room = new("Test");

app.MapPost("/room/join", (HttpContext context, ILogger<Program> logger) =>
{
    string? ip = context.Connection.RemoteIpAddress?.ToString();
    logger.LogInformation($"{ip} attempting to join room");

    if (ip is null)
        return Results.BadRequest("Unable to get IP");

    RoomMember? member = room.Members.Find(m => m.User.ID == ip);
    if (member is null)
    {
        // TODO: autogenerate for anonymous users
        string userID = ip;
        room.Members.Add(new RoomMember(new User(userID)));
    }

    return Results.Ok();
})
.WithName("RoomJoin")
.WithOpenApi();

app.MapPost("/room/leave", (HttpContext context) =>
{
    string? ip = context.Connection.RemoteIpAddress?.ToString();

    if (ip is null)
        return Results.BadRequest("Unable to get IP");

    RoomMember? member = room.Members.Find(m => m.User.ID == ip);
    if (member != null)
        room.Members.Remove(member);

    return Results.Ok();
})
.WithName("RoomLeave")
.WithOpenApi();

app.MapGet("/room/members", (ILogger<Program> logger) =>
{
    logger.LogInformation($"Getting room members {room}");
    return Results.Ok(room.Members);
})
.WithName("RoomMembers")
.Produces<List<RoomMember>>()
.WithOpenApi();

app.MapGet("/room/song/position", (ILogger<Program> logger) =>
{
    DateTime startTime = DateTime.Now.AddSeconds(-20);
    logger.LogInformation($"Getting room song position");
    TimeSpan duration = DateTime.Now - startTime;
    return Results.Ok((int)duration.TotalSeconds);
})
.WithName("RoomSongPosition")
.WithOpenApi();

app.Run();
