using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Exceptions;
using WorkflowService.Activities;
using WorkflowService.Models;
using Xunit;

namespace WorkflowService.Tests;

public class OnboardingActivitiesTests
{
    private static readonly OnboardingInput SampleInput = new(
        EmployeeId: new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        EmployeeNumber: "EMP001",
        FirstName: "Alice",
        LastName: "Tan",
        Email: "alice.tan@htx.gov.sg",
        Department: "Engineering",
        Position: "Engineer"
    );

    private static readonly Guid SampleOnboardingId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static (OnboardingActivities act, RequestCapture capture) Build(
        string urlMatch, HttpStatusCode status, string responseBody)
    {
        var capture = new RequestCapture();
        var handler = new RoutedStubHandler();
        handler.Add(urlMatch, status, responseBody, capture);
        // Default catch-all for any other URL
        handler.Add("", HttpStatusCode.OK, "{}", null);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://onboarding-svc") };
        var act = new OnboardingActivities(client, NullLogger<OnboardingActivities>.Instance);
        return (act, capture);
    }

    // ── StartOnboardingRecordAsync ─────────────────────────────────────────────

    [Fact]
    public async Task StartOnboardingRecordAsync_ParsesOnboardingId_FromCamelCaseProperty()
    {
        var expected = Guid.NewGuid();
        var (act, _) = Build("/api/onboarding/start", HttpStatusCode.Created,
            JsonSerializer.Serialize(new { onboardingId = expected }));

        var result = await act.StartOnboardingRecordAsync(SampleInput);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task StartOnboardingRecordAsync_ParsesOnboardingId_FromSnakeCaseProperty()
    {
        var expected = Guid.NewGuid();
        var (act, _) = Build("/api/onboarding/start", HttpStatusCode.Created,
            JsonSerializer.Serialize(new { onboarding_id = expected }));

        var result = await act.StartOnboardingRecordAsync(SampleInput);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task StartOnboardingRecordAsync_Throws_WhenNeitherPropertyExists()
    {
        var (act, _) = Build("/api/onboarding/start", HttpStatusCode.Created,
            "{\"message\":\"something\"}");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => act.StartOnboardingRecordAsync(SampleInput));
    }

    [Fact]
    public async Task StartOnboardingRecordAsync_PostsToCorrectUrl()
    {
        var capture = new RequestCapture();
        var handler = new RoutedStubHandler();
        handler.Add("/api/onboarding/start", HttpStatusCode.Created,
            JsonSerializer.Serialize(new { onboardingId = Guid.NewGuid() }), capture);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://onboarding-svc") };
        var act = new OnboardingActivities(client, NullLogger<OnboardingActivities>.Instance);

        await act.StartOnboardingRecordAsync(SampleInput);

        Assert.Equal("/api/onboarding/start", capture.LastPath);
        Assert.Equal(HttpMethod.Post, capture.LastMethod);
    }

    // ── CreateAccountsAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateAccountsAsync_PostsToCorrectUrlWithEmployeeDetails()
    {
        var capture = new RequestCapture();
        var handler = new RoutedStubHandler();
        handler.Add($"/accounts/create", HttpStatusCode.OK, "{}", capture);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://onboarding-svc") };
        var act = new OnboardingActivities(client, NullLogger<OnboardingActivities>.Instance);

        await act.CreateAccountsAsync(SampleInput, SampleOnboardingId);

        Assert.Contains(SampleOnboardingId.ToString(), capture.LastPath!);
        Assert.Contains("accounts/create", capture.LastPath!);
        var body = JsonDocument.Parse(capture.LastBody!);
        Assert.Equal(SampleInput.Email, body.RootElement.GetProperty("employeeEmail").GetString());
        Assert.Equal(SampleInput.EmployeeNumber, body.RootElement.GetProperty("employeeNumber").GetString());
    }

    [Fact]
    public async Task CreateAccountsAsync_Throws_OnHttpFailure()
    {
        var (act, _) = Build("/accounts/create", HttpStatusCode.InternalServerError, "");
        await Assert.ThrowsAsync<HttpRequestException>(
            () => act.CreateAccountsAsync(SampleInput, SampleOnboardingId));
    }

    [Fact]
    public async Task CreateAccountsAsync_ThrowsNonRetryable_On4xxResponse()
    {
        var (act, _) = Build("/accounts/create", HttpStatusCode.BadRequest, "");
        var ex = await Assert.ThrowsAsync<ApplicationFailureException>(
            () => act.CreateAccountsAsync(SampleInput, SampleOnboardingId));
        Assert.True(ex.NonRetryable);
    }

    [Fact]
    public async Task CreateAccountsAsync_IsRetryable_On429TooManyRequests()
    {
        // 429 is retryable — the downstream service is throttling, not rejecting bad input
        var (act, _) = Build("/accounts/create", HttpStatusCode.TooManyRequests, "");
        await Assert.ThrowsAsync<HttpRequestException>(
            () => act.CreateAccountsAsync(SampleInput, SampleOnboardingId));
    }

    // ── IssueEquipmentAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task IssueEquipmentAsync_PostsToCorrectUrlWithDepartment()
    {
        var capture = new RequestCapture();
        var handler = new RoutedStubHandler();
        handler.Add("/equipment/issue", HttpStatusCode.OK, "{}", capture);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://onboarding-svc") };
        var act = new OnboardingActivities(client, NullLogger<OnboardingActivities>.Instance);

        await act.IssueEquipmentAsync(SampleInput, SampleOnboardingId);

        Assert.Contains(SampleOnboardingId.ToString(), capture.LastPath!);
        var body = JsonDocument.Parse(capture.LastBody!);
        Assert.Equal(SampleInput.Department, body.RootElement.GetProperty("department").GetString());
    }

    [Fact]
    public async Task IssueEquipmentAsync_Throws_OnHttpFailure()
    {
        var (act, _) = Build("/equipment/issue", HttpStatusCode.BadGateway, "");
        await Assert.ThrowsAsync<HttpRequestException>(
            () => act.IssueEquipmentAsync(SampleInput, SampleOnboardingId));
    }

    [Fact]
    public async Task IssueEquipmentAsync_ThrowsNonRetryable_On4xxResponse()
    {
        var (act, _) = Build("/equipment/issue", HttpStatusCode.BadRequest, "");
        var ex = await Assert.ThrowsAsync<ApplicationFailureException>(
            () => act.IssueEquipmentAsync(SampleInput, SampleOnboardingId));
        Assert.True(ex.NonRetryable);
    }

    [Fact]
    public async Task IssueEquipmentAsync_IsRetryable_On429TooManyRequests()
    {
        var (act, _) = Build("/equipment/issue", HttpStatusCode.TooManyRequests, "");
        await Assert.ThrowsAsync<HttpRequestException>(
            () => act.IssueEquipmentAsync(SampleInput, SampleOnboardingId));
    }

    // ── CompleteOnboardingAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CompleteOnboardingAsync_PatchesStatusToCompleted()
    {
        var capture = new RequestCapture();
        var handler = new RoutedStubHandler();
        handler.Add("/status", HttpStatusCode.OK, "{}", capture);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://onboarding-svc") };
        var act = new OnboardingActivities(client, NullLogger<OnboardingActivities>.Instance);

        await act.CompleteOnboardingAsync(SampleOnboardingId);

        Assert.Contains(SampleOnboardingId.ToString(), capture.LastPath!);
        Assert.Equal(HttpMethod.Patch, capture.LastMethod);
        var body = JsonDocument.Parse(capture.LastBody!);
        Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
    }

    // ── FailOnboardingAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task FailOnboardingAsync_PatchesStatusToFailed()
    {
        var capture = new RequestCapture();
        var handler = new RoutedStubHandler();
        handler.Add("/status", HttpStatusCode.OK, "{}", capture);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://onboarding-svc") };
        var act = new OnboardingActivities(client, NullLogger<OnboardingActivities>.Instance);

        await act.FailOnboardingAsync(SampleOnboardingId, "unit test failure");

        Assert.Equal(HttpMethod.Patch, capture.LastMethod);
        var body = JsonDocument.Parse(capture.LastBody!);
        Assert.Equal("failed", body.RootElement.GetProperty("status").GetString());
    }

    // ── ResetOnboardingStatusAsync ────────────────────────────────────────────

    [Fact]
    public async Task ResetOnboardingStatusAsync_PatchesStatusToInProgress()
    {
        var capture = new RequestCapture();
        var handler = new RoutedStubHandler();
        handler.Add("/status", HttpStatusCode.OK, "{}", capture);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://onboarding-svc") };
        var act = new OnboardingActivities(client, NullLogger<OnboardingActivities>.Instance);

        await act.ResetOnboardingStatusAsync(SampleOnboardingId);

        Assert.Equal(HttpMethod.Patch, capture.LastMethod);
        var body = JsonDocument.Parse(capture.LastBody!);
        Assert.Equal("in_progress", body.RootElement.GetProperty("status").GetString());
        Assert.True(body.RootElement.GetProperty("incrementRetryCount").GetBoolean());
    }

}

// ── Helpers ───────────────────────────────────────────────────────────────────

internal sealed class RequestCapture
{
    public string? LastPath { get; set; }
    public HttpMethod? LastMethod { get; set; }
    public string? LastBody { get; set; }
}

internal sealed class RoutedStubHandler : HttpMessageHandler
{
    private readonly List<(string UrlContains, HttpStatusCode Status, string Body, RequestCapture? Capture)> _routes = [];

    public void Add(string urlContains, HttpStatusCode status, string body, RequestCapture? capture)
        => _routes.Add((urlContains, status, body, capture));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.PathAndQuery;
        var reqBody = request.Content is not null ? await request.Content.ReadAsStringAsync(ct) : null;

        foreach (var (urlContains, status, body, capture) in _routes)
        {
            if (!path.Contains(urlContains)) continue;

            if (capture is not null)
            {
                capture.LastPath = path;
                capture.LastMethod = request.Method;
                capture.LastBody = reqBody;
            }

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
