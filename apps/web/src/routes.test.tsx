import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearSession, getSession, saveSession } from './auth/session';
import { routeConfig } from './routes';
import { page, ticketDetail, ticketListItem } from './test/fixtures';
import type { LoginResult } from './types/api';

/**
 * These tests run the whole front end, routes, sign in and the real API client, against a fake
 * server standing in for fetch. Nothing in the app itself is mocked.
 */

const loginResult: LoginResult = {
  token: 'a.jwt.token',
  expiresAtUtc: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
  user: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind-support.example' },
};

interface SentRequest {
  url: string;
  authorization: string | undefined;
}

let sent: SentRequest[] = [];
let ticketListStatus = 200;

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status });
}

const unauthorized = { title: 'Unauthorized', detail: 'Your session is no longer valid. Sign in again.' };

const fakeServer = vi.fn(async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
  const url = String(input);
  const headers = (init?.headers ?? {}) as Record<string, string>;
  sent.push({ url, authorization: headers.Authorization });

  if (url === '/api/auth/login') {
    const { password } = JSON.parse(String(init?.body)) as { password: string };

    return password === 'right'
      ? json(200, loginResult)
      : json(401, { title: 'Unauthorized', detail: 'Invalid email or password.' });
  }

  if (url.startsWith('/api/tickets?')) {
    return ticketListStatus === 200 ? json(200, page([ticketListItem()])) : json(401, unauthorized);
  }

  if (url === '/api/tickets/12') {
    return json(200, ticketDetail({ title: 'Invoice total does not match the order' }));
  }

  return json(200, []);
});

function open(path: string) {
  const router = createMemoryRouter(routeConfig, { initialEntries: [path] });
  render(<RouterProvider router={router} />);

  return router;
}

async function signInAs(user: ReturnType<typeof userEvent.setup>, password = 'right') {
  await user.type(await screen.findByLabelText('Email'), 'alex.turner@northwind-support.example');
  await user.type(screen.getByLabelText('Password'), password);
  await user.click(screen.getByRole('button', { name: /Sign in/ }));
}

const callsTo = (fragment: string) => sent.filter((request) => request.url.includes(fragment));

describe('the app when signing in is required', () => {
  beforeEach(() => {
    window.localStorage.clear();
    clearSession();
    sent = [];
    ticketListStatus = 200;
    vi.stubGlobal('fetch', fakeServer);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    fakeServer.mockClear();
  });

  describe('a visitor who is not signed in', () => {
    it('is sent to the login page and has no data requested for them', async () => {
      const router = open('/tickets/12');

      expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument();
      expect(router.state.location.pathname).toBe('/login');
      expect(callsTo('/api/tickets')).toHaveLength(0);
    });

    it.each(['/', '/tickets', '/tickets/new', '/customers', '/customers/3', '/no/such/page'])(
      'cannot reach %s',
      async (path) => {
        const router = open(path);

        expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument();
        expect(router.state.location.pathname).toBe('/login');
      },
    );

    it('comes back to the page they wanted after signing in, with the token on its requests', async () => {
      const user = userEvent.setup();
      const router = open('/tickets/12');

      await signInAs(user);

      expect(await screen.findByRole('heading', { name: 'Invoice total does not match the order' })).toBeInTheDocument();
      expect(router.state.location.pathname).toBe('/tickets/12');
      expect(callsTo('/api/tickets/12').every((request) => request.authorization === 'Bearer a.jwt.token')).toBe(true);
    });

    it('lands on the ticket list when they had no particular page in mind', async () => {
      const user = userEvent.setup();
      const router = open('/');

      await signInAs(user);

      expect(await screen.findByRole('link', { name: 'TCK-0012' })).toBeInTheDocument();
      expect(router.state.location.pathname).toBe('/tickets');
    });

    it('stays on the login page after a wrong password, and sees the generic message', async () => {
      const user = userEvent.setup();
      const router = open('/tickets');

      await signInAs(user, 'wrong');

      expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password');
      expect(router.state.location.pathname).toBe('/login');
      expect(getSession()).toBeUndefined();
      expect(callsTo('/api/tickets?')).toHaveLength(0);
    });
  });

  describe('an agent who is signed in', () => {
    beforeEach(() => {
      saveSession(loginResult);
    });

    it('sees their name in the header, and the page they asked for', async () => {
      open('/tickets');

      expect(await screen.findByText('Alex Turner')).toBeInTheDocument();
      expect(screen.getByText(/Signed in as/)).toBeInTheDocument();
      expect(await screen.findByRole('link', { name: 'TCK-0012' })).toBeInTheDocument();
    });

    it('sends the token on every request', async () => {
      open('/tickets');
      await screen.findByRole('link', { name: 'TCK-0012' });

      expect(sent.length).toBeGreaterThan(1);
      expect(sent.every((request) => request.authorization === 'Bearer a.jwt.token')).toBe(true);
    });

    it('is taken away from the login page, to the ticket list', async () => {
      const router = open('/login');

      expect(await screen.findByRole('link', { name: 'TCK-0012' })).toBeInTheDocument();
      expect(router.state.location.pathname).toBe('/tickets');
    });

    it('signs out: the session is cleared and the login page is shown', async () => {
      const user = userEvent.setup();
      const router = open('/tickets');
      await screen.findByRole('link', { name: 'TCK-0012' });

      await user.click(screen.getByRole('button', { name: 'Sign out' }));

      expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument();
      expect(router.state.location.pathname).toBe('/login');
      expect(getSession()).toBeUndefined();
      expect(window.localStorage.length).toBe(0);
    });

    it('cannot go back into the app after signing out', async () => {
      const user = userEvent.setup();
      const router = open('/tickets');
      await screen.findByRole('link', { name: 'TCK-0012' });
      await user.click(screen.getByRole('button', { name: 'Sign out' }));
      await screen.findByRole('heading', { name: 'Welcome back' });

      sent = [];

      await router.navigate('/tickets/12');

      // Turned back to the login page, which remembers where they were trying to go.
      await waitFor(() =>
        expect(router.state.location.state).toMatchObject({ from: { pathname: '/tickets/12' } }),
      );
      expect(router.state.location.pathname).toBe('/login');
      expect(screen.getByRole('heading', { name: 'Welcome back' })).toBeInTheDocument();
      expect(callsTo('/api/tickets')).toHaveLength(0);
    });

    it('is sent to the login page when the API says the session is no longer valid, then returns', async () => {
      const user = userEvent.setup();
      ticketListStatus = 401;
      const router = open('/tickets');

      expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument();
      expect(router.state.location.pathname).toBe('/login');
      expect(getSession()).toBeUndefined();
      expect(window.localStorage.length).toBe(0);

      ticketListStatus = 200;
      await signInAs(user);

      expect(await screen.findByRole('link', { name: 'TCK-0012' })).toBeInTheDocument();
      expect(router.state.location.pathname).toBe('/tickets');
    });

    it('is sent to the login page without the old token being sent, when it expired while the page was open', async () => {
      saveSession({ ...loginResult, token: 'expired.token', expiresAtUtc: new Date(Date.now() - 1000).toISOString() });

      const router = open('/tickets');

      await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
      expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument();
      expect(sent.some((request) => request.authorization === 'Bearer expired.token')).toBe(false);
    });
  });
});
