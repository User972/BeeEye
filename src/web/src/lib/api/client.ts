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
        search.append(key, String(v));
      }
    } else {
      search.append(key, String(value));
    }
  }
  const qs = search.toString();
  return qs ? `?${qs}` : '';
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    // Non-JSON error body — the caller falls back to a status-based message.
    return null;
  }
}

/** Typed GET against the platform API. Supports cancellation via an AbortSignal. */
export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'GET',
    headers: { Accept: 'application/json' },
    signal: signal ?? null,
  });

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }

  return (await response.json()) as T;
}

/**
 * A key identifying one *user intent*, sent as `Idempotency-Key` (ADR-0007 §2.1).
 *
 * Generated once per intent and reused across every retry of it. A fresh key per retry
 * would defeat the entire mechanism: the server would see two unrelated requests and
 * could record the same decision twice.
 */
export function newIdempotencyKey(): string {
  // `randomUUID` needs a secure context; the fallback keeps the app usable over plain
  // HTTP in development rather than failing the mutation outright.
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `beeeye-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;
}

interface PostOptions {
  /** Reused across retries of the same intent. Generated per intent if omitted. */
  idempotencyKey?: string;
  signal?: AbortSignal;
}

/**
 * Typed POST against the platform API.
 *
 * Every state-changing endpoint requires an `Idempotency-Key`, so one is always sent.
 * Pass an explicit key when a single user intent may be retried — the mutation hooks in
 * `decisions.ts` do exactly that.
 */
export async function apiPost<T>(path: string, body?: unknown, options: PostOptions = {}): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      'Idempotency-Key': options.idempotencyKey ?? newIdempotencyKey(),
    },
    // `null`, not `undefined`: several of these endpoints legitimately take no body, and
    // `exactOptionalPropertyTypes` rejects an explicitly-undefined RequestInit member.
    body: body === undefined ? null : JSON.stringify(body),
    signal: options.signal ?? null,
  });

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }

  // 204 and an empty 200 both mean "no payload"; parsing either would throw.
  if (response.status === 204 || response.headers.get('content-length') === '0') {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/**
 * A message a user can act on. Only an API-shaped failure carries anything worth showing;
 * anything else (a DNS failure, a dropped connection) surfaces as an opaque browser string
 * like "Failed to fetch", so it is replaced with the actual next step.
 */
export function apiErrorMessage(error: unknown, fallback?: string): string {
  if (error instanceof ApiError) {
    return (
      error.problem?.detail ??
      error.problem?.title ??
      `The BeeEye API responded with status ${error.status}. Try again in a moment.`
    );
  }
  return fallback ?? 'The BeeEye API did not respond. Start the API host (dotnet run) and try again.';
}
