'use client'

// React Imports
import { useState } from 'react'

// MUI Imports
import Drawer from '@mui/material/Drawer'
import Button from '@mui/material/Button'
import Typography from '@mui/material/Typography'
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Divider from '@mui/material/Divider'

// Component Imports
import CustomTextField from '@core/components/mui/TextField'
import type { PartnerType } from '@/types/apps/partnerTypes'

type Props = {
  open: boolean
  handleClose: () => void
  onAddPartner?: (partner: PartnerType) => void
}

const initialForm = {
  fullName: '',
  shortName: '',
  entityType: 'Cơ quan Nhà nước',
  email: '',
  phone: '',
  address: '',
  taxCode: ''
}

const AddPartnerDrawer = ({ open, handleClose, onAddPartner }: Props) => {
  const [formData, setFormData] = useState(initialForm)

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!formData.fullName || !formData.shortName) return

    const newPartner: PartnerType = {
      id: String(Date.now()),
      fullName: formData.fullName,
      shortName: formData.shortName,
      entityType: formData.entityType,
      email: formData.email || undefined,
      phone: formData.phone || undefined,
      address: formData.address || undefined,
      taxCode: formData.taxCode || undefined,
      isActive: true,
      isDeleted: false,
      createdAt: new Date().toISOString().split('T')[0]
    }

    onAddPartner?.(newPartner)
    setFormData(initialForm)
    handleClose()
  }

  const handleReset = () => {
    setFormData(initialForm)
  }

  return (
    <Drawer
      open={open}
      anchor='right'
      variant='temporary'
      onClose={handleClose}
      ModalProps={{ keepMounted: true }}
      sx={{ '& .MuiDrawer-paper': { width: { xs: 320, sm: 400 } } }}
    >
      <div className='flex items-center justify-between plb-5 pli-6'>
        <Typography variant='h5'>Thêm Đối Tác Mới</Typography>
        <IconButton size='small' onClick={handleClose}>
          <i className='tabler-x text-2xl text-textPrimary' />
        </IconButton>
      </div>
      <Divider />
      <div className='p-6'>
        <form onSubmit={handleSubmit} className='flex flex-col gap-5'>
          <CustomTextField
            fullWidth
            label='Tên Đầy Đủ *'
            placeholder='VD: Sở Giáo dục và Đào tạo Hà Nội'
            value={formData.fullName}
            onChange={e => setFormData({ ...formData, fullName: e.target.value })}
            required
          />
          <CustomTextField
            fullWidth
            label='Tên Viết Tắt *'
            placeholder='VD: SGD-HN'
            value={formData.shortName}
            onChange={e => setFormData({ ...formData, shortName: e.target.value })}
            required
          />
          <CustomTextField
            select
            fullWidth
            label='Loại Tổ Chức'
            value={formData.entityType}
            onChange={e => setFormData({ ...formData, entityType: e.target.value })}
          >
            <MenuItem value='Cơ quan Nhà nước'>Cơ quan Nhà nước</MenuItem>
            <MenuItem value='Bộ ban ngành'>Bộ ban ngành</MenuItem>
            <MenuItem value='Doanh nghiệp'>Doanh nghiệp</MenuItem>
            <MenuItem value='Đơn vị sự nghiệp'>Đơn vị sự nghiệp</MenuItem>
          </CustomTextField>
          <CustomTextField
            fullWidth
            type='email'
            label='Email'
            placeholder='contact@agency.gov.vn'
            value={formData.email}
            onChange={e => setFormData({ ...formData, email: e.target.value })}
          />
          <CustomTextField
            fullWidth
            label='Số Điện Thoại'
            placeholder='024 3825 3527'
            value={formData.phone}
            onChange={e => setFormData({ ...formData, phone: e.target.value })}
          />
          <CustomTextField
            fullWidth
            label='Mã Số Thuế'
            placeholder='0100109106'
            value={formData.taxCode}
            onChange={e => setFormData({ ...formData, taxCode: e.target.value })}
          />
          <CustomTextField
            fullWidth
            multiline
            rows={3}
            label='Địa Chỉ'
            placeholder='Số nhà, đường, quận/huyện, tỉnh/TP'
            value={formData.address}
            onChange={e => setFormData({ ...formData, address: e.target.value })}
          />

          <div className='flex items-center gap-4 mbs-2'>
            <Button variant='contained' type='submit'>
              Thêm Đối Tác
            </Button>
            <Button variant='tonal' color='secondary' type='reset' onClick={handleReset}>
              Nhập Lại
            </Button>
          </div>
        </form>
      </div>
    </Drawer>
  )
}

export default AddPartnerDrawer
