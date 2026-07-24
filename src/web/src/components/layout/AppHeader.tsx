import { useEffect, useState } from 'react';
import { Icon } from '@/components/ui/Icon';

type Theme = 'light' | 'dark';

function readStoredTheme(): Theme | null {
  try {
    const stored = localStorage.getItem('beeeye-theme');
    return stored === 'dark' || stored === 'light' ? stored : null;
  } catch {
    return null;
  }
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
      <button className="icon-btn" type="button" aria-label="Toggle colour theme" onClick={toggleTheme}>
        <Icon name="contrast" />
      </button>
    </header>
  );
}
