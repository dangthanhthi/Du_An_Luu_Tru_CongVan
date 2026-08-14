export type UserRole = 'Admin' | 'Secretary' | 'SecretaryDirector' | 'Employee'

export type UsersType = {
  id: number | string
  role: string
  email: string
  status: string
  avatar: string
  company?: string
  country?: string
  contact?: string
  fullName: string
  username: string
  userName?: string
  currentPlan?: string
  avatarColor?: string
  billing?: string
  departmentId?: string
  isActive?: boolean
  createdAt?: string
}

export type UserType = UsersType
