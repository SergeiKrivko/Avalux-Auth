export interface FieldErrors {
  login?: string
  password?: string
  passwordAgain?: string
  email?: string
  oldPassword?: string
  newPassword?: string
  newPasswordAgain?: string
}

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

// Mirrors Accounts.Email (the tighter of the two columns sign-up writes to).
const MAX_EMAIL_LENGTH = 50

// Mirrors PasswordPolicy: BCrypt ignores everything past 72 bytes, so the API
// rejects longer values instead of truncating them silently.
const PASSWORD_MAX_BYTES = 72

export function validateEmail(value: string): string | null {
  const email = value.trim()
  if (!email) return null
  if (email.length > MAX_EMAIL_LENGTH) {
    return `Email не должен превышать ${MAX_EMAIL_LENGTH} символов.`
  }
  return EMAIL_PATTERN.test(email) ? null : 'Проверьте адрес электронной почты.'
}

export function passwordByteLength(value: string): number {
  return new TextEncoder().encode(value).length
}

export function validateNewPassword(value: string): string | null {
  if (value.length < 8) return 'Пароль должен содержать минимум 8 символов.'
  if (passwordByteLength(value) > PASSWORD_MAX_BYTES) {
    return 'Пароль слишком длинный: максимум 72 байта.'
  }
  return null
}
