import type { LoginResult } from '../types/api';

/** What is kept after signing in. It is exactly what the login call returned. */
export type Session = LoginResult;

/**
 * Where the session lives: this browser's localStorage, so it survives a refresh and a new tab.
 * The trade-off is that any script running on the page can read it, so a cross-site scripting hole
 * would leak the token. A production system would keep it in an httpOnly cookie instead, which
 * scripts cannot read. See the README.
 */
const storageKey = 'northwind.session';

type Listener = () => void;

const listeners = new Set<Listener>();

let current: Session | undefined = readStoredSession();

/** The session right now, or undefined when nobody is signed in. Safe to call while rendering. */
export function getSession(): Session | undefined {
  return current;
}

/** The token to send, or undefined. A token that has run out is thrown away instead of sent. */
export function getToken(): string | undefined {
  if (current && isExpired(current)) {
    clearSession();
  }

  return current?.token;
}

export function saveSession(session: Session): void {
  current = session;

  try {
    window.localStorage.setItem(storageKey, JSON.stringify(session));
  } catch {
    // Storage can be blocked or full. The session still works until the page is closed.
  }

  notify();
}

export function clearSession(): void {
  current = undefined;

  try {
    window.localStorage.removeItem(storageKey);
  } catch {
    // Nothing to remove if storage cannot be reached.
  }

  notify();
}

/** Calls the listener whenever the session is saved, cleared or changed in another tab. */
export function subscribeToSession(listener: Listener): () => void {
  listeners.add(listener);

  return () => listeners.delete(listener);
}

function notify(): void {
  listeners.forEach((listener) => listener());
}

function isExpired(session: Session): boolean {
  return Date.parse(session.expiresAtUtc) <= Date.now();
}

/**
 * Reads what an earlier visit left behind. Anything that is missing, damaged, not shaped like a
 * session or already expired counts as signed out, and is removed so it is not read again.
 */
function readStoredSession(): Session | undefined {
  try {
    const raw = window.localStorage.getItem(storageKey);

    if (raw === null) {
      return undefined;
    }

    const parsed = parseJson(raw);

    if (isSession(parsed) && !isExpired(parsed)) {
      return parsed;
    }

    window.localStorage.removeItem(storageKey);
  } catch {
    // Storage that cannot be reached at all: start signed out.
  }

  return undefined;
}

/** Damaged JSON is just something that is not a session, so it comes back as undefined. */
function parseJson(raw: string): unknown {
  try {
    return JSON.parse(raw);
  } catch {
    return undefined;
  }
}

function isSession(value: unknown): value is Session {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Partial<Record<keyof Session, unknown>>;
  const user = candidate.user as Partial<Record<keyof Session['user'], unknown>> | null | undefined;

  return (
    typeof candidate.token === 'string' &&
    candidate.token.length > 0 &&
    typeof candidate.expiresAtUtc === 'string' &&
    !Number.isNaN(Date.parse(candidate.expiresAtUtc)) &&
    typeof user === 'object' &&
    user !== null &&
    typeof user.id === 'number' &&
    typeof user.fullName === 'string' &&
    typeof user.email === 'string'
  );
}

// Signing in or out in another tab should be reflected here without a reload.
window.addEventListener('storage', (event) => {
  if (event.key === storageKey || event.key === null) {
    current = readStoredSession();
    notify();
  }
});
