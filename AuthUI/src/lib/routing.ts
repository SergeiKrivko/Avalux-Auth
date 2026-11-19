export type View = 'login' | 'profile' | 'recover'

/**
 * Returns the relative path inside the application (without the base path).
 */
export function relativePath(): string {
  const base = import.meta.env.BASE_URL.replace(/\/+$/, '')
  const pathname = window.location.pathname
  const withoutBase = base && pathname.startsWith(base) ? pathname.slice(base.length) : pathname
  return withoutBase.replace(/^\/+/, '')
}

export function getView(): View {
  const path = relativePath()
  if (path.startsWith('recover')) return 'recover'
  return path.startsWith('profile') ? 'profile' : 'login'
}

export function getState(): string {
  return new URLSearchParams(window.location.search).get('state')?.trim() ?? ''
}

/**
 * The one-time password reset token is delivered in the URL fragment. A
 * fragment never reaches the server (access logs, Referer) and is removed from
 * the address bar by `clearResetHash` as soon as the page has read it.
 */
export function getResetToken(): string {
  const raw = window.location.hash.replace(/^#/, '')
  return new URLSearchParams(raw).get('token')?.trim() ?? ''
}

export function clearResetHash(): void {
  if (!window.location.hash) return
  window.history.replaceState(null, '', window.location.pathname + window.location.search)
}

export function redirectTo(url: string): void {
  window.location.assign(url)
}
