using BeeEye.Shared.Idempotency;
using Xunit;

namespace BeeEye.UnitTests.Idempotency;

/// <summary>
/// Tests for the <c>Idempotency-Key</c> fingerprint and key validation (ADR 0007 §2.1).
/// <para>
/// The fingerprint decides whether a replayed key is the <i>same</i> request. Get it too strict and a
/// client library that serialises properties in a different order on a retry gets a 422 for doing
/// exactly what the header exists to make safe; get it too loose and a genuinely different request
/// silently replays someone else's answer.
/// </para>
/// </summary>
public sealed class FingerprintTests
{
    private const string Route = "POST /api/v1/decisions/abc/accept-with-modification";
    private const string Principal = "6f8b1c2e-user";

    // ---------------------------------------------------------------- stability

    [Fact]
    public void Reordering_the_properties_of_a_body_does_not_change_the_fingerprint()
    {
        var a = RequestFingerprint.Compute(Route, """{"field":"discount_pct","from":15,"to":10}""", Principal);
        var b = RequestFingerprint.Compute(Route, """{"to":10,"from":15,"field":"discount_pct"}""", Principal);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Reordering_nested_properties_does_not_change_the_fingerprint()
    {
        var a = RequestFingerprint.Compute(Route, """{"body":{"to":10,"from":15},"route":{"id":"x"}}""", Principal);
        var b = RequestFingerprint.Compute(Route, """{"route":{"id":"x"},"body":{"from":15,"to":10}}""", Principal);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Whitespace_and_formatting_do_not_change_the_fingerprint()
    {
        var a = RequestFingerprint.Compute(Route, """{"field":"proposed_qty","to":30}""", Principal);
        var b = RequestFingerprint.Compute(Route, "{\n  \"field\" : \"proposed_qty\",\n  \"to\" : 30\n}", Principal);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Numeric_scale_does_not_change_the_fingerprint()
    {
        // 10, 10.0 and 1e1 are the same number, and a client library may emit any of them.
        var a = RequestFingerprint.Compute(Route, """{"to":10}""", Principal);
        var b = RequestFingerprint.Compute(Route, """{"to":10.0}""", Principal);
        var c = RequestFingerprint.Compute(Route, """{"to":1e1}""", Principal);

        Assert.Equal(a, b);
        Assert.Equal(a, c);
    }

    [Fact]
    public void The_fingerprint_is_deterministic_across_calls()
    {
        var body = """{"note":"Not this cycle"}""";

        Assert.Equal(
            RequestFingerprint.Compute(Route, body, Principal),
            RequestFingerprint.Compute(Route, body, Principal));
    }

    // ---------------------------------------------------------------- discrimination

    [Fact]
    public void A_different_value_changes_the_fingerprint()
    {
        var a = RequestFingerprint.Compute(Route, """{"field":"discount_pct","from":15,"to":10}""", Principal);
        var b = RequestFingerprint.Compute(Route, """{"field":"discount_pct","from":15,"to":11}""", Principal);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void A_different_principal_changes_the_fingerprint()
    {
        var body = """{"to":10}""";

        Assert.NotEqual(
            RequestFingerprint.Compute(Route, body, Principal),
            RequestFingerprint.Compute(Route, body, "0a1b2c3d-other"));
    }

    [Fact]
    public void A_different_route_changes_the_fingerprint()
    {
        var body = """{"to":10}""";

        Assert.NotEqual(
            RequestFingerprint.Compute(Route, body, Principal),
            RequestFingerprint.Compute("POST /api/v1/decisions/abc/reject", body, Principal));
    }

    [Fact]
    public void Array_order_is_significant_because_it_carries_meaning()
    {
        var a = RequestFingerprint.Compute(Route, """{"steps":[1,2]}""", Principal);
        var b = RequestFingerprint.Compute(Route, """{"steps":[2,1]}""", Principal);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Field_boundaries_cannot_be_shifted_to_forge_a_collision()
    {
        // Without a separator, route "ab" + principal "c" and route "a" + principal "bc" would hash
        // the same material.
        Assert.NotEqual(
            RequestFingerprint.Compute("ab", null, "c"),
            RequestFingerprint.Compute("a", null, "bc"));
    }

    // ---------------------------------------------------------------- shape and robustness

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_endpoint_with_no_body_still_has_a_fingerprint(string? body)
    {
        var fingerprint = RequestFingerprint.Compute(Route, body, Principal);

        Assert.Equal(64, fingerprint.Length);
        Assert.Equal(fingerprint.ToLowerInvariant(), fingerprint);
    }

    [Fact]
    public void Bodyless_requests_to_different_routes_still_differ()
    {
        Assert.NotEqual(
            RequestFingerprint.Compute("POST /api/v1/decisions/a/accept", null, Principal),
            RequestFingerprint.Compute("POST /api/v1/decisions/b/accept", null, Principal));
    }

    [Fact]
    public void Unparseable_json_is_hashed_verbatim_rather_than_throwing()
    {
        // A body this application cannot parse never reaches a handler; the fingerprint still has to
        // be computable so the request is refused consistently on every retry.
        var fingerprint = RequestFingerprint.Compute(Route, "{not json", Principal);

        Assert.Equal(64, fingerprint.Length);
    }

    [Fact]
    public void Canonicalising_sorts_object_keys_and_preserves_array_order()
    {
        Assert.Equal(
            """{"a":1,"b":[3,2]}""",
            RequestFingerprint.Canonicalise("""{"b":[3,2],"a":1}"""));
    }

    [Fact]
    public void Canonicalising_preserves_the_json_literals()
    {
        Assert.Equal(
            """{"missing":null,"no":false,"yes":true}""",
            RequestFingerprint.Canonicalise("""{"yes":true,"no":false,"missing":null}"""));
    }

    [Fact]
    public void Compute_rejects_a_null_route_or_principal()
    {
        Assert.Throws<ArgumentNullException>(() => RequestFingerprint.Compute(null!, null, Principal));
        Assert.Throws<ArgumentNullException>(() => RequestFingerprint.Compute(Route, null, null!));
    }

    // ---------------------------------------------------------------- the key itself

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_key_is_reported_as_missing_not_malformed(string? raw)
    {
        Assert.Equal(IdempotencyKeyProblem.Missing, IdempotencyKey.Validate(raw, out _));
    }

    [Theory]
    [InlineData("0198f0c2-8f4a-7b31-9d2e-4a1f5c6b7d8e")]
    [InlineData("01JQ8ZC3K7YV9WQ2N4M6P8R0TA")]
    [InlineData("cockpit:0198f0c2-8f4a-7b31-9d2e-4a1f5c6b7d8e")]
    public void A_uuid_or_ulid_is_accepted(string raw)
    {
        Assert.Equal(IdempotencyKeyProblem.None, IdempotencyKey.Validate(raw, out var key));
        Assert.Equal(raw, key);
    }

    [Fact]
    public void A_key_is_normalised_by_trimming()
    {
        Assert.Equal(IdempotencyKeyProblem.None, IdempotencyKey.Validate("  abc-defgh  ", out var key));
        Assert.Equal("abc-defgh", key);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("has spaces in it")]
    [InlineData("drop;table")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("key/with/slashes")]
    public void An_unusable_key_is_malformed(string raw)
    {
        Assert.Equal(IdempotencyKeyProblem.Malformed, IdempotencyKey.Validate(raw, out var key));
        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public void An_over_long_key_is_malformed()
    {
        Assert.Equal(
            IdempotencyKeyProblem.Malformed,
            IdempotencyKey.Validate(new string('a', IdempotencyKey.MaxLength + 1), out _));

        Assert.Equal(
            IdempotencyKeyProblem.None,
            IdempotencyKey.Validate(new string('a', IdempotencyKey.MaxLength), out _));
    }

    [Theory]
    [InlineData(IdempotencyKeyProblem.Missing)]
    [InlineData(IdempotencyKeyProblem.Malformed)]
    public void Every_refusal_names_the_header_so_the_fix_is_obvious(IdempotencyKeyProblem problem)
    {
        Assert.Contains(IdempotencyKey.HeaderName, IdempotencyKey.Explain(problem), StringComparison.Ordinal);
    }
}
