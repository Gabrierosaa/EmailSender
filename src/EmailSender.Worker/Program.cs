using EmailSender.Application;
using EmailSender.Application.Abstractions;
using EmailSender.Infrastructure;
using EmailSender.Worker;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<EnvioEmailWorkerSettings>()
    .Bind(builder.Configuration.GetSection(EnvioEmailWorkerSettings.Secao))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RetrySettings>()
    .Bind(builder.Configuration.GetSection(RetrySettings.Secao))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<RetrySettings>, RetrySettingsValidator>();

builder.Services.AddApplication();
// Registrado depois do AddApplication para sobrescrever o padrão.
builder.Services.AddSingleton(sp =>
{
    var s = sp.GetRequiredService<IOptions<RetrySettings>>().Value;
    return new PoliticaRetry(s.MaxTentativas, s.IntervalosSegundos.Select(seg => TimeSpan.FromSeconds(seg)).ToList());
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));
builder.Services.AddHostedService<EnvioEmailWorker>();

var host = builder.Build();
host.Run();
