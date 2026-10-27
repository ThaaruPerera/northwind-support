export interface LoginErrors {
  email?: string;
  password?: string;
}

/** Plain enough to catch a typo; the API is the one that decides whether the email is real. */
const looksLikeAnEmail = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** What is wrong with the form, if anything. The password is judged as typed, spaces included. */
export function validateLogin(email: string, password: string): LoginErrors {
  const errors: LoginErrors = {};
  const trimmed = email.trim();

  if (trimmed.length === 0) {
    errors.email = 'Enter your email';
  } else if (!looksLikeAnEmail.test(trimmed)) {
    errors.email = 'Enter a valid email address';
  }

  if (password.length === 0) {
    errors.password = 'Enter your password';
  }

  return errors;
}
