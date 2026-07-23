using BeeEye.Shared.Decisions;
using Xunit;

namespace BeeEye.UnitTests.Decisions;

/// <summary>
/// Tests for the recommendation lifecycle state machine and its guards
/// (<c>docs/adr/0006-recommendation-decision-workflow.md</c> §3).
/// <para>
/// Every edge and every non-edge is asserted, because a state machine that silently permits an
/// unintended transition is exactly how an audit trail becomes untrustworthy.
/// </para>
/// </summary>
public sealed class RecommendationLifecycleTests
{
    private static readonly RecommendationStatus[] AllStates = Enum.GetValues<RecommendationStatus>();

    /// <summary>The complete edge set from ADR 0006 §3, written out independently of the implementation.</summary>
    private static readonly HashSet<(RecommendationStatus From, RecommendationStatus To)> ExpectedEdges =
    [
        (RecommendationStatus.Generated, RecommendationStatus.UnderReview),
        (RecommendationStatus.Generated, RecommendationStatus.Superseded),
        (RecommendationStatus.Generated, RecommendationStatus.Expired),
        (RecommendationStatus.UnderReview, RecommendationStatus.Accepted),
        (RecommendationStatus.UnderReview, RecommendationStatus.AcceptedModified),
        (RecommendationStatus.UnderReview, RecommendationStatus.Rejected),
        (RecommendationStatus.UnderReview, RecommendationStatus.Superseded),
        (RecommendationStatus.Accepted, RecommendationStatus.Implemented),
        (RecommendationStatus.AcceptedModified, RecommendationStatus.Implemented),
        (RecommendationStatus.Implemented, RecommendationStatus.OutcomeRecorded),
    ];

    // ---------------------------------------------------------------- shape

    [Fact]
    public void Every_recommendation_starts_as_generated()
    {
        Assert.Equal(RecommendationStatus.Generated, RecommendationLifecycle.InitialStatus);
    }

    [Fact]
    public void The_initial_state_is_not_terminal()
    {
        Assert.False(RecommendationLifecycle.IsTerminal(RecommendationLifecycle.InitialStatus));
    }

    [Theory]
    [InlineData(RecommendationStatus.Rejected)]
    [InlineData(RecommendationStatus.Expired)]
    [InlineData(RecommendationStatus.Superseded)]
    [InlineData(RecommendationStatus.OutcomeRecorded)]
    public void Terminal_states_are_terminal(RecommendationStatus status)
    {
        Assert.True(RecommendationLifecycle.IsTerminal(status));
        Assert.Empty(RecommendationLifecycle.NextStates(status));
    }

    [Theory]
    [InlineData(RecommendationStatus.Generated)]
    [InlineData(RecommendationStatus.UnderReview)]
    [InlineData(RecommendationStatus.Accepted)]
    [InlineData(RecommendationStatus.AcceptedModified)]
    [InlineData(RecommendationStatus.Implemented)]
    public void Non_terminal_states_have_at_least_one_next_state(RecommendationStatus status)
    {
        Assert.False(RecommendationLifecycle.IsTerminal(status));
        Assert.NotEmpty(RecommendationLifecycle.NextStates(status));
    }

    [Fact]
    public void Every_state_is_reachable_from_generated()
    {
        // A state nothing can reach is dead code in the workflow.
        var reached = new HashSet<RecommendationStatus> { RecommendationStatus.Generated };
        var frontier = new Queue<RecommendationStatus>([RecommendationStatus.Generated]);

        while (frontier.Count > 0)
        {
            foreach (var next in RecommendationLifecycle.NextStates(frontier.Dequeue()))
            {
                if (reached.Add(next))
                {
                    frontier.Enqueue(next);
                }
            }
        }

        var unreachable = AllStates.Where(s => !reached.Contains(s)).ToList();
        Assert.True(unreachable.Count == 0, $"Unreachable states: {string.Join(", ", unreachable)}");
    }

    // ---------------------------------------------------------------- the full transition matrix

    [Fact]
    public void The_transition_matrix_matches_the_adr_exactly()
    {
        var unexpected = new List<string>();

        foreach (var from in AllStates)
        {
            foreach (var to in AllStates)
            {
                var expected = ExpectedEdges.Contains((from, to));
                var actual = RecommendationLifecycle.CanTransition(from, to).Allowed;

                if (expected != actual)
                {
                    unexpected.Add($"{from}->{to}: expected {(expected ? "allowed" : "refused")}, was {(actual ? "allowed" : "refused")}");
                }
            }
        }

        Assert.True(unexpected.Count == 0, string.Join("; ", unexpected));
    }

    [Fact]
    public void No_state_transitions_to_itself()
    {
        foreach (var state in AllStates)
        {
            Assert.False(
                RecommendationLifecycle.CanTransition(state, state).Allowed,
                $"{state} must not transition to itself");
        }
    }

    [Fact]
    public void A_recommendation_cannot_go_straight_from_generated_to_accepted()
    {
        // A human must claim it first; skipping review would produce an approval with no reviewer.
        var result = RecommendationLifecycle.CanTransition(
            RecommendationStatus.Generated, RecommendationStatus.Accepted);

        Assert.False(result.Allowed);
        Assert.Equal(TransitionRefusal.NotAllowed, result.Refusal);
    }

