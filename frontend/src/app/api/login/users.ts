// Official System User Table definition
export type UserTable = {
  id: number
  name: string
  email: string
  image: string
  role?: string
}

export const users: UserTable[] = [
  {
    id: 1,
    name: 'Quản trị viên Hệ thống',
    email: 'admin@das.vn',
    image: '/images/avatars/1.png',
    role: 'Admin'
  },
  {
    id: 2,
    name: 'Cán bộ Văn thư - Thư ký',
    email: 'vanthu@das.vn',
    image: '/images/avatars/2.png',
    role: 'Secretary'
  },
  {
    id: 3,
    name: 'Chuyên viên Quản lý Văn bản',
    email: 'chuyenvien@das.vn',
    image: '/images/avatars/3.png',
    role: 'Employee'
  }
]
