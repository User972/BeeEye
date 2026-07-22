import { Link } from '@tanstack/react-router';
import { navItems, navSections } from '@/config/navigation';
import { Icon } from '@/components/ui/Icon';

export function NavRail() {
  return (
    <nav className="nav-rail" aria-label="Primary navigation">
      <div className="nav-brand">
        <div className="nav-brand__mark" aria-hidden="true">B</div>
        <div>
          <div className="nav-brand__name">BeeEye</div>
          <div className="nav-brand__sub">Decision Intelligence</div>
        </div>
      </div>

      {navSections.map((section) => {
        const items = navItems.filter((item) => item.section === section.id);
        if (items.length === 0) return null;
        return (
          <div key={section.id}>
            <div className="nav-section__label">{section.label}</div>
            {items.map((item) => (
              <Link
                key={item.id}
                to={item.path}
                className="nav-item"
                activeProps={{ className: 'nav-item nav-item--active' }}
                activeOptions={{ exact: item.path === '/' }}
              >
                <Icon name={item.icon} />
                <span>{item.label}</span>
                {item.useCase ? <span className="nav-item__uc">{item.useCase}</span> : null}
              </Link>
            ))}
          </div>
        );
      })}
    </nav>
  );
}
