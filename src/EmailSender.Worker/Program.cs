using EmailSender.Application;
using EmailSender.Infrastructure;
using EmailSender.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<EnvioEmailWorkerSettings>()
    .Bind(builder.Configuration.GetSection(EnvioEmailWorkerSettings.Secao))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));
builder.Services.AddHostedService<EnvioEmailWorker>();

var host = builder.Build();
host.Run();
