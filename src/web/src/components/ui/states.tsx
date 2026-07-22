import { Icon } from './Icon';

interface LoadingStateProps {
  label?: string;
}

export function LoadingState({ label = 'Loading…' }: LoadingStateProps) {
  return (
    <div className="state" role="status" aria-live="polite">
      <div className="skeleton" style={{ width: 180, height: 12 }} />
      <div className="skeleton" style={{ width: 120, height: 12 }} />
      <span className="sr-only">{label}</span>
    </div>
  );
}

interface EmptyStateProps {
  title: string;
  message?: string;
  icon?: string;
}

export function EmptyState({ title, message, icon = 'inbox' }: EmptyStateProps) {
  return (
    <div className="state">
      <Icon name={icon} className="state__icon" />
      <strong>{title}</strong>
      {message ? <span>{message}</span> : null}
    </div>
  );
}

interface ErrorStateProps {
  title?: string;
  message?: string;
  onRetry?: () => void;
}

export function ErrorState({ title = 'Something went wrong', message, onRetry }: ErrorStateProps) {
  return (
    <div className="state state--error" role="alert">
      <Icon name="error" className="state__icon" />
      <strong>{title}</strong>
      {message ? <span>{message}</span> : null}
      {onRetry ? (
        <button className="icon-btn" style={{ width: 'auto', padding: '6px 14px' }} onClick={onRetry}>
          Retry
        </button>
      ) : null}
    </div>
  );
}
