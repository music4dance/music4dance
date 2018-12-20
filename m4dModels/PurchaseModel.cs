using System;

namespace m4dModels
{
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
        public Guid Confirmation { get; set; }
        public string Description => Kind == PurchaseKind.Purchase ? "Premium Subscription" : "Donation";

        public int Pennies => (int) (Amount* 100);

    }
}
