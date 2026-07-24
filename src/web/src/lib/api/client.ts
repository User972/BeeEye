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

// ---------------------------------------------------------------------------
// Auth seam (ADR 0008 §2, S4b)
//
// The transport layer stays free of React and MSAL: it never imports either. The auth provider
// installs a token provider (and a post-401 refresher) at start-up, and defaults keep the app
// anonymous — which is exactly the `local` mode / LocalDev posture, byte-for-byte the behaviour
// before sign-in existed.
// ---------------------------------------------------------------------------

/** Returns the current access token, or `null` in local/anonymous mode. */
export type TokenProvider = () => Promise<string | null>;

/**
 * Obtains a fresh token after a 401 (MSAL `acquireTokenSilent`, falling back to interactive on
 * `InteractionRequiredAuthError`). Returns `null` when it cannot — the caller then surfaces the 401
 * rather than looping.
 */
export type TokenRefresher = () => Promise<string | null>;

let getAccessToken: TokenProvider = () => Promise.resolve(null);
let refreshAccessToken: TokenRefresher = () => Promise.resolve(null);

/** Installs the token source every request attaches as a bearer. */
export function setAccessTokenProvider(provider: TokenProvider): void {
  getAccessToken = provider;
}

/** Installs the recovery used once on a 401 before the request is replayed. */
export function setAccessTokenRefresher(refresher: TokenRefresher): void {
  refreshAccessToken = refresher;
}

/** Restores the anonymous defaults. Used at sign-out, and by tests between cases. */
export function resetAuthSeam(): void {
  getAccessToken = () => Promise.resolve(null);
  refreshAccessToken = () => Promise.resolve(null);
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

type Method = 'GET' | 'POST';

interface RequestSpec {
  method: Method;
  /** `undefined` sends no body. */
  body: unknown;
  /** POST only. Resolved once and reused across the auth retry, never regenerated. */
  idempotencyKey: string | undefined;
  signal: AbortSignal | undefined;
}

/**
 * The single request runner beneath `apiGet`/`apiPost`.
 *
 * It attaches the current bearer (or none, in local mode) and recovers from **exactly one** 401 by
 * refreshing the token and replaying the *identical* request — same method, same body, and the same
 * `Idempotency-Key` for a POST. Reusing the key is the whole point: minting a fresh one on the retry
 * would let the server record a second, unrelated decision worth millions of SAR. A 403 is
 * authenticated-but-forbidden and is deliberately left untouched (never refreshed, never a sign-in
 * prompt). This auth retry lives *below* TanStack Query and is distinct from Query's transport retry.
 */
async function request<T>(path: string, spec: RequestSpec): Promise<T> {
  // Resolved ONCE here, not inside the header builder, so the original attempt and the auth replay
  // carry the very same key. See ADR-0007 §2.1 and the S4b progress note.
  const idempotencyKey =
    spec.method === 'POST' ? (spec.idempotencyKey ?? newIdempotencyKey()) : undefined;
  // `null`, not `undefined`: several endpoints legitimately take no body, and
  // `exactOptionalPropertyTypes` rejects an explicitly-undefined RequestInit member.
  const bodyInit = spec.body === undefined ? null : JSON.stringify(spec.body);

  const send = (token: string | null): Promise<Response> => {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (spec.method === 'POST') {
      headers['Content-Type'] = 'application/json';
      // Every state-changing endpoint requires an Idempotency-Key, so one is always sent.
      headers['Idempotency-Key'] = idempotencyKey as string;
    }
    if (token !== null) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    return fetch(`${BASE_URL}${path}`, {
      method: spec.method,
      headers,
      body: spec.method === 'POST' ? bodyInit : null,
      signal: spec.signal ?? null,
    });
  };

  let response = await send(await getAccessToken());

  // Recover from a single 401 only. A caller-triggered abort short-circuits the retry so a cancelled
  // request never provokes a token refresh.
  if (response.status === 401 && spec.signal?.aborted !== true) {
    const refreshed = await refreshAccessToken();
    response = await send(refreshed);
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }

  // 204 and an empty 200 both mean "no payload"; parsing either would throw.
  if (response.status === 204 || response.headers.get('content-length') === '0') {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** Typed GET against the platform API. Supports cancellation via an AbortSignal. */
export function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  return request<T>(path, { method: 'GET', body: undefined, idempotencyKey: undefined, signal });
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
 * `decisions.ts` do exactly that, and the auth-layer 401 replay preserves it too.
 */
export function apiPost<T>(path: string, body?: unknown, options: PostOptions = {}): Promise<T> {
  return request<T>(path, {
    method: 'POST',
    body,
    idempotencyKey: options.idempotencyKey,
    signal: options.signal,
  });
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
