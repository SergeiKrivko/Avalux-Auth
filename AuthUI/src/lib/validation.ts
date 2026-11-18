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

export function validateEmail(value: string): string | null {
  const email = value.trim()
  if (!email) return null
  return EMAIL_PATTERN.test(email) ? null : 'Проверьте адрес электронной почты.'
}
