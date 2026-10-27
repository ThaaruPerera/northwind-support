import { useEffect, useRef, useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { ApiError } from '../../api/client';
import { readReturnPath } from '../../auth/returnPath';
import { useAuth } from '../../auth/useAuth';
import { LoginShowcase } from './LoginShowcase';
import { validateLogin, type LoginErrors } from './loginRules';
import './login.css';

/** What the person is told when the API says no. It never says which part was wrong. */
const rejectedMessage = 'Invalid email or password';
const failedMessage = 'Could not sign in right now. Try again in a moment.';

/**
 * The sign in page. Checks the form before calling the API, shows one generic message when the
 * credentials are rejected, and sends the agent back to the page they were heading for.
 */
export function LoginPage() {
  const { user, signIn } = useAuth();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<LoginErrors>({});
  const [banner, setBanner] = useState<string>();
  const [isPending, setIsPending] = useState(false);

  const emailInput = useRef<HTMLInputElement>(null);
  const passwordInput = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const previous = document.title;
    document.title = 'Sign in · Northwind Support';

    return () => {
      document.title = previous;
    };
  }, []);

  // Already signed in, or just signed in: carry on to where they were going.
  if (user) {
    return <Navigate to={readReturnPath(location.state)} replace />;
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const found = validateLogin(email, password);
    setErrors(found);
    setBanner(undefined);

    if (found.email || found.password) {
      (found.email ? emailInput : passwordInput).current?.focus();
      return;
    }

    setIsPending(true);

    try {
      await signIn(email.trim(), password);
    } catch (caught) {
      const rejected = caught instanceof ApiError && caught.status === 401;

      setBanner(rejected ? rejectedMessage : failedMessage);

      if (rejected) {
        // Never leave a wrong password sitting in the box.
        setPassword('');
        passwordInput.current?.focus();
      }

      setIsPending(false);
    }
  };

  return (
    <div className="login">
      <div className="login__column">
        <div className="login__brand">
          <span className="login__logo" aria-hidden="true">
            N
          </span>
          <span className="login__brand-name">Northwind Support</span>
        </div>

        <main className="login__center">
          <div className="login__card">
            <div className="login__intro">
              <h1>Welcome back</h1>
              <p>Your queue is right where you left it.</p>
            </div>

            <form className="login__form" onSubmit={submit} noValidate aria-busy={isPending}>
              {banner && (
                <div className="login__banner" role="alert">
                  <span className="login__mark" aria-hidden="true">
                    !
                  </span>
                  <span>{banner}</span>
                </div>
              )}

              <div className="login__field">
                <label htmlFor="login-email">Email</label>
                <input
                  ref={emailInput}
                  id="login-email"
                  name="email"
                  type="email"
                  autoComplete="username"
                  inputMode="email"
                  autoCapitalize="off"
                  spellCheck={false}
                  placeholder="name@northwind-support.example"
                  value={email}
                  readOnly={isPending}
                  aria-invalid={Boolean(errors.email)}
                  aria-describedby={errors.email ? 'login-email-error' : undefined}
                  onChange={(event) => setEmail(event.target.value)}
                />
                {errors.email && (
                  <p className="login__error" id="login-email-error">
                    <span className="login__mark" aria-hidden="true">
                      !
                    </span>
                    {errors.email}
                  </p>
                )}
              </div>

              <div className="login__field">
                <label htmlFor="login-password">Password</label>
                <input
                  ref={passwordInput}
                  id="login-password"
                  name="password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  readOnly={isPending}
                  aria-invalid={Boolean(errors.password)}
                  aria-describedby={errors.password ? 'login-password-error' : undefined}
                  onChange={(event) => setPassword(event.target.value)}
                />
                {errors.password && (
                  <p className="login__error" id="login-password-error">
                    <span className="login__mark" aria-hidden="true">
                      !
                    </span>
                    {errors.password}
                  </p>
                )}
              </div>

              <button type="submit" className="login__submit" disabled={isPending}>
                {isPending ? (
                  <>
                    <span className="login__spinner" aria-hidden="true" />
                    <span>Signing in...</span>
                  </>
                ) : (
                  <>
                    <span>Sign in</span>
                    <span aria-hidden="true">→</span>
                  </>
                )}
              </button>
            </form>
          </div>
        </main>

        <footer className="login__footer">
          <span>© {new Date().getFullYear()} Northwind</span>
          <span>Accounts are created by your admin</span>
        </footer>
      </div>

      <LoginShowcase />
    </div>
  );
}
