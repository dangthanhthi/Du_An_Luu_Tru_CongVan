'use client'

import { useEffect, useMemo, useState } from 'react'
import Link from 'next/link'
import { useParams, useRouter, useSearchParams } from 'next/navigation'
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
import IconButton from '@mui/material/IconButton'
import TablePagination from '@mui/material/TablePagination'
import CustomTextField from '@core/components/mui/TextField'
import type { Locale } from '@configs/i18n'
import type { ThemeColor } from '@core/types'
import type { DocumentKind, DocumentListQuery, DocumentPage, DocumentStatus } from '@/types/das/documents'
import { documentApi } from '@/services/api'
import { documentsV2Api } from '@/services/das/documents'
import { useAppDictionary } from '@/hooks/useDictionary'
import { getLocalizedUrl } from '@/utils/i18n'
import tableStyles from '@core/styles/table.module.css'

const kinds: DocumentKind[] = ['Incoming', 'Outgoing', 'Internal']
const views = ['all', 'mine', 'department', 'cancelled']
const statuses: DocumentStatus[] = ['InProgress', 'Distributed', 'Cancelled']

function readQuery(search: string): DocumentListQuery {
  const params = new URLSearchParams(search)

  for (const key of ['kind', 'view', 'pageNumber', 'pageSize', 'searchTerm', 'status']) {
    if (params.getAll(key).length > 1) throw new Error('Tham số danh sách công văn không hợp lệ.')
  }
  const kind = params.get('kind') ?? 'Incoming', view = params.get('view') ?? 'all'
  const status = params.get('status') ?? ''
  const pageNumber = Number(params.get('pageNumber') ?? 1), pageSize = Number(params.get('pageSize') ?? 20)

  if (!kinds.includes(kind as DocumentKind) || !views.includes(view) || (status && !statuses.includes(status as DocumentStatus)) ||
      !Number.isSafeInteger(pageNumber) || pageNumber < 1 || !Number.isSafeInteger(pageSize) || pageSize < 1 || pageSize > 100)
    throw new Error('Loại công văn, phạm vi hoặc phân trang không hợp lệ.')
  return { kind: kind as DocumentKind, view: view as DocumentListQuery['view'], status: status as DocumentStatus | '',
    searchTerm: params.get('searchTerm') ?? '', pageNumber, pageSize }
}

