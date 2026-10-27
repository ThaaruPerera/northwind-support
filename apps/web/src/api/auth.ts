import type { AuthUser, LoginResult } from '../types/api';
import { request } from './client';

export const authApi = {
  /** Signs in. Anonymous: there is no token yet, and a 401 here just means wrong credentials. */
  login: (email: string, password: string) =>
    request<LoginResult>(
      '/auth/login',
      { method: 'POST', body: JSON.stringify({ email, password }) },
      { anonymous: true },
    ),

  /** The agent the current token belongs to. */
  me: () => request<AuthUser>('/auth/me'),
};
