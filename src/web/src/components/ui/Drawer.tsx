import { useEffect, useRef } from 'react';
import type { ReactNode } from 'react';
import { Icon } from './Icon';

interface DrawerProps {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
  /** Rendered below the body, pinned — used for the workflow actions on an explainability drawer. */
  footer?: ReactNode;
}

/** Elements that can hold focus inside the panel, in document order. */
const FOCUSABLE =
  'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * Right-side detail panel with an overlay. Closes on overlay click or Escape.
 *
 * Focus is **trapped** while open and **restored** to the invoking control on close. Neither v3 nor
 * the earlier version of this component did either, which left keyboard and screen-reader users
 * tabbing into the page behind an open modal and then stranded at the top of the document when it
 * closed (V3-DS-007).
 */
export function Drawer({ open, title, onClose, children, footer }: DrawerProps) {
  const panelRef = useRef<HTMLElement | null>(null);
  const returnFocusTo = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;

    // Captured before focus moves into the panel, so it is the control the user actually left.
    returnFocusTo.current = document.activeElement as HTMLElement | null;

    const panel = panelRef.current;
    const first = panel?.querySelector<HTMLElement>(FOCUSABLE);
    (first ?? panel)?.focus();

    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
        return;
      }

      if (e.key !== 'Tab' || !panel) return;

      const focusable = [...panel.querySelectorAll<HTMLElement>(FOCUSABLE)].filter(
        (el) => el.offsetParent !== null || el === document.activeElement,
      );
      if (focusable.length === 0) {
        // Nothing to move to; keep focus on the panel rather than letting it escape behind the overlay.
        e.preventDefault();
        panel.focus();
        return;
      }

      const firstEl = focusable[0]!;
      const lastEl = focusable[focusable.length - 1]!;

      if (e.shiftKey && document.activeElement === firstEl) {
        e.preventDefault();
        lastEl.focus();
      } else if (!e.shiftKey && document.activeElement === lastEl) {
        e.preventDefault();
        firstEl.focus();
      }
    };

    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('keydown', onKey);
      // Restored on close, so the user resumes where they were rather than at the top of the page.
      returnFocusTo.current?.focus?.();
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="drawer-overlay" onClick={onClose}>
      <aside
        ref={panelRef}
        tabIndex={-1}
        className="drawer"
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="drawer__header">
          <strong>{title}</strong>
          <button className="icon-btn" type="button" aria-label="Close panel" onClick={onClose}>
            <Icon name="close" />
          </button>
        </div>
        <div className="drawer__body">{children}</div>
        {footer ? <div className="drawer__footer">{footer}</div> : null}
      </aside>
    </div>
  );
}
