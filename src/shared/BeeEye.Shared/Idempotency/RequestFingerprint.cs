using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BeeEye.Shared.Idempotency;

/// <summary>
/// The stable hash that decides whether a replayed <c>Idempotency-Key</c> is the <i>same</i> request
/// (ADR 0007 §2.1).
/// <para>
/// The fingerprint covers the route, the canonicalised payload and the caller's subject id. Reordering
/// the properties of a JSON body must not change it — a client library that serialises in a different
/// order on a retry is not making a different request — while a genuinely different value, a different
/// route or a different principal must.
/// </para>
/// <para>
/// Pure and framework-free, so the property is testable directly rather than only through a host.
/// </para>
/// </summary>
public static class RequestFingerprint
{
    /// <summary>
    /// Computes the fingerprint. <paramref name="payloadJson"/> may be null or empty for an endpoint
    /// with no body, in which case the route and principal are the whole request.
    /// </summary>
    public static string Compute(string route, string? payloadJson, string principalId)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(principalId);

        // '\n' separators, so "ab" + "c" and "a" + "bc" cannot produce one string.
        var material = string.Join(
            '\n',
            route.Trim(),
            principalId.Trim(),
            Canonicalise(payloadJson));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    /// <summary>
    /// Rewrites JSON into a canonical form: object properties sorted by name (ordinal), arrays left
    /// in order because their order carries meaning, and no insignificant whitespace.
    /// <para>
    /// Unparseable input is returned trimmed rather than throwing. A body this application cannot
    /// parse never reaches a handler, and hashing it verbatim still gives a stable comparison.
    /// </para>
    /// </summary>
    public static string Canonicalise(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var builder = new StringBuilder();
            Write(document.RootElement, builder);
            return builder.ToString();
        }
        catch (JsonException)
        {
            return json.Trim();
        }
    }

    private static void Write(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                var first = true;
                foreach (var property in element.EnumerateObject()
                             .OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    builder.Append(JsonSerializer.Serialize(property.Name)).Append(':');
                    Write(property.Value, builder);
                }

                builder.Append('}');
                break;

            case JsonValueKind.Array:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in element.EnumerateArray())
                {
                    if (!firstItem)
                    {
                        builder.Append(',');
                    }

                    firstItem = false;
                    Write(item, builder);
                }

                builder.Append(']');
                break;

            case JsonValueKind.Number:
                // Round-tripped through decimal where possible so 10, 10.0 and 1e1 agree; a value
                // outside decimal's range falls back to its raw text rather than being lost.
                builder.Append(
                    element.TryGetDecimal(out var number)
                        ? number.ToString("0.############################", CultureInfo.InvariantCulture)
                        : element.GetRawText());
                break;

            case JsonValueKind.String:
                builder.Append(JsonSerializer.Serialize(element.GetString()));
                break;

            case JsonValueKind.True:
                builder.Append("true");
                break;

            case JsonValueKind.False:
                builder.Append("false");
                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                builder.Append("null");
                break;
        }
    }
}
