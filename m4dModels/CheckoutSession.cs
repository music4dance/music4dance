namespace m4dModels;

/// <summary>
/// A Stripe checkout session that has already been credited. The session id is the primary key,
/// so a second attempt to record the same session fails at the database even when two requests
/// race, and a reload of an old /payment/success URL can't grant another year of premium after a
/// restart or on another instance.
/// </summary>
public class CheckoutSession
{
    public string SessionId { get; set; }
    public string ApplicationUserId { get; set; }
    public DateTimeOffset Processed { get; set; }
}
