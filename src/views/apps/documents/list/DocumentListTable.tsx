'use client'

// React Imports
import { useState, useEffect, useMemo } from 'react'

// Next Imports
import Link from 'next/link'
import { useParams } from 'next/navigation'

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
  { id: '1', documentNumber: 'CV-128/BGDDT', title: 'Quyết định ban hành quy chế đào tạo và lưu trữ văn bản điện tử', direction: 'incoming', issuedDate: '10/08/2026', partnerName: 'Bộ Giáo dục và Đào tạo', status: 'completed', summary: 'Quy định về việc tiếp nhận và xử lý số hóa văn bản hành chính.' },
  { id: '2', documentNumber: 'TB-45/SGD-HN', title: 'Thông báo lịch trực nghỉ lễ Quốc khánh 02/09', direction: 'outgoing', issuedDate: '11/08/2026', partnerName: 'Sở Giáo dục và Đào tạo Hà Nội', status: 'pending', summary: 'Thông báo phân công lịch trực ban chỉ huy và bảo vệ cơ quan.' },
  { id: '3', documentNumber: 'HD-89/UBND', title: 'Hướng dẫn chuẩn hóa quy trình lưu trữ hồ sơ điện tử', direction: 'incoming', issuedDate: '12/08/2026', partnerName: 'Ủy ban Nhân dân TP Hà Nội', status: 'processing', summary: 'Tài liệu hướng dẫn chuyên môn cho cán bộ văn thư lưu trữ.' },
  { id: '4', documentNumber: 'BC-204/DAS', title: 'Báo cáo tổng kết công tác chuyển đổi số tháng 7/2026', direction: 'outgoing', issuedDate: '13/08/2026', partnerName: 'Tập đoàn VNPT', status: 'completed', summary: 'Đánh giá tiến độ số hóa hồ sơ và ứng dụng AI OCR vào tiếp nhận công văn.' },
  { id: '5', documentNumber: 'KH-56/BGDDT', title: 'Kế hoạch kiểm tra công tác lưu trữ công văn năm học 2026-2027', direction: 'incoming', issuedDate: '14/08/2026', partnerName: 'Bộ Giáo dục và Đào tạo', status: 'overdue', summary: 'Lịch thanh tra và tiêu chuẩn đánh giá xếp loại phòng văn thư.' },
  { id: '6', documentNumber: 'TM-12/VNU', title: 'Thư mời tham dự hội thảo khoa học Quản trị văn phòng hiện đại', direction: 'incoming', issuedDate: '15/08/2026', partnerName: 'Đại học Quốc gia Hà Nội', status: 'completed', summary: 'Hội thảo trao đổi kinh nghiệm ứng dụng công nghệ trong lưu trữ văn thư.' },
  { id: '7', documentNumber: 'YCBG-08/DAS', title: 'Yêu cầu báo giá phần mềm số hóa và nhận diện chữ quang học', direction: 'outgoing', issuedDate: '16/08/2026', partnerName: 'Công ty Cổ phần Công nghệ ABC', status: 'pending', summary: 'Hồ sơ mời báo giá gói nâng cấp máy quét và máy chủ nhận diện OCR.' },
  { id: '8', documentNumber: 'CVPH-99/PTIT', title: 'Công văn phản hồi về việc tiếp nhận sinh viên thực tập ngành Văn thư', direction: 'incoming', issuedDate: '17/08/2026', partnerName: 'Học viện CNBC Viễn thông', status: 'processing', summary: 'Danh sách và kế hoạch phân công sinh viên thực tập tại trung tâm lưu trữ.' }
]

const columnHelper = createColumnHelper<DocumentTypeWithAction>()

const DocumentListTable = () => {
  const [data, setData] = useState<DocumentType[]>(defaultDocuments)
  const [loading, setLoading] = useState(false)
  const [status, setStatus] = useState<DocumentStatus | ''>('')
  const [direction, setDirection] = useState<DocumentDirection | ''>('')
  const [globalFilter, setGlobalFilter] = useState('')
  const [notification, setNotification] = useState<{ type: 'success' | 'error'; message: string } | null>(null)
  const { lang: locale } = useParams()
  const { t } = useAppDictionary()

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

  const columns = useMemo<ColumnDef<DocumentTypeWithAction, any>[]>(
    () => [
      columnHelper.accessor('documentNumber', {
        header: t.documents.docNumber,
        cell: ({ row }) => (
          <Typography
            component={Link}
            href={getLocalizedUrl(`/apps/documents/${row.original.id}`, locale as Locale)}
            color='primary.main'
            sx={{ fontWeight: 600, '&:hover': { textDecoration: 'underline' } }}
          >
            {row.original.documentNumber}
          </Typography>
        )
      }),
      columnHelper.accessor('title', {
        header: t.documents.title,
        cell: ({ row }) => (
          <div className='flex flex-col max-w-[320px]'>
            <Typography variant='body2' className='font-medium line-clamp-2 text-textPrimary'>
              {row.original.title}
            </Typography>
          </div>
        )
      }),
      columnHelper.accessor('direction', {
        header: t.documents.type,
        cell: ({ row }) => {
          const isIncoming = row.original.direction === 'incoming'
          return (
            <Chip
              label={isIncoming ? t.documents.incoming : t.documents.outgoing}
              size='small'
              variant='tonal'
              color={isIncoming ? 'primary' : 'success'}
              icon={<i className={isIncoming ? 'tabler-arrow-down-left' : 'tabler-arrow-up-right'} />}
            />
          )
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
        <CardContent className='flex flex-wrap items-center justify-between gap-4'>
          <div className='flex flex-wrap items-center gap-4'>
            <CustomTextField
              placeholder={t.documents.searchPlaceholder}
              value={globalFilter}
              onChange={e => setGlobalFilter(e.target.value)}
              className='is-full sm:is-auto'
            />
            <CustomTextField
              select
              value={direction}
              onChange={e => setDirection(e.target.value as DocumentDirection | '')}
              className='is-full sm:is-auto min-is-[160px]'
              SelectProps={{ displayEmpty: true }}
            >
              <MenuItem value=''>{t.documents.allTypes}</MenuItem>
              <MenuItem value='incoming'>{t.documents.incoming}</MenuItem>
              <MenuItem value='outgoing'>{t.documents.outgoing}</MenuItem>
            </CustomTextField>
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
