'use client'

import { useEffect, useState, useMemo } from 'react'
import { useParams } from 'next/navigation'

// MUI Imports
import Grid from '@mui/material/Grid'
import Card from '@mui/material/Card'
import CardHeader from '@mui/material/CardHeader'
import CardContent from '@mui/material/CardContent'
import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import Avatar from '@mui/material/Avatar'
import LinearProgress from '@mui/material/LinearProgress'
import Button from '@mui/material/Button'
import Divider from '@mui/material/Divider'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'

// Component & Helper Imports
import Link from '@/components/Link'
import { getLocalizedUrl } from '@/utils/i18n'
import { documentApi, partnerApi, authApi, tokenManager } from '@/services/api'
import { useAppDictionary } from '@/hooks/useDictionary'

// Table Style
import tableStyles from '@core/styles/table.module.css'

interface StatCardProps {
  title: string
  value: number | string
  icon: string
  color: 'primary' | 'success' | 'warning' | 'info' | 'error' | 'secondary'
  href?: string
}

function StatCard({ title, value, icon, color, href }: StatCardProps) {
  const content = (
    <Card sx={{ height: '100%', transition: 'all 0.2s ease-in-out', '&:hover': { boxShadow: 4, transform: 'translateY(-2px)' } }}>
      <CardContent sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 4 }}>
        <Box>
          <Typography variant='body2' color='text.disabled'>
            {title}
          </Typography>
          <Typography variant='h4' sx={{ mt: 1, fontWeight: 700 }}>
            {value}
          </Typography>
        </Box>
        <Avatar
          variant='rounded'
          sx={{
            bgcolor: `var(--mui-palette-${color}-lightOpacity, rgba(115, 103, 240, 0.16))`,
            color: `var(--mui-palette-${color}-main, #7367f0)`,
            width: 46,
            height: 46
          }}
        >
          <i className={icon} style={{ fontSize: '1.5rem' }} />
        </Avatar>
      </CardContent>
    </Card>
  )

  return href ? (
    <Link href={href} className='block' style={{ textDecoration: 'none' }}>
      {content}
    </Link>
  ) : (
    content
  )
}

function PageHeader({ title, subtitle, action }: { title: string; subtitle?: string; action?: React.ReactNode }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', mb: 5, gap: 4 }}>
      <Box>
        <Typography variant='h4' fontWeight={700}>{title}</Typography>
        {subtitle && (
          <Typography variant='body2' color='text.disabled' sx={{ mt: 1 }}>
            {subtitle}
          </Typography>
        )}
      </Box>
      {action}
    </Box>
  )
}

