import type { ReactNode } from 'react';
import { useRouterState } from '@tanstack/react-router';
import { useAppAuth } from '@/lib/auth/context';
import { useCurrentUser } from '@/lib/api/identity';
import { apiErrorMessage } from '@/lib/api/client';
import { LoadingState, ErrorState } from '@/components/ui/states';
import { Icon } from '@/components/ui/Icon';

function SignInRequired({ onSignIn }: { onSignIn: () => void }) {
  return (
    <div className="state signin">
      <Icon name="lock" className="state__icon" />
      <strong>Sign in to continue</strong>
      <span>
        You are not signed in, or your session has ended. BeeEye needs to know who you are before it
        shows analytics or records a decision.
      </span>
      <button type="button" className="signin__btn" onClick={onSignIn}>
        <Icon name="login" />
        Sign in
      </button>
    </div>
  );
}

/**
 * The route gate (A.6).
 *
 * In `local` mode there is **no** gate — the app renders anonymously exactly as it did before sign-in
 * existed. In `entra` mode an anonymous user is shown a sign-in screen that preserves the deep-link
 * `returnTo`, and the shell is held on a `LoadingState` while identity resolves so no control flashes
 * in and then disappears. Whether the app renders is decided by the *server's* `/identity/me`, never
 * by decoding the token in the browser.
 */
export function AuthGate({ children }: { children: ReactNode }) {
  const auth = useAppAuth();
  const href = useRouterState({ select: (state) => state.location.href });
  const identity = useCurrentUser();

  if (auth.mode === 'local') {
    return <>{children}</>;
  }

  if (identity.isLoading) {
    return <LoadingState label="Checking your access…" />;
  }

  // A transport failure is not being signed out (A.7.11): offer a retry, never a sign-in loop.
  if (identity.isError) {
    return (
      <ErrorState
        title="Could not reach BeeEye"
        message={apiErrorMessage(identity.error)}
        onRetry={() => void identity.refetch()}
      />
    );
  }

  if (identity.data?.isAuthenticated) {
    return <>{children}</>;
  }

  return <SignInRequired onSignIn={() => auth.signIn(href)} />;
}
