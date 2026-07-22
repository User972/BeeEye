import type { ReactNode } from 'react';

interface CardProps {
  children: ReactNode;
  className?: string;
}

export function Card({ children, className }: CardProps) {
  return <div className={className ? `card ${className}` : 'card'}>{children}</div>;
}

interface CardHeaderProps {
  title: string;
  subtitle?: string;
  action?: ReactNode;
}

export function CardHeader({ title, subtitle, action }: CardHeaderProps) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 14 }}>
      <div>
        <div className="card__title">{title}</div>
        {subtitle ? <div className="card__subtitle">{subtitle}</div> : null}
      </div>
      {action}
    </div>
  );
}
