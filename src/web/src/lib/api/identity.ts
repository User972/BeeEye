import { useQuery } from '@tanstack/react-query';
import { apiGet } from './client';

/** The caller's own identity, as `GET /api/v1/identity/me` reports it. */
export interface CurrentUser {
  isAuthenticated: boolean;
  /** Stable subject id — the actor recorded on every decision. Never a display name. */
  subjectId: string | null;
  displayName: string | null;
  roles: string[];
  permissions: string[];
}

/** The permissions this slice gates controls on. Mirrors `BeeEye.Shared.Security.Permissions`. */
export const permissions = {
  recommendationReview: 'recommendation.review',
  recommendationApprove: 'recommendation.approve',
  recommendationGenerate: 'recommendation.generate',
  decisionOutcomeRecord: 'decision-outcome.record',
} as const;

export const identityKeys = {
  me: ['identity', 'me'] as const,
};

/**
 * The signed-in user. The endpoint allows anonymous access and answers `isAuthenticated: false`
 * rather than 401, so a signed-out session renders its own state from a successful response
 * instead of a failure every screen would have to special-case.
 */
export function useCurrentUser() {
  return useQuery({
    queryKey: identityKeys.me,
    queryFn: ({ signal }) => apiGet<CurrentUser>('/api/v1/identity/me', signal),
    // Identity changes on sign-in, not while a screen is open.
    staleTime: 5 * 60_000,
  });
}

/**
 * Whether the caller holds a permission.
 *
 * This decides what to **render**. The server decides what to **accept**, and the two are not
 * the same thing — hiding a control is a courtesy, never the authorization. While identity is
 * still loading this answers false, so a control never flashes into view and then disappears.
 */
export function useHasPermission(permission: string): boolean {
  const { data } = useCurrentUser();
  return data?.permissions.includes(permission) ?? false;
}
