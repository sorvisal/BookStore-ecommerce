using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace E_Commerce.Services.Khqr;

/// <summary>Result of reading <c>aba_data</c> from a PayWay link page.</summary>
public sealed record AbaDataResult
{
    public bool Success { get; init; }
    public string? AbaData { get; init; }
    public string? RequestTime { get; init; }
    public string? Error { get; init; }

    public static AbaDataResult Ok(string abaData, string? requestTime) =>
        new() { Success = true, AbaData = abaData, RequestTime = requestTime };

    public static AbaDataResult Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>Result of requesting a live ABA PayWay QR.</summary>
public sealed record LiveQrResult
{
    public bool Success { get; init; }
    public string? QrString { get; init; }
    public string? QrImage { get; init; }
    public string? Token { get; init; }
    public string? ClientId { get; init; }
    public string? TranId { get; init; }
    public int? ExpireIn { get; init; }
    public string? Error { get; init; }

    public static LiveQrResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>Result of a payment status check.</summary>
public sealed record PaymentStatusResult
{
    public bool Approved { get; init; }
    public bool Pending { get; init; }

    /// <summary>HTTP status returned by ABA (nullable when the call never completed).</summary>
    public int? HttpStatus { get; init; }

    /// <summary>The raw <c>action</c> reported by ABA (for logging; never a secret).</summary>
    public string? Action { get; init; }

    /// <summary>The raw <c>status.code</c> reported by ABA.</summary>
    public string? StatusCode { get; init; }

    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Typed HTTP client for the ABA PayWay link/QR endpoints. Reads <c>aba_data</c> from a
/// PayWay link page without ever executing JavaScript, and talks to the (undocumented)
/// <c>pwapp.ababank.com</c> endpoints exactly as the reference implementation does.
/// Tokens are never logged.
/// </summary>
public sealed class PayWayLinkClient
{
    private const string ListOptionsPath = "/api/pw-app/v1/payment/gateway/list-payment-options";
    private const string RequestQrPath = "/api/pw-app/v1/payment/gateway/abapay-khqr/request-qr";
    private const string CheckStatusPath = "/api/pw-app/v1/payment-link/check-payment-status";