    [Fact]
    public void A_terminal_state_refuses_every_transition_with_a_specific_reason()
    {
        foreach (var terminal in RecommendationLifecycle.Terminal)
        {
            foreach (var to in AllStates)
            {
                var result = RecommendationLifecycle.CanTransition(terminal, to);
                Assert.False(result.Allowed);
                Assert.Equal(TransitionRefusal.SourceIsTerminal, result.Refusal);
            }
        }
    }

    [Fact]
    public void A_rejected_recommendation_cannot_be_revived()
    {
        Assert.False(RecommendationLifecycle
            .CanTransition(RecommendationStatus.Rejected, RecommendationStatus.UnderReview).Allowed);
        Assert.False(RecommendationLifecycle
            .CanTransition(RecommendationStatus.Rejected, RecommendationStatus.Accepted).Allowed);
    }

    // ---------------------------------------------------------------- guards

    [Fact]
    public void Expiry_is_suspended_once_a_human_owns_the_item()
    {
        var result = RecommendationLifecycle.CanTransition(
            RecommendationStatus.UnderReview, RecommendationStatus.Expired);

        Assert.False(result.Allowed);
        Assert.Equal(TransitionRefusal.ExpiryBlockedByReview, result.Refusal);
    }

    [Fact]
    public void An_unclaimed_recommendation_may_still_expire()
    {
        Assert.True(RecommendationLifecycle
            .CanTransition(RecommendationStatus.Generated, RecommendationStatus.Expired).Allowed);
    }

    [Fact]
    public void Supersession_is_blocked_while_an_approval_is_in_flight()
    {
        var result = RecommendationLifecycle.CanTransition(
            RecommendationStatus.UnderReview, RecommendationStatus.Superseded, approvalInFlight: true);

        Assert.False(result.Allowed);
        Assert.Equal(TransitionRefusal.SupersessionBlockedByApprovalInFlight, result.Refusal);
    }

    [Fact]
    public void Supersession_is_allowed_when_no_approval_is_in_flight()
    {
        Assert.True(RecommendationLifecycle.CanTransition(
            RecommendationStatus.UnderReview, RecommendationStatus.Superseded, approvalInFlight: false).Allowed);

        Assert.True(RecommendationLifecycle.CanTransition(
            RecommendationStatus.Generated, RecommendationStatus.Superseded, approvalInFlight: false).Allowed);
    }

    [Fact]
    public void An_approval_in_flight_does_not_block_unrelated_transitions()
    {
        Assert.True(RecommendationLifecycle.CanTransition(
            RecommendationStatus.UnderReview, RecommendationStatus.Accepted, approvalInFlight: true).Allowed);
    }

    [Fact]
    public void Rejection_requires_a_reason()
    {
        var result = RecommendationLifecycle.CanTransition(
            RecommendationStatus.UnderReview, RecommendationStatus.Rejected, hasReason: false);

        Assert.False(result.Allowed);
        Assert.Equal(TransitionRefusal.ReasonRequired, result.Refusal);
    }

    [Fact]
    public void Rejection_with_a_reason_is_allowed()
    {
        Assert.True(RecommendationLifecycle.CanTransition(
            RecommendationStatus.UnderReview, RecommendationStatus.Rejected, hasReason: true).Allowed);
    }

    [Fact]
    public void Only_rejection_requires_a_reason()
    {
        Assert.True(RecommendationLifecycle.CanTransition(
            RecommendationStatus.UnderReview, RecommendationStatus.Accepted, hasReason: false).Allowed);
    }

    // ---------------------------------------------------------------- explanations

    [Fact]
    public void An_allowed_transition_explains_itself_as_allowed()
    {
        var result = RecommendationLifecycle.CanTransition(
            RecommendationStatus.Generated, RecommendationStatus.UnderReview);

        Assert.True(result.Allowed);
        Assert.Equal(TransitionRefusal.None, result.Refusal);
        Assert.Equal("Allowed.", result.Explain());
    }

    [Fact]
    public void Every_refusal_has_a_safe_non_technical_explanation()
    {
        foreach (var refusal in Enum.GetValues<TransitionRefusal>())
        {
            var text = new TransitionResult(refusal == TransitionRefusal.None, refusal).Explain();

            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.EndsWith(".", text, StringComparison.Ordinal);
            // No type names, stack traces or internal identifiers leak to the caller.
            Assert.DoesNotContain("Exception", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RecommendationStatus", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_happy_path_runs_generated_to_outcome_recorded()
    {
        var path = new[]
        {
            RecommendationStatus.Generated,
            RecommendationStatus.UnderReview,
            RecommendationStatus.Accepted,
            RecommendationStatus.Implemented,
            RecommendationStatus.OutcomeRecorded,
        };

        for (var i = 0; i < path.Length - 1; i++)
        {
            Assert.True(
                RecommendationLifecycle.CanTransition(path[i], path[i + 1]).Allowed,
                $"{path[i]} -> {path[i + 1]} must be allowed");
        }
    }

    [Fact]
    public void An_accepted_with_modification_path_also_reaches_outcome_recorded()
    {
        Assert.True(RecommendationLifecycle
            .CanTransition(RecommendationStatus.UnderReview, RecommendationStatus.AcceptedModified).Allowed);
        Assert.True(RecommendationLifecycle
            .CanTransition(RecommendationStatus.AcceptedModified, RecommendationStatus.Implemented).Allowed);
        Assert.True(RecommendationLifecycle
            .CanTransition(RecommendationStatus.Implemented, RecommendationStatus.OutcomeRecorded).Allowed);
    }
}
