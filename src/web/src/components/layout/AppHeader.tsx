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

export function AppHeader() {
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
      <strong>BeeEye Platform</strong>
      <span className="badge">Read-only analytics</span>
      <div className="app-header__spacer" />
      <button className="icon-btn" type="button" aria-label="Toggle colour theme" onClick={toggleTheme}>
        <Icon name="contrast" />
      </button>
    </header>
  );
}
