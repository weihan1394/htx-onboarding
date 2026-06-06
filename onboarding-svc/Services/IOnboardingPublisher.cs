namespace OnboardingService.Services;

public interface IOnboardingPublisher
{
    Task PublishStatusChangedAsync(Guid employeeId, string status);
}