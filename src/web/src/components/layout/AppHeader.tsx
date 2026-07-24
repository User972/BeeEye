import { useEffect, useState } from 'react';
import { Icon } from '@/components/ui/Icon';
import { useAppAuth } from '@/lib/auth/context';
import { useCurrentUser } from '@/lib/api/identity';

type Theme = 'light' | 'dark';

function readStoredTheme(): Theme | null {
  try {
    const stored = localStorage.getItem('beeeye-theme');
    return stored === 'dark' || stored === 'light' ? stored : null;
  } catch {
    return null;
  }
}

/**
 * The account chip / sign-in control (A.6).
 *
 * Rendered only in `entra` mode — `local` mode has no sign-in, so the header stays exactly as it was
 * before this slice (A.7.14). The signed-in user's name comes from `/identity/me`, never from the
 * token. Every control carries a text label, so meaning never rests on colour or an icon alone.
 */
function AccountControls() {
  const auth = useAppAuth();
  const identity = useCurrentUser();

  if (auth.mode !== 'entra') {
    return null;
  }

  if (identity.isLoading) {
    return (
      <span className="app-account__status" role="status" aria-live="polite">
        Checking access…
      </span>
    );
  }

  if (identity.data?.isAuthenticated) {
    const name = identity.data.displayName ?? 'Signed in';
    return (
      <div className="app-account">
        <span className="app-account__chip">
          <Icon name="account_circle" />
          <span className="app-account__name">{name}</span>
        </span>
        <button
          type="button"
          className="app-account__btn"
          onClick={() => auth.switchAccount()}
        >
          Switch account
        </button>
        <button type="button" className="app-account__btn" onClick={() => auth.signOut()}>
          Sign out
        </button>
      </div>
    );
  }

  return (
    <button type="button" className="app-account__signin" onClick={() => auth.signIn()}>
      <Icon name="login" />
      Sign in
    </button>
  );
}

interface AppHeaderProps {
  /** Mobile only: current expanded state of the navigation rail. */
  navOpen: boolean;
  onToggleNav: () => void;
}

export function AppHeader({ navOpen, onToggleNav }: AppHeaderProps) {
  const [theme, setTheme] = useState<Theme | null>(readStoredTheme);

  useEffect(() => {
    if (theme === null) {
      delete document.documentElement.dataset['theme'];
      return;
    }
    document.documentElement.dataset['theme'] = theme;
    try {
      localStorage.setItem('beeeye-theme', theme);
    } catch {
      // Storage unavailable — theme still applies for the session.
    }
  }, [theme]);

  const toggleTheme = () => setTheme((current) => (current === 'dark' ? 'light' : 'dark'));

  return (
    <header className="app-header">
      <button
        className="icon-btn nav-toggle"
        type="button"
        aria-label={navOpen ? 'Close navigation menu' : 'Open navigation menu'}
        aria-expanded={navOpen}
        aria-controls="nav-rail"
        onClick={onToggleNav}
      >
        <Icon name={navOpen ? 'close' : 'menu'} />
      </button>
      <strong>BeeEye Platform</strong>
      <span className="badge">Read-only analytics</span>
      <div className="app-header__spacer" />
      <AccountControls />
      <button className="icon-btn" type="button" aria-label="Toggle colour theme" onClick={toggleTheme}>
        <Icon name="contrast" />
      </button>
    </header>
  );
}
