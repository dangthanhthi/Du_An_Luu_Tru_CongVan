export type UserMenuIdentity = { name: string | null; role: string | null }

// Presentation only; cached identity never grants DAS resource capabilities.
export const getUserMenuIdentity = (hasToken: boolean, stored: unknown): UserMenuIdentity | null => {
  if (!hasToken || !stored || typeof stored !== 'object' || Array.isArray(stored)) return null
  const user = stored as Record<string, unknown>
  const text = (value: unknown) => typeof value === 'string' && value.trim() ? value.trim() : null

  return { name: text(user.fullName) || text(user.userName), role: text(user.role) }
}
