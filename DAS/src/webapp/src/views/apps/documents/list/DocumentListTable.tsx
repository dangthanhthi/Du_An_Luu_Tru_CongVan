'use client'

// React Imports
import { useState, useEffect, useMemo } from 'react'

// Next Imports
import Link from 'next/link'
import { useParams, useSearchParams, useRouter } from 'next/navigation'

// MUI Imports
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Button from '@mui/material/Button'
import Typography from '@mui/material/Typography'
import Chip from '@mui/material/Chip'
import MenuItem from '@mui/material/MenuItem'
import Alert from '@mui/material/Alert'
import CircularProgress from '@mui/material/CircularProgress'
import Tabs from '@mui/material/Tabs'
import Tab from '@mui/material/Tab'
import Box from '@mui/material/Box'
import Divider from '@mui/material/Divider'

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
import type { ThemeColor } from '@core/types'
import type { DocumentType, DocumentStatus, DocumentDirection } from '@/types/apps/documentTypes'
import type { Locale } from '@configs/i18n'

// Component Imports
import OptionMenu from '@core/components/option-menu'
import TablePaginationComponent from '@components/TablePaginationComponent'
import CustomTextField from '@core/components/mui/TextField'
import { documentApi } from '@/services/api'
import { useAppDictionary } from '@/hooks/useDictionary'

// Util Imports
import { getLocalizedUrl } from '@/utils/i18n'

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

type DocumentTypeWithAction = DocumentType & {
  action?: string
}

const fuzzyFilter: FilterFn<any> = (row, columnId, value, addMeta) => {
  const itemRank = rankItem(row.getValue(columnId), value)
  addMeta({ itemRank })
  return itemRank.passed
}

const defaultDocuments: DocumentType[] = [
  // 1. CÔNG VĂN ĐẾN (INCOMING)
  { id: '1', documentNumber: 'CV-DEN-2026-0001', referenceNumber: '128/BGDĐT-GDĐH', title: 'Quyết định ban hành quy chế đào tạo và lưu trữ văn bản điện tử', direction: 'incoming', issuedDate: '10/08/2026', partnerName: 'Bộ Giáo dục và Đào tạo', status: 'completed', summary: 'Quy định về việc tiếp nhận và xử lý số hóa văn bản hành chính.' },
  { id: '2', documentNumber: 'CV-DEN-2026-0002', referenceNumber: 'HD-89/UBND', title: 'Hướng dẫn chuẩn hóa quy trình lưu trữ hồ sơ điện tử', direction: 'incoming', issuedDate: '12/08/2026', partnerName: 'Ủy ban Nhân dân TP Hà Nội', status: 'processing', summary: 'Tài liệu hướng dẫn chuyên môn cho cán bộ văn thư lưu trữ.' },
  { id: '3', documentNumber: 'CV-DEN-2026-0003', referenceNumber: 'KH-56/BGDĐT', title: 'Kế hoạch kiểm tra công tác lưu trữ công văn năm học 2026-2027', direction: 'incoming', issuedDate: '14/08/2026', partnerName: 'Bộ Giáo dục và Đào tạo', status: 'overdue', summary: 'Lịch thanh tra và tiêu chuẩn đánh giá xếp loại phòng văn thư.' },
  { id: '4', documentNumber: 'CV-DEN-2026-0004', referenceNumber: 'TM-12/VNU', title: 'Thư mời tham dự hội thảo khoa học Quản trị văn phòng hiện đại', direction: 'incoming', issuedDate: '15/08/2026', partnerName: 'Đại học Quốc gia Hà Nội', status: 'completed', summary: 'Hội thảo trao đổi kinh nghiệm ứng dụng công nghệ trong lưu trữ văn thư.' },
  { id: '5', documentNumber: 'CV-DEN-2026-0005', referenceNumber: 'CVPH-99/PTIT', title: 'Công văn phản hồi về việc tiếp nhận sinh viên thực tập ngành Văn thư', direction: 'incoming', issuedDate: '17/08/2026', partnerName: 'Học viện CNBC Viễn thông', status: 'processing', summary: 'Danh sách và kế hoạch phân công sinh viên thực tập tại trung tâm lưu trữ.' },

  // 2. CÔNG VĂN ĐI (OUTGOING)
  { id: '6', documentNumber: 'CV-DI-2026-0001', referenceNumber: 'TB-45/SGD-HN', title: 'Thông báo lịch trực nghỉ lễ Quốc khánh 02/09', direction: 'outgoing', issuedDate: '11/08/2026', partnerName: 'Sở Giáo dục và Đào tạo Hà Nội', status: 'pending', summary: 'Thông báo phân công lịch trực ban chỉ huy và bảo vệ cơ quan.' },
  { id: '7', documentNumber: 'CV-DI-2026-0002', referenceNumber: 'BC-204/DAS', title: 'Báo cáo tổng kết công tác chuyển đổi số tháng 7/2026', direction: 'outgoing', issuedDate: '13/08/2026', partnerName: 'Tập đoàn VNPT', status: 'completed', summary: 'Đánh giá tiến độ số hóa hồ sơ và ứng dụng AI OCR vào tiếp nhận công văn.' },
  { id: '8', documentNumber: 'CV-DI-2026-0003', referenceNumber: 'YCBG-08/DAS', title: 'Yêu cầu báo giá phần mềm số hóa và nhận diện chữ quang học', direction: 'outgoing', issuedDate: '16/08/2026', partnerName: 'Công ty Cổ phần Công nghệ ABC', status: 'pending', summary: 'Hồ sơ mời báo giá gói nâng cấp máy quét và máy chủ nhận diện OCR.' },

  // 3. CÔNG VĂN NỘI BỘ (INTERNAL)
  { id: '9', documentNumber: 'CV-NB-2026-0001', referenceNumber: 'QĐ-01/NB-DAS', title: 'Quyết định thành lập Tổ công tác chuyển đổi số và số hóa tài liệu', direction: 'internal', issuedDate: '01/08/2026', partnerName: 'Ban Giám Đốc & Các Phòng Ban', status: 'completed', summary: 'Kiện toàn nhân sự ban chỉ đạo triển khai hệ thống lưu trữ văn thư điện tử nội bộ.' },
  { id: '10', documentNumber: 'CV-NB-2026-0002', referenceNumber: 'TB-18/NB-VP', title: 'Thông báo hướng dẫn quy trình luân chuyển và ký duyệt hồ sơ điện tử', direction: 'internal', issuedDate: '05/08/2026', partnerName: 'Toàn thể Cán bộ - Nhân viên', status: 'completed', summary: 'Quy trình ký số và duyệt công văn nội bộ qua phần mềm DAS.' },
  { id: '11', documentNumber: 'CV-NB-2026-0003', referenceNumber: 'TTr-09/NB-TC', title: 'Tờ trình đề xuất trang bị bổ sung thiết bị quét văn bản tốc độ cao', direction: 'internal', issuedDate: '18/08/2026', partnerName: 'Phòng Tài Chính - Kế Hoạch', status: 'pending', summary: 'Dự toán kinh phí nâng cấp máy quét chuyên dụng cho phòng văn thư.' }
]

