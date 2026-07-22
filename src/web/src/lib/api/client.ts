import type { ProblemDetails } from './types';

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '';

/** Error carrying the HTTP status and any Problem Details returned by the API. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails | null,
  ) {
    super(problem?.title ?? `Request failed with status ${status}`);
    this.name = 'ApiError';
  }
}

type QueryValue = string | number | boolean | null | undefined | ReadonlyArray<string>;

/** Builds a query string, repeating keys for array values and dropping empties. */
export function buildQuery(params: Record<string, QueryValue>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === null || value === undefined || value === '') {
      continue;
    }
    if (Array.isArray(value)) {
      for (const v of value) {
        search.append(key, v);
      }
    } else {
      search.append(key, String(value));
    }
  }
  const qs = search.toString();
  return qs ? `?${qs}` : '';
}

/** Typed GET against the platform API. Supports cancellation via an AbortSignal. */
export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'GET',
    headers: { Accept: 'application/json' },
    signal: signal ?? null,
  });

  if (!response.ok) {
    let problem: ProblemDetails | null = null;
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      // Non-JSON error body — leave problem null.
    }
    throw new ApiError(response.status, problem);
  }

  return (await response.json()) as T;
}
