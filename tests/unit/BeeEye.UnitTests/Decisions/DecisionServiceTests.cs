using BeeEye.Modules.DecisionsAndOutcomes.Application;
using BeeEye.Persistence.Entities;
using BeeEye.Shared.Decisions;
using BeeEye.Shared.Security;
using Xunit;

namespace BeeEye.UnitTests.Decisions;

/// <summary>
/// Tests for the decision workflow's rules that carry no data access: which action each state offers,
/// who may take it, whether the same person may approve their own decision, and what value the engine
/// actually recommended.
/// <para>
/// Everything asserted here is checked <b>against <see cref="RecommendationLifecycle"/></b> rather than
/// against a hand-written table of expectations, so the workflow and the state machine cannot drift
/// apart while both remain internally consistent. The behaviour that needs a database is covered
/// end-to-end in <c>BeeEye.IntegrationTests</c>.
/// </para>
/// </summary>
public sealed class DecisionServiceTests
{
    private static readonly IReadOnlySet<string> Reviewer =
        new HashSet<string>(StringComparer.Ordinal) { Permissions.RecommendationReview };

    private static readonly IReadOnlySet<string> Approver =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Permissions.RecommendationReview, Permissions.RecommendationApprove,
        };

    private static readonly IReadOnlySet<string> Recorder =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Permissions.RecommendationReview, Permissions.DecisionOutcomeRecord,
        };

    private static readonly IReadOnlySet<string> Nobody = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The lifecycle move each exposed action performs, as the service implements it.</summary>
    public static TheoryData<string, RecommendationStatus, RecommendationStatus> ActionTransitions() => new()
    {
        { DecisionActions.Claim, RecommendationStatus.Generated, RecommendationStatus.UnderReview },
        { DecisionActions.Accept, RecommendationStatus.UnderReview, RecommendationStatus.Accepted },
        { DecisionActions.AcceptWithModification, RecommendationStatus.UnderReview, RecommendationStatus.AcceptedModified },
        { DecisionActions.Reject, RecommendationStatus.UnderReview, RecommendationStatus.Rejected },
        { DecisionActions.MarkImplemented, RecommendationStatus.Accepted, RecommendationStatus.Implemented },
        { DecisionActions.MarkImplemented, RecommendationStatus.AcceptedModified, RecommendationStatus.Implemented },
        { DecisionActions.RecordOutcome, RecommendationStatus.Implemented, RecommendationStatus.OutcomeRecorded },
    };

    // ---------------------------------------------------------------- transitions the service exposes

    [Theory]
    [MemberData(nameof(ActionTransitions))]
    public void Every_action_the_service_exposes_is_a_transition_the_state_machine_allows(
        string action, RecommendationStatus from, RecommendationStatus to)
    {
        var verdict = RecommendationLifecycle.CanTransition(
            from, to, approvalInFlight: false, hasReason: true);

        Assert.True(verdict.Allowed, $"'{action}' claims {from} → {to}, which the state machine refuses.");
    }

    [Theory]
    [MemberData(nameof(ActionTransitions))]
    public void Every_action_is_offered_exactly_where_the_state_machine_permits_it(
        string action, RecommendationStatus from, RecommendationStatus to)
    {
        // Offered from the state it applies to...
        Assert.Contains(action, DecisionActions.For(from, OutcomeFor(from), false, Everything()));

        // ...and offered from no state the machine will not move.
        foreach (var other in Enum.GetValues<RecommendationStatus>())
        {
            if (RecommendationLifecycle.NextStates(other).Contains(to))
            {
                continue;
            }

            Assert.DoesNotContain(action, DecisionActions.For(other, OutcomeFor(other), false, Everything()));
        }
    }

    [Theory]
    [InlineData(RecommendationStatus.Rejected)]
    [InlineData(RecommendationStatus.Expired)]
    [InlineData(RecommendationStatus.Superseded)]
    [InlineData(RecommendationStatus.OutcomeRecorded)]
    public void A_terminal_record_offers_nothing_at_all(RecommendationStatus terminal)
    {
        Assert.Empty(DecisionActions.For(terminal, DecisionOutcome.Accepted, false, Everything()));
    }

    [Fact]
    public void An_unclaimed_record_offers_only_the_claim()
    {
        var available = DecisionActions.For(RecommendationStatus.Generated, null, false, Everything());

        Assert.Equal([DecisionActions.Claim], available);
    }

    [Fact]
    public void A_claimed_record_no_longer_offers_a_second_claim()
    {
        var available = DecisionActions.For(
            RecommendationStatus.UnderReview, DecisionOutcome.Open, false, Everything());

        Assert.DoesNotContain(DecisionActions.Claim, available);
    }

    [Fact]
    public void Sign_off_is_offered_only_while_a_step_is_pending()
    {
        Assert.Contains(
            DecisionActions.SignOff,
            DecisionActions.For(RecommendationStatus.UnderReview, DecisionOutcome.Open, true, Approver));

        Assert.DoesNotContain(
            DecisionActions.SignOff,
            DecisionActions.For(RecommendationStatus.UnderReview, DecisionOutcome.Open, false, Approver));
    }

    // ---------------------------------------------------------------- permissions

    [Fact]
    public void A_reviewer_may_claim_but_may_not_decide()
    {
        var available = DecisionActions.For(RecommendationStatus.UnderReview, DecisionOutcome.Open, true, Reviewer);

        Assert.DoesNotContain(DecisionActions.Accept, available);
        Assert.DoesNotContain(DecisionActions.AcceptWithModification, available);
        Assert.DoesNotContain(DecisionActions.Reject, available);
        Assert.DoesNotContain(DecisionActions.SignOff, available);
    }

    [Fact]
    public void An_approver_is_offered_every_verdict()
    {
        var available = DecisionActions.For(RecommendationStatus.UnderReview, DecisionOutcome.Open, false, Approver);

        Assert.Contains(DecisionActions.Accept, available);
        Assert.Contains(DecisionActions.AcceptWithModification, available);
        Assert.Contains(DecisionActions.Reject, available);
    }

    [Fact]
    public void Recording_an_outcome_needs_its_own_permission_not_the_approval_one()
    {
        Assert.Contains(
            DecisionActions.RecordOutcome,
            DecisionActions.For(RecommendationStatus.Implemented, DecisionOutcome.Accepted, false, Recorder));

        Assert.DoesNotContain(
            DecisionActions.RecordOutcome,
            DecisionActions.For(RecommendationStatus.Implemented, DecisionOutcome.Accepted, false, Reviewer));
    }

    [Theory]
    [InlineData(RecommendationStatus.Generated)]
    [InlineData(RecommendationStatus.UnderReview)]
    [InlineData(RecommendationStatus.Accepted)]
    [InlineData(RecommendationStatus.AcceptedModified)]
    [InlineData(RecommendationStatus.Implemented)]
    public void A_caller_with_no_permissions_is_offered_nothing(RecommendationStatus status)
    {
        Assert.Empty(DecisionActions.For(status, OutcomeFor(status), true, Nobody));
    }

    [Fact]
    public void For_rejects_null_permissions()
    {
        Assert.Throws<ArgumentNullException>(
            () => DecisionActions.For(RecommendationStatus.Generated, null, false, null!));
    }

    // ---------------------------------------------------------------- self-approval

    [Fact]
    public void The_same_subject_cannot_both_decide_and_approve()
    {
        Assert.True(SubjectIds.Same("6f8b1c2e-user", "6f8b1c2e-user"));
    }

    [Fact]
    public void A_different_subject_may_approve()
    {
        Assert.False(SubjectIds.Same("6f8b1c2e-user", "0a1b2c3d-other"));
    }

    [Theory]
    [InlineData("  6f8b1c2e-user  ")]
    [InlineData("6f8b1c2e-user\t")]
    [InlineData("\n6f8b1c2e-user")]
    public void Padding_a_subject_id_does_not_create_a_second_identity(string padded)
    {
        // Otherwise a stray space in a claim would be a one-character self-approval bypass.
        Assert.True(SubjectIds.Same("6f8b1c2e-user", padded));
    }

    [Fact]
    public void Subject_ids_are_compared_ordinally_so_case_is_significant()
    {
        // Opaque identifiers: treating ABC and abc as one person would be a different bug, and worse.
        Assert.False(SubjectIds.Same("6F8B1C2E-USER", "6f8b1c2e-user"));
    }

    [Theory]
    [InlineData(null, "someone")]
    [InlineData("someone", null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void An_unidentified_actor_matches_nobody_including_another_unidentified_actor(
        string? left, string? right)
    {
        Assert.False(SubjectIds.Same(left, right));
    }

    // ---------------------------------------------------------------- the engine's own value

    [Theory]
    [InlineData("Prepare a transfer recommendation: 3 unit(s) Riyadh → Jeddah.", 3)]
    [InlineData("Increase order allocation by 364 units next month.", 364)]
    [InlineData("Reduce procurement to 12 units.", 12)]
    public void A_quantity_stated_by_the_engine_is_recovered_from_the_frozen_action(string action, decimal expected)
    {
        Assert.True(RecommendedValues.TryDerive(
            Recommendation(action), ModificationRules.TransferQty, out var value));

        Assert.Equal(expected, value);
    }

    [Fact]
    public void A_discount_stated_by_the_engine_is_recovered()
    {
        Assert.True(RecommendedValues.TryDerive(
            Recommendation("Apply a controlled discount of 15% for one cycle."),
            ModificationRules.DiscountPct,
            out var value));

        Assert.Equal(15m, value);
    }

    [Theory]
    [InlineData("Retain. No action required.")]
    [InlineData("Investigate demand data.")]
    [InlineData("")]
    public void An_action_stating_no_number_yields_nothing_rather_than_a_guess(string action)
    {
        Assert.False(RecommendedValues.TryDerive(
            Recommendation(action), ModificationRules.ProposedQty, out var value));

        Assert.Equal(0m, value);
    }

    [Fact]
    public void A_field_outside_the_allowlist_derives_nothing()
    {
        Assert.False(RecommendedValues.TryDerive(
            Recommendation("Transfer 3 units."), "selling_price", out _));
    }

    [Fact]
    public void A_decimal_quantity_is_parsed_invariantly()
    {
        Assert.True(RecommendedValues.TryDerive(
            Recommendation("Order 12.5 units."), ModificationRules.ProposedQty, out var value));

        Assert.Equal(12.5m, value);
    }

    [Fact]
    public void TryDerive_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(
            () => RecommendedValues.TryDerive(null!, ModificationRules.ProposedQty, out _));
    }

    // ---------------------------------------------------------------- helpers

    private static Recommendation Recommendation(string action) => new() { Action = action };

    private static IReadOnlySet<string> Everything() =>
        new HashSet<string>(Permissions.All, StringComparer.Ordinal);

    /// <summary>The decision outcome a record in this state would realistically carry.</summary>
    private static DecisionOutcome? OutcomeFor(RecommendationStatus status) => status switch
    {
        RecommendationStatus.Generated => null,
        RecommendationStatus.UnderReview => DecisionOutcome.Open,
        RecommendationStatus.Accepted => DecisionOutcome.Accepted,
        RecommendationStatus.AcceptedModified => DecisionOutcome.AcceptedModified,
        RecommendationStatus.Rejected => DecisionOutcome.Rejected,
        _ => DecisionOutcome.Accepted,
    };
}
