import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearSession, getSession, saveSession } from '../auth/session';
import { authApi } from './auth';
import { setUnauthorizedHandler } from './client';

const fetchMock = vi.fn<typeof fetch>();

function signIn() {
  saveSession({
    token: 'existing.token',
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind-support.example' },
  });
}

describe('authApi', () => {
  beforeEach(() => {
    window.localStorage.clear();
    clearSession();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    setUnauthorizedHandler(undefined);
    vi.unstubAllGlobals();
    fetchMock.mockReset();
  });

  it('posts the email and password to the login endpoint, without a token', async () => {
    signIn();
    fetchMock.mockResolvedValue(
      new Response(
        JSON.stringify({
          token: 'new.token',
          expiresAtUtc: '2030-01-01T00:00:00Z',
          user: { id: 1, fullName: 'Alex Turner', email: 'a@b.test' },
        }),
        { status: 200 },
      ),
    );

    const result = await authApi.login('alex.turner@northwind-support.example', 'a password');

    const [url, init] = fetchMock.mock.lastCall ?? [];
    expect(url).toBe('/api/auth/login');
    expect(init?.method).toBe('POST');
    expect(JSON.parse(String(init?.body))).toEqual({
      email: 'alex.turner@northwind-support.example',
      password: 'a password',
    });
    expect((init?.headers as Record<string, string>).Authorization).toBeUndefined();
    expect(result.token).toBe('new.token');
  });

  it('reports rejected credentials as an error without ending an existing session or redirecting', async () => {
    signIn();
    const handler = vi.fn();
    setUnauthorizedHandler(handler);
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ title: 'Unauthorized', detail: 'Invalid email or password.' }), { status: 401 }),
    );

    await expect(authApi.login('alex@b.test', 'wrong')).rejects.toMatchObject({
      status: 401,
      message: 'Invalid email or password.',
    });

    expect(getSession()).toBeDefined();
    expect(handler).not.toHaveBeenCalled();
  });

  it('asks who the current token belongs to, sending the token', async () => {
    signIn();
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ id: 1, fullName: 'Alex Turner', email: 'a@b.test' }), { status: 200 }),
    );

    const me = await authApi.me();

    const [url, init] = fetchMock.mock.lastCall ?? [];
    expect(url).toBe('/api/auth/me');
    expect((init?.headers as Record<string, string>).Authorization).toBe('Bearer existing.token');
    expect(me.fullName).toBe('Alex Turner');
  });
});
