'use client'
import { useSessionIntent } from '@/hooks/useSessionIntent'
import { useAppDictionary } from '@/hooks/useDictionary'
import { useEffect, useRef, useState } from 'react'
import Card from '@mui/material/Card'
import CardHeader from '@mui/material/CardHeader'
import CardContent from '@mui/material/CardContent'
import Alert from '@mui/material/Alert'
import Autocomplete from '@mui/material/Autocomplete'
import TextField from '@mui/material/TextField'
import Button from '@mui/material/Button'
import Typography from '@mui/material/Typography'
import CircularProgress from '@mui/material/CircularProgress'
import { documentTasksApi, isPendingTask, type TaskHistory, type TaskOptions, type TaskReceipt, type TaskState } from '@/services/das/document-tasks'
import { clearTaskRequest, loadTaskRequest, saveTaskRequest, type FrozenTaskRequest } from '@/services/das/task-session'

export default function DocumentTaskPanel({ documentId }: { documentId: string }) {
  const sessionIntent = useSessionIntent()
  const { isEn } = useAppDictionary()
  const [options, setOptions] = useState<TaskOptions | null>(null), [history, setHistory] = useState<TaskHistory | null>(null)
  const [pending, setPending] = useState<FrozenTaskRequest | null>(null), [receipt, setReceipt] = useState<TaskReceipt | null>(null)
  const [assignee, setAssignee] = useState(''), [title, setTitle] = useState(''), [error, setError] = useState('')
  const [loading, setLoading] = useState(true), [busy, setBusy] = useState(false)
  const mounted = useRef(false), inFlight = useRef(false)

  const labels: Record<TaskState, string> = {
    PendingConfiguration: isEn ? 'Pending TMS Configuration' : 'Chờ kết nối TMS',
    Preparing: isEn ? 'Awaiting Confirmation' : 'Đang chờ xác nhận',
    UnknownOutcome: isEn ? 'Outcome Unknown — Reconciliation Required' : 'Chưa rõ kết quả — cần đối soát',
    Linked: isEn ? 'Task Created' : 'Đã tạo task',
    Rejected: isEn ? 'TMS Rejected Request' : 'TMS từ chối yêu cầu'
  }
  const errorMessage = (err: unknown) => err instanceof Error ? err.message : (isEn ? 'Unable to verify task request.' : 'Không thể xác nhận yêu cầu task.')

  async function load(signal?: AbortSignal) {
    setLoading(true); setOptions(null); setHistory(null); setError('')
    const [opts, rows] = await Promise.allSettled([documentTasksApi.options(documentId, signal), documentTasksApi.history(documentId, signal)])
    if (!mounted.current || signal?.aborted) return
    const errors: string[] = []
    if (rows.status === 'fulfilled') setHistory(rows.value)
    else errors.push(isEn ? `Could not load task request history: ${errorMessage(rows.reason)}` : `Chưa tải được lịch sử yêu cầu task: ${errorMessage(rows.reason)}`)
    if (opts.status === 'fulfilled') {
      try {
        const frozen = loadTaskRequest(window.sessionStorage, opts.value.userId, documentId)
        setPending(frozen); setOptions(opts.value)
        setAssignee(current => opts.value.assignees.some(a => a.userId === current) ? current : opts.value.userId)
      } catch { errors.push(isEn ? 'Unable to read task request session. Reconciliation required before creating more.' : 'Không thể đọc phiên yêu cầu task. Cần đối soát trước khi tạo thêm.') }
    } else errors.push(isEn ? 'Assignee list awaiting permission authority. Cannot create task.' : 'Danh sách người được giao đang chờ nguồn phân quyền. Chưa thể tạo task.')
    setError(errors.join(' ')); setLoading(false)
  }
  useEffect(() => {
    mounted.current = true
    const controller = new AbortController()
    void load(controller.signal)
    return () => { mounted.current = false; controller.abort() }
    // Parent keys this panel by document ID; each request belongs to that mounted document.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [documentId])

  async function send() {
    if (inFlight.current || loading || !options || !history || error || !pending && history.hasPending) return
    inFlight.current = true; setBusy(true); setError('')
    const actor = options.userId
    try {
      sessionIntent.assertCurrent()
      const frozen = pending ?? { key: crypto.randomUUID(), draft: { assigneeUserId: assignee, title: title.trim() } }
      // Persist before the network call. Storage failure must prevent any task side effect.
      saveTaskRequest(window.sessionStorage, actor, documentId, frozen)
      setPending(frozen)
      const result = await documentTasksApi.create(documentId, frozen.key, frozen.draft, sessionIntent)
      clearTaskRequest(window.sessionStorage, actor, documentId)
      if (!mounted.current) return
      setPending(null); setReceipt(result); setTitle('')
      await load()
    } catch (e) {
      if (mounted.current) {
        const prefix = typeof isEn !== 'undefined' && isEn ? 'Could not confirm request. Retain request to re-check; do not create duplicate request.' : 'Chưa xác nhận được yêu cầu. Giữ nguyên yêu cầu để kiểm tra lại; không tạo yêu cầu mới.'
        setError(`${prefix} ${errorMessage(e)}`)
      }
    } finally { inFlight.current = false; if (mounted.current) setBusy(false) }
  }
  async function operate(id: string, state: TaskState) {
    if (inFlight.current || loading || !options || !history || error || pending) return
    inFlight.current = true; setBusy(true); setError('')
    try {
      const result = await (state === 'PendingConfiguration' ? documentTasksApi.retry(id, sessionIntent) : documentTasksApi.reconcile(id, sessionIntent))
      if (!mounted.current) return
      setReceipt(result); await load()
    } catch (e) {
      if (mounted.current) {
        const prefix = typeof isEn !== 'undefined' && isEn ? 'Could not confirm status. Reload request before continuing.' : 'Chưa xác nhận được trạng thái. Tải lại yêu cầu trước khi tiếp tục.'
        setError(`${prefix} ${errorMessage(e)}`)
      }
    }
    finally { inFlight.current = false; if (mounted.current) setBusy(false) }
  }
  const canCreate = !busy && !loading && !!options && !!history && !error && !pending && !history.hasPending
  return <Card><CardHeader title={isEn ? 'Document Tasks' : 'Task của công văn'} /><CardContent>
    <Typography className='mbe-4'>{isEn ? 'Assign tasks to yourself or managed personnel. Document and PDF access permissions are verified separately.' : 'Giao task cho bản thân hoặc nhân sự thuộc phạm vi quản lý. Quyền xem công văn và PDF được kiểm tra riêng.'}</Typography>
    {error && <Alert severity='warning' className='mbe-4'>{error}</Alert>}
    {loading && <CircularProgress size={24} aria-label={isEn ? 'Loading task requests' : 'Đang tải yêu cầu task'} />}
    {receipt && <Alert severity={receipt.state === 'Linked' ? 'success' : 'info'} className='mbe-4'>{labels[receipt.state]}{receipt.taskId ? `: ${receipt.taskId}` : ''}</Alert>}
    {pending && <Alert severity='warning' className='mbe-4'>{isEn ? `Pending confirmation request: ${pending.draft.title}. Verify using same request ID; content is retained after reload.` : `Có yêu cầu đang chờ xác nhận: ${pending.draft.title}. Kiểm tra lại bằng cùng mã yêu cầu; nội dung được giữ nguyên sau tải lại trang.`}</Alert>}
    {history?.hasPending && !pending && <Alert severity='info' className='mbe-4'>{isEn ? 'There are incomplete requests. Reconnect or reconcile requests below before creating more.' : 'Có yêu cầu chưa hoàn tất. Thử kết nối lại hoặc đối soát yêu cầu bên dưới trước khi tạo thêm.'}</Alert>}
    {options && <div className='flex flex-col gap-4 mbe-4'>
      <Autocomplete options={options.assignees} value={options.assignees.find(a => a.userId === assignee) ?? null} disabled={!canCreate}
        openText={isEn ? 'Open options' : 'Mở lựa chọn'} closeText={isEn ? 'Close options' : 'Đóng lựa chọn'} clearText={isEn ? 'Clear selection' : 'Xóa lựa chọn'}
        getOptionLabel={a => a.isSelf ? (isEn ? `${a.name} (Myself)` : `${a.name} (Bản thân)`) : `${a.name} — ${a.departmentName}`} isOptionEqualToValue={(a, b) => a.userId === b.userId}
        onChange={(_, value) => setAssignee(value?.userId ?? '')} renderInput={params => <TextField {...params} label={isEn ? 'Assignee' : 'Người được giao task'} required />} />
      <TextField fullWidth required label={isEn ? 'Task Title' : 'Tiêu đề task'} value={title} onChange={e => setTitle(e.target.value)} disabled={!canCreate} inputProps={{ maxLength: 250 }} helperText={`${title.length}/250`} />
      <div><Button variant='contained' disabled={!canCreate || !assignee || !title.trim() || title.length > 250} onClick={() => void send()}>{isEn ? 'Create Task' : 'Tạo task'}</Button></div>
    </div>}
    <div className='flex flex-wrap gap-3 mbe-4'>
      <Button disabled={busy || loading} onClick={() => void load()}>{isEn ? 'Reload Task Requests' : 'Tải lại yêu cầu task'}</Button>
      {pending && <Button disabled={busy || loading || !options || !history || !!error} onClick={() => void send()}>{isEn ? 'Check In-flight Request' : 'Kiểm tra yêu cầu đang gửi'}</Button>}
    </div>
    {history && <Typography className='mbe-3'>{history.total === 0 ? (isEn ? 'No task requests created by you for this document.' : 'Chưa có yêu cầu task của bạn cho công văn này.') : (isEn ? `${history.total} request(s) by you; showing up to 50, prioritizing incomplete requests.` : `${history.total} yêu cầu của bạn; hiển thị tối đa 50, ưu tiên yêu cầu chưa hoàn tất.`)}</Typography>}
    {history?.items.map(item => <div key={item.correlationId} className='border rounded p-4 mbe-3' style={{ overflowWrap: 'anywhere' }}>
      <Typography fontWeight={600}>{item.title}</Typography><Typography>{labels[item.state]}</Typography>
      <Typography variant='body2'>{isEn ? 'Assignee: ' : 'Người được giao: '}{options?.assignees.find(a => a.userId === item.assigneeUserId)?.name ?? item.assigneeUserId}</Typography>
      <Typography variant='body2'>{isEn ? 'Correlation ID: ' : 'Mã yêu cầu: '}{item.correlationId}</Typography>
      {item.taskId && <Typography>{isEn ? 'TMS Task ID: ' : 'Mã task TMS: '}{item.taskId}</Typography>}
      {isPendingTask(item.state) && <Button disabled={busy || loading || !!error || !options || !!pending} onClick={() => void operate(item.correlationId, item.state)}>{item.state === 'PendingConfiguration' ? (isEn ? 'Retry Connection' : 'Thử kết nối lại') : (isEn ? 'Reconcile Request' : 'Đối soát yêu cầu')}</Button>}
    </div>)}
  </CardContent></Card>
}
