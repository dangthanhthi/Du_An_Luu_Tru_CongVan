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

// Type Imports
import type { UsersType } from '@/types/apps/userTypes'
import type { Locale } from '@configs/i18n'

// Component Imports
import RoleCards from './RoleCards'
import RolesTable from './RolesTable'
import { tokenManager } from '@/services/api'
import { getLocalizedUrl } from '@/utils/i18n'

const Roles = ({ userData }: { userData?: UsersType[] }) => {
  const [role, setRole] = useState<string>('Admin')
  const { lang: locale } = useParams()

  useEffect(() => {
    const user = tokenManager.getUser()
    if (user && user.role) {
      setRole(user.role)
    }
  }, [])

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
                Chức năng Cấu hình Vai trò & Phân quyền chỉ dành riêng cho tài khoản <strong>Quản Trị Viên (Admin)</strong>.
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
        <Typography variant='h4' className='mbe-1'>
          Danh Sách Vai Trò & Quyền Hạn
        </Typography>
        <Typography>
          Quản lý quyền truy cập và phân bổ các phân hệ nghiệp vụ cho từng nhóm người dùng trong hệ thống DAS.
        </Typography>
      </Grid>
      <Grid size={{ xs: 12 }}>
        <RoleCards />
      </Grid>
      <Grid size={{ xs: 12 }} className='!pbs-12'>
        <Typography variant='h4' className='mbe-1'>
          Danh Sách Tài Khoản Theo Vai Trò
        </Typography>
        <Typography>Tổng hợp tất cả tài khoản người dùng và vai trò phân quyền tương ứng.</Typography>
      </Grid>
      <Grid size={{ xs: 12 }}>
        <RolesTable tableData={userData} />
      </Grid>
    </Grid>
  )
}

export default Roles
