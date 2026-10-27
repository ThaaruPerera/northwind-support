import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { toReturnLocation } from './returnPath';
import { useAuth } from './useAuth';

/**
 * Wraps the pages that need a signed in agent. Anyone else is sent to the login page, which
 * remembers where they were going and brings them back after they sign in.
 */
export function RequireAuth() {
  const { user } = useAuth();
  const location = useLocation();

  if (!user) {
    return <Navigate to="/login" replace state={{ from: toReturnLocation(location) }} />;
  }

  return <Outlet />;
}
