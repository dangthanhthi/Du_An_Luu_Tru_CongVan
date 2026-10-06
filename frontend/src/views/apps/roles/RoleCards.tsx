'use client'

// MUI Imports
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Grid from '@mui/material/Grid'
import Typography from '@mui/material/Typography'
import Avatar from '@mui/material/Avatar'
import AvatarGroup from '@mui/material/AvatarGroup'
import Chip from '@mui/material/Chip'

type RoleCardType = {
  title: string
  roleKey: string
  totalUsers: number
  description: string
  color: 'error' | 'primary' | 'warning' | 'info'
  avatars: string[]
}

const dasRoles: RoleCardType[] = [
  {
    title: 'Quản Trị Viên (Admin)',
    roleKey: 'Admin',
    totalUsers: 1,
    description: 'Toàn quyền cấu hình hệ thống, quản lý người dùng, phân quyền và đối tác.',
    color: 'error',
    avatars: ['1.png']
  },
  {
    title: 'Văn Thư / Thư Ký (Secretary)',
    roleKey: 'Secretary',
    totalUsers: 2,
    description: 'Tiếp nhận công văn đến, quét AI OCR bóc tách, tạo công văn đi, chuyển xử lý phòng ban.',
    color: 'primary',
    avatars: ['2.png', '3.png']
  },
  {
    title: 'Thư Ký Ban Giám Đốc (SecretaryDirector)',
    roleKey: 'SecretaryDirector',
    totalUsers: 1,
    description: 'Trình ký văn bản, phê duyệt/từ chối công văn, khôi phục đối tác đã xóa.',
    color: 'warning',
    avatars: ['4.png']
  },
  {
    title: 'Chuyên Viên / Nhân Viên (Employee)',
    roleKey: 'Employee',
    totalUsers: 5,
    description: 'Xem công văn được phân công, thực hiện xử lý công việc và báo cáo tiến độ.',
    color: 'info',
    avatars: ['5.png', '6.png', '7.png']
  }
]

const RoleCards = () => {
  return (
    <Grid container spacing={6}>
      {dasRoles.map((item, index) => (
        <Grid size={{ xs: 12, sm: 6, lg: 6 }} key={index}>
          <Card className='h-full'>
            <CardContent className='flex flex-col justify-between h-full gap-4'>
              <div className='flex items-center justify-between'>
                <Chip label={item.roleKey} color={item.color} size='small' variant='tonal' />
                <AvatarGroup total={item.totalUsers}>
                  {item.avatars.map((img, i) => (
                    <Avatar key={i} alt={item.title} src={`/images/avatars/${img}`} />
                  ))}
                </AvatarGroup>
              </div>

              <div>
                <Typography variant='h5' className='font-semibold mbe-1'>
                  {item.title}
                </Typography>
                <Typography variant='body2' color='text.secondary'>
                  {item.description}
                </Typography>
              </div>

              <div className='flex items-center justify-between pt-2 border-t border-divider text-xs text-textSecondary'>
                <span>Quy mô: {item.totalUsers} nhân sự</span>
                <span className='text-primary font-medium'>Đang hoạt động</span>
              </div>
            </CardContent>
          </Card>
        </Grid>
      ))}
    </Grid>
  )
}

export default RoleCards
