using PowerPositionReportGenerator;
using PowerPositionReportGenerator.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<IPowerService, PowerServiceWrapper>();
builder.Services.AddHostedService<PowerPositionReportService>();

var host = builder.Build();
host.Run();
