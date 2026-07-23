using BeeEye.Analytics.Explainability;
using Microsoft.Extensions.Logging;

namespace BeeEye.Modules.Predictions.Application;

/// <summary>Why an explanation request produced no explanation.</summary>
public enum ExplanationStatus
{
    /// <summary>An explanation was assembled.</summary>
    Explained,

    /// <summary>No provider claims the requested subject kind — a caller error, answered with 400.</summary>
    UnknownKind,

    /// <summary>
    /// The kind is known but the provider had nothing for that reference — answered with 404.
    /// <para>
    /// Distinct from <see cref="Failed"/> on purpose: "there is no such unit" and "we could not reach
    /// the inventory data" are different answers, and collapsing them would tell a user that a figure
    /// carries no explanation when in truth the explanation could not be fetched.
    /// </para>
    /// </summary>
    NotFound,

    /// <summary>
    /// Every provider that could have answered failed. Answered with <b>200 and a gap</b>, never a
    /// 500: the drawer says what is missing rather than showing a stack of nothing.
    /// </summary>
    Failed,
}

/// <summary>A context that could not be reached while assembling an explanation.</summary>
/// <param name="Area">The subject kind whose provider failed.</param>
/// <param name="Reason">A safe, non-technical summary — never an exception message or a stack trace.</param>
public sealed record ExplanationGap(string Area, string Reason);

/// <summary>What the service found for one subject.</summary>
/// <param name="Status">Which of the four outcomes occurred.</param>
/// <param name="Explanation">The explanation, when one was assembled.</param>
/// <param name="Gaps">
/// Contexts that failed. Non-empty with a null <paramref name="Explanation"/> means nothing could be
/// assembled; non-empty <i>alongside</i> an explanation means the answer is incomplete and must say so.
/// </param>
public sealed record ExplanationOutcome(
    ExplanationStatus Status,
    Explanation? Explanation,
    IReadOnlyList<ExplanationGap> Gaps);

/// <summary>
/// Assembles the explainability payload (V3-DS-006) from the <see cref="IExplainabilityProvider"/>
/// instances each bounded context registers.
/// <para>
/// This service holds no domain rules and touches no database — exactly like
/// <c>DecisionFeedService</c>, and for the same reason: every rule about <i>why</i> a figure is what
/// it is lives with the context that owns the figure. That is what allows one endpoint to explain
/// eight use cases without referencing a single module.
/// </para>
/// </summary>
public sealed class ExplainabilityService
{
    private readonly IReadOnlyList<IExplainabilityProvider> providers;
    private readonly ILogger<ExplainabilityService> logger;

    /// <summary>
    /// Indexes the registered providers by kind and <b>fails immediately</b> on a duplicate claim.
    /// <para>
    /// The service is resolved once during endpoint mapping precisely so this runs at start-up. Two
    /// providers claiming one kind is a composition-root bug: whichever answered would depend on DI
    /// registration order, which is not a contract anyone should be reasoning about. Discovering that
    /// on the request path — intermittently, in production, under whichever module happened to
    /// register first — is strictly worse than refusing to boot.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">When two providers claim the same subject kind.</exception>
    public ExplainabilityService(
        IEnumerable<IExplainabilityProvider> providers,
        ILogger<ExplainabilityService> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);

        this.providers = [.. providers];
        this.logger = logger;

        var claimedBy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var provider in this.providers)
        {
            foreach (var kind in provider.SubjectKinds)
            {
                var providerName = provider.GetType().Name;
                if (claimedBy.TryGetValue(kind, out var incumbent))
                {
                    throw new InvalidOperationException(
                        $"Subject kind '{kind}' is claimed by both {incumbent} and {providerName}. "
                        + "Exactly one provider may explain a kind, otherwise which one answers depends "
                        + "on dependency-injection registration order.");
                }

                claimedBy[kind] = providerName;
            }
        }

        RegisteredKinds = [.. claimedBy.Keys.OrderBy(k => k, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Every subject kind some provider claims, ordered. Named in the 400 a caller gets for an
    /// unknown kind, so the fix is in the error rather than in the source.
    /// </summary>
    public IReadOnlyList<string> RegisteredKinds { get; }

    public async Task<ExplanationOutcome> ExplainAsync(
        string subjectKind,
        string subjectRef,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subjectKind);
        ArgumentNullException.ThrowIfNull(subjectRef);

        var claimants = providers.Where(p => p.SubjectKinds.Contains(subjectKind)).ToList();
        if (claimants.Count == 0)
        {
            return new ExplanationOutcome(ExplanationStatus.UnknownKind, null, []);
        }

        var gaps = new List<ExplanationGap>();

        // Sequential by design. Providers resolve read services that share this request's scoped
        // DbContext, and EF Core's DbContext is not thread-safe — running them concurrently would
        // race on the same connection. The constructor guarantees at most one claimant per kind
        // today, so this loop runs once; it stays a loop so that stays true by construction rather
        // than by everyone remembering.
        foreach (var provider in claimants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var explanation = await provider.ExplainAsync(subjectKind, subjectRef, cancellationToken);
                if (explanation is not null)
                {
                    return new ExplanationOutcome(ExplanationStatus.Explained, explanation, gaps);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller went away — propagate rather than reporting a false data gap. A
                // cancelled request that reports "this area could not be assessed" would have people
                // investigating an outage that never happened.
                throw;
            }
            catch (Exception ex)
            {
                // One failing context must not become a 500, but it must not be hidden either: the
                // gap is reported so the reader knows the panel is incomplete. The exception detail
                // goes to the log only — never to the browser.
                logger.LogError(
                    ex,
                    "Explainability provider {Provider} failed for subject kind {Kind}; the response will report a gap.",
                    provider.GetType().Name,
                    subjectKind);

                gaps.Add(new ExplanationGap(
                    subjectKind,
                    "This explanation could not be assembled. The underlying analysis is unavailable."));
            }
        }

        // Every claimant either failed or had nothing. A gap means the former, which is a different
        // answer from "no such subject" and gets a different status code.
        return gaps.Count > 0
            ? new ExplanationOutcome(ExplanationStatus.Failed, null, gaps)
            : new ExplanationOutcome(ExplanationStatus.NotFound, null, []);
    }
}
