import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from '@tanstack/react-router';
import { router } from './router';
import '@/styles/global.css';
import '@/styles/components.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      staleTime: 30_000,
    },
  },
});

// Reveal icons only once the icon font is actually usable — otherwise every icon renders
// as its raw ligature name. See the .icons-ready rule in components.css.
if ('fonts' in document) {
  document.fonts
    .load('24px "Material Symbols Outlined"')
    .then((loaded) => {
      if (loaded.length > 0) {
        document.documentElement.classList.add('icons-ready');
      }
    })
    .catch(() => {
      /* offline: icons stay hidden, text labels carry the meaning */
    });
}

const rootElement = document.getElementById('root');
if (!rootElement) {
  throw new Error('Root element #root not found.');
}

createRoot(rootElement).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
);
