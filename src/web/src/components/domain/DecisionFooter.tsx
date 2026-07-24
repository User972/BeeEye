import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { Icon } from '@/components/ui/Icon';
import { apiErrorMessage } from '@/lib/api/client';
import { permissions, useHasPermission } from '@/lib/api/identity';
import { statusLabels, useClaim, useDecisionLog } from '@/lib/api/decisions';

interface DecisionFooterProps {
  /**
   * What the drawer is about — a model, a configuration, a part family. Matched against the
   * subject of any persisted recommendation.
   */
  subjectRef: string;
}

/**
 * Routes a recommendation shown on an intelligence screen into the governed Decision Log
 * (V3-GOV-007).
 *
 * **When no record exists it says so.** The intelligence screens compute their advice live, while
 * the log holds recommendations a generation run has frozen. Offering "Accept & log" against
 * something that has never been persisted would either do nothing or fabricate a record in the
 * browser — which is exactly the prototype behaviour ADR-0006 rejects. So the footer looks for the
 * real record first and, finding none, explains why rather than presenting a dead control.
 *
 * **v3's "Assign owner" and "Watchlist" are deliberately absent.** They map to v3's `Assigned` and
 * `Snoozed` statuses, which have no counterpart in ADR-0006's lifecycle: ownership is the
 * recommendation's owner role plus whoever claims it, and there is no snooze state. A button that
 * looked like it changed something and did not would be worse than its absence.
 */
export function DecisionFooter({ subjectRef }: DecisionFooterProps) {
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const canReview = useHasPermission(permissions.recommendationReview);
  const claim = useClaim();
  const log = useDecisionLog({ q: subjectRef, pageSize: 5 });

  const record = log.data?.items[0] ?? null;

  if (log.isLoading) {
    return <p className="dl-row__hint">Checking the decision log…</p>;
  }

  if (!record) {
    return (
      <div className="decision-footer">
        <p className="dl-row__hint">
          No decision record exists for this yet. Records are created by a recommendation generation
          run, then reviewed here.
        </p>
        <Link to="/decisions" className="dl-btn">
          Open Decision Log
          <Icon name="arrow_forward" />
        </Link>
      </div>
    );
  }

  const canClaim = record.availableActions.includes('claim');

  const acceptAndLog = () => {
    setError(null);
    claim
      .mutateAsync(record.recommendationId)
      .then((response) => setMessage(response.message))
      .catch((e: unknown) => setError(apiErrorMessage(e)));
  };

  return (
    <div className="decision-footer">
      <p className="dl-row__hint">
        {record.ruleId} · {statusLabels[record.status]} · owner {record.ownerRole}
      </p>

      {canReview && canClaim ? (
        <button type="button" className="dl-btn dl-btn--primary" disabled={claim.isPending} onClick={acceptAndLog}>
          <Icon name="check_circle" />
          {claim.isPending ? 'Claiming…' : 'Accept & log'}
        </button>
      ) : null}

      <Link to="/decisions" className="dl-btn">
        Open in Decision Log
        <Icon name="arrow_forward" />
      </Link>

      {error ? (
        <p className="dl-row__error" role="alert">
          {error}
        </p>
      ) : null}
      {message ? (
        <p className="dl-row__ok" role="status">
          {message}
        </p>
      ) : null}
    </div>
  );
}