    private const string AllowedHost = "link.payway.com.kh";
    private const int MaxRedirects = 3;
    private const long MaxPageBytes = 2L * 1024 * 1024;
    private static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(6);

    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static readonly Regex PathRegex =
        new("^/ABAPAY[A-Za-z0-9]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex[] AbaDataPatterns =
    {
        new("aba_data[\"']?\\s*[:=]\\s*[\"']([A-Za-z0-9+/=]{200,})[\"']", RegexOptions.Compiled),
        new("\"aba_data\"\\s*:\\s*\"([A-Za-z0-9+/=]{200,})\"", RegexOptions.Compiled),
        new("aba_data\\s*[=:]\\s*[\"']([^\"']{200,})[\"']", RegexOptions.Compiled),
    };

    private static readonly Regex RequestTimeRegex =
        new("request_time[\"']?\\s*[:=]\\s*[\"']?([0-9]{14})", RegexOptions.Compiled);

    private static readonly Regex UnicodeEscapeRegex =
        new("\\\\u([0-9a-fA-F]{4})", RegexOptions.Compiled);

    private static readonly Regex Base64Regex =
        new("^[A-Za-z0-9+/=]+$", RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly ILogger<PayWayLinkClient> _logger;

    public PayWayLinkClient(HttpClient http, ILogger<PayWayLinkClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches the PayWay link page (HTTPS only, host <c>link.payway.com.kh</c>, path
    /// <c>/ABAPAY...</c>, at most 3 same-host redirects, 6 s timeout, 2 MB cap) and extracts
    /// <c>aba_data</c> and <c>request_time</c> using regex. JavaScript is never executed.
    /// </summary>
    public async Task<AbaDataResult> GetAbaDataAsync(string? paymentLink, CancellationToken cancellationToken = default)
    {
        if (!TryValidateLink(paymentLink, out Uri uri, out string? validationError))
        {
            return AbaDataResult.Fail(validationError ?? "Invalid payment link.");
        }

        string html;
        try
        {
            html = await FetchHtmlAsync(uri, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PayWay link page could not be read ({Type}).", ex.GetType().Name);
            return AbaDataResult.Fail(
                "Could not read the PayWay link page. Set Aba:LinkAbaData manually.");
        }

        string? abaData = ExtractAbaData(html);
        if (string.IsNullOrEmpty(abaData))
        {
            return AbaDataResult.Fail(
                "Could not safely read aba_data from the PayWay link page. Set Aba:LinkAbaData manually.");
        }

        string? requestTime = ExtractRequestTime(html);
        return AbaDataResult.Ok(abaData, requestTime);
    }

    /// <summary>
    /// Requests a live ABA PayWay KHQR: first <c>list-payment-options</c> (SHA-512 body hash
    /// over request time + aba_data + additional_fields), then <c>abapay-khqr/request-qr</c>
    /// (HMAC-SHA512 with the returned token).
    /// </summary>
    public async Task<LiveQrResult> RequestQrAsync(
        string? abaData,
        decimal amount,
        string? currency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(abaData))
        {
            return LiveQrResult.Fail("aba_data is empty.");
        }

        string requestTime = UtcRequestTime(DateTime.UtcNow);
        bool isUsd = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase);
        string formattedAmount = isUsd
            ? amount.ToString("F2", CultureInfo.InvariantCulture)
            : Math.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

        string additionalFields = "{\"amount\":\"" + formattedAmount + "\"}";
        string listHash = Sha512Hex(requestTime + abaData + additionalFields);

        string listResponse;
        try
        {
            var listBody = new
            {
                request_time = requestTime,
                aba_data = abaData,
                additional_fields = additionalFields,
                hash = listHash,
            };
            var listHeaders = new Dictionary<string, string> { ["language"] = "en" };
            listResponse = await PostJsonAsync(ListOptionsPath, listBody, listHeaders, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PayWay list-payment-options request failed ({Type}).", ex.GetType().Name);
            return LiveQrResult.Fail("list-payment-options request failed.");
        }

        string? token; string? newAbaData;
        try
        {
            using var listDoc = JsonDocument.Parse(listResponse);
            JsonElement listRoot = listDoc.RootElement;
            if (!TryGetStringDeep(listRoot, "token", out token) ||
                !TryGetStringDeep(listRoot, "aba_data", out newAbaData))
            {
                return LiveQrResult.Fail("list-payment-options did not return a token.");
            }
        }
        catch (JsonException)
        {
            return LiveQrResult.Fail("list-payment-options returned malformed JSON.");
        }

        string qrHash = HmacSha512Hex(token, requestTime + newAbaData);

        string qrResponse;
        try
        {
            var qrBody = new
            {
                request_time = requestTime,
                aba_data = newAbaData,
                hash = qrHash,
            };
            var qrHeaders = new Dictionary<string, string> { ["token"] = token };
            qrResponse = await PostJsonAsync(RequestQrPath, qrBody, qrHeaders, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PayWay request-qr request failed ({Type}).", ex.GetType().Name);
            return LiveQrResult.Fail("request-qr request failed.");
        }

        try
        {
            using var qrDoc = JsonDocument.Parse(qrResponse);
            JsonElement qrRoot = qrDoc.RootElement;

            // The ABA response shape varies: values may sit at the top level or nested
            // under a "data" object. Read both so a live session is never misread as
            // manual (which would stop the payment being detected automatically).
            if (!TryGetStringDeep(qrRoot, "qr_string", out string? qrString) || string.IsNullOrEmpty(qrString))
            {
                return LiveQrResult.Fail("request-qr did not return a QR string.");
            }

            string? qrImage = TryGetStringDeep(qrRoot, "qr_image", out string? img) ? img : null;
            string finalToken = TryGetStringDeep(qrRoot, "token", out string? t) && !string.IsNullOrEmpty(t)
                ? t
                : token;
            string? clientId = TryGetStringDeep(qrRoot, "client_id", out string? cid) ? cid : null;

            string? tranId = TryGetStatusString(qrRoot, "tran_id", out string? tid) ? tid : null;

            int? expireIn = ReadIntDeep(qrRoot, "expire_in");

            return new LiveQrResult
            {
                Success = true,
                QrString = qrString,
                QrImage = qrImage,
                Token = finalToken,
                ClientId = clientId,
                TranId = tranId,
                ExpireIn = expireIn,
            };
        }
        catch (JsonException)
        {
            return LiveQrResult.Fail("request-qr returned malformed JSON.");
        }
    }

    /// <summary>
    /// Checks a payment status using <paramref name="clientId"/> and <paramref name="token"/>.
    /// Approved when the response has <c>data.action == "approved"</c>. Tokens are never logged.
    /// </summary>
    public async Task<PaymentStatusResult> CheckPaymentStatusAsync(
        string? clientId,
        string? token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(token))
        {
            return new PaymentStatusResult
            {
                Approved = false,
                Pending = true,
                Message = "Missing client_id or token.",
            };
        }

        string requestTime = UtcRequestTime(DateTime.UtcNow);
        string deviceId = RandomHex(5);
        string hash = Sha512Hex(clientId + deviceId + requestTime);

        (int HttpStatus, string Body) raw;
        try
        {
            var body = new
            {
                request_time = requestTime,
                client_id = clientId,
                device_id = deviceId,
                hash,
            };
            var headers = new Dictionary<string, string>
            {
                ["language"] = "en",
                ["token"] = token,
            };

            raw = await PostJsonRawAsync(CheckStatusPath, body, headers, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PayWay payment status check failed ({Type}).", ex.GetType().Name);
            return new PaymentStatusResult
            {
                Approved = false,
                Pending = true,
                Message = "Status check failed.",
            };
        }

        if (raw.HttpStatus is < 200 or >= 300)
        {
            _logger.LogWarning("PayWay payment status endpoint returned HTTP {Status}.", raw.HttpStatus);
            return new PaymentStatusResult
            {
                Approved = false,
                Pending = true,
                HttpStatus = raw.HttpStatus,
                Message = $"HTTP {raw.HttpStatus}",
            };
        }

        // Strict approval: only "data.action" == "approved" counts. HTTP 200, a present
        // "data" object, "status.code" == "00", "status.message" == "Success" and words like
        // "completed"/"success"/"ok" are never treated as a paid payment.
        PayWayStatusParseResult parsed = PayWayStatusParser.Parse(raw.Body);

        if (parsed.Malformed)
        {
            _logger.LogWarning("PayWay payment status returned malformed JSON.");
            return new PaymentStatusResult
            {
                Approved = false,
                Pending = true,
                HttpStatus = raw.HttpStatus,
                Message = "Malformed response.",
            };
        }

        return new PaymentStatusResult
        {
            Approved = parsed.Approved,
            Pending = !parsed.Approved,
            HttpStatus = raw.HttpStatus,
            Action = parsed.Action,
            StatusCode = parsed.StatusCode,
            Message = !string.IsNullOrWhiteSpace(parsed.Action)
                ? parsed.Action!
                : !string.IsNullOrWhiteSpace(parsed.StatusMessage)
                    ? parsed.StatusMessage!
                    : "pending",
        };
    }

    private static bool TryValidateLink(string? link, out Uri uri, out string? error)
    {
        uri = null!;
        error = null;

        if (string.IsNullOrWhiteSpace(link))
        {
            error = "Payment link is empty.";
            return false;
        }

        if (!Uri.TryCreate(link.Trim(), UriKind.Absolute, out Uri? parsed))
        {
            error = "Payment link is not a valid URL.";
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "Payment link must use HTTPS.";
            return false;
        }

        if (!string.Equals(parsed.Host, AllowedHost, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Payment link host must be {AllowedHost}.";
            return false;
        }

        string path = parsed.AbsolutePath.TrimEnd('/');
        if (!PathRegex.IsMatch(path))
        {
            error = "Payment link path must match /ABAPAY[A-Za-z0-9]+.";
            return false;
        }

        uri = parsed;
        return true;
    }

    private async Task<string> FetchHtmlAsync(Uri start, CancellationToken cancellationToken)
    {
        Uri current = start;

        for (int redirect = 0; redirect <= MaxRedirects; redirect++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(PageTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
            request.Headers.TryAddWithoutValidation(
                "Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            using HttpResponseMessage response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);

            int status = (int)response.StatusCode;
            if (status is >= 300 and < 400)
            {
                if (redirect == MaxRedirects)
                {
                    throw new InvalidOperationException("Too many redirects.");
                }

                Uri? location = response.Headers.Location
                    ?? throw new InvalidOperationException("Redirect without a Location header.");
                Uri next = location.IsAbsoluteUri ? location : new Uri(current, location);

                if (!string.Equals(next.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(next.Host, AllowedHost, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Redirect left the allowed host.");
                }

                current = next;
                continue;
            }

            response.EnsureSuccessStatusCode();
            return await ReadCappedStringAsync(response, MaxPageBytes, timeoutCts.Token);
        }

        throw new InvalidOperationException("Too many redirects.");
    }

    private static async Task<string> ReadCappedStringAsync(
        HttpResponseMessage response,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is long contentLength && contentLength > maxBytes)
        {
            throw new InvalidOperationException("Response is too large.");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;

        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new InvalidOperationException("Response is too large.");
            }

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private async Task<string> PostJsonAsync(
        string path,
        object body,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        (int status, string text) = await PostJsonRawAsync(path, body, headers, cancellationToken);
        if (status is < 200 or >= 300)
        {
            throw new HttpRequestException($"PayWay endpoint {path} returned {status}.");
        }

        return text;
    }

    private async Task<(int HttpStatus, string Body)> PostJsonRawAsync(
        string path,
        object body,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };

        if (headers is not null)
        {
            foreach (KeyValuePair<string, string> header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        string text = await response.Content.ReadAsStringAsync(cancellationToken);
        return ((int)response.StatusCode, text);
    }

    private static string? ExtractAbaData(string html)
    {
        foreach (Regex pattern in AbaDataPatterns)
        {
            Match match = pattern.Match(html);
            if (!match.Success)
            {
                continue;
            }

            string candidate = UnescapeJs(match.Groups[1].Value);
            if (candidate.Length >= 200 && Base64Regex.IsMatch(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? ExtractRequestTime(string html)
    {
        Match match = RequestTimeRegex.Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string UnescapeJs(string value)
    {
        string unescaped = value.Replace("\\u002F", "/", StringComparison.Ordinal)
            .Replace("\\u002f", "/", StringComparison.Ordinal);
        return UnicodeEscapeRegex.Replace(
            unescaped,
            m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
    }

    private static bool TryGetString(JsonElement element, string property, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
    {
        value = null;
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out JsonElement propertyElement) &&
            propertyElement.ValueKind == JsonValueKind.String)
        {
            value = propertyElement.GetString();
            return value is not null;
        }

        return false;
    }

    /// <summary>Returns the nested <c>data</c> object when the payload is wrapped in one.</summary>
    private static JsonElement? GetDataObject(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("data", out JsonElement data) &&
        data.ValueKind == JsonValueKind.Object
            ? data
            : null;

    /// <summary>
    /// Reads a string property from the top level of the payload, then from a nested
    /// <c>data</c> object. ABA sometimes wraps the QR fields under <c>data</c>.
    /// </summary>
    private static bool TryGetStringDeep(
        JsonElement root,
        string property,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
    {
        if (TryGetString(root, property, out value))
        {
            return true;
        }

        if (GetDataObject(root) is JsonElement data && TryGetString(data, property, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>Reads a property inside the <c>status</c> object at the top level or under <c>data</c>.</summary>
    private static bool TryGetStatusString(
        JsonElement root,
        string property,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("status", out JsonElement status) &&
            TryGetString(status, property, out value))
        {
            return true;
        }

        if (GetDataObject(root) is JsonElement data &&
            data.TryGetProperty("status", out status) &&
            TryGetString(status, property, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    private static int? ReadIntDeep(JsonElement root, string property)
    {
        int? value = ReadInt(root, property);
        if (value is not null)
        {
            return value;
        }

        return GetDataObject(root) is JsonElement data ? ReadInt(data, property) : null;
    }

    private static int? ReadInt(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(property, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string UtcRequestTime(DateTime utcNow) =>
        utcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    private static string RandomHex(int byteCount) =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(byteCount)).ToLowerInvariant();

    private static string Sha512Hex(string value) =>
        Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string HmacSha512Hex(string key, string message) =>
        Convert.ToHexString(
            HMACSHA512.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(message)))
        .ToLowerInvariant();
}
