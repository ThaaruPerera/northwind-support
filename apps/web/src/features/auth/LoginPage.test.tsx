import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import { ApiError } from '../../api/client';
import { WithAuth } from '../../auth/WithAuth';
import { clearSession, getSession, saveSession } from '../../auth/session';
import type { LoginResult } from '../../types/api';
import { LoginPage } from './LoginPage';

vi.mock('../../api/auth', () => ({ authApi: { login: vi.fn() } }));

const login = vi.mocked(authApi.login);

const signedIn: LoginResult = {
  token: 'a.jwt.token',
  expiresAtUtc: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
  user: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind-support.example' },
};

type Entry = string | { pathname: string; state?: unknown };

/** The login page and two pages to land on, inside the real sign in provider. */
function renderLogin(entry: Entry = '/login') {
  const router = createMemoryRouter(
    [
      {
        element: <WithAuth />,
        children: [
          { path: '/login', element: <LoginPage /> },
          { path: '/tickets', element: <p>The ticket list</p> },
          { path: '/tickets/:id', element: <p>A ticket page</p> },
        ],
      },
    ],
    { initialEntries: [entry] },
  );

  render(<RouterProvider router={router} />);

  return router;
}

async function fillIn(user: ReturnType<typeof userEvent.setup>, email: string, password: string) {
  if (email) {
    await user.type(screen.getByLabelText('Email'), email);
  }

  if (password) {
    await user.type(screen.getByLabelText('Password'), password);
  }
}

