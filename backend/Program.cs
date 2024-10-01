using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, NameUserIdProvider>();
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<YoutubeExplode.YoutubeClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

using var httpEventListener = new HttpEventListener();

app.MapHub<RoomHub>("/roomHub");

app.Run("http://0.0.0.0:5066");
