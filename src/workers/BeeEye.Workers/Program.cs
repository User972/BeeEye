using BeeEye.Shared.Time;
using BeeEye.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddHostedService<HeartbeatWorker>();

var host = builder.Build();
host.Run();
