import { Link } from '@tanstack/react-router';
import { navGroups, navItemsInGroup } from '@/config/navigation';
import { Icon } from '@/components/ui/Icon';

interface NavRailProps {
  /** Mobile only: whether the collapsible rail is expanded. Ignored at desktop widths. */
  open: boolean;
  /** Called when a nav link is followed, so the mobile rail can close itself. */
  onNavigate: () => void;
}

/**
 * Primary navigation. Groups and their delivery-phase labels mirror the v3
 * designs (`docs/implementation/v3-design-inventory.md` §1).
 *
 * A group renders only when it currently contains at least one built screen, so
 * the rail never shows an empty heading or a link that goes nowhere.
 */
export function NavRail({ open, onNavigate }: NavRailProps) {
  return (
    <nav
      id="nav-rail"
      className={`nav-rail${open ? ' nav-rail--open' : ''}`}
      aria-label="Primary navigation"
    >
      <div className="nav-brand">
        <div className="nav-brand__mark" aria-hidden="true">
          B
        </div>
        <div>
          <div className="nav-brand__name">BeeEye</div>
          <div className="nav-brand__sub">Decision Intelligence</div>
        </div>
      </div>

      {navGroups.map((group) => {
        const items = navItemsInGroup(group.id);
        if (items.length === 0) return null;
        const headingId = `nav-group-${group.id}`;
        return (
          <div key={group.id} role="group" aria-labelledby={headingId}>
            <h2 className="nav-section__label" id={headingId}>
              {group.label}
              {group.phase ? <span className="nav-section__phase">{group.phase}</span> : null}
            </h2>
            {items.map((item) => (
              <Link
                key={item.id}
                to={item.path}
                className="nav-item"
                activeProps={{ className: 'nav-item nav-item--active', 'aria-current': 'page' }}
                activeOptions={{ exact: item.path === '/' }}
                onClick={onNavigate}
              >
                <Icon name={item.icon} />
                <span className="nav-item__label">{item.label}</span>
                {item.useCase ? <span className="nav-item__uc">{item.useCase}</span> : null}
              </Link>
            ))}
          </div>
        );
      })}
    </nav>
  );
}
