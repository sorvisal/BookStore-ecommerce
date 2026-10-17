namespace E_Commerce.ViewModels
{
    /// <summary>
    /// Data for the ABA Pay (KHQR) page shown right after an order is placed
    /// with the ABA Pay method. The QR encodes the exact order total computed
    /// on the server. The client_id / token used to verify the payment are never
    /// part of this model so they can never reach the browser.
    /// </summary>
    public class AbaPayViewModel
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;

        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";

        /// <summary>A data URI, or the static fallback image path in manual mode.</summary>
        public string? QrImageSource { get; set; }

        /// <summary>The KHQR payload (safe to show as text / copy).</summary>
        public string? QrString { get; set; }

        /// <summary>Mobile deep link that opens the ABA app with the QR prefilled.</summary>
        public string? DeepLink { get; set; }

        /// <summary>True when a QR was generated from configured ABA settings.</summary>
        public bool IsConfigured { get; set; }

        /// <summary>True only in live mode, where the status can be checked automatically.</summary>
        public bool CanVerifyPayment { get; set; }

        public string Mode { get; set; } = string.Empty;

        public DateTimeOffset? ExpiresAtUtc { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
