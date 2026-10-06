// Temporary adapter for the existing DAS credential API. EAP BFF replaces this at G2.
export class LegacyAuthError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message)
    this.name = 'LegacyAuthError'
  }
}

export type LegacySession = {
  accessToken: string
  refreshToken: string
  user: {
    id: string
    fullName: string
    email: string | null
    roles: string[]
    departmentId?: string | null
    departmentName?: string | null
  }
}

const nonempty = (value: unknown): value is string => typeof value === 'string' && value.trim().length > 0

export async function requestLegacyLogin(baseUrl: string, username: string, password: string): Promise<LegacySession> {
  let response: Response
  try {
    response = await fetch(`${baseUrl}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password })
    })
  } catch {
    throw new LegacyAuthError(0, 'Không thể kết nối đến máy chủ. Vui lòng thử lại.')
  }
  if (!response.ok) {
    const message = response.status === 401 ? 'Tên đăng nhập hoặc mật khẩu không đúng.'
      : response.status === 403 ? 'Tài khoản không được phép truy cập.'
      : 'Dịch vụ đăng nhập đang không khả dụng. Vui lòng thử lại.'
    throw new LegacyAuthError(response.status, message)
  }
  let body: unknown
  try { body = await response.json() } catch {
    throw new LegacyAuthError(502, 'Phản hồi đăng nhập không hợp lệ. Vui lòng liên hệ quản trị viên.')
  }
  const envelope = body as { success?: unknown; data?: Partial<LegacySession> } | null
  const session = envelope?.data
  const user = session?.user
  if (envelope?.success !== true || !nonempty(session?.accessToken) || !nonempty(session?.refreshToken) ||
      !user || !nonempty(user.id) || !nonempty(user.fullName) ||
      !(user.email === null || typeof user.email === 'string') ||
      !Array.isArray(user.roles) || !user.roles.every(nonempty)) {
    throw new LegacyAuthError(502, 'Phản hồi đăng nhập không hợp lệ. Vui lòng liên hệ quản trị viên.')
  }
  return { accessToken: session.accessToken, refreshToken: session.refreshToken, user }
}
