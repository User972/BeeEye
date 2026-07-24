import { useQuery } from '@tanstack/react-query';
import { apiGet } from './client';
import type { ModuleInfo } from './types';

export const queryKeys = {
  modules: ['platform', 'modules'] as const,
  module: (routePrefix: string) => ['module', routePrefix] as const,
};

/** Lists every bounded-context module mounted by the API host. */
export function usePlatformModules() {
  return useQuery({
    queryKey: queryKeys.modules,
    queryFn: ({ signal }) => apiGet<ModuleInfo[]>('/api/v1/platform/modules', signal),
    staleTime: 60_000,
  });
}

/** Fetches a single module's discovery payload (proves live API integration per screen). */
export function useModuleInfo(routePrefix: string) {
  return useQuery({
    queryKey: queryKeys.module(routePrefix),
    queryFn: ({ signal }) => apiGet<ModuleInfo>(`/api/v1/${routePrefix}`, signal),
    staleTime: 60_000,
  });
}
