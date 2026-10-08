'use client'
import { useEffect, useState } from 'react'
import Link from 'next/link'
import { useParams } from 'next/navigation'
import { Alert, Button, Card, CardContent, CircularProgress, Stack, Typography } from '@mui/material'
import { incompleteReportsApi, type IncompleteReport } from '@/services/das/reports-staff'
export default function IncompleteOverview() {
  const params=useParams(),lang=typeof params.lang==='string'?params.lang:'vi'
  const isEn = lang === 'en'
  const [data,setData]=useState<IncompleteReport>(),[error,setError]=useState(''),[reload,setReload]=useState(0),[loading,setLoading]=useState(false)
  useEffect(()=>{const controller=new AbortController();setData(undefined);setError('');setLoading(true);incompleteReportsApi.list({},controller.signal).then(r=>{if(!controller.signal.aborted)setData(r)}).catch(()=>{if(!controller.signal.aborted)setError('unavailable')}).finally(()=>{if(!controller.signal.aborted)setLoading(false)});return()=>controller.abort()},[reload])
  return <Card><CardContent><Stack spacing={3}>
    <Typography variant='h4'>{isEn ? 'Incomplete document overview' : 'Tổng quan hồ sơ chưa hoàn tất'}</Typography>
    <Typography>{isEn ? 'Outgoing/internal documents registered more than 7 days ago; totals use the same authorized scope as the report.' : 'Công văn đi/nội bộ đăng ký quá 7 ngày; số liệu theo cùng phạm vi với báo cáo.'}</Typography>
    {error && <Alert severity='warning'>{isEn ? 'Unable to load totals within your authorized scope. Please try again later.' : 'Chưa thể tải số liệu trong phạm vi được cấp. Vui lòng thử lại sau.'}</Alert>}
    {loading && <CircularProgress aria-label={isEn ? 'Loading overview' : 'Đang tải tổng quan'} />}
    {data && <>
      <Typography variant='h3'>{data.total}</Typography>
      <Typography>{isEn ? 'documents' : 'hồ sơ'} • {new Date(data.evaluatedAt).toLocaleString(isEn ? 'en-US' : 'vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</Typography>
      <Stack spacing={1}>{data.groups.map(group => <Typography key={group.departmentId}>{group.departmentName}: {group.count}</Typography>)}</Stack>
      <Link href={`/${lang}/apps/reports/incomplete`}>{isEn ? 'View detailed report' : 'Xem báo cáo chi tiết'}</Link>
    </>}
    <Button onClick={() => setReload(value => value + 1)} disabled={loading}>{isEn ? 'Reload overview' : 'Tải lại tổng quan'}</Button>
  </Stack></CardContent></Card>
}
