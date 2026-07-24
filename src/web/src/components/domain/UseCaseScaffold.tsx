import { navItemById } from '@/config/navigation';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card, CardHeader } from '@/components/ui/Card';
import { Icon } from '@/components/ui/Icon';
import { EmptyState } from '@/components/ui/states';
import { ModuleStatusPanel } from './ModuleStatusPanel';

/**
 * Shared page body for a use-case screen. Renders the standard header + metadata
 * strip, the planned capabilities (traceable to the use-case spec), and the live
 * module-status panel. Replaced module-by-module as real analytics are wired.
 */
export function UseCaseScaffold({ navId }: { navId: string }) {
  const item = navItemById(navId);
  if (!item) {
    return <EmptyState title="Unknown screen" message={`No navigation entry for "${navId}".`} />;
  }

  return (
    <>
      <PageHeader
        title={item.label}
        summary={item.summary}
        {...(item.useCase ? { useCase: item.useCase } : {})}
        wireframed={item.wireframed}
        meta={[
          { label: 'Data source', value: 'Sample dataset (not live Oracle Fusion)' },
          { label: 'Analysis date', value: '30 Jun 2026 (configurable)' },
          { label: 'Implementation', value: 'Scaffolded' },
        ]}
      />

      <div className="grid grid--cards">
        <Card>
          <CardHeader
            title="Planned capabilities"
            subtitle={item.useCase ? `Specified in docs/product/use-cases/` : 'Platform capability'}
          />
          <ul className="capability-list">
            {item.capabilities.map((capability) => (
              <li key={capability}>
                <Icon name="task_alt" />
                <span>{capability}</span>
              </li>
            ))}
          </ul>
        </Card>

        {item.moduleRoute ? <ModuleStatusPanel routePrefix={item.moduleRoute} /> : null}
      </div>
    </>
  );
}
