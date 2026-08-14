export type UserRole = 'Admin' | 'Secretary' | 'SecretaryDirector' | 'Employee'

export type UserType = {
  id: string
  userName: string
  fullName: string
  email: string
  departmentId?: string
  role: UserRole
  isActive: boolean
  createdAt: string
}
