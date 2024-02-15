namespace m4d.ViewModels;

public enum PurchaseKind
{
    None,
    Purchase,
    Donation
}

public class PurchaseModel
{
    public string Key { get; set; }
    public decimal Amount { get; set; }
    public PurchaseKind Kind { get; set; }
    public string User { get; set; }
    public string Email { get; set; }
    public string Confirmation { get; set; }

    public string Description =>
        Kind == PurchaseKind.Purchase ? "Premium Subscription" : "Donation";

    public int Pennies => (int)(Amount * 100);

    public PurchaseError Error { get; set; }
}

public class PurchaseError
{
    public string ErrorType { get; set; }
    public string ErrorCode { get; set; }
    public string ErrorMessage { get; set; }
}

public class ContributeModel
{
    public bool CommerceEnabled { get; set; }
    public bool IsAuthenticated { get; set; }
    public bool FraudDetected { get; set; }
    public bool CurrentPremium { get; set; }
    public DateTime? PremiumExpiration { get; set; }
    public bool RecaptchaFailed { get; set; }
}
