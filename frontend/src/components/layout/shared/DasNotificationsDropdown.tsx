'use client'
import { useSessionIntent } from '@/hooks/useSessionIntent'
import { useCallback, useEffect, useState } from 'react'
import Link from 'next/link'
import { Alert, Badge, Button, CircularProgress, Divider, IconButton, Popover, Stack, Typography } from '@mui/material'
import { notificationsApi, type InAppNotification } from '@/services/das/notifications'

export default function DasNotificationsDropdown() {
  const sessionIntent = useSessionIntent()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null), [items, setItems] = useState<InAppNotification[]>([]), [count, setCount] = useState(0), [error, setError] = useState(''), [loading, setLoading] = useState(false), [busy, setBusy] = useState(false)
  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true)
    try { const [page, unread] = await Promise.all([notificationsApi.list(signal), notificationsApi.unread(signal)]); if (!signal?.aborted) { setItems(page.items); setCount(unread); setError('') } }
    catch { if (!signal?.aborted) { setItems([]); setCount(0); setError('Chưa thể tải thông báo. Vui lòng thử lại.') } }
    finally { if (!signal?.aborted) setLoading(false) }
  }, [])
  useEffect(() => { const controller = new AbortController(); void load(controller.signal); const timer = setInterval(() => void load(controller.signal), 60000); return () => { controller.abort(); clearInterval(timer) } }, [load])
  const read = async (id?: string) => { setBusy(true); try { if (id) await notificationsApi.read(id, sessionIntent); else await notificationsApi.readAll(sessionIntent); await load() } catch { setError('Chưa lưu được trạng thái đã đọc.') } finally { setBusy(false) } }
  return <>
    <IconButton aria-label='Thông báo' onClick={e => { setAnchor(e.currentTarget); void load() }}><Badge badgeContent={count} color='error'><i className='tabler-bell' /></Badge></IconButton>
    <Popover open={!!anchor} anchorEl={anchor} onClose={() => setAnchor(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }} transformOrigin={{ vertical: 'top', horizontal: 'right' }}>
      <Stack spacing={2} sx={{ p: 3, width: 'min(380px, 90vw)', maxHeight: '70vh', overflow: 'auto' }}>
        <Typography variant='h6'>Thông báo của tôi</Typography>{error && <Alert severity='warning'>{error}</Alert>}{loading && <CircularProgress size={24} aria-label='Đang tải thông báo' />}
        {!loading && !error && items.length === 0 && <Typography>Chưa có thông báo.</Typography>}
        {!error && items.map(n => <Stack spacing={1} key={n.id}><Typography fontWeight={n.isRead ? 400 : 600}>{n.title}</Typography><Typography variant='body2'>{n.message}</Typography><Typography variant='caption'>{new Date(n.createdAt).toLocaleString('vi-VN')}</Typography>{n.actionUrl && <Link href={n.actionUrl} onClick={() => setAnchor(null)}>Mở</Link>}{!n.isRead && <Button disabled={busy || loading} onClick={() => read(n.id)}>Đánh dấu đã đọc</Button>}<Divider /></Stack>)}
        <Button onClick={() => load()} disabled={loading || busy}>Tải lại thông báo</Button>{count > 0 && <Button onClick={() => read()} disabled={loading || busy}>Đánh dấu tất cả đã đọc</Button>}
      </Stack>
    </Popover>
  </>
}
