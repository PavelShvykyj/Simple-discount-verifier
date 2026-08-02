namespace SimpleDiscountVerifier.Api.Application.Redemptions;

public interface ITurnstileVerifier
{
    /// <summary>
    /// Returns true when the request should proceed: either verification succeeded, or
    /// verification is disabled via the <c>TurnstileEnabled</c> kill switch.
    /// </summary>
    Task<bool> VerifyAsync(string? token, CancellationToken cancellationToken);
}
