import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Session } from './session';

const storageKey = 'northwind.session';

function aSession(overrides: Partial<Session> = {}): Session {
  return {
    token: 'a.jwt.token',
    expiresAtUtc: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind-support.example' },
    ...overrides,
  };
}

/** Loads the module afresh, as a page load does, so what was left in storage is read again. */
async function openThePage() {
  vi.resetModules();

  return import('./session');
}

describe('session', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('starts signed out when nothing was stored', async () => {
    const session = await openThePage();

    expect(session.getSession()).toBeUndefined();
    expect(session.getToken()).toBeUndefined();
  });

  it('keeps a saved session across a page load', async () => {
    const first = await openThePage();
    first.saveSession(aSession());

    const reloaded = await openThePage();

    expect(reloaded.getSession()?.user.fullName).toBe('Alex Turner');
    expect(reloaded.getToken()).toBe('a.jwt.token');
  });

  it('throws away an expired session on page load, and removes it from storage', async () => {
    window.localStorage.setItem(
      storageKey,
      JSON.stringify(aSession({ expiresAtUtc: new Date(Date.now() - 1000).toISOString() })),
    );

    const session = await openThePage();

    expect(session.getSession()).toBeUndefined();
    expect(window.localStorage.getItem(storageKey)).toBeNull();
  });

  it('treats a session that expires exactly now as expired', async () => {
    window.localStorage.setItem(storageKey, JSON.stringify(aSession({ expiresAtUtc: new Date().toISOString() })));

    const session = await openThePage();

    expect(session.getSession()).toBeUndefined();
  });

  it.each([
    ['not JSON at all', 'this is not json'],
    ['JSON that is not an object', '42'],
    ['null', 'null'],
    ['a session with no token', JSON.stringify({ ...aSession(), token: undefined })],
    ['a session with an empty token', JSON.stringify(aSession({ token: '' }))],
    ['a session with no user', JSON.stringify({ ...aSession(), user: undefined })],
    ['a user with no name', JSON.stringify({ ...aSession(), user: { id: 1, email: 'a@b.test' } })],
    ['an expiry that is not a date', JSON.stringify(aSession({ expiresAtUtc: 'later' }))],
  ])('starts signed out, and clears storage, when it finds %s', async (_name, stored) => {
    window.localStorage.setItem(storageKey, stored);

    const session = await openThePage();

    expect(session.getSession()).toBeUndefined();
    expect(window.localStorage.getItem(storageKey)).toBeNull();
  });

  it('clears the session from memory and from storage', async () => {
    const session = await openThePage();
    session.saveSession(aSession());

    session.clearSession();

    expect(session.getSession()).toBeUndefined();
    expect(window.localStorage.getItem(storageKey)).toBeNull();
  });

  it('stops handing out a token once it has expired while the page was open', async () => {
    vi.useFakeTimers();
    const session = await openThePage();
    session.saveSession(aSession({ expiresAtUtc: new Date(Date.now() + 60_000).toISOString() }));

    expect(session.getToken()).toBe('a.jwt.token');

    vi.advanceTimersByTime(61_000);

    expect(session.getToken()).toBeUndefined();
    expect(session.getSession()).toBeUndefined();
    expect(window.localStorage.getItem(storageKey)).toBeNull();
  });

  it('tells listeners when the session is saved or cleared, and stops once they unsubscribe', async () => {
    const session = await openThePage();
    const listener = vi.fn();
    const unsubscribe = session.subscribeToSession(listener);

    session.saveSession(aSession());
    session.clearSession();

    expect(listener).toHaveBeenCalledTimes(2);

    unsubscribe();
    session.saveSession(aSession());

    expect(listener).toHaveBeenCalledTimes(2);
  });

  it('still works for this page when storage cannot be written', async () => {
    const session = await openThePage();
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('storage is full');
    });

    session.saveSession(aSession());

    expect(session.getToken()).toBe('a.jwt.token');
  });

  it('does not fail when storage cannot be read at all', async () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('storage is blocked');
    });

    const session = await openThePage();

    expect(session.getSession()).toBeUndefined();
  });

  it('follows a sign in or sign out made in another tab', async () => {
    const session = await openThePage();
    const listener = vi.fn();
    session.subscribeToSession(listener);

    window.localStorage.setItem(storageKey, JSON.stringify(aSession({ token: 'from.other.tab' })));
    window.dispatchEvent(new StorageEvent('storage', { key: storageKey }));

    expect(session.getToken()).toBe('from.other.tab');

    window.localStorage.removeItem(storageKey);
    window.dispatchEvent(new StorageEvent('storage', { key: storageKey }));

    expect(session.getSession()).toBeUndefined();
    expect(listener).toHaveBeenCalledTimes(2);
  });

  it('ignores changes to other storage keys', async () => {
    const session = await openThePage();
    session.saveSession(aSession());
    const listener = vi.fn();
    session.subscribeToSession(listener);

    window.dispatchEvent(new StorageEvent('storage', { key: 'something.else' }));

    expect(listener).not.toHaveBeenCalled();
    expect(session.getToken()).toBe('a.jwt.token');
  });

  it('stores the token only under its own key', async () => {
    const session = await openThePage();

    session.saveSession(aSession());

    expect(Object.keys(window.localStorage)).toEqual([storageKey]);
  });
});
