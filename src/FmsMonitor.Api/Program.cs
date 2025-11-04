using FmsMonitor.Api.Repositories;
using FmsMonitor.Api.Services;
using OllamaSharp;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/fms-monitor-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "FMS Monitor API",
        Version = "v1",
        Description = "API cho hệ thống giám sát vận hành máy trong sản xuất với AI Agent"
    });
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// Register Repositories
builder.Services.AddScoped<IMachineAmperageRepository, MachineAmperageRepository>();
builder.Services.AddScoped<IMonitoringConfigurationRepository, MonitoringConfigurationRepository>();
builder.Services.AddScoped<IAlertRepository, AlertRepository>();
builder.Services.AddScoped<IEmailRecipientRepository, EmailRecipientRepository>();
builder.Services.AddScoped<IPostgresVectorRepository, PostgresVectorRepository>();

// Register Services
builder.Services.AddScoped<IAmperageMonitoringService, AmperageMonitoringService>();
builder.Services.AddScoped<IDataSynchronizationService, DataSynchronizationService>();
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();
builder.Services.AddScoped<IAnomalyDetectionAgent, AnomalyDetectionAgent>();

// Register Ollama Client
builder.Services.AddSingleton<IOllamaApiClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var ollamaEndpoint = config["Ollama:Endpoint"] ?? "http://localhost:11434";
    return new OllamaApiClient(ollamaEndpoint);
});

// Register Background Service
builder.Services.AddHostedService<MonitoringBackgroundService>();

// Add Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FMS Monitor API v1");
        c.RoutePrefix = string.Empty; // Swagger UI at root
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// Log startup information
Log.Information("FMS Monitor API đang khởi động...");
Log.Information("Môi trường: {Environment}", app.Environment.EnvironmentName);

try
{
    app.Run();
    Log.Information("FMS Monitor API đã dừng");
}
catch (Exception ex)
{
    Log.Fatal(ex, "FMS Monitor API gặp lỗi nghiêm trọng");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
