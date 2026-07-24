import '@testing-library/jest-dom/vitest';

// jsdom ships window.scrollTo as a stub that logs a "Not implemented" trace on
// every call. TanStack Router's scroll restoration calls it on each navigation,
// which floods the test output and hides real failures. Replace it outright —
// a conditional guard would not fire, because the throwing stub is itself a
// function.
if (typeof window !== 'undefined') {
  window.scrollTo = () => undefined;
}
