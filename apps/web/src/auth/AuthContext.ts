import { createContext } from 'react';
import type { AuthUser } from '../types/api';

export interface AuthContextValue {
  /** The signed in agent, or undefined when nobody is. */
  user: AuthUser | undefined;
  /** Signs in and keeps the session. Throws the API's error when the credentials are rejected. */
  signIn: (email: string, password: string) => Promise<void>;
  /** Ends the session on this browser. */
  signOut: () => void;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);
