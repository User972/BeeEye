import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { Icon } from './Icon';

interface DrawerProps {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
  /** Rendered below the body, pinned — used for the workflow actions on an explainability drawer. */
  footer?: ReactNode;
  /** Extra class on the panel, e.g. `drawer--explain` for v3's 474px explainability geometry. */
  className?: string;
  /**
   * Replaces the default header, which is a plain bold title. The close button is still rendered by
   * the primitive, so a custom header never has to remember one.
   */
  header?: ReactNode;
}

/** Elements that can hold focus inside the panel, in document order. */
const FOCUSABLE =
  'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * Every drawer currently open, oldest first.
 *
 * **Why a module-level stack.** Each drawer attaches its own `window` keydown listener, so before
 * this existed two open drawers both acted on one Escape: opening the explainability drawer over the
 * Decision Log's detail drawer and pressing Escape closed *both*, losing the context the user was
 * reading. The same defect applied to Tab — two live focus traps fight over the same keypress, and
 * which one wins depends on listener registration order.
 *
 * A stack fixes both at once: only the entry on top acts, on Escape and on Tab. This is a real defect
 * found by stacking them, and there is a regression test for it.
 */
const drawerStack: symbol[] = [];

export function Drawer({ open, title, onClose, children, footer, className, header }: DrawerProps) {
  const panelRef = useRef<HTMLElement | null>(null);
  const returnFocusTo = useRef<HTMLElement | null>(null);

  // Identity per mounted drawer, so the stack can be popped precisely rather than by position.
  const idRef = useRef<symbol | null>(null);
  idRef.current ??= Symbol('drawer');

  // Drives the overlay's stacking order, so a drawer opened second paints above one opened first
  // regardless of where the two sit in the React tree.
  const [depth, setDepth] = useState(0);

  // `onClose` is almost always an inline arrow, so its identity changes on every parent render. Held
  // in a ref and kept out of the effect's dependencies: with it in the array, the effect tore down
  // and rebuilt on every render — and its teardown *restores focus*, so a re-render while the drawer
  // was open yanked focus back to the invoking control and then back into the panel. Invisible with
  // one drawer, and the cause of focus landing in the wrong panel with two. Found by the stacked
  // test, not by reading the code.
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;

  useEffect(() => {
    if (!open) return;

    const id = idRef.current!;
    drawerStack.push(id);
    setDepth(drawerStack.length - 1);

    const isTopmost = () => drawerStack[drawerStack.length - 1] === id;

    // Captured before focus moves into the panel, so it is the control the user actually left —
    // which, for a stacked drawer, is a control inside the drawer beneath.
    returnFocusTo.current = document.activeElement as HTMLElement | null;

    const panel = panelRef.current;
    const first = panel?.querySelector<HTMLElement>(FOCUSABLE);
    (first ?? panel)?.focus();

    const onKey = (e: KeyboardEvent) => {
      // Only the topmost drawer responds. The one beneath must not close and must not steal Tab.
      if (!isTopmost()) return;

      if (e.key === 'Escape') {
        onCloseRef.current();
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

      const index = drawerStack.lastIndexOf(id);
      if (index !== -1) drawerStack.splice(index, 1);

      // Restored on close, so the user resumes where they were rather than at the top of the page —
      // and, when a drawer was stacked over another, back inside the one beneath.
      returnFocusTo.current?.focus?.();
    };
  }, [open]);

  if (!open) return null;

  return (
    <div className="drawer-overlay" style={{ zIndex: 50 + depth * 2 }} onClick={onClose}>
      <aside
        ref={panelRef}
        tabIndex={-1}
        className={className ? `drawer ${className}` : 'drawer'}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="drawer__header">
          {header ?? <strong>{title}</strong>}
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
