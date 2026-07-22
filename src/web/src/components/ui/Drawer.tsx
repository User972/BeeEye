import { useEffect } from 'react';
import type { ReactNode } from 'react';
import { Icon } from './Icon';

interface DrawerProps {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
}

/** Right-side detail panel with an overlay. Closes on overlay click or Escape. */
export function Drawer({ open, title, onClose, children }: DrawerProps) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="drawer-overlay" onClick={onClose}>
      <aside className="drawer" role="dialog" aria-modal="true" aria-label={title} onClick={(e) => e.stopPropagation()}>
        <div className="drawer__header">
          <strong>{title}</strong>
          <button className="icon-btn" type="button" aria-label="Close panel" onClick={onClose}>
            <Icon name="close" />
          </button>
        </div>
        <div className="drawer__body">{children}</div>
      </aside>
    </div>
  );
}
