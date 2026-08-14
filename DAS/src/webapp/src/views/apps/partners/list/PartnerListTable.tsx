'use client'

// React Imports
import { useState, useMemo, useEffect } from 'react'

// MUI Imports
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Button from '@mui/material/Button'
import Typography from '@mui/material/Typography'
import Chip from '@mui/material/Chip'
import MenuItem from '@mui/material/MenuItem'
import Alert from '@mui/material/Alert'
import CircularProgress from '@mui/material/CircularProgress'

// Third-party Imports
import { rankItem } from '@tanstack/match-sorter-utils'
import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  useReactTable,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel
} from '@tanstack/react-table'
import type { ColumnDef, FilterFn } from '@tanstack/react-table'
import type { RankingInfo } from '@tanstack/match-sorter-utils'

// Type Imports
import type { PartnerType } from '@/types/apps/partnerTypes'

// Component Imports
import OptionMenu from '@core/components/option-menu'
import TablePaginationComponent from '@components/TablePaginationComponent'
import CustomTextField from '@core/components/mui/TextField'
import AddPartnerDrawer from './AddPartnerDrawer'
import { partnerApi } from '@/services/api'
import { useAppDictionary } from '@/hooks/useDictionary'

// Style Imports
import tableStyles from '@core/styles/table.module.css'

declare module '@tanstack/react-table' {
  interface FilterFns {
    fuzzy: FilterFn<unknown>
  }
  interface FilterMeta {
    itemRank: RankingInfo
  }
}

type PartnerTypeWithAction = PartnerType & {
  action?: string
}

const fuzzyFilter: FilterFn<any> = (row, columnId, value, addMeta) => {
  const itemRank = rankItem(row.getValue(columnId), value)
  addMeta({ itemRank })
  return itemRank.passed
}

const defaultPartners: PartnerType[] = [
  { id: '1', code: 'BGDDT', fullName: 'Bộ Giáo dục và Đào tạo', shortName: 'BGDĐT', taxCode: '0100107518', email: 'bogddt@moet.gov.vn', phone: '024.38695144', address: '35 Đại Cồ Việt, Hai Bà Trưng, Hà Nội', entityType: 'Cơ quan Nhà nước', isActive: true },
  { id: '2', code: 'UBND-HN', fullName: 'Ủy ban Nhân dân Thành phố Hà Nội', shortName: 'UBND TP Hà Nội', taxCode: '0100781290', email: 'ubnd@hanoi.gov.vn', phone: '024.38253536', address: '12 Lê Lai, Hoàn Kiếm, Hà Nội', entityType: 'Cơ quan Nhà nước', isActive: true },
  { id: '3', code: 'VNPT', fullName: 'Tập đoàn Bưu chính Viễn thông Việt Nam', shortName: 'VNPT', taxCode: '0100684378', email: 'vanphong@vnpt.vn', phone: '024.37741091', address: '57 Huỳnh Thúc Kháng, Đống Đa, Hà Nội', entityType: 'Doanh nghiệp', isActive: true },
  { id: '4', code: 'VNU', fullName: 'Đại học Quốc gia Hà Nội', shortName: 'ĐHQGHN', taxCode: '0100779951', email: 'vanphong@vnu.edu.vn', phone: '024.37547670', address: '144 Xuân Thủy, Cầu Giấy, Hà Nội', entityType: 'Đơn vị sự nghiệp', isActive: true },
  { id: '5', code: 'ABCTECH', fullName: 'Công ty Cổ phần Công nghệ ABC', shortName: 'ABC Tech', taxCode: '0108992145', email: 'contact@abctech.vn', phone: '024.66889900', address: 'Tầng 12, Tòa nhà Keangnam, Mễ Trì, Nam Từ Liêm, Hà Nội', entityType: 'Doanh nghiệp', isActive: true }
]

const columnHelper = createColumnHelper<PartnerTypeWithAction>()

