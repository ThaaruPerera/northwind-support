import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearSession, getSession, saveSession } from '../auth/session';
import { ApiError, request, setUnauthorizedHandler } from './client';

const fetchMock = vi.fn<typeof fetch>();

function reply(status: number, body?: unknown): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), { status });
}

/** The headers of the one request that was made. */
function sentHeaders(): Record<string, string> {
  const init = fetchMock.mock.lastCall?.[1];

  return (init?.headers ?? {}) as Record<string, string>;
}

function signIn(token = 'a.jwt.token') {
  saveSession({
    token,
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind-support.example' },
  });
}

describe('request', () => {
  beforeEach(() => {
    window.localStorage.clear();
    clearSession();
    // A fresh response for every call: a response body can only be read once.
    fetchMock.mockImplementation(async () => reply(200, { ok: true }));
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    setUnauthorizedHandler(undefined);
    vi.unstubAllGlobals();
    fetchMock.mockReset();
  });

  it('sends the token with every call once someone is signed in', async () => {
    signIn('my.token');

    await request('/tickets');
    await request('/customers');

    expect(fetchMock).toHaveBeenCalledTimes(2);
    for (const [, init] of fetchMock.mock.calls) {
      expect((init?.headers as Record<string, string>).Authorization).toBe('Bearer my.token');
    }
  });

  it('sends no Authorization header when nobody is signed in', async () => {
    await request('/tickets');

    expect(sentHeaders()).not.toHaveProperty('Authorization');
  });

  it('sends no token on an anonymous call even when someone is signed in', async () => {
    signIn();

    await request('/auth/login', { method: 'POST' }, { anonymous: true });

    expect(sentHeaders()).not.toHaveProperty('Authorization');
  });

  it('keeps the other headers and the request body it was given', async () => {
    signIn();

    await request('/tickets', { method: 'POST', body: '{"a":1}', headers: { 'X-Test': 'yes' } });

    const init = fetchMock.mock.lastCall?.[1];
    expect(init?.method).toBe('POST');
    expect(init?.body).toBe('{"a":1}');
    expect(sentHeaders()).toMatchObject({ 'Content-Type': 'application/json', 'X-Test': 'yes' });
  });

  it('ends the session and runs the handler when the API answers 401', async () => {
    signIn();
    const handler = vi.fn();
    setUnauthorizedHandler(handler);
    fetchMock.mockResolvedValue(reply(401, { title: 'Unauthorized', detail: 'Your session is no longer valid.' }));

    await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

    expect(getSession()).toBeUndefined();
    expect(window.localStorage.getItem('northwind.session')).toBeNull();
    expect(handler).toHaveBeenCalledTimes(1);
  });

  it('still throws the API error, with its message, on a 401', async () => {
    signIn();
    fetchMock.mockResolvedValue(reply(401, { title: 'Unauthorized', detail: 'Your session is no longer valid.' }));

    await expect(request('/tickets')).rejects.toMatchObject({
      status: 401,
      message: 'Your session is no longer valid.',
    });
  });

  it('ends the session on a 401 even when no handler has been registered', async () => {
    signIn();
    fetchMock.mockResolvedValue(reply(401));

    await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

    expect(getSession()).toBeUndefined();
  });

  it('treats a 401 on an anonymous call as an ordinary error and leaves the session alone', async () => {
    signIn();
    const handler = vi.fn();
    setUnauthorizedHandler(handler);
    fetchMock.mockResolvedValue(reply(401, { title: 'Unauthorized', detail: 'Invalid email or password.' }));

    await expect(request('/auth/login', { method: 'POST' }, { anonymous: true })).rejects.toMatchObject({
      status: 401,
      message: 'Invalid email or password.',
    });

    expect(getSession()).toBeDefined();
    expect(handler).not.toHaveBeenCalled();
  });

  it.each([400, 403, 404, 409, 500])('keeps the session when the API answers %i', async (status) => {
    signIn();
    const handler = vi.fn();
    setUnauthorizedHandler(handler);
    fetchMock.mockResolvedValue(reply(status, { title: 'Problem' }));

    await expect(request('/tickets')).rejects.toMatchObject({ status });

    expect(getSession()).toBeDefined();
    expect(handler).not.toHaveBeenCalled();
  });

  it('does not send a token that has already expired, and ends the session', async () => {
    saveSession({
      token: 'old.token',
      expiresAtUtc: new Date(Date.now() - 1000).toISOString(),
      user: { id: 1, fullName: 'Alex Turner', email: 'a@b.test' },
    });

    await request('/tickets');

    expect(sentHeaders()).not.toHaveProperty('Authorization');
    expect(getSession()).toBeUndefined();
  });

  it('returns the parsed body on success and nothing on 204', async () => {
    fetchMock.mockResolvedValueOnce(reply(200, { id: 7 }));
    fetchMock.mockResolvedValueOnce(reply(204));

    expect(await request<{ id: number }>('/tickets/7')).toEqual({ id: 7 });
    expect(await request('/tickets/7', { method: 'DELETE' })).toBeUndefined();
  });
});
