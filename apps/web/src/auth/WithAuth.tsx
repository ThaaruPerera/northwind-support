import { Outlet } from 'react-router-dom';
import { AuthProvider } from './AuthProvider';

/** Puts the sign in state around every page, the login page included. */
export function WithAuth() {
  return (
    <AuthProvider>
      <Outlet />
    </AuthProvider>
  );
}
