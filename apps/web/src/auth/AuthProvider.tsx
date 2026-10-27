import { useEffect, useMemo, useRef, useSyncExternalStore } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { authApi } from '../api/auth';
import { setUnauthorizedHandler } from '../api/client';
import { AuthContext, type AuthContextValue } from './AuthContext';
import { toReturnLocation } from './returnPath';
import { clearSession, getSession, saveSession, subscribeToSession } from './session';

interface AuthProviderProps {
  children: React.ReactNode;
}

/**
 * Makes the signed in agent available to the app, and sends the browser to the login page whenever
 * the API turns a request away, remembering where it was so signing in can bring it back.
 */
export function AuthProvider({ children }: AuthProviderProps) {
  const session = useSyncExternalStore(subscribeToSession, getSession);
  const navigate = useNavigate();
  const location = useLocation();

  // The handler outlives any one render, so it reads the latest location from here.
  const latestLocation = useRef(location);

  useEffect(() => {
    latestLocation.current = location;
  }, [location]);

  useEffect(() => {
    setUnauthorizedHandler(() => {
      const here = latestLocation.current;

      if (here.pathname !== '/login') {
        void navigate('/login', { replace: true, state: { from: toReturnLocation(here) } });
      }
    });

    return () => setUnauthorizedHandler(undefined);
  }, [navigate]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user: session?.user,
      signIn: async (email, password) => {
        saveSession(await authApi.login(email, password));
      },
      signOut: clearSession,
    }),
    [session],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
