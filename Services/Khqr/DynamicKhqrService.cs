using System.Globalization;
using System.Security.Cryptography;
using QRCoder;

namespace E_Commerce.Services.Khqr;

/// <summary>Input for <see cref="DynamicKhqrService.GenerateAsync"/>.</summary>
public sealed record DynamicKhqrRequest
{
    public string? TranId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public string? CustomerName { get; init; }
    public int ExpiresInMinutes { get; init; } = 15;
}

/// <summary>Result of <see cref="DynamicKhqrService.GenerateAsync"/>.</summary>
public sealed record DynamicKhqrResult
{
    public bool IsConfigured { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string? QrString { get; init; }
    public string? QrImageDataUri { get; init; }
    public string? DeepLink { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public string? TranId { get; init; }
    public string? ClientId { get; init; }
    public string? Token { get; init; }
    public bool CanVerifyPayment { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>Result of <see cref="DynamicKhqrService.CheckAsync"/>.</summary>
public sealed record DynamicKhqrCheckResult
{
    /// <summary>
    /// True only when ABA explicitly approved the payment (<c>data.action == "approved"</c>).
    /// </summary>
    public bool Approved { get; init; }

    /// <summary>True whenever the payment is not (yet) approved.</summary>
    public bool Pending { get; init; } = true;

    /// <summary>HTTP status returned by ABA, when the call completed.</summary>
    public int? HttpStatus { get; init; }

    /// <summary>The raw ABA <c>action</c> (for logging; never a secret).</summary>
    public string? Action { get; init; }

    /// <summary>The raw ABA <c>status.code</c>.</summary>
    public string? StatusCode { get; init; }

    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Facade over the live PayWay link client and the local KHQR builder. Prefers the live path
/// when a link (or link <c>aba_data</c>) is configured, then falls back to a locally built
/// KHQR, and reports <see cref="IsConfigured"/> false when no ABA configuration exists.
/// </summary>
public sealed class DynamicKhqrService
{
    // Public ABA acquiring bank name required as EMVCo tag 30 sub 02. This is not a
    // merchant-specific secret. The merchant name and account numbers are always configured.
    private const string AbaBankName = "ABA Bank";
    private const int QrImageTargetPixels = 340;

    private readonly PayWayLinkClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DynamicKhqrService> _logger;

    public DynamicKhqrService(
        PayWayLinkClient client,
        IConfiguration configuration,
        ILogger<DynamicKhqrService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsConfigured => HasLiveLinkConfiguration || HasLocalConfiguration;

    private string? PaymentLink => NullIfBlank(_configuration["Aba:PaymentLink"]);
    private string? LinkAbaData => NullIfBlank(_configuration["Aba:LinkAbaData"]);
    private string? MerchantName => NullIfBlank(_configuration["Aba:MerchantName"]);
    private string? BakongAccountId => NullIfBlank(_configuration["Aba:BakongAccountId"]);
    private string? AbaAccountNumberUsd => NullIfBlank(_configuration["Aba:AbaAccountNumberUsd"]);
    private string? AbaAccountNumberKhr => NullIfBlank(_configuration["Aba:AbaAccountNumberKhr"]);
    private string DefaultCurrency => NullIfBlank(_configuration["Aba:Currency"]) ?? "USD";
    private int QrLifetimeMinutes =>
        int.TryParse(_configuration["Aba:QrLifetimeMinutes"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes)
            && minutes > 0
            ? minutes
            : 10;

    private bool HasLiveLinkConfiguration => PaymentLink is not null || LinkAbaData is not null;

    private bool HasLocalConfiguration =>
        BakongAccountId is not null &&
        MerchantName is not null &&
        (AbaAccountNumberUsd is not null || AbaAccountNumberKhr is not null);

    public async Task<DynamicKhqrResult> GenerateAsync(
        DynamicKhqrRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string currency = string.IsNullOrWhiteSpace(request.Currency) ? DefaultCurrency : request.Currency.Trim();
        string tranId = string.IsNullOrWhiteSpace(request.TranId) ? GenerateTranId() : request.TranId.Trim();
        int expiresMinutes = request.ExpiresInMinutes > 0 ? request.ExpiresInMinutes : QrLifetimeMinutes;

        // (a) Live path when a PayWay link or a cached link aba_data is configured.
        if (HasLiveLinkConfiguration)
        {
            DynamicKhqrResult? liveResult = await TryGenerateLiveAsync(
                request, tranId, currency, expiresMinutes, cancellationToken);
            if (liveResult is not null)
            {
                return liveResult;
            }
        }

        // (b) Local KHQR when merchant account settings are configured.
        if (HasLocalConfiguration)
        {
            try
            {
                return BuildLocalResult(request, tranId, currency, expiresMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Local KHQR generation failed ({Type}).", ex.GetType().Name);
                return NotConfigured($"Local KHQR generation failed: {ex.Message}");
            }
        }

        // (c) Nothing configured.
        return NotConfigured("ABA PayWay is not configured. Set the Aba configuration values.");
    }

    /// <summary>
    /// Verifies a payment. The ABA API is only queried when both <paramref name="clientId"/>
    /// and <paramref name="token"/> are present; otherwise the result is "pending" and no
    /// network call is made. Approval comes solely from <c>data.action == "approved"</c>.
    /// </summary>
    public async Task<DynamicKhqrCheckResult> CheckAsync(
        string? clientId,
        string? token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(token))
        {
            return new DynamicKhqrCheckResult
            {
                Approved = false,
                Pending = true,
                Message = "Payment verification requires a client_id and token.",
            };
        }

        try
        {
            PaymentStatusResult status = await _client.CheckPaymentStatusAsync(clientId, token, cancellationToken);
            return new DynamicKhqrCheckResult
            {
                Approved = status.Approved,
                Pending = status.Pending,
                HttpStatus = status.HttpStatus,
                Action = status.Action,
                StatusCode = status.StatusCode,
                Message = status.Message,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Payment status check failed ({Type}).", ex.GetType().Name);
            return new DynamicKhqrCheckResult
            {
                Approved = false,
                Pending = true,
                Message = "Status check failed.",
            };
        }
    }

    private async Task<DynamicKhqrResult?> TryGenerateLiveAsync(
        DynamicKhqrRequest request,
        string tranId,
        string currency,
        int expiresMinutes,
        CancellationToken cancellationToken)
    {
        try
        {
            string? abaData = LinkAbaData;
            if (string.IsNullOrEmpty(abaData) && PaymentLink is not null)
            {
                AbaDataResult extracted = await _client.GetAbaDataAsync(PaymentLink, cancellationToken);
                if (extracted.Success)
                {
                    abaData = extracted.AbaData;
                }
                else
                {
                    _logger.LogWarning(
                        "Live PayWay link lookup failed; continuing with local generation. Reason: {Reason}",
                        extracted.Error);
                }
            }

            if (string.IsNullOrEmpty(abaData))
            {
                return null;
            }

            LiveQrResult live = await _client.RequestQrAsync(abaData, request.Amount, currency, cancellationToken);
            if (!live.Success || string.IsNullOrEmpty(live.QrString))
            {
                _logger.LogWarning(
                    "Live PayWay QR request failed; continuing with local generation. Reason: {Reason}",
                    live.Error);
                return null;
            }

            string qrString = live.QrString;
            DateTimeOffset expiresAt = live.ExpireIn is int seconds && seconds > 0
                ? DateTimeOffset.UtcNow.AddSeconds(seconds)
                : DateTimeOffset.UtcNow.AddMinutes(expiresMinutes);

            return new DynamicKhqrResult
            {
                IsConfigured = true,
                Mode = "link-live",
                QrString = qrString,
                QrImageDataUri = ResolveQrImage(live.QrImage, qrString),
                DeepLink = BuildDeepLink(qrString),
                ExpiresAtUtc = expiresAt,
                TranId = string.IsNullOrEmpty(live.TranId) ? tranId : live.TranId,
                ClientId = live.ClientId,
                Token = live.Token,
                CanVerifyPayment = !string.IsNullOrEmpty(live.ClientId) && !string.IsNullOrEmpty(live.Token),
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Live PayWay path threw {Type}; continuing with local generation.", ex.GetType().Name);
            return null;
        }
    }

    private DynamicKhqrResult BuildLocalResult(
        DynamicKhqrRequest request,
        string tranId,
        string currency,
        int expiresMinutes)
    {
        bool isUsd = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase);
        string? account = (isUsd ? AbaAccountNumberUsd : AbaAccountNumberKhr)
            ?? AbaAccountNumberUsd
            ?? AbaAccountNumberKhr;

        if (string.IsNullOrEmpty(account))
        {
            throw new InvalidOperationException(
                $"No {(isUsd ? "USD" : "KHR")} ABA account number is configured.");
        }

        string qrString = KhqrBuilder.BuildBakongKhqr(new BakongKhqrRequest
        {
            Amount = request.Amount,
            Currency = currency,
            TranId = tranId,
            MerchantName = MerchantName,
            BakongAccountId = BakongAccountId,
            AbaAccountNumber = account,
            BankName = AbaBankName,
            PaymentLink = PaymentLink,
            ExpiresInMinutes = expiresMinutes,
        });

        return new DynamicKhqrResult
        {
            IsConfigured = true,
            Mode = "local",
            QrString = qrString,
            QrImageDataUri = TryGenerateQrDataUri(qrString),
            DeepLink = BuildDeepLink(qrString),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(expiresMinutes),
            TranId = tranId,
            ClientId = null,
            Token = null,
            CanVerifyPayment = false,
        };
    }

    private static DynamicKhqrResult NotConfigured(string message) => new()
    {
        IsConfigured = false,
        Mode = string.Empty,
        ErrorMessage = message,
    };

    private static string ResolveQrImage(string? liveImage, string qrString)
    {
        if (!string.IsNullOrEmpty(liveImage) &&
            liveImage.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return liveImage;
        }

        return TryGenerateQrDataUri(qrString) ?? string.Empty;
    }

    private static string? TryGenerateQrDataUri(string qrString)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            using QRCodeData data = generator.CreateQrCode(qrString, QRCodeGenerator.ECCLevel.Q);
            int modules = Math.Max(1, data.ModuleMatrix.Count);
            int pixelsPerModule = Math.Max(1, QrImageTargetPixels / modules);
            using var png = new PngByteQRCode(data);
            byte[] bytes = png.GetGraphic(pixelsPerModule);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildDeepLink(string qrString) =>
        "abamobilebank://ababank.com?type=payway&qrcode=" + Uri.EscapeDataString(qrString);

    private static string GenerateTranId(string prefix = "PW")
    {
        long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return prefix + seconds.ToString(CultureInfo.InvariantCulture) + RandomBase36(4);
    }

    private static string RandomBase36(int length)
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        Span<char> result = stackalloc char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        return new string(result).ToUpperInvariant();
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
