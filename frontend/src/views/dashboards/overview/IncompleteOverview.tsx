'use client'
import { useEffect, useState } from 'react'
import Link from 'next/link'
import { useParams } from 'next/navigation'
import { Alert, Button, Card, CardContent, CircularProgress, Stack, Typography } from '@mui/material'
import { incompleteReportsApi, type IncompleteReport } from '@/services/das/reports-staff'
export default function IncompleteOverview() {
  const params=useParams(),lang=typeof params.lang==='string'?params.lang:'vi'
  const [data,setData]=useState<IncompleteReport>(),[error,setError]=useState(''),[reload,setReload]=useState(0),[loading,setLoading]=useState(false)
  useEffect(()=>{const controller=new AbortController();setData(undefined);setError('');setLoading(true);incompleteReportsApi.list({},controller.signal).then(r=>{if(!controller.signal.aborted)setData(r)}).catch(()=>{if(!controller.signal.aborted)setError('Chưa thể tải số liệu trong phạm vi được cấp. Vui lòng thử lại sau.')}).finally(()=>{if(!controller.signal.aborted)setLoading(false)});return()=>controller.abort()},[reload])
  return <Card><CardContent><Stack spacing={3}><Typography variant='h4'>Tổng quan hồ sơ chưa hoàn tất</Typography><Typography>Công văn đi/nội bộ đăng ký quá 7 ngày; số liệu theo cùng phạm vi với báo cáo.</Typography>{error&&<Alert severity='warning'>{error}</Alert>}{loading&&<CircularProgress aria-label='Đang tải tổng quan'/>}{data&&<><Typography variant='h3'>{data.total}</Typography><Typography>hồ sơ • {new Date(data.evaluatedAt).toLocaleString('vi-VN',{timeZone:'Asia/Ho_Chi_Minh'})}</Typography><Stack spacing={1}>{data.groups.map(g=><Typography key={g.departmentId}>{g.departmentName}: {g.count}</Typography>)}</Stack><Link href={`/${lang}/apps/reports/incomplete`}>Xem báo cáo chi tiết</Link></>}<Button onClick={()=>setReload(n=>n+1)} disabled={loading}>Tải lại tổng quan</Button></Stack></CardContent></Card>
}
