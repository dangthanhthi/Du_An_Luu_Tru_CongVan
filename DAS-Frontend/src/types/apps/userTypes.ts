export type UserRole = 'Admin' | 'Secretary' | 'SecretaryDirector' | 'Employee'

export type UserType = {
  id: string | number
  userName?: string
  username?: string
  fullName: string
  email: string
  departmentId?: string
  role: UserRole | string
  isActive?: boolean
  status?: string
  createdAt?: string
  company?: string
  country?: string
  contact?: string
  currentPlan?: string
  avatar?: string
  avatarColor?: string
  billing?: string
}

export type UsersType = UserType