const DocumentListTable = () => {
  const searchParams = useSearchParams()
  const router = useRouter()
  const { lang } = useParams()
  const locale = lang as Locale
  const { t } = useAppDictionary()
  const vi = locale === 'vi'
  const [revision, setRevision] = useState(0)
  const search = searchParams.toString()
  const queryState = useMemo(() => {
    try { return { query: readQuery(search), error: null } }
    catch (error) { return { query: null, error: (error as Error).message } }
  }, [search])
  const key = `${search}:${revision}`
  const [result, setResult] = useState<{ key: string; page?: DocumentPage; canRegister?: boolean; error?: string }>({ key: '' })
  const query = queryState.query
  const current = result.key === key ? result : null
  const error = queryState.error || current?.error
  const page = current?.page
  const loading = !!query && !error && !page

  useEffect(() => {
    const refresh = () => setRevision(value => value + 1)

    window.addEventListener('das_documents_updated', refresh)
    return () => window.removeEventListener('das_documents_updated', refresh)
  }, [])

  useEffect(() => {
    if (!query) return
    const controller = new AbortController()

    Promise.all([documentApi.getScopedList(query, controller.signal), documentsV2Api.options(query.kind, controller.signal).then(x => x.canRegister).catch(() => false)]).then(([value, canRegister]) => {
      if (!controller.signal.aborted) setResult({ key, page: value, canRegister })
    }).catch((failure: unknown) => {
      if (!controller.signal.aborted) setResult({ key, error: failure instanceof Error ? failure.message : 'Không thể tải công văn.' })
    })
    return () => controller.abort()
  }, [query, key])

  function navigate(changes: Record<string, string | number>) {
    const params = new URLSearchParams(search)

    params.set('kind', query?.kind ?? 'Incoming')
    params.set('view', query?.view ?? 'all')
    for (const [name, value] of Object.entries(changes)) {
      if (value === '') params.delete(name)
      else params.set(name, String(value))
    }
    router.replace(`${getLocalizedUrl('/apps/documents/list', locale)}?${params}`)
  }

  const kindLabels = { Incoming: t.nav.incomingDocs, Outgoing: t.nav.outgoingDocs, Internal: t.nav.internalDocs }
  const statusLabels: Record<DocumentStatus, { text: string; color: ThemeColor }> = {
    InProgress: { text: vi ? 'Đang thực hiện' : 'In progress', color: 'warning' },
    Distributed: { text: vi ? 'Đã phân phối' : 'Distributed', color: 'info' },
    Cancelled: { text: vi ? 'Đã hủy' : 'Cancelled', color: 'secondary' }
  }
  const canRegister = !!query && current?.canRegister === true

  return (
    <Card sx={{ minWidth: 0, width: '100%' }}>
      <Box sx={{ borderBottom: 1, borderColor: 'divider', px: 4, pt: 2, minWidth: 0 }}>
        <Tabs value={query?.kind ?? false} onChange={(_event, kind: DocumentKind) => navigate({ kind, pageNumber: 1 })}
          variant='scrollable' scrollButtons='auto'>
          {kinds.map(kind => <Tab key={kind} value={kind} label={kindLabels[kind]} />)}
        </Tabs>
      </Box>
      <CardContent className='flex flex-wrap items-center justify-between gap-4'>
        <div className='flex flex-wrap gap-4 min-is-0 is-full sm:is-auto'>
          <CustomTextField placeholder={t.documents.searchPlaceholder} value={query?.searchTerm ?? ''}
            disabled={!query} onChange={event => navigate({ searchTerm: event.target.value, pageNumber: 1 })}
            className='is-full sm:is-auto' />
          <CustomTextField select value={query?.status ?? ''} disabled={!query || query.view === 'cancelled'}
            onChange={event => navigate({ status: event.target.value, pageNumber: 1 })} SelectProps={{ displayEmpty: true }}>
            <MenuItem value=''>{t.documents.allStatus}</MenuItem>
            {statuses.map(status => <MenuItem key={status} value={status}>{statusLabels[status].text}</MenuItem>)}
          </CustomTextField>
        </div>
        <div className='flex flex-wrap gap-3'>
          {canRegister && <Button component={Link} variant='contained'
            href={`${getLocalizedUrl('/apps/documents/add', locale)}?kind=${query!.kind}`}>{t.documents.addDoc}</Button>}
        </div>
      </CardContent>
      {error && <CardContent><Alert severity='error' action={query ? <Button color='inherit'
        onClick={() => setRevision(value => value + 1)}>{vi ? 'Thử lại' : 'Retry'}</Button> : undefined}>{error}</Alert></CardContent>}
      {loading && <div className='flex justify-center p-8'><CircularProgress aria-label={vi ? 'Đang tải công văn' : 'Loading documents'} /></div>}
      {page && <>
        <div className='overflow-x-auto'>
          <table className={tableStyles.table} aria-label={vi ? 'Danh sách công văn' : 'Documents'}>
            <thead><tr>
              <th>{t.documents.docNumber}</th><th>{t.documents.title}</th><th>{vi ? 'Công ty / Phòng' : 'Company / Department'}</th>
              <th>{t.documents.issuedDate}</th><th>{t.documents.status}</th><th>{vi ? 'Tình trạng hồ sơ' : 'Completeness'}</th><th>{t.documents.actions}</th>
            </tr></thead>
            <tbody>{page.items.length === 0 ? <tr><td colSpan={7} className='text-center py-6'>{t.documents.emptyData}</td></tr> : page.items.map(item => <tr key={item.id}>
              <td><div className='flex flex-col gap-1'>
                <Typography component={Link} color='primary.main' className='font-medium'
                  href={getLocalizedUrl(`/apps/documents/${item.id}`, locale)}>{item.registrationNumber}</Typography>
                {item.referenceNumber && <Typography variant='caption'>{item.referenceNumber}</Typography>}
              </div></td>
              <td><Typography variant='body2' sx={{ maxWidth: 320, whiteSpace: 'normal', overflowWrap: 'anywhere' }}>{item.subject || '—'}</Typography></td>
              <td>{[item.companyCode, item.departmentName].filter(Boolean).join(' / ') || '—'}</td>
              <td>{item.issuedDate || '—'}</td>
              <td><Chip size='small' label={statusLabels[item.status].text} color={statusLabels[item.status].color} /></td>
              <td>{item.isComplete === undefined ? '—' : <Chip size='small' variant='tonal' color={item.isComplete ? 'success' : 'warning'}
                label={item.isComplete ? (vi ? 'Đủ hồ sơ' : 'Complete') : (vi ? 'Chưa đủ hồ sơ' : 'Incomplete')} />}</td>
              <td>{item.allowedActions?.includes('Edit') && <IconButton component={Link} aria-label={`${t.documents.edit}: ${item.registrationNumber}`}
                href={getLocalizedUrl(`/apps/documents/edit/${item.id}`, locale)}><i className='tabler-edit' /></IconButton>}</td>
            </tr>)}</tbody>
          </table>
        </div>
        <TablePagination component='div' count={page.totalCount} page={page.pageNumber - 1} rowsPerPage={page.pageSize}
          rowsPerPageOptions={[10, 20, 50, 100]} onPageChange={(_event, number) => navigate({ pageNumber: number + 1 })}
          onRowsPerPageChange={event => navigate({ pageSize: Number(event.target.value), pageNumber: 1 })}
          labelRowsPerPage={vi ? 'Số dòng:' : 'Rows per page:'}
          getItemAriaLabel={type => vi ? ({ first: 'Trang đầu', last: 'Trang cuối', next: 'Trang tiếp theo', previous: 'Trang trước' })[type] : `Go to ${type} page`}
          sx={{ '& .MuiTablePagination-toolbar': { flexWrap: 'wrap', paddingInline: 2 }, '& .MuiTablePagination-spacer': { flex: '1 1 0' } }} />
      </>}
    </Card>
  )
}

export default DocumentListTable
