using Amazon.XRay.Recorder.Handlers.AspNetCore;
using Dapper;
using HrService.Repositories;
using HrService.Services;
using Serilog;

// Postgres uses snake_case columns — Dapper maps them to PascalCase C# properties automatically
DefaultTypeMap.MatchNamesWithUnderscores = true;
// Dapper doesn't handle DateOnly natively — teach it to convert from DateTime
SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "HR Service API", Version = "v1" }));

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// scoped so each request gets its own instance
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();

// service URLs — config takes priority, then env var, then container-network default
var workflowSvcUrl = builder.Configuration["Services:WorkflowSvc"]
    ?? Environment.GetEnvironmentVariable("WORKFLOW_SVC_URL")
    ?? "http://workflow-svc:8080";

var onboardingSvcUrl = builder.Configuration["Services:OnboardingSvc"]
    ?? Environment.GetEnvironmentVariable("ONBOARDING_SVC_URL")
    ?? "http://onboarding-svc:8080";

// longer timeout for workflow-svc — startup can be slow on cold start while Temporal connects
builder.Services.AddHttpClient<IWorkflowTriggerService, WorkflowTriggerService>(client =>
{
    client.BaseAddress = new Uri(workflowSvcUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// named clients used for BFF proxy calls (onboarding status + retry)
builder.Services.AddHttpClient("onboarding-svc", client =>
{
    client.BaseAddress = new Uri(onboardingSvcUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient("workflow-svc", client =>
{
    client.BaseAddress = new Uri(workflowSvcUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Separate client for SSE streaming — no timeout because the connection
// stays open for as long as the user has the page open.
builder.Services.AddHttpClient("onboarding-svc-sse", client =>
{
    client.BaseAddress = new Uri(onboardingSvcUrl);
    client.Timeout = Timeout.InfiniteTimeSpan;
});

var app = builder.Build();

app.UseCors();
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/health"), b => b.UseXRay("hr-svc"));
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
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "HR Service v1"));

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "hr-svc" }));

app.Run();

// Dapper type handler — converts PostgreSQL date to DateOnly and back
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(System.Data.IDbDataParameter parameter, DateOnly value)
    {
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value)
    {
        return value is DateOnly d ? d : DateOnly.FromDateTime((DateTime)value);
    }
}
