using Amazon.XRay.Recorder.Handlers.AspNetCore;
using Serilog;
using Temporalio.Client;
using WorkflowService.Services;
using WorkflowService.Activities;
using WorkflowService.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Workflow Service API", Version = "v1" }));

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// onboarding-svc URL — used by OnboardingActivities to make HTTP calls inside each activity
var onboardingSvcUrl = builder.Configuration["Services:OnboardingSvc"]
    ?? Environment.GetEnvironmentVariable("ONBOARDING_SVC_URL")
    ?? "http://onboarding-svc:8080";

// named client injected into OnboardingActivities
builder.Services.AddHttpClient<OnboardingActivities>(client =>
{
    client.BaseAddress = new Uri(onboardingSvcUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Temporal connection options — registered as singleton, shared by worker and gateway
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new TemporalClientConnectOptions(
        config["Temporal:Host"]
            ?? Environment.GetEnvironmentVariable("TEMPORAL_HOST")
            ?? "temporal:7233")
    {
        Namespace = config["Temporal:Namespace"]
            ?? Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE")
            ?? "htx-onboarding"
    };
});

// TemporalClientHolder bridges async Temporal connection to synchronous DI
// TemporalWorkerService is the background service that connects and runs the worker
builder.Services.AddSingleton<TemporalClientHolder>();
builder.Services.AddSingleton<ITemporalGateway, TemporalGateway>();
builder.Services.AddHostedService<TemporalWorkerService>();

// scoped so each HTTP request gets its own service instance
builder.Services.AddScoped<IOnboardingWorkflowService, OnboardingWorkflowService>();

var app = builder.Build();

app.UseCors();
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/health"), b => b.UseXRay("workflow-svc"));
app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Workflow Service v1"));

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "workflow-svc" }));

app.Run();