const PartnerListTable = () => {
  const [data, setData] = useState<PartnerType[]>(defaultPartners)
  const [loading, setLoading] = useState(false)
  const [addPartnerOpen, setAddPartnerOpen] = useState(false)
  const [globalFilter, setGlobalFilter] = useState('')
  const [entityTypeFilter, setEntityTypeFilter] = useState('')
  const [notification, setNotification] = useState<{ type: 'success' | 'error'; message: string } | null>(null)
  const { t, isEn } = useAppDictionary()

  useEffect(() => {
    const fetchPartners = async () => {
      setLoading(true)
      try {
        const res = await partnerApi.getList({ pageSize: 100 })
        if (res?.success && res?.data) {
          const items = Array.isArray(res.data) ? res.data : res.data.items || []
          if (items.length > 0) {
            setData(items)
          }
        }
      } catch {}
      setLoading(false)
    }

    fetchPartners()
  }, [])

  const columns = useMemo<ColumnDef<PartnerTypeWithAction, any>[]>(
    () => [
      columnHelper.accessor('code', {
        header: t.partners.partnerCode,
        cell: ({ row }) => (
          <Typography color='primary.main' sx={{ fontWeight: 600 }}>
            {row.original.code}
          </Typography>
        )
      }),
      columnHelper.accessor('fullName', {
        header: t.partners.partnerName,
        cell: ({ row }) => (
          <div className='flex flex-col'>
            <Typography variant='body2' className='font-medium text-textPrimary'>
              {row.original.fullName}
            </Typography>
            {row.original.shortName && (
              <Typography variant='caption' color='text.secondary'>
                ({row.original.shortName})
              </Typography>
            )}
          </div>
        )
      }),
      columnHelper.accessor('entityType', {
        header: t.partners.entityType,
        cell: ({ row }) => (
          <Chip
            label={row.original.entityType || (isEn ? 'Government' : 'Cơ quan')}
            size='small'
            variant='tonal'
            color={row.original.entityType === 'Doanh nghiệp' ? 'info' : 'primary'}
          />
        )
      }),
      columnHelper.accessor('email', {
        header: t.partners.email,
        cell: ({ row }) => (
          <Typography variant='body2'>{row.original.email || '—'}</Typography>
        )
      }),
      columnHelper.accessor('phone', {
        header: t.partners.phone,
        cell: ({ row }) => (
          <Typography variant='body2'>{row.original.phone || '—'}</Typography>
        )
      }),
      columnHelper.accessor('isActive', {
        header: t.partners.status,
        cell: ({ row }) => (
          <Chip
            label={row.original.isActive ? t.partners.active : t.partners.inactive}
            size='small'
            color={row.original.isActive ? 'success' : 'secondary'}
          />
        )
      }),
      columnHelper.display({
        id: 'action',
        header: t.documents.actions,
        cell: ({ row }) => (
          <OptionMenu
            iconButtonProps={{ size: 'medium' }}
            iconClassName='tabler-dots-vertical text-xl'
            options={[
              { text: t.documents.viewDetail, icon: 'tabler-eye' },
              { text: t.documents.edit, icon: 'tabler-edit' },
              {
                text: t.documents.delete,
                icon: 'tabler-trash',
                menuItemProps: {
                  className: 'text-error',
                  onClick: async () => {
                    await partnerApi.delete(row.original.id)
                    setData(prev => prev.filter(p => p.id !== row.original.id))
                    setNotification({ type: 'success', message: `${t.documents.delete}: '${row.original.fullName}'` })
                  }
                }
              }
            ]}
          />
        )
      })
    ],
    [t, isEn]
  )

  const filteredData = useMemo(() => {
    return data.filter(item => {
      if (entityTypeFilter && item.entityType !== entityTypeFilter) return false
      if (globalFilter) {
        const query = globalFilter.toLowerCase()
        return (
          item.fullName.toLowerCase().includes(query) ||
          item.shortName.toLowerCase().includes(query) ||
          (item.email && item.email.toLowerCase().includes(query)) ||
          (item.phone && item.phone.includes(query))
        )
      }
      return true
    })
  }, [data, globalFilter, entityTypeFilter])

  const table = useReactTable({
    data: filteredData,
    columns,
    filterFns: { fuzzy: fuzzyFilter },
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    initialState: { pagination: { pageSize: 10 } }
  })

  return (
    <>
      {notification && (
        <Alert severity={notification.type} className='mbe-4' onClose={() => setNotification(null)}>
          {notification.message}
        </Alert>
      )}

      <Card>
        <CardContent className='flex flex-wrap items-center justify-between gap-4'>
          <div className='flex flex-wrap items-center gap-4'>
            <CustomTextField
              placeholder={t.partners.searchPlaceholder}
              value={globalFilter}
              onChange={e => setGlobalFilter(e.target.value)}
              className='is-full sm:is-auto'
            />
            <CustomTextField
              select
              value={entityTypeFilter}
              onChange={e => setEntityTypeFilter(e.target.value)}
              className='is-full sm:is-auto min-is-[180px]'
              SelectProps={{ displayEmpty: true }}
            >
              <MenuItem value=''>{isEn ? 'All Entity Types' : 'Tất cả loại hình'}</MenuItem>
              <MenuItem value='Cơ quan Nhà nước'>{t.partners.government}</MenuItem>
              <MenuItem value='Doanh nghiệp'>{t.partners.enterprise}</MenuItem>
              <MenuItem value='Đơn vị sự nghiệp'>{t.partners.organization}</MenuItem>
            </CustomTextField>
          </div>
          <Button
            variant='contained'
            startIcon={<i className='tabler-plus' />}
            onClick={() => setAddPartnerOpen(true)}
          >
            {t.partners.addPartner}
          </Button>
        </CardContent>

        <div className='overflow-x-auto'>
          {loading ? (
            <div className='flex justify-center p-8'>
              <CircularProgress />
            </div>
          ) : (
            <table className={tableStyles.table}>
              <thead>
                {table.getHeaderGroups().map(headerGroup => (
                  <tr key={headerGroup.id}>
                    {headerGroup.headers.map(header => (
                      <th key={header.id}>
                        {header.isPlaceholder
                          ? null
                          : flexRender(header.column.columnDef.header, header.getContext())}
                      </th>
                    ))}
                  </tr>
                ))}
              </thead>
              <tbody>
                {table.getRowModel().rows.length === 0 ? (
                  <tr>
                    <td colSpan={columns.length} className='text-center py-6'>
                      {isEn ? 'No partner data found' : 'Không tìm thấy dữ liệu đối tác'}
                    </td>
                  </tr>
                ) : (
                  table.getRowModel().rows.map(row => (
                    <tr key={row.id}>
                      {row.getVisibleCells().map(cell => (
                        <td key={cell.id}>
                          {flexRender(cell.column.columnDef.cell, cell.getContext())}
                        </td>
                      ))}
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          )}
        </div>

        <TablePaginationComponent table={table} />
      </Card>

      <AddPartnerDrawer
        open={addPartnerOpen}
        handleClose={() => setAddPartnerOpen(false)}
        onAddPartner={async newPartner => {
          try {
            const res = await partnerApi.create({
              fullName: newPartner.fullName,
              shortName: newPartner.shortName,
              taxCode: newPartner.taxCode,
              email: newPartner.email,
              phone: newPartner.phone,
              address: newPartner.address,
              entityType: newPartner.entityType,
              isActive: true
            })
            if (res?.data) {
              setData(prev => [res.data, ...prev])
            }
          } catch {}
          setAddPartnerOpen(false)
          setNotification({ type: 'success', message: `${t.partners.addPartner}: '${newPartner.fullName}'` })
        }}
      />
    </>
  )
}

export default PartnerListTable
