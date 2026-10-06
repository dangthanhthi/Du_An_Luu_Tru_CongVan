'use client'
import { useEffect, useState } from 'react'
import { Alert, Button, Card, CardContent, CircularProgress, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material'
import { getMyStaff, type MyStaffPage } from '@/services/das/reports-staff'
export default function MyStaff() {
  const [page, setPage] = useState(1), [data, setData] = useState<MyStaffPage>(), [error, setError] = useState(''), [loading, setLoading] = useState(false), [reload, setReload] = useState(0)
  useEffect(() => {
    const controller = new AbortController(); setData(undefined); setError(''); setLoading(true)
    getMyStaff(page, 20, controller.signal).then(r => { if (!controller.signal.aborted) setData(r) }).catch(() => { if (!controller.signal.aborted) setError('Danh sách nhân sự đang chờ kết nối nguồn quyền quản lý. Vui lòng thử lại sau.') }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [page, reload])
  return <Card><CardContent><Stack spacing={3}><Typography variant='h4'>My Staff</Typography><Typography>Thành viên thuộc phạm vi quản lý và công việc từ TMS.</Typography><Button onClick={() => setReload(n => n + 1)} disabled={loading}>Tải lại</Button>{error && <Alert severity='warning'>{error}</Alert>}{loading && <CircularProgress aria-label='Đang tải nhân sự' />}{data && <>
    <Typography>{data.total} thành viên</Typography><Table><TableHead><TableRow><TableCell>Thành viên</TableCell><TableCell>Phòng / nhóm</TableCell></TableRow></TableHead><TableBody>{data.staff.map(s => <TableRow key={s.userId}><TableCell>{s.name}</TableCell><TableCell>{s.departmentName}</TableCell></TableRow>)}</TableBody></Table>
    {data.taskState !== 'Connected' && <Alert severity='info'>TMS chưa kết nối; công việc sẽ hiển thị sau khi có cấu hình.</Alert>}
    {data.tasks && <><Typography variant='h6'>Công việc ({data.tasks.total})</Typography><Table><TableHead><TableRow><TableCell>Công việc</TableCell><TableCell>Người thực hiện (ID)</TableCell><TableCell>Trạng thái</TableCell><TableCell>Hạn</TableCell></TableRow></TableHead><TableBody>{data.tasks.items.map(t => <TableRow key={t.taskId}><TableCell>{t.title}</TableCell><TableCell>{t.assigneeUserId}</TableCell><TableCell>{t.status}</TableCell><TableCell>{t.dueAt ?? '—'}</TableCell></TableRow>)}</TableBody></Table></>}
    <Stack direction='row' spacing={2}><Button disabled={page <= 1} onClick={() => setPage(n => n - 1)}>Trước</Button><Typography>Trang {page}</Typography><Button disabled={page * 20 >= Math.max(data.total, data.tasks?.total ?? 0)} onClick={() => setPage(n => n + 1)}>Sau</Button></Stack>
  </>}</Stack></CardContent></Card>
}