const columnHelper = createColumnHelper<DocumentTypeWithAction>()

const DocumentListTable = () => {
  const [data, setData] = useState<DocumentType[]>(defaultDocuments)
  const [loading, setLoading] = useState(false)
  const [status, setStatus] = useState<DocumentStatus | ''>('')
  
  const searchParams = useSearchParams()
  const router = useRouter()
  const urlType = searchParams.get('type') as DocumentDirection | null
  
  const [direction, setDirection] = useState<DocumentDirection | ''>(urlType || '')
  const [globalFilter, setGlobalFilter] = useState('')
  const [notification, setNotification] = useState<{ type: 'success' | 'error'; message: string } | null>(null)
  const { lang: locale } = useParams()
  const { t } = useAppDictionary()

  // Đồng bộ trạng thái Tab khi query parameter trên URL thay đổi
  useEffect(() => {
    if (urlType === 'incoming' || urlType === 'outgoing' || urlType === 'internal') {
      setDirection(urlType)
    } else if (urlType === null) {
      setDirection('')
    }
  }, [urlType])

  const handleTabChange = (_event: React.SyntheticEvent, newValue: string) => {
    const dir = newValue as DocumentDirection | ''
    setDirection(dir)
    
    // Cập nhật URL query params mượt mà
    const basePath = getLocalizedUrl('/apps/documents/list', locale as Locale)
    if (dir) {
      router.push(`${basePath}?type=${dir}`)
    } else {
      router.push(basePath)
    }
  }

  const statusMap: Record<string, { text: string; color: ThemeColor }> = {
    pending: { text: t.documents.pending, color: 'warning' },
    processing: { text: t.documents.processing, color: 'info' },
    completed: { text: t.documents.completed, color: 'success' },
    rejected: { text: t.documents.rejected, color: 'error' },
    overdue: { text: t.documents.overdue, color: 'error' }
  }

  // Fetch live documents from backend or persistent store
  useEffect(() => {
    const fetchDocs = async () => {
      setLoading(true)
      try {
        const res = await documentApi.getList({ pageSize: 100 })
        if (res?.success && res?.data) {
          const items = Array.isArray(res.data) ? res.data : res.data.items || []
          if (items.length > 0) {
            setData(items)
          }
        }
      } catch {}
      setLoading(false)
    }

    fetchDocs()
  }, [])

  // Đếm số lượng tài liệu theo từng phân loại
  const counts = useMemo(() => {
    return {
      all: data.length,
      incoming: data.filter(d => d.direction === 'incoming').length,
      outgoing: data.filter(d => d.direction === 'outgoing').length,
      internal: data.filter(d => d.direction === 'internal').length
    }
  }, [data])

  const columns = useMemo<ColumnDef<DocumentTypeWithAction, any>[]>(
    () => [
      columnHelper.accessor('documentNumber', {
        header: 'Số Nội Bộ / Số Đối Tác',
        cell: ({ row }) => (
          <div className='flex flex-col gap-1'>
            <Typography
              component={Link}
              href={getLocalizedUrl(`/apps/documents/${row.original.id}`, locale as Locale)}
              color='primary.main'
              sx={{ fontWeight: 700, fontSize: '0.92rem', '&:hover': { textDecoration: 'underline' } }}
            >
              {row.original.documentNumber}
            </Typography>
            {row.original.referenceNumber && (
              <div className='flex items-center gap-1'>
                <Chip
                  label={`Ref: ${row.original.referenceNumber}`}
                  size='small'
                  variant='tonal'
                  color='secondary'
                  sx={{ height: 20, fontSize: '0.72rem', px: 0.5 }}
                />
              </div>
            )}
          </div>
        )
      }),
      columnHelper.accessor('title', {
        header: t.documents.title,
        cell: ({ row }) => (
          <div className='flex flex-col max-w-[320px]'>
            <Typography variant='body2' className='font-medium line-clamp-2 text-textPrimary'>
              {row.original.title}
            </Typography>
            {row.original.summary && (
              <Typography variant='caption' color='text.secondary' className='line-clamp-1'>
                {row.original.summary}
              </Typography>
            )}
          </div>
        )
      }),
      columnHelper.accessor('direction', {
        header: t.documents.type,
        cell: ({ row }) => {
          const dir = row.original.direction
          if (dir === 'incoming') {
            return (
              <Chip
                label={t.documents.incoming || 'Công văn đến'}
                size='small'
                variant='tonal'
                color='primary'
                icon={<i className='tabler-arrow-down-left' />}
              />
            )
          } else if (dir === 'outgoing') {
            return (
              <Chip
                label={t.documents.outgoing || 'Công văn đi'}
                size='small'
                variant='tonal'
                color='success'
                icon={<i className='tabler-arrow-up-right' />}
              />
            )
          } else {
            return (
              <Chip
                label={t.documents.internal || 'Công văn nội bộ'}
                size='small'
                variant='tonal'
                color='info'
                icon={<i className='tabler-file-description' />}
              />
            )
          }
        }
      }),
      columnHelper.accessor('issuedDate', {
        header: t.documents.issuedDate,
        cell: ({ row }) => (
          <Typography variant='body2' color='text.secondary'>
            {row.original.issuedDate}
          </Typography>
        )
      }),
      columnHelper.accessor('partnerName', {
        header: t.documents.partner,
        cell: ({ row }) => (
          <Typography variant='body2' className='font-medium'>
            {row.original.partnerName || '—'}
          </Typography>
        )
      }),
      columnHelper.accessor('status', {
        header: t.documents.status,
        cell: ({ row }) => {
          const st = statusMap[row.original.status] || { text: row.original.status, color: 'default' }
          return <Chip label={st.text} size='small' color={st.color} />
        }
      }),
      columnHelper.display({
        id: 'action',
        header: t.documents.actions,
        cell: ({ row }) => (
          <OptionMenu
            iconButtonProps={{ size: 'medium' }}
            iconClassName='tabler-dots-vertical text-xl'
            options={[
              {
                text: t.documents.viewDetail,
                icon: 'tabler-eye',
                href: getLocalizedUrl(`/apps/documents/${row.original.id}`, locale as Locale),
                linkProps: { component: Link }
              },
              {
                text: t.documents.edit,
                icon: 'tabler-edit',
                href: getLocalizedUrl(`/apps/documents/edit/${row.original.id}`, locale as Locale),
                linkProps: { component: Link }
              },
              {
                text: t.documents.delete,
                icon: 'tabler-trash',
                menuItemProps: {
                  className: 'text-error',
                  onClick: async () => {
                    await documentApi.delete(row.original.id)
                    setData(prev => prev.filter(d => d.id !== row.original.id))
                    setNotification({ type: 'success', message: `${t.documents.delete} #${row.original.documentNumber}` })
                  }
                }
              }
            ]}
          />
        )
      })
    ],
    [locale, t]
  )

  const filteredData = useMemo(() => {
    return data.filter(item => {
      if (status && item.status !== status) return false
      if (direction && item.direction !== direction) return false
      if (globalFilter) {
        const query = globalFilter.toLowerCase()
        return (
          item.documentNumber.toLowerCase().includes(query) ||
          (item.referenceNumber && item.referenceNumber.toLowerCase().includes(query)) ||
          item.title.toLowerCase().includes(query) ||
          (item.partnerName && item.partnerName.toLowerCase().includes(query)) ||
          (item.summary && item.summary.toLowerCase().includes(query))
        )
      }
      return true
    })
  }, [data, status, direction, globalFilter])

  const table = useReactTable({
    data: filteredData,
    columns,
    filterFns: {
      fuzzy: fuzzyFilter
    },
    state: {
      globalFilter
    },
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getPaginationRowModel: getPaginationRowModel()
  })

  return (
    <>
      {notification && (
        <Alert severity={notification.type} className='mbe-4' onClose={() => setNotification(null)}>
          {notification.message}
        </Alert>
      )}

      <Card>
        {/* 1. TABS PHÂN CHIA 3 PHÂN HỆ CÔNG VĂN RÕ RÀNG */}
        <Box sx={{ borderBottom: 1, borderColor: 'divider', px: 4, pt: 2 }}>
          <Tabs
            value={direction}
            onChange={handleTabChange}
            variant='scrollable'
            scrollButtons='auto'
            textColor='primary'
            indicatorColor='primary'
          >
            <Tab
              value=''
              icon={<i className='tabler-files text-lg' />}
              iconPosition='start'
              label={
                <div className='flex items-center gap-2'>
                  <span>{t.nav.allDocs || 'Tất Cả'}</span>
                  <Chip label={counts.all} size='small' variant='tonal' color='default' />
                </div>
              }
            />
            <Tab
              value='incoming'
              icon={<i className='tabler-arrow-down-left text-lg text-primary' />}
              iconPosition='start'
              label={
                <div className='flex items-center gap-2'>
                  <span>{t.nav.incomingDocs || 'Công Văn Đến'}</span>
                  <Chip label={counts.incoming} size='small' variant='tonal' color='primary' />
                </div>
              }
            />
            <Tab
              value='outgoing'
              icon={<i className='tabler-arrow-up-right text-lg text-success' />}
              iconPosition='start'
              label={
                <div className='flex items-center gap-2'>
                  <span>{t.nav.outgoingDocs || 'Công Văn Đi'}</span>
                  <Chip label={counts.outgoing} size='small' variant='tonal' color='success' />
                </div>
              }
            />
            <Tab
              value='internal'
              icon={<i className='tabler-file-description text-lg text-info' />}
              iconPosition='start'
              label={
                <div className='flex items-center gap-2'>
                  <span>{t.nav.internalDocs || 'Công Văn Nội Bộ'}</span>
                  <Chip label={counts.internal} size='small' variant='tonal' color='info' />
                </div>
              }
            />
          </Tabs>
        </Box>

        {/* 2. BỘ LỌC TÌM KIẾM & NÚT THAO TÁC */}
        <CardContent className='flex flex-wrap items-center justify-between gap-4'>
          <div className='flex flex-wrap items-center gap-4'>
            <CustomTextField
              placeholder={t.documents.searchPlaceholder || 'Tìm kiếm số hiệu, tiêu đề, đối tác...'}
              value={globalFilter}
              onChange={e => setGlobalFilter(e.target.value)}
              className='is-full sm:is-auto min-is-[240px]'
              InputProps={{
                startAdornment: <i className='tabler-search text-xl mie-2 text-textSecondary' />
              }}
            />
            <CustomTextField
              select
              value={status}
              onChange={e => setStatus(e.target.value as DocumentStatus | '')}
              className='is-full sm:is-auto min-is-[160px]'
              SelectProps={{ displayEmpty: true }}
            >
              <MenuItem value=''>{t.documents.allStatus}</MenuItem>
              <MenuItem value='pending'>{t.documents.pending}</MenuItem>
              <MenuItem value='processing'>{t.documents.processing}</MenuItem>
              <MenuItem value='completed'>{t.documents.completed}</MenuItem>
              <MenuItem value='overdue'>{t.documents.overdue}</MenuItem>
              <MenuItem value='rejected'>{t.documents.rejected}</MenuItem>
            </CustomTextField>
          </div>

          <div className='flex items-center gap-3'>
            <Button
              variant='outlined'
              color='primary'
              component={Link}
              href={getLocalizedUrl('/apps/email-integration', locale as Locale)}
              startIcon={<i className='tabler-mail-spark' />}
            >
              {t.documents.scanEmail}
            </Button>
            <Button
              variant='contained'
              component={Link}
              href={getLocalizedUrl('/apps/documents/add', locale as Locale)}
              startIcon={<i className='tabler-plus' />}
            >
              {t.documents.addDoc}
            </Button>
          </div>
        </CardContent>

        {/* 3. BẢNG DỮ LIỆU CÔNG VĂN */}
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
                      {t.documents.emptyData}
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
    </>
  )
}

export default DocumentListTable

