import { describe, expect, it } from 'vitest';
import { validateLogin } from './loginRules';

describe('validateLogin', () => {
  it('accepts an email and a password', () => {
    expect(validateLogin('alex.turner@northwind-support.example', 'a password')).toEqual({});
  });

  it.each(['', '   '])('asks for an email when it is %j', (email) => {
    expect(validateLogin(email, 'x').email).toBe('Enter your email');
  });

  it.each(['alex', 'alex@', '@northwind.example', 'alex@northwind', 'a b@northwind.example'])(
    'asks for a valid email when it is %j',
    (email) => {
      expect(validateLogin(email, 'x').email).toBe('Enter a valid email address');
    },
  );

  it('ignores spaces around an email', () => {
    expect(validateLogin('  alex@northwind.example  ', 'x').email).toBeUndefined();
  });

  it('asks for a password when it is empty', () => {
    expect(validateLogin('alex@northwind.example', '').password).toBe('Enter your password');
  });

  it('accepts a password made only of spaces, because it is judged exactly as typed', () => {
    expect(validateLogin('alex@northwind.example', '   ').password).toBeUndefined();
  });

  it('reports both problems at once', () => {
    expect(validateLogin('', '')).toEqual({ email: 'Enter your email', password: 'Enter your password' });
  });
});
