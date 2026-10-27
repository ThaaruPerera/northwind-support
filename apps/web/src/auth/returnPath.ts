/** Where a visitor was heading, kept in the router's state while they sign in. */
export interface ReturnLocation {
  pathname: string;
  search: string;
  hash: string;
}

/** Where to go when nothing sensible was remembered. */
export const defaultReturnPath = '/tickets';

export function toReturnLocation(location: ReturnLocation): ReturnLocation {
  return { pathname: location.pathname, search: location.search, hash: location.hash };
}

/**
 * The path to go back to after signing in. Only a path inside this app is accepted, and never the
 * login page itself, so a crafted value cannot send someone to another site or loop them.
 */
export function readReturnPath(state: unknown): string {
  if (typeof state !== 'object' || state === null || !('from' in state)) {
    return defaultReturnPath;
  }

  const from = (state as { from: Partial<Record<keyof ReturnLocation, unknown>> | null }).from;

  if (typeof from !== 'object' || from === null || typeof from.pathname !== 'string') {
    return defaultReturnPath;
  }

  const { pathname } = from;
  const isInsideTheApp = pathname.startsWith('/') && !pathname.startsWith('//');

  if (!isInsideTheApp || pathname === '/login' || pathname === '/') {
    return defaultReturnPath;
  }

  const search = typeof from.search === 'string' ? from.search : '';
  const hash = typeof from.hash === 'string' ? from.hash : '';

  return `${pathname}${search}${hash}`;
}
