namespace HrService.Services;

// thrown when a duplicate email is detected — controller maps this to HTTP 409
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
