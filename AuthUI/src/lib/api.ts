export interface ClientInfoResponse {
  name?: string | null
}

export interface PasswordUserInfo {
  name?: string | null
  email?: string | null
  avatarId?: string | null
}

export interface AuthResponse {
  redirectUrl?: string | null
}

export interface UpdateProfilePayload {
  oldPassword?: string
  newPassword?: string
  userInfo: PasswordUserInfo
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

const API_ROOT = '/api/v1'

function parseMessage(raw: string): string {
  const text = raw.trim()
  if (!text) return ''

  if (text.startsWith('"') && text.endsWith('"')) {
    try {
      const parsed: unknown = JSON.parse(text)
      if (typeof parsed === 'string') return parsed
    } catch {
      // fall through to the raw text
    }
  }

  try {
    const data: unknown = JSON.parse(text)
    if (data && typeof data === 'object') {
      for (const key of ['detail', 'title', 'message'] as const) {
        const value = (data as Record<string, unknown>)[key]
        if (typeof value === 'string' && value) return value
      }
    }
  } catch {
    // not a JSON object
  }

  return text
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${API_ROOT}${path}`, {
      ...init,
      headers: {
        ...(init?.body instanceof FormData ? {} : {'Content-Type': 'application/json'}),
        ...init?.headers,
      },
    })
  } catch {
    throw new ApiError(0, 'NETWORK_ERROR')
  }

  if (!response.ok) {
    const message = parseMessage(await response.text().catch(() => ''))
    throw new ApiError(response.status, message)
  }

  if (response.status === 204) return undefined as T

  const text = await response.text()
  if (!text) return undefined as T
  return JSON.parse(text) as T
}

function withState(state: string): string {
  return `?${new URLSearchParams({state}).toString()}`
}

export function getClientInfo(state: string) {
  return request<ClientInfoResponse | null>(`/password/clientInfo${withState(state)}`)
}

export function signIn(state: string, login: string, password: string) {
  return request<AuthResponse>(`/password/signin${withState(state)}`, {
    method: 'POST',
    body: JSON.stringify({login, password}),
  })
}

export function signUp(
  state: string,
  login: string,
  password: string,
  userInfo: PasswordUserInfo,
) {
  return request<AuthResponse>(`/password/signup${withState(state)}`, {
    method: 'POST',
    body: JSON.stringify({login, password, userInfo}),
  })
}

export function getProfile(state: string) {
  return request<PasswordUserInfo | null>(`/password/profile${withState(state)}`)
}

export function updateProfile(state: string, payload: UpdateProfilePayload) {
  return request<AuthResponse>(`/password/profile${withState(state)}`, {
    method: 'PUT',
    body: JSON.stringify(payload),
  })
}

export async function uploadAvatar(file: File): Promise<string> {
  const body = new FormData()
  body.append('file', file, file.name)
  return request<string>('/avatar', {method: 'POST', body})
}

export function friendlyError(
  error: unknown,
  context: 'signin' | 'signup' | 'profile' | 'state',
): string {
  if (error instanceof ApiError) {
    if (error.status === 0) {
      return 'Нет связи с сервером. Проверьте подключение и попробуйте ещё раз.'
    }
    if (error.status === 401) {
      if (context === 'signin') return 'Неверный логин или пароль.'
      return 'Ссылка недействительна или устарела. Вернитесь в приложение и начните вход заново.'
    }
    if (error.status === 429) {
      return 'Слишком много попыток. Подождите немного и попробуйте ещё раз.'
    }
    if (error.status === 409) return 'Аккаунт с таким логином уже существует.'
    if (error.status >= 500) {
      if (/old password/i.test(error.message)) return 'Неверный старый пароль.'
      return 'Сервер не смог обработать запрос. Попробуйте ещё раз.'
    }
    return error.message || 'Не удалось выполнить запрос. Попробуйте ещё раз.'
  }

  return 'Что-то пошло не так. Попробуйте ещё раз.'
}
