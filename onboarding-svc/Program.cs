using Amazon.XRay.Recorder.Handlers.AspNetCore;
using Dapper;
using OnboardingService.Repositories;
using OnboardingService.Services;
using Serilog;
using StackExchange.Redis;

// Postgres uses snake_case columns — Dapper maps them to PascalCase C# properties automatically
DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Onboarding Service API", Version = "v1" }));

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
// abortConnect=false — don't crash on startup if Valkey isn't up yet; multiplexer retries in background
var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
redisOptions.AbortOnConnectFail = false;
var redis = ConnectionMultiplexer.Connect(redisOptions);
if (!redis.IsConnected)
{
    Log.Warning("Valkey is not reachable at {Endpoint} — SSE live updates will be unavailable until it connects", redisConnectionString);
}
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton<IOnboardingPublisher, ValkeyPublisher>();

// scoped so each request gets its own instance
builder.Services.AddScoped<IOnboardingRepository, OnboardingRepository>();
builder.Services.AddScoped<IOnboardingRecordService, OnboardingRecordService>();

var app = builder.Build();

app.UseCors();
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/health"), b => b.UseXRay("onboarding-svc"));
// suppress /health logs at Debug to keep noise down
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (ctx, _, ex) =>
        ex is null && ctx.Request.Path.StartsWithSegments("/health")
            ? Serilog.Events.LogEventLevel.Debug
            : ex is not null || ctx.Response.StatusCode >= 500
                ? Serilog.Events.LogEventLevel.Error
                : Serilog.Events.LogEventLevel.Information;
});
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Onboarding Service v1"));

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "onboarding-svc" }));

app.Run();
