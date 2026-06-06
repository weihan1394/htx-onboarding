namespace OnboardingService.Services;

// thrown when StartAsync is called for an employee that already has an onboarding record
// controller maps this to HTTP 409 — activity reads the existing onboardingId from the body
// and resets its status instead of creating a duplicate
public class OnboardingAlreadyExistsException : Exception
{
    public Guid OnboardingId { get; }

    public OnboardingAlreadyExistsException(Guid onboardingId)
        : base("Onboarding already exists for this employee")
    {
        OnboardingId = onboardingId;
    }
}
