'use client'

import { useSessionIntent } from '@/hooks/useSessionIntent'
import { useAppDictionary } from '@/hooks/useDictionary'
import { useCallback, useEffect, useRef, useState } from 'react'
import Link from 'next/link'
import {
  Alert,
  Badge,
  Box,
  Button,
  Chip,
  CircularProgress,
  Divider,
  IconButton,
  Paper,
  Popover,
  Stack,
  Tooltip,
  Typography
} from '@mui/material'
import { notificationsApi, type InAppNotification } from '@/services/das/notifications'

export default function DasNotificationsDropdown() {
  const sessionIntent = useSessionIntent()
  const { isEn } = useAppDictionary()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)
  const [items, setItems] = useState<InAppNotification[]>([])
  const [count, setCount] = useState(0)
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [busy, setBusy] = useState(false)

  const pageSize = 10
  const currentReqRef = useRef(0)
  const abortCtrlRef = useRef<AbortController | null>(null)
  const isMountedRef = useRef(true)
  const inFlightRef = useRef(false)
  const pageRef = useRef(page)
  pageRef.current = page

  useEffect(() => {
    isMountedRef.current = true
    return () => {
      isMountedRef.current = false
    }
  }, [])

  const fetchNotifications = useCallback(async (targetPage: number) => {
    // Abort previous in-flight request
    if (abortCtrlRef.current) {
      abortCtrlRef.current.abort()
    }
    const controller = new AbortController()
    abortCtrlRef.current = controller
    const requestId = ++currentReqRef.current

    setLoading(true)
    setError('')

    try {
      const [pageData, unread] = await Promise.all([
        notificationsApi.list(targetPage, pageSize, controller.signal),
        notificationsApi.unread(controller.signal)
      ])

      // Ignore if superseded by a newer request or aborted
      if (controller.signal.aborted || currentReqRef.current !== requestId) return

      setItems(pageData.items)
      setTotalCount(pageData.totalCount)
      setCount(unread)
      setError('')

      // Guarded clamp if total dataset shrunk and targetPage exceeds totalPages
      const calculatedTotalPages = Math.ceil(pageData.totalCount / pageSize) || 1
      if (targetPage > calculatedTotalPages) {
        setPage(calculatedTotalPages)
      }
    } catch {
      if (controller.signal.aborted || currentReqRef.current !== requestId) return
      setError(isEn ? 'Unable to load notifications. Please try again.' : 'Chưa thể tải thông báo. Vui lòng thử lại.')
    } finally {
      if (!controller.signal.aborted && currentReqRef.current === requestId) {
        setLoading(false)
      }
    }
  }, [isEn])

  // Single effect owner for data loading and 60s background polling
  useEffect(() => {
    void fetchNotifications(page)
    const timer = setInterval(() => void fetchNotifications(page), 60000)
    return () => {
      clearInterval(timer)
      if (abortCtrlRef.current) {
        abortCtrlRef.current.abort()
      }
    }
  }, [fetchNotifications, page])

  const handlePageChange = (newPage: number) => {
    // Solely update page state; useEffect will handle single, orderly fetch
    setPage(newPage)
  }

  const read = async (id?: string) => {
    if (inFlightRef.current || busy) return
    inFlightRef.current = true
    setBusy(true)
    let mutationSaved = false
    try {
      if (id) await notificationsApi.read(id, sessionIntent)
      else await notificationsApi.readAll(sessionIntent)
      mutationSaved = true
      if (!isMountedRef.current) return
      await fetchNotifications(pageRef.current)
    } catch {
      if (!isMountedRef.current) return
      if (mutationSaved) {
        setError(isEn ? 'Read status saved, but failed to reload list.' : 'Đã lưu trạng thái đọc, nhưng chưa tải lại được danh sách.')
      } else {
        setError(isEn ? 'Failed to update read status.' : 'Chưa lưu được trạng thái đã đọc.')
      }
    } finally {
      inFlightRef.current = false
      if (isMountedRef.current) {
        setBusy(false)
      }
    }
  }

  const formatNotificationTime = (isoString: string) => {
    try {
      const d = new Date(isoString)
      return d.toLocaleString(isEn ? 'en-US' : 'vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })
    } catch {
      return isoString
    }
  }

  const totalPages = Math.ceil(totalCount / pageSize) || 1

  return (
    <>
      <Tooltip title={isEn ? 'Notifications' : 'Thông báo'}>
        <IconButton
          aria-label={isEn ? 'Notifications' : 'Thông báo'}
          aria-haspopup='dialog'
          aria-expanded={Boolean(anchor)}
          aria-controls={anchor ? 'das-notification-inbox' : undefined}
          onClick={e => {
            setAnchor(e.currentTarget)
            void fetchNotifications(page)
          }}
        >
          <Badge badgeContent={count} color='error'>
            <i className='tabler-bell text-[22px]' />
          </Badge>
        </IconButton>
      </Tooltip>

      <Popover
        open={!!anchor}
        anchorEl={anchor}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{
          paper: {
            id: 'das-notification-inbox',
            role: 'dialog',
            'aria-modal': true,
            'aria-labelledby': 'das-notification-title',
            sx: {
              width: 'min(420px, 92vw)',
              maxHeight: '75vh',
              display: 'flex',
              flexDirection: 'column',
              boxShadow: 4,
              borderRadius: 2
            }
          }
        }}
      >
        {/* Header */}
        <Box className='flex items-center justify-between p-4 border-b border-divider'>
          <div className='flex items-center gap-2'>
            <Typography id='das-notification-title' variant='h6' className='font-semibold'>
              {isEn ? 'Notifications' : 'Thông Báo'}
            </Typography>
            {count > 0 && (
              <Chip
                label={isEn ? `${count} unread` : `${count} mới`}
                color='primary'
                size='small'
                variant='tonal'
              />
            )}
          </div>
          <div className='flex items-center gap-1'>
            {count > 0 && (
              <Tooltip title={isEn ? 'Mark all as read' : 'Đánh dấu tất cả đã đọc'}>
                <span>
                  <IconButton
                    aria-label={isEn ? 'Mark all as read' : 'Đánh dấu tất cả đã đọc'}
                    size='small'
                    color='primary'
                    disabled={busy || loading}
                    onClick={() => read()}
                  >
                    <i className='tabler-mail-opened text-lg' />
                  </IconButton>
                </span>
              </Tooltip>
            )}
            <Tooltip title={isEn ? 'Reload' : 'Tải lại'}>
              <span>
                <IconButton
                  aria-label={isEn ? 'Reload notifications' : 'Tải lại thông báo'}
                  size='small'
                  disabled={loading || busy}
                  onClick={() => void fetchNotifications(page)}
                >
                  <i className='tabler-refresh text-lg' />
                </IconButton>
              </span>
            </Tooltip>
          </div>
        </Box>

        {/* Content list */}
        <Box sx={{ overflowY: 'auto', p: 2, flexGrow: 1 }}>
          <Stack spacing={2}>
            {error && <Alert severity='warning'>{error}</Alert>}

            {loading && items.length === 0 && (
              <div className='flex justify-center p-6'>
                <CircularProgress size={28} aria-label={isEn ? 'Loading notifications' : 'Đang tải thông báo'} />
              </div>
            )}

            {!loading && !error && items.length === 0 && (
              <Box className='text-center py-8 text-textSecondary'>
                <i className='tabler-bell-off text-4xl mbe-2 opacity-60' />
                <Typography variant='body2'>
                  {isEn ? 'No notifications at this time.' : 'Hiện chưa có thông báo nào.'}
                </Typography>
              </Box>
            )}

            {!error &&
              items.map(n => (
                <Paper
                  key={n.id}
                  variant='outlined'
                  sx={{
                    p: 2,
                    borderRadius: 1.5,
                    bgcolor: n.isRead ? 'transparent' : 'action.hover',
                    borderColor: n.isRead ? 'divider' : 'primary.main',
                    transition: 'all 0.2s ease-in-out'
                  }}
                >
                  <div className='flex items-start justify-between gap-2 mbe-1'>
                    <Typography
                      variant='subtitle2'
                      fontWeight={n.isRead ? 500 : 700}
                      color={n.isRead ? 'text.primary' : 'primary.main'}
                    >
                      {n.title}
                    </Typography>
                    {!n.isRead && (
                      <Box
                        sx={{
                          width: 8,
                          height: 8,
                          borderRadius: '50%',
                          bgcolor: 'primary.main',
                          flexShrink: 0,
                          mt: 0.8
                        }}
                      />
                    )}
                  </div>

                  <Typography
                    variant='body2'
                    color='text.secondary'
                    sx={{ overflowWrap: 'anywhere', whiteSpace: 'pre-wrap', mbe: 1.5 }}
                  >
                    {n.message}
                  </Typography>

                  <div className='flex flex-wrap items-center justify-between gap-2 text-xs'>
                    <Typography variant='caption' color='text.disabled'>
                      {formatNotificationTime(n.createdAt)}
                    </Typography>

                    <div className='flex items-center gap-2'>
                      {n.actionUrl && (
                        <Button
                          component={Link}
                          href={n.actionUrl}
                          size='small'
                          variant='tonal'
                          onClick={() => setAnchor(null)}
                          sx={{ textTransform: 'none', py: 0.25, px: 1, minHeight: 0 }}
                        >
                          {isEn ? 'View' : 'Mở'}
                        </Button>
                      )}
                      {!n.isRead && (
                        <Button
                          size='small'
                          variant='text'
                          disabled={busy || loading}
                          onClick={() => read(n.id)}
                          sx={{ textTransform: 'none', py: 0.25, px: 1, minHeight: 0 }}
                        >
                          {isEn ? 'Mark read' : 'Đã đọc'}
                        </Button>
                      )}
                    </div>
                  </div>
                </Paper>
              ))}
          </Stack>
        </Box>

        {/* Footer with Full Pagination Controls */}
        <Divider />
        <Box className='p-3 bg-actionHover flex flex-wrap items-center justify-between gap-2'>
          <div className='flex items-center gap-1'>
            <Button
              size='small'
              variant='outlined'
              disabled={page <= 1 || loading}
              onClick={() => handlePageChange(page - 1)}
              sx={{ minWidth: 0, px: 1, py: 0.25, textTransform: 'none' }}
            >
              {isEn ? 'Prev' : 'Trước'}
            </Button>
            <Typography variant='caption' color='text.secondary' className='px-1'>
              {error && items.length === 0
                ? (isEn ? `Page ${page}/${totalPages} (?)` : `Trang ${page}/${totalPages} (?)`)
                : (isEn ? `Page ${page}/${totalPages} (${totalCount})` : `Trang ${page}/${totalPages} (${totalCount})`)}
            </Typography>
            <Button
              size='small'
              variant='outlined'
              disabled={page >= totalPages || loading}
              onClick={() => handlePageChange(page + 1)}
              sx={{ minWidth: 0, px: 1, py: 0.25, textTransform: 'none' }}
            >
              {isEn ? 'Next' : 'Sau'}
            </Button>
          </div>
          <div className='flex items-center gap-2'>
            <Typography variant='caption' color='text.disabled'>
              {isEn ? 'Polling 60s' : 'Làm mới 60s'}
            </Typography>
            {count > 0 && (
              <Button
                size='small'
                disabled={loading || busy}
                onClick={() => read()}
                sx={{ textTransform: 'none', py: 0.25, px: 1, minHeight: 0 }}
              >
                {isEn ? 'Mark all' : 'Đọc hết'}
              </Button>
            )}
          </div>
        </Box>
      </Popover>
    </>
  )
}
