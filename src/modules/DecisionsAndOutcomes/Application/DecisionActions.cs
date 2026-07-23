using BeeEye.Persistence.Entities;
using BeeEye.Shared.Decisions;
using BeeEye.Shared.Security;

namespace BeeEye.Modules.DecisionsAndOutcomes.Application;

/// <summary>
/// The named operations the Decision Log exposes, and which of them a given record and caller allow.
/// <para>
/// Computed on the server from the state machine and the caller's permissions, and sent to the screen,
/// so the UI renders exactly the controls the API would accept. The alternative — the client deciding
/// for itself — is how a button appears that always returns 403.
/// </para>
/// </summary>
public static class DecisionActions
{
    public const string Claim = "claim";
    public const string Accept = "accept";
    public const string AcceptWithModification = "accept-with-modification";
    public const string Reject = "reject";
    public const string SignOff = "sign-off";
    public const string MarkImplemented = "implemented";
    public const string RecordOutcome = "record-outcome";

    /// <summary>Every action name, in workflow order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Claim, Accept, AcceptWithModification, Reject, SignOff, MarkImplemented, RecordOutcome,
    ];

    /// <summary>
    /// The actions available on one record for one caller.
    /// </summary>
    /// <param name="status">The recommendation's current lifecycle status.</param>
    /// <param name="outcome">The open decision's outcome, or null when nobody has claimed it.</param>
    /// <param name="hasPendingStep">True when an approval step is still awaiting sign-off.</param>
    /// <param name="permissions">The caller's expanded permissions.</param>
    public static IReadOnlyList<string> For(
        RecommendationStatus status,
        DecisionOutcome? outcome,
        bool hasPendingStep,
        IReadOnlySet<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var available = new List<string>();
        var next = RecommendationLifecycle.NextStates(status);

        // Claiming is a review activity, not an approval — it opens the decision, it does not make it.
        if (next.Contains(RecommendationStatus.UnderReview)
            && outcome is null
            && permissions.Contains(Permissions.RecommendationReview))
        {
            available.Add(Claim);
        }

        if (permissions.Contains(Permissions.RecommendationApprove))
        {
            if (next.Contains(RecommendationStatus.Accepted))
            {
                available.Add(Accept);
            }

            if (next.Contains(RecommendationStatus.AcceptedModified))
            {
                available.Add(AcceptWithModification);
            }

            if (next.Contains(RecommendationStatus.Rejected))
            {
                available.Add(Reject);
            }

            if (hasPendingStep)
            {
                available.Add(SignOff);
            }

            if (next.Contains(RecommendationStatus.Implemented))
            {
                available.Add(MarkImplemented);
            }
        }

        if (next.Contains(RecommendationStatus.OutcomeRecorded)
            && permissions.Contains(Permissions.DecisionOutcomeRecord))
        {
            available.Add(RecordOutcome);
        }

        return available;
    }
}
