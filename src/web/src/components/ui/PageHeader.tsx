import { Icon } from './Icon';

export interface MetaItem {
  label: string;
  value: string;
}

interface PageHeaderProps {
  title: string;
  summary?: string;
  useCase?: string;
  wireframed?: boolean;
  meta?: MetaItem[];
}

/**
 * Standard analytical page header with a metadata strip (data freshness, source
 * period, model version, calculation timestamp) as mandated by the UX spec.
 */
export function PageHeader({ title, summary, useCase, wireframed, meta }: PageHeaderProps) {
  return (
    <header className="page-header">
      <div className="page-header__top">
        <h1 className="page-header__title">{title}</h1>
        {useCase ? <span className="badge">{useCase}</span> : null}
        {wireframed ? (
          <span className="badge risk-low">
            <Icon name="check_circle" />
            Wireframed
          </span>
        ) : (
          <span className="badge">
            <Icon name="draw" />
            Designed from shared language
          </span>
        )}
      </div>
      {summary ? <p className="page-header__summary">{summary}</p> : null}
      {meta && meta.length > 0 ? (
        <div className="page-header__meta">
          {meta.map((m) => (
            <span key={m.label}>
              <b>{m.label}:</b> {m.value}
            </span>
          ))}
        </div>
      ) : null}
    </header>
  );
}
