using System.Text.Json;

namespace E_Commerce.Services.Khqr;

/// <summary>
/// Outcome of parsing an ABA PayWay <c>check-payment-status</c> response.
/// </summary>
public sealed record PayWayStatusParseResult
{
    /// <summary>
    /// True only when ABA explicitly approved the payment
    /// (<c>data.action == "approved"</c>). Every other value means "not paid yet".
    /// </summary>
    public bool Approved { get; init; }

    /// <summary>The raw <c>data.action</c> value (for logging; never a secret).</summary>
    public string? Action { get; init; }

    /// <summary>The raw <c>status.code</c> value (for logging only; never implies approval).</summary>
    public string? StatusCode { get; init; }

    /// <summary>The raw <c>status.message</c> value (for logging only; never implies approval).</summary>
    public string? StatusMessage { get; init; }

    /// <summary>True when the body was empty or was not valid JSON.</summary>
    public bool Malformed { get; init; }
}

/// <summary>
/// Strict, side-effect free parser for the ABA PayWay <c>check-payment-status</c> response.
/// Mirrors the Node reference (<c>pwRes.data?.data?.action === 'approved'</c>): a payment is
/// approved <b>only</b> when <c>data.action</c> equals <c>"approved"</c> (trimmed,
/// case-insensitive). The HTTP status, the presence of a <c>data</c> object, <c>status.code</c>
/// (for example <c>"00"</c>), <c>status.message</c> (for example <c>"Success"</c>) and any of
/// the words "completed"/"success"/"ok" are <b>never</b> treated as approval. Kept static so it
/// can be unit-tested directly.
/// </summary>
public static class PayWayStatusParser
{
    /// <summary>The single action value that means ABA approved the payment.</summary>
    public const string ApprovedAction = "approved";

    /// <summary>Convenience wrapper returning only the approval flag.</summary>
    public static bool IsApproved(string? json) => Parse(json).Approved;

    /// <summary>
    /// Parses the response body. Returns a result whose <see cref="PayWayStatusParseResult.Approved"/>
    /// is true only for <c>data.action == "approved"</c>. Malformed or empty input yields an
    /// unapproved, non-approved result (<see cref="PayWayStatusParseResult.Malformed"/> is set).
    /// </summary>
    public static PayWayStatusParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PayWayStatusParseResult { Malformed = true };
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            string? action = ReadDataAction(root);
            string? statusCode = ReadStatusField(root, "code");
            string? statusMessage = ReadStatusField(root, "message");

            return new PayWayStatusParseResult
            {
                Action = action,
                StatusCode = statusCode,
                StatusMessage = statusMessage,
                Approved = IsApprovedAction(action),
            };
        }
        catch (JsonException)
        {
            return new PayWayStatusParseResult { Malformed = true };
        }
    }

    /// <summary>
    /// True only when the given <c>action</c> trims to "approved" (case-insensitive).
    /// </summary>
    public static bool IsApprovedAction(string? action) =>
        string.Equals(action?.Trim(), ApprovedAction, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the <c>action</c> field from the nested <c>data</c> object only. This matches the
    /// Node reference and the documented ABA response (<c>{ "data": { "action": "approved" } }</c>).
    /// </summary>
    private static string? ReadDataAction(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out JsonElement data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("action", out JsonElement action) &&
            action.ValueKind == JsonValueKind.String)
        {
            return action.GetString();
        }

        return null;
    }

    /// <summary>
    /// Reads a <c>status</c> field from the top level, or from a nested <c>data</c> object.
    /// Used for logging only; the value never affects approval.
    /// </summary>
    private static string? ReadStatusField(JsonElement root, string field)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("status", out JsonElement status))
        {
            string? value = ReadString(status, field);
            if (value is not null)
            {
                return value;
            }
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out JsonElement data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("status", out JsonElement dataStatus))
        {
            return ReadString(dataStatus, field);
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string property)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}
