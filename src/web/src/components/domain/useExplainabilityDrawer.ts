import { useState } from 'react';
import type { ExplainSubject } from '@/lib/api/explainability';

/**
 * Holds the subject the drawer is open for.
 *
 * A hook rather than nine copies of the same `useState`, so every screen opens the drawer the same
 * way and closing it always clears the subject (which is what cancels the query).
 */
export function useExplainabilityDrawer() {
  const [subject, setSubject] = useState<ExplainSubject | null>(null);

  return {
    subject,
    open: (next: ExplainSubject) => setSubject(next),
    close: () => setSubject(null),
  };
}