describe('LoginPage', () => {
  beforeEach(() => {
    window.localStorage.clear();
    clearSession();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('has labelled email and password fields and a sign in button', () => {
    renderLogin();

    expect(screen.getByRole('heading', { name: 'Welcome back' })).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveAttribute('type', 'email');
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password');
    expect(screen.getByRole('button', { name: /Sign in/ })).toBeEnabled();
  });

  it('asks the browser to offer saved credentials', () => {
    renderLogin();

    expect(screen.getByLabelText('Email')).toHaveAttribute('autocomplete', 'username');
    expect(screen.getByLabelText('Password')).toHaveAttribute('autocomplete', 'current-password');
  });

  it('offers no way to register or reset a password, as the brief leaves them out', () => {
    renderLogin();

    expect(screen.queryByText(/register|sign up|forgot|reset|remember me/i)).not.toBeInTheDocument();
    expect(screen.getByText('Accounts are created by your admin')).toBeInTheDocument();
  });

  it('hides the decorative panel from assistive technology', () => {
    renderLogin();

    expect(document.querySelector('aside')).toHaveAttribute('aria-hidden', 'true');
  });

  it('sets the page title while it is shown', () => {
    renderLogin();

    expect(document.title).toBe('Sign in · Northwind Support');
  });

  describe('checking the form before calling the API', () => {
    it('reports both fields when nothing is filled in, and focuses the email', async () => {
      const user = userEvent.setup();
      renderLogin();

      await user.click(screen.getByRole('button', { name: /Sign in/ }));

      expect(screen.getByText('Enter your email')).toBeInTheDocument();
      expect(screen.getByText('Enter your password')).toBeInTheDocument();
      expect(screen.getByLabelText('Email')).toHaveFocus();
      expect(login).not.toHaveBeenCalled();
    });

    it('marks the invalid fields and ties each message to its field', async () => {
      const user = userEvent.setup();
      renderLogin();

      await user.click(screen.getByRole('button', { name: /Sign in/ }));

      const email = screen.getByLabelText('Email');
      expect(email).toHaveAttribute('aria-invalid', 'true');
      expect(email).toHaveAccessibleDescription('Enter your email');
      expect(screen.getByLabelText('Password')).toHaveAccessibleDescription('Enter your password');
    });

    it('asks for a valid email when the email is malformed', async () => {
      const user = userEvent.setup();
      renderLogin();

      await fillIn(user, 'not-an-email', 'a password');
      await user.click(screen.getByRole('button', { name: /Sign in/ }));

      expect(screen.getByText('Enter a valid email address')).toBeInTheDocument();
      expect(login).not.toHaveBeenCalled();
    });

    it('focuses the password when only the password is missing', async () => {
      const user = userEvent.setup();
      renderLogin();

      await fillIn(user, 'alex@northwind.example', '');
      await user.click(screen.getByRole('button', { name: /Sign in/ }));

      expect(screen.getByLabelText('Password')).toHaveFocus();
      expect(login).not.toHaveBeenCalled();
    });
  });

  describe('signing in', () => {
    it('sends the trimmed email and the password exactly as typed', async () => {
      const user = userEvent.setup();
      login.mockResolvedValue(signedIn);
      renderLogin();

      await fillIn(user, '  alex.turner@northwind-support.example ', ' pass word ');
      await user.click(screen.getByRole('button', { name: /Sign in/ }));

      await waitFor(() => expect(login).toHaveBeenCalledTimes(1));
      expect(login).toHaveBeenCalledWith('alex.turner@northwind-support.example', ' pass word ');
    });

    it('shows a pending state and locks the form while the request runs', async () => {
      const user = userEvent.setup();
      let finish: (value: LoginResult) => void = () => undefined;
      login.mockReturnValue(new Promise((resolve) => (finish = resolve)));
      renderLogin();

      await fillIn(user, 'alex@northwind.example', 'a password');
      await user.click(screen.getByRole('button', { name: /Sign in/ }));

      const pending = screen.getByRole('button', { name: 'Signing in...' });
      expect(pending).toBeDisabled();
      expect(screen.getByLabelText('Email')).toHaveAttribute('readonly');
      expect(screen.getByLabelText('Password')).toHaveAttribute('readonly');
      expect(pending.closest('form')).toHaveAttribute('aria-busy', 'true');

      await user.click(pending);
      expect(login).toHaveBeenCalledTimes(1);

      finish(signedIn);
      await screen.findByText('The ticket list');
    });

    it('keeps the session and lands on the ticket list after signing in', async () => {
      const user = userEvent.setup();
      login.mockResolvedValue(signedIn);
      const router = renderLogin();

      await fillIn(user, 'alex@northwind.example', 'a password');
      await user.click(screen.getByRole('button', { name: /Sign in/ }));

      expect(await screen.findByText('The ticket list')).toBeInTheDocument();
      expect(router.state.location.pathname).toBe('/tickets');
      expect(getSession()?.token).toBe('a.jwt.token');
    });

    it('returns to the page that was wanted, with its query', async () => {
      const user = userEvent.setup();
      login.mockResolvedValue(signedIn);
      const router = renderLogin({
        pathname: '/login',
        state: { from: { pathname: '/tickets/12', search: '?tab=history', hash: '' } },
      });

      await fillIn(user, 'alex@northwind.example', 'a password');
      await user.click(screen.getByRole('button', { name: /Sign in/ }));

      expect(await screen.findByText('A ticket page')).toBeInTheDocument();
      expect(router.state.location.pathname).toBe('/tickets/12');
      expect(router.state.location.search).toBe('?tab=history');
    });

    it.each(['//evil.example', 'https://evil.example', '/login'])(
      'does not return to %s, and lands on the ticket list instead',
      async (pathname) => {
        const user = userEvent.setup();
        login.mockResolvedValue(signedIn);
        const router = renderLogin({ pathname: '/login', state: { from: { pathname, search: '', hash: '' } } });

        await fillIn(user, 'alex@northwind.example', 'a password');
        await user.click(screen.getByRole('button', { name: /Sign in/ }));

        await screen.findByText('The ticket list');
        expect(router.state.location.pathname).toBe('/tickets');
      },
    );
  });

  describe('when the credentials are rejected', () => {
    it('shows one generic message, keeps the email and empties and focuses the password', async () => {
      const user = userEvent.setup();
      login.mockRejectedValue(new ApiError(401, 'Invalid email or password.'));
      const router = renderLogin();

      await fillIn(user, 'alex@northwind.example', 'wrong password');
      await user.click(screen.getByRole('button', { name: /Sign in/ }));

      expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password');
      expect(screen.getByLabelText('Email')).toHaveValue('alex@northwind.example');
      expect(screen.getByLabelText('Password')).toHaveValue('');
      expect(screen.getByLabelText('Password')).toHaveFocus();
      expect(screen.getByRole('button', { name: /Sign in/ })).toBeEnabled();
      expect(router.state.location.pathname).toBe('/login');
      expect(getSession()).toBeUndefined();
    });

    it('does not say whether the email or the password was wrong', async () => {
      const user = userEvent.setup();
      login.mockRejectedValue(new ApiError(401, 'Invalid email or password.'));
      renderLogin();

      await fillIn(user, 'alex@northwind.example', 'wrong');
      await user.click(screen.getByRole('button', { name: /Sign in/ }));

      const message = (await screen.findByRole('alert')).textContent ?? '';
      expect(message).not.toMatch(/not found|unknown|no such|incorrect password|wrong password/i);
    });

    it('clears the message when the next attempt starts', async () => {
      const user = userEvent.setup();
      login.mockRejectedValueOnce(new ApiError(401, 'Invalid email or password.'));
      let finish: (value: LoginResult) => void = () => undefined;
      login.mockReturnValueOnce(new Promise((resolve) => (finish = resolve)));
      renderLogin();

      await fillIn(user, 'alex@northwind.example', 'wrong');
      await user.click(screen.getByRole('button', { name: /Sign in/ }));
      await screen.findByRole('alert');

      await user.type(screen.getByLabelText('Password'), 'right');
      await user.click(screen.getByRole('button', { name: /Sign in/ }));

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();

      finish(signedIn);
      await screen.findByText('The ticket list');
    });
  });

  describe('when something else goes wrong', () => {
    it.each([
      ['the server fails', new ApiError(500, 'Internal Server Error')],
      ['the network is down', new TypeError('Failed to fetch')],
    ])('says it could not sign in, and does not blame the password, when %s', async (_name, failure) => {
      const user = userEvent.setup();
      login.mockRejectedValue(failure);
      renderLogin();

      await fillIn(user, 'alex@northwind.example', 'a password');
      await user.click(screen.getByRole('button', { name: /Sign in/ }));

      expect(await screen.findByRole('alert')).toHaveTextContent('Could not sign in right now');
      expect(screen.getByRole('alert')).not.toHaveTextContent('Invalid email or password');
      expect(screen.getByLabelText('Password')).toHaveValue('a password');
    });
  });

  it('sends someone who is already signed in straight on to the ticket list', async () => {
    saveSession(signedIn);
    const router = renderLogin();

    expect(await screen.findByText('The ticket list')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/tickets');
  });
});
