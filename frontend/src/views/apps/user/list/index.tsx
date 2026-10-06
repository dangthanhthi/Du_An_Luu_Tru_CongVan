'use client'

// React Imports
import { useState, useEffect } from 'react'
import Link from 'next/link'
import { useParams } from 'next/navigation'

// MUI Imports
import Grid from '@mui/material/Grid'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import Alert from '@mui/material/Alert'

// Type Imports
import type { UsersType } from '@/types/apps/userTypes'
import type { Locale } from '@configs/i18n'

// Component Imports
import UserListTable from './UserListTable'
import UserListCards from './UserListCards'
import { tokenManager } from '@/services/api'
import { getLocalizedUrl } from '@/utils/i18n'

const UserList = ({ userData }: { userData?: UsersType[] }) => {
  const [role, setRole] = useState<string>('Admin')
  const { lang: locale } = useParams()

  useEffect(() => {
    const user = tokenManager.getUser()
    if (user && user.role) {
      setRole(user.role)
    }
  }, [])

  // Page-level Authorization Guard: Only Admin can access
  if (role !== 'Admin') {
    return (
      <Grid container spacing={6}>
        <Grid size={{ xs: 12 }}>
          <Card>
            <CardContent className='flex flex-col items-center justify-center p-12 text-center'>
              <div className='w-16 h-16 rounded-full bg-error/10 flex items-center justify-center text-error mbe-4'>
                <i className='tabler-lock-square-rounded text-3xl' />
              </div>
              <Typography variant='h5' className='mbe-2 font-semibold'>
                Không Có Quyền Truy Cập (403 Forbidden)
              </Typography>
              <Typography variant='body2' color='text.secondary' className='max-w-[480px] mbe-6'>
                Chức năng Quản lý Người dùng & Hệ thống chỉ dành riêng cho tài khoản có vai trò <strong>Quản Trị Viên (Admin)</strong>.
              </Typography>
              <Button
                variant='contained'
                component={Link}
                href={getLocalizedUrl('/dashboards/overview', locale as Locale)}
                startIcon={<i className='tabler-arrow-left' />}
              >
                Quay Lại Bảng Điều Khiển
              </Button>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    )
  }

  return (
    <Grid container spacing={6}>
      <Grid size={{ xs: 12 }}>
        <UserListCards />
      </Grid>
      <Grid size={{ xs: 12 }}>
        <UserListTable tableData={userData} />
      </Grid>
    </Grid>
  )
}

export default UserList
