import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  ApiError,
  apiGet,
  apiPost,
  resetAuthSeam,
  setAccessTokenProvider,
  setAccessTokenRefresher,
} from './client';

// The transport seam is module-level state; reset it (and the fetch stub) around every case so no
// provider leaks between tests.
const fetchMock = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>();

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

function problem(status: number) {
  return new Response(JSON.stringify({ status, title: `Status ${status}` }), {
    status,
    headers: { 'content-type': 'application/problem+json' },
  });
}

/** Header records captured in send order, so the original attempt and any retry can be compared. */
const sentHeaders: Array<Record<string, string>> = [];

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
  fetchMock.mockReset();
  sentHeaders.length = 0;
  resetAuthSeam();
});

afterEach(() => {
  vi.unstubAllGlobals();
  resetAuthSeam();
});

function capture(response: (call: number) => Response): void {
  let call = 0;
  fetchMock.mockImplementation((_input, init?: RequestInit) => {
    call += 1;
    sentHeaders.push((init?.headers as Record<string, string>) ?? {});
    return Promise.resolve(response(call));
  });
}

describe('client transport seam — bearer attachment', () => {
  it('attaches the bearer when a token provider yields one', async () => {
    setAccessTokenProvider(() => Promise.resolve('tok-abc'));
    capture(() => json({ ok: true }));

    await apiGet('/api/v1/identity/me');

    expect(sentHeaders[0]?.['Authorization']).toBe('Bearer tok-abc');
  });

  it('attaches no Authorization header in local mode (the default, no provider)', async () => {
    // No provider installed — mirrors local/LocalDev, byte-for-byte the behaviour before sign-in.
    capture(() => json({ ok: true }));

    await apiGet('/api/v1/identity/me');

    expect(sentHeaders[0]).not.toHaveProperty('Authorization');
  });

  it('forwards the AbortSignal so a cancelled request still cancels', async () => {
    const controller = new AbortController();
    controller.abort();
    const refresher = vi.fn(() => Promise.resolve('tok'));
    setAccessTokenRefresher(refresher);
    fetchMock.mockImplementation((_input, init?: RequestInit) => {
      if (init?.signal?.aborted) {
        return Promise.reject(new DOMException('Aborted', 'AbortError'));
      }
      return Promise.resolve(json({ ok: true }));
    });

    await expect(apiGet('/api/v1/forecasting', controller.signal)).rejects.toThrow();
    // An aborted request never provokes a token refresh.
    expect(refresher).not.toHaveBeenCalled();
  });
});

describe('client transport seam — 401 recovery', () => {
  it('refreshes once and replays the request, and it succeeds', async () => {
    setAccessTokenProvider(() => Promise.resolve('stale'));
    const refresher = vi.fn(() => Promise.resolve('fresh'));
    setAccessTokenRefresher(refresher);
    capture((call) => (call === 1 ? problem(401) : json({ ok: true })));

    const result = await apiGet<{ ok: boolean }>('/api/v1/forecasting');

    expect(result).toEqual({ ok: true });
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(refresher).toHaveBeenCalledTimes(1);
    expect(sentHeaders[0]?.['Authorization']).toBe('Bearer stale');
    expect(sentHeaders[1]?.['Authorization']).toBe('Bearer fresh');
  });

  it('surfaces a typed ApiError(401) and does not loop when the refresh cannot help', async () => {
    setAccessTokenProvider(() => Promise.resolve('stale'));
    const refresher = vi.fn(() => Promise.resolve(null)); // interaction required → cannot refresh
    setAccessTokenRefresher(refresher);
    capture(() => problem(401));

    await expect(apiGet('/api/v1/forecasting')).rejects.toMatchObject({
      name: 'ApiError',
      status: 401,
    });
    // Exactly one refresh and one replay — never a loop.
    expect(refresher).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('never turns a 403 into a sign-in prompt: no refresh, no retry', async () => {
    setAccessTokenProvider(() => Promise.resolve('tok'));
    const refresher = vi.fn(() => Promise.resolve('fresh'));
    setAccessTokenRefresher(refresher);
    capture(() => problem(403));

    await expect(apiGet('/api/v1/decisions')).rejects.toMatchObject({ status: 403 });
    expect(refresher).not.toHaveBeenCalled();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});

describe('client transport seam — idempotency key across the 401 replay (the subtle one)', () => {
  it('reuses the caller-supplied Idempotency-Key on both the original POST and the retry', async () => {
    setAccessTokenProvider(() => Promise.resolve('stale'));
    setAccessTokenRefresher(() => Promise.resolve('fresh'));
    capture((call) => (call === 1 ? problem(401) : json({ ok: true })));

    await apiPost('/api/v1/decisions/d1/accept', { note: null }, { idempotencyKey: 'intent-42' });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    // The same intent, so the same key — minting a fresh one on the retry would double-book the
    // decision. This is the single subtlest edge case in the whole slice.
    expect(sentHeaders[0]?.['Idempotency-Key']).toBe('intent-42');
    expect(sentHeaders[1]?.['Idempotency-Key']).toBe('intent-42');
  });

  it('reuses the *generated* Idempotency-Key across the replay when none is supplied', async () => {
    setAccessTokenProvider(() => Promise.resolve('stale'));
    setAccessTokenRefresher(() => Promise.resolve('fresh'));
    capture((call) => (call === 1 ? problem(401) : json({ ok: true })));

    await apiPost('/api/v1/decisions/d1/accept', { note: null });

    const first = sentHeaders[0]?.['Idempotency-Key'];
    expect(first).toBeTruthy();
    // Resolved once, before the first attempt, so the replay carries the identical key.
    expect(sentHeaders[1]?.['Idempotency-Key']).toBe(first);
  });

  it('sends an Idempotency-Key on a POST but never on a GET', async () => {
    capture(() => json({ ok: true }));
    await apiPost('/api/v1/decisions/d1/accept', { note: null }, { idempotencyKey: 'k' });
    await apiGet('/api/v1/decisions');

    expect(sentHeaders[0]).toHaveProperty('Idempotency-Key', 'k');
    expect(sentHeaders[1]).not.toHaveProperty('Idempotency-Key');
  });
});

describe('ApiError shape', () => {
  it('carries the status and problem detail', async () => {
    capture(() => problem(409));
    const error = await apiPost('/api/v1/decisions/d1/accept', {}, { idempotencyKey: 'k' }).catch(
      (e: unknown) => e,
    );
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(409);
  });
});
