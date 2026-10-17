using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace E_Commerce.Services.Khqr;

/// <summary>
/// Input for <see cref="KhqrBuilder.BuildBakongKhqr"/>. Mirrors the argument object of the
/// reference Node.js <c>generateBakongKhqr</c> function.
/// </summary>
public sealed record BakongKhqrRequest
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public string? TranId { get; init; }
    public string? MerchantName { get; init; }
    public string? BakongAccountId { get; init; }
    public string? AbaAccountNumber { get; init; }
    public string? BankName { get; init; }
    public string? PaymentLink { get; init; }
    public int ExpiresInMinutes { get; init; } = 15;
    public int? ExpiresInSeconds { get; init; }
}

/// <summary>
/// Pure, side-effect-free EMVCo / Bakong KHQR helpers ported from the reference
/// Node.js service (<c>crc16Ccitt</c>, <c>formatTlv</c>, <c>generateBakongKhqr</c>).
///
/// No merchant-specific values (account numbers, names, keys or links) are hardcoded here;
/// every such value is supplied by the caller through <see cref="BakongKhqrRequest"/>.
/// </summary>
public static class KhqrBuilder
{
    // PayWay routing protocol constants used inside tag 62/sub 68. These are protocol
    // markers from the reference implementation, not merchant secrets.
    private const string PayWayRoutingNamespace = "PAYWAY@ABA";
    private const string PayWayStoreId = "042202220";

    private static readonly Regex LinkPrefixRegex =
        new(@"^https?://link\.payway\.com\.kh/?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AbaPayPrefixRegex =
        new("^ABAPAY", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SixDigitsRegex =
        new(@"\d{6}", RegexOptions.Compiled);

    /// <summary>
    /// CRC-16/CCITT-FALSE (polynomial 0x1021, initial value 0xFFFF) returning a 4-digit
    /// uppercase hex string. Iterates UTF-16 code units to stay byte-for-byte identical to
    /// the reference implementation.
    /// </summary>
    public static string Crc16CcittFalse(string data)
    {
        ArgumentNullException.ThrowIfNull(data);

        int crc = 0xFFFF;
        foreach (char ch in data)
        {
            crc ^= ch << 8;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                {
                    crc = ((crc << 1) ^ 0x1021) & 0xFFFF;
                }
                else
                {
                    crc = (crc << 1) & 0xFFFF;
                }
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats one EMVCo TLV element. The length is the UTF-8 <b>byte</b> count padded to at
    /// least two digits.
    /// </summary>
    public static string Tlv(string tag, string value)
    {
        ArgumentNullException.ThrowIfNull(tag);
        value ??= string.Empty;
        int length = Encoding.UTF8.GetByteCount(value);
        return tag + length.ToString("D2", CultureInfo.InvariantCulture) + value;
    }

    /// <summary>
    /// Amount formatting used by tag 54. USD is rendered as a plain number without trailing
    /// zeros (<c>12.50</c> to <c>"12.5"</c>, <c>12.00</c> to <c>"12"</c>); KHR is rounded to an
    /// integer.
    /// </summary>
    public static string FormatAmount(decimal amount, string? currency)
    {
        bool isUsd = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase);
        if (isUsd)
        {
            return amount.ToString("0.###############################", CultureInfo.InvariantCulture);
        }

        return Math.Round(amount, 0, MidpointRounding.AwayFromZero)
            .ToString("0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Builds a complete EMVCo Bakong KHQR string, reproducing the reference
    /// <c>generateBakongKhqr</c> output.
    /// </summary>
    public static string BuildBakongKhqr(BakongKhqrRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string bakongAccountId = request.BakongAccountId?.Trim() ?? string.Empty;
        if (bakongAccountId.Length == 0)
        {
            throw new ArgumentException("BakongAccountId is required.", nameof(request));
        }

        string abaAccountNumber = request.AbaAccountNumber?.Trim() ?? string.Empty;
        if (abaAccountNumber.Length == 0)
        {
            throw new ArgumentException("AbaAccountNumber is required.", nameof(request));
        }

        string bankName = request.BankName?.Trim() ?? string.Empty;
        if (bankName.Length == 0)
        {
            throw new ArgumentException("BankName is required.", nameof(request));
        }

        string currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency;
        bool isUsd = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase);
        string currencyCode = isUsd ? "840" : "116";
        string amount = FormatAmount(request.Amount, currency);
        string tranId = request.TranId ?? string.Empty;

        var payload = new StringBuilder();
        payload.Append(Tlv("00", "01"));
        payload.Append(Tlv("01", "12"));

        string merchantAccountInfo =
            Tlv("00", bakongAccountId) +
            Tlv("01", abaAccountNumber) +
            Tlv("02", bankName);
        payload.Append(Tlv("30", merchantAccountInfo));

        payload.Append(Tlv("52", "5399"));
        payload.Append(Tlv("53", currencyCode));
        payload.Append(Tlv("54", amount));
        payload.Append(Tlv("58", "KH"));
        payload.Append(Tlv("59", Truncate(request.MerchantName ?? string.Empty, 25)));
        payload.Append(Tlv("60", "N/A"));

        string linkId = DeriveLinkId(request.PaymentLink);
        if (linkId.Length > 0)
        {
            string sessionRef = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                .ToString(CultureInfo.InvariantCulture);
            sessionRef = sessionRef.Substring(sessionRef.Length - 8);
            string paywayRef = linkId + "-" + sessionRef;

            string routing =
                Tlv("00", PayWayRoutingNamespace) +
                Tlv("01", paywayRef) +
                Tlv("02", PayWayStoreId) +
                Tlv("03", "1") +
                Tlv("05", "1");
            payload.Append(Tlv("62", Tlv("68", routing)));
        }
        else
        {
            string reference = tranId.Length > 0
                ? tranId
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
            payload.Append(Tlv("62", Tlv("01", reference)));
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long durationMs = request.ExpiresInSeconds.HasValue
            ? request.ExpiresInSeconds.Value * 1000L
            : (request.ExpiresInMinutes > 0 ? request.ExpiresInMinutes : 15) * 60L * 1000L;
        long expire = now + durationMs;

        string tokenHex = Md5HexUpper($"{now}{tranId}{amount}").Substring(0, 17);
        string dynamicBlock =
            Tlv("00", now.ToString(CultureInfo.InvariantCulture)) +
            Tlv("01", expire.ToString(CultureInfo.InvariantCulture)) +
            Tlv("67", Tlv("00", tokenHex)) +
            Tlv("68", "PLIK");
        payload.Append(Tlv("99", dynamicBlock));

        string body = payload.ToString();
        string toChecksum = body + "6304";
        return toChecksum + Crc16CcittFalse(toChecksum);
    }

    private static string DeriveLinkId(string? paymentLink)
    {
        if (string.IsNullOrWhiteSpace(paymentLink))
        {
            return string.Empty;
        }

        string clean = LinkPrefixRegex.Replace(paymentLink.Trim(), string.Empty);
        string slug = AbaPayPrefixRegex.Replace(clean, string.Empty);

        Match match = SixDigitsRegex.Match(slug);
        if (match.Success)
        {
            return match.Value;
        }

        return slug.Length > 0 ? slug.Substring(0, Math.Min(10, slug.Length)) : string.Empty;
    }

    private static string Md5HexUpper(string input)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString().ToUpperInvariant();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value.Substring(0, maxLength);
}
