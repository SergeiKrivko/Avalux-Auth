export type View = 'login' | 'profile'

/**
 * Returns the relative path inside the application (without the /auth base).
 */
export function relativePath(): string {
  const base = import.meta.env.BASE_URL.replace(/\/+$/, '')
  const pathname = window.location.pathname
  const withoutBase = base && pathname.startsWith(base) ? pathname.slice(base.length) : pathname
  return withoutBase.replace(/^\/+/, '')
}

export function getView(): View {
  return relativePath().startsWith('profile') ? 'profile' : 'login'
}

export function getState(): string {
  return new URLSearchParams(window.location.search).get('state')?.trim() ?? ''
}

export function redirectTo(url: string): void {
  window.location.assign(url)
}