export default function DashboardOverview() {
  const { lang: locale } = useParams()
  const { t } = useAppDictionary()

  const [loading, setLoading] = useState(true)
  const [currentUser, setCurrentUser] = useState<any>(null)
  const [documents, setDocuments] = useState<any[]>([])
  const [partners, setPartners] = useState<any[]>([])
  const [usersCount, setUsersCount] = useState(4)
  const [departmentsCount, setDepartmentsCount] = useState(6)
  const [filterType, setFilterType] = useState<'all' | 'incoming' | 'outgoing' | 'internal'>('all')

  useEffect(() => {
    let active = true

    const loadData = async () => {
      try {
        const u = tokenManager.getUser()
        if (u) setCurrentUser(u)

        // Tải danh sách công văn và đối tác đồng thời
        const [docRes, partnerRes] = await Promise.allSettled([
          documentApi.getList({ pageSize: 500 }),
          partnerApi.getList({ pageSize: 500 }),
          authApi.getUsers().catch(() => null)
        ])

        if (!active) return

        if (docRes.status === 'fulfilled' && docRes.value?.success && Array.isArray(docRes.value.data)) {
          setDocuments(docRes.value.data)
        }

        if (partnerRes.status === 'fulfilled' && partnerRes.value?.success && Array.isArray(partnerRes.value.data)) {
          setPartners(partnerRes.value.data)
        }
      } catch (err) {
        console.warn('[DashboardOverview] Error fetching live data:', err)
      } finally {
        if (active) setLoading(false)
      }
    }

    loadData()

    const handleUpdate = () => loadData()
    if (typeof window !== 'undefined') {
      window.addEventListener('das_documents_updated', handleUpdate)
    }

    return () => {
      active = false
      if (typeof window !== 'undefined') {
        window.removeEventListener('das_documents_updated', handleUpdate)
      }
    }
  }, [])

  // Phân loại số lượng
  const incomingDocs = useMemo(() => documents.filter(d => (d.direction || d.docType || '').toLowerCase() === 'incoming'), [documents])
  const outgoingDocs = useMemo(() => documents.filter(d => (d.direction || d.docType || '').toLowerCase() === 'outgoing'), [documents])
  const internalDocs = useMemo(() => documents.filter(d => (d.direction || d.docType || '').toLowerCase() === 'internal'), [documents])

  const reviewedDocs = useMemo(() => documents.filter(d => {
    const s = (d.status || '').toLowerCase()
    return s === 'reviewed' || s === 'processing' || s === 'in review' || s === 'đã duyệt'
  }), [documents])

  const distributedDocs = useMemo(() => documents.filter(d => {
    const s = (d.status || '').toLowerCase()
    return s === 'distributed' || s === 'completed' || s === 'đã phát hành' || s === 'hoàn thành'
  }), [documents])

  const draftDocs = useMemo(() => documents.filter(d => {
    const s = (d.status || '').toLowerCase()
    return s === 'draft' || s === 'pending' || s === 'bản thảo'
  }), [documents])

  // Phân bố trạng thái chuẩn
  const totalCount = documents.length || 1
  const statusBreakdown = useMemo(() => [
    {
      status: 'Bản thảo (Draft)',
      count: draftDocs.length,
      percent: Math.round((draftDocs.length / totalCount) * 100),
      color: 'var(--mui-palette-grey-500, #808390)'
    },
    {
      status: 'Đã duyệt (Reviewed)',
      count: reviewedDocs.length,
      percent: Math.round((reviewedDocs.length / totalCount) * 100),
      color: 'var(--mui-palette-warning-main, #ff9f43)'
    },
    {
      status: 'Đã phát hành (Distributed)',
      count: distributedDocs.length,
      percent: Math.round((distributedDocs.length / totalCount) * 100),
      color: 'var(--mui-palette-success-main, #28c76f)'
    }
  ], [draftDocs.length, reviewedDocs.length, distributedDocs.length, totalCount])

  // Danh sách công văn gần đây (lọc theo nút bấm)
  const filteredRecentDocs = useMemo(() => {
    let list = documents
    if (filterType !== 'all') {
      list = documents.filter(d => (d.direction || d.docType || '').toLowerCase() === filterType)
    }
    return list.slice(0, 7)
  }, [documents, filterType])

  const getStatusChip = (status: string) => {
    const s = (status || '').toLowerCase()
    if (s === 'distributed' || s === 'completed' || s === 'đã phát hành' || s === 'hoàn thành') {
      return <Chip size='small' variant='tonal' label='Đã phát hành' color='success' />
    }
    if (s === 'reviewed' || s === 'processing' || s === 'đã duyệt') {
      return <Chip size='small' variant='tonal' label='Đã duyệt' color='warning' />
    }
    return <Chip size='small' variant='tonal' label='Bản thảo' color='default' />
  }

  const getTypeChip = (dir: string) => {
    const d = (dir || '').toLowerCase()
    if (d === 'incoming') return <Chip size='small' variant='tonal' label='Đến' color='primary' />
    if (d === 'outgoing') return <Chip size='small' variant='tonal' label='Đi' color='success' />
    return <Chip size='small' variant='tonal' label='Nội bộ' color='info' />
  }

  const greetingName = currentUser?.fullName || currentUser?.username || 'Quản trị viên'

  return (
    <>
      {/* 1. Page Header */}
      <PageHeader
        title={`Chào mừng trở lại, ${greetingName}`}
        subtitle='Tổng quan về công văn đến, công văn đi, nội bộ, đối tác và phòng ban trong toàn bộ hệ thống.'
        action={
          <Button
            variant='contained'
            startIcon={<i className='tabler-plus' />}
            component={Link}
            href={getLocalizedUrl('/apps/documents/add', locale as string)}
          >
            Thêm Công Văn Mới
          </Button>
        }
      />

      {/* 2. 8 Stat Cards Grid */}
      <Grid container spacing={6} sx={{ mb: 6 }}>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title='Công văn đến'
            value={incomingDocs.length}
            icon='tabler-file-import'
            color='primary'
            href={getLocalizedUrl('/apps/documents?direction=incoming', locale as string)}
          />
        </Grid>

        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title='Công văn đi'
            value={outgoingDocs.length}
            icon='tabler-file-export'
            color='success'
            href={getLocalizedUrl('/apps/documents?direction=outgoing', locale as string)}
          />
        </Grid>

        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title='Công văn nội bộ'
            value={internalDocs.length}
            icon='tabler-file-text'
            color='info'
            href={getLocalizedUrl('/apps/documents?direction=internal', locale as string)}
          />
        </Grid>

        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title='Chờ duyệt / Đang xử lý'
            value={reviewedDocs.length}
            icon='tabler-clock-hour-4'
            color='warning'
          />
        </Grid>

        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title='Danh bạ đối tác'
            value={partners.length || 90}
            icon='tabler-affiliate'
            color='success'
            href={getLocalizedUrl('/apps/partners', locale as string)}
          />
        </Grid>

        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title='Người dùng'
            value={usersCount}
            icon='tabler-users'
            color='secondary'
            href={getLocalizedUrl('/apps/user/list', locale as string)}
          />
        </Grid>

        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title='Phòng ban nội bộ'
            value={departmentsCount}
            icon='tabler-building-community'
            color='secondary'
            href={getLocalizedUrl('/apps/departments', locale as string)}
          />
        </Grid>

        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard
            title='Tổng số công văn'
            value={documents.length}
            icon='tabler-files'
            color='primary'
            href={getLocalizedUrl('/apps/documents', locale as string)}
          />
        </Grid>
      </Grid>

      {/* 3. Bottom Grid: Documents by Status & Recent Documents */}
      <Grid container spacing={6}>
        {/* Left Side: Documents by Status */}
        <Grid size={{ xs: 12, md: 5 }}>
          <Card sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
            <CardHeader
              title='Phân bố theo trạng thái'
              subheader='Tỷ lệ phân bố trạng thái công văn trong hệ thống'
            />
            <CardContent sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
              {statusBreakdown.map(({ status, count, percent, color }) => (
                <Box key={status} sx={{ mb: 4, '&:last-child': { mb: 0 } }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1.5 }}>
                    <Typography variant='body2' fontWeight={500}>{status}</Typography>
                    <Typography variant='body2' color='text.disabled' fontWeight={600}>
                      {count} ({percent}%)
                    </Typography>
                  </Box>
                  <LinearProgress
                    variant='determinate'
                    value={percent}
                    sx={{
                      height: 10,
                      borderRadius: 5,
                      bgcolor: 'action.hover',
                      '& .MuiLinearProgress-bar': { bgcolor: color, borderRadius: 5 }
                    }}
                  />
                </Box>
              ))}
            </CardContent>
          </Card>
        </Grid>

        {/* Right Side: Recent Documents */}
        <Grid size={{ xs: 12, md: 7 }}>
          <Card sx={{ height: '100%' }}>
            <CardHeader
              title='Công văn gần đây'
              subheader='Danh sách công văn tiếp nhận và ban hành mới nhất'
              action={
                <Box sx={{ display: 'flex', gap: 1.5 }}>
                  <Button
                    size='small'
                    variant={filterType === 'all' ? 'contained' : 'tonal'}
                    onClick={() => setFilterType('all')}
                  >
                    Tất cả
                  </Button>
                  <Button
                    size='small'
                    variant={filterType === 'incoming' ? 'contained' : 'tonal'}
                    onClick={() => setFilterType('incoming')}
                  >
                    Đến
                  </Button>
                  <Button
                    size='small'
                    variant={filterType === 'outgoing' ? 'contained' : 'tonal'}
                    onClick={() => setFilterType('outgoing')}
                  >
                    Đi
                  </Button>
                  <Button
                    size='small'
                    variant={filterType === 'internal' ? 'contained' : 'tonal'}
                    onClick={() => setFilterType('internal')}
                  >
                    Nội bộ
                  </Button>
                </Box>
              }
            />
            <Divider />
            <div className='overflow-x-auto'>
              {loading ? (
                <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', py: 8 }}>
                  <CircularProgress size={32} />
                </Box>
              ) : (
                <table className={tableStyles.table}>
                  <thead>
                    <tr>
                      <th>Số công văn</th>
                      <th>Tiêu đề / Trích yếu</th>
                      <th>Phân loại</th>
                      <th>Cơ quan / Đối tác</th>
                      <th>Trạng thái</th>
                      <th>Ngày</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredRecentDocs.length === 0 ? (
                      <tr>
                        <td colSpan={6} style={{ textAlign: 'center', padding: '32px 0', color: 'var(--mui-palette-text-disabled)' }}>
                          Chưa có công văn nào
                        </td>
                      </tr>
                    ) : (
                      filteredRecentDocs.map((doc, idx) => (
                        <tr key={doc.id || idx}>
                          <td>
                            <Typography variant='body2' className='font-mono font-semibold' color='primary.main'>
                              {doc.documentNumber || doc.referenceNumber || '—'}
                            </Typography>
                          </td>
                          <td>
                            <Typography
                              variant='body2'
                              fontWeight={500}
                              sx={{
                                maxWidth: 260,
                                whiteSpace: 'nowrap',
                                overflow: 'hidden',
                                textOverflow: 'ellipsis'
                              }}
                              title={doc.title}
                            >
                              {doc.title || 'Không có tiêu đề'}
                            </Typography>
                          </td>
                          <td>
                            {getTypeChip(doc.direction || doc.docType)}
                          </td>
                          <td>
                            <Typography
                              variant='body2'
                              sx={{
                                maxWidth: 160,
                                whiteSpace: 'nowrap',
                                overflow: 'hidden',
                                textOverflow: 'ellipsis'
                              }}
                              title={doc.partnerName}
                            >
                              {doc.partnerName || '—'}
                            </Typography>
                          </td>
                          <td>
                            {getStatusChip(doc.status)}
                          </td>
                          <td>
                            <Typography variant='body2' color='text.secondary'>
                              {doc.issuedDate || doc.receivedAt || (doc.createdAt ? new Date(doc.createdAt).toLocaleDateString('vi-VN') : '—')}
                            </Typography>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              )}
            </div>
          </Card>
        </Grid>
      </Grid>
    </>
  )
}
