'use client'

import { useEffect, useRef, useState } from 'react'
import { Alert, Box, Button, Card, CardHeader, CardContent, Chip, CircularProgress, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material'
import { getMyStaffMembers, getMyStaffTasks, type MyStaffPage, type MyStaffTasksResult, type StaffMember } from '@/services/das/reports-staff'
import { ApiRequestError, tokenManager } from '@/services/api'
import { subscribeBrowserSession } from '@/services/browserSession'
import { useAppDictionary } from '@/hooks/useDictionary'

export default function MyStaff() {
  const { t, isEn } = useAppDictionary()
  const labels = t.myStaff
  const [epoch, setEpoch] = useState(tokenManager.getEpoch)
  const [staffPage, setStaffPage] = useState(1)
  const [taskPage, setTaskPage] = useState(1)
  const [selectedId, setSelectedId] = useState<string>()
  const [staffData, setStaffData] = useState<MyStaffPage>()
  const [taskData, setTaskData] = useState<MyStaffTasksResult>()
  const [staffLoading, setStaffLoading] = useState(false)
  const [taskLoading, setTaskLoading] = useState(false)
  const [staffError, setStaffError] = useState(false)
  const [taskError, setTaskError] = useState(false)
  const [scopeError, setScopeError] = useState<number>()
  const [reload, setReload] = useState(0)
  const scope = useRef({ generation: 0, blocked: false, staff: undefined as AbortController | undefined, tasks: undefined as AbortController | undefined })

  // One scope generation owns both requests. Authority loss revokes all pending
  // successes, even when a transport ignores abort. Ordinary task degradation
  // only affects the task request and preserves separately verified staff.
  const clearScope = () => {
    scope.current.generation++
    scope.current.staff?.abort()
    scope.current.tasks?.abort()
    setStaffData(undefined)
    setTaskData(undefined)
    setSelectedId(undefined)
    setStaffLoading(false)
    setTaskLoading(false)
    setStaffError(false)
    setTaskError(false)
  }
  useEffect(() => subscribeBrowserSession(() => {
    const next = tokenManager.getEpoch()
    if (next !== epoch) {
      clearScope()
      scope.current.blocked = false
      setScopeError(undefined)
      setStaffPage(1)
      setTaskPage(1)
      setEpoch(next)
    }
  }, tokenManager.getEpoch), [epoch])

  const handleError = (error: unknown, part: 'staff' | 'tasks') => {
    if (error instanceof ApiRequestError && [401, 403, 409, 503].includes(error.status)) {
      clearScope()
      scope.current.blocked = true
      setScopeError(error.status)
    } else if (part === 'staff') setStaffError(true)
    else setTaskError(true)
  }
  useEffect(() => {
    if (scope.current.blocked) return
    const controller = new AbortController(), generation = scope.current.generation
    scope.current.staff = controller
    const owned = () => !controller.signal.aborted && generation === scope.current.generation && epoch === tokenManager.getEpoch()
    setStaffData(undefined)
    setStaffError(false)
    setStaffLoading(true)
    getMyStaffMembers(staffPage, 20, controller.signal)
      .then(result => { if (owned()) setStaffData(result) })
      .catch(error => { if (owned()) handleError(error, 'staff') })
      .finally(() => { if (owned()) setStaffLoading(false) })
    return () => controller.abort()
  }, [epoch, staffPage, reload])

  useEffect(() => {
    if (scope.current.blocked) return
    const controller = new AbortController(), generation = scope.current.generation
    scope.current.tasks = controller
    const owned = () => !controller.signal.aborted && generation === scope.current.generation && epoch === tokenManager.getEpoch()
    setTaskData(undefined)
    setTaskError(false)
    setTaskLoading(true)
    getMyStaffTasks({ pageNumber: taskPage, pageSize: 20, assigneeUserId: selectedId }, controller.signal)
      .then(result => { if (owned()) setTaskData(result) })
      .catch(error => { if (owned()) handleError(error, 'tasks') })
      .finally(() => { if (owned()) setTaskLoading(false) })
    return () => controller.abort()
  }, [epoch, selectedId, taskPage, reload])

  const select = (member?: StaffMember) => {
    scope.current.tasks?.abort()
    setTaskData(undefined)
    setTaskLoading(true)
    setTaskPage(1)
    setSelectedId(member?.userId)
  }
  const refresh = () => {
    clearScope()
    scope.current.blocked = false
    setScopeError(undefined)
    setReload(value => value + 1)
  }
  const current = epoch === tokenManager.getEpoch()
  const staff = current ? staffData : undefined
  const taskResult = current ? taskData : undefined
  const taskList = taskResult?.tasks
  const selected = taskResult?.selectedAssignee
  const statusChip = (status: string) => {
    const known: Record<string, { label: string; color: 'success' | 'primary' | 'warning' | 'secondary' }> = {
      completed: { label: labels.completed, color: 'success' }, done: { label: labels.completed, color: 'success' },
      inprogress: { label: labels.inProgress, color: 'primary' }, doing: { label: labels.inProgress, color: 'primary' },
      pending: { label: labels.pending, color: 'warning' }, cancelled: { label: labels.cancelled, color: 'secondary' }
    }
    const value = known[status.toLowerCase()]
    return <Chip label={value?.label ?? status} color={value?.color ?? 'default'} size='small' sx={{ maxWidth: '100%', height: 'auto', '& .MuiChip-label': { whiteSpace: 'normal', overflowWrap: 'anywhere' } }} />
  }
  const pager = (part: 'staff' | 'tasks', page: number, total: number, loading: boolean) => (
    <Box className='flex flex-wrap items-center justify-between gap-2' component='nav' aria-label={part === 'staff' ? labels.staff : labels.tasks}>
      <Typography variant='body2'>{labels.page} {page} · {labels.total}: {total}</Typography>
      <Stack direction='row' spacing={1}>
        <Button size='small' disabled={page <= 1 || loading} onClick={() => {
          if (part === 'staff') { scope.current.staff?.abort(); setStaffData(undefined); setStaffPage(page - 1) }
          else { scope.current.tasks?.abort(); setTaskData(undefined); setTaskPage(page - 1) }
        }}>{part === 'staff' ? labels.previousStaff : labels.previousTask}</Button>
        <Button size='small' disabled={page * 20 >= total || loading} onClick={() => {
          if (part === 'staff') { scope.current.staff?.abort(); setStaffData(undefined); setStaffPage(page + 1) }
          else { scope.current.tasks?.abort(); setTaskData(undefined); setTaskPage(page + 1) }
        }}>{part === 'staff' ? labels.nextStaff : labels.nextTask}</Button>
      </Stack>
    </Box>
  )
  return (
    <Card>
      <CardHeader title={labels.title} subheader={labels.subtitle} action={<Button onClick={refresh}>{labels.reload}</Button>} />
      <CardContent>
        <Stack spacing={4}>
          {scopeError !== undefined && <Alert severity='error'>{scopeError === 401 ? labels.expired : scopeError === 403 ? labels.forbidden : scopeError === 409 ? labels.scopeChanged : labels.authorityUnavailable}</Alert>}
          <Box component='section' aria-label={labels.staff}>
            <Typography variant='h6' className='mbe-2'>{labels.staff}</Typography>
            {staffLoading && <Box role='status'><CircularProgress size={20} /> {labels.loadingStaff}</Box>}
            {staffError && <Alert severity='error'>{labels.failed}</Alert>}
            {staff && <>
              <TableContainer sx={{ maxWidth: '100%', overflowX: 'auto' }}>
                <Table size='small' aria-label={labels.staff}>
                  <TableHead><TableRow>{[labels.name, labels.department, labels.action].map(label => <TableCell key={label}>{label}</TableCell>)}</TableRow></TableHead>
                  <TableBody>
                    {staff.staff.length === 0 ? <TableRow><TableCell colSpan={3}>{staff.total === 0 ? labels.emptyStaff : labels.emptyStaffPage}</TableCell></TableRow> : staff.staff.map(member => (
                      <TableRow key={member.userId} selected={selectedId === member.userId}>
                        <TableCell sx={{ overflowWrap: 'anywhere' }}>{member.name}</TableCell>
                        <TableCell sx={{ overflowWrap: 'anywhere' }}>{member.departmentName}</TableCell>
                        <TableCell><Button size='small' aria-label={`${labels.viewTasks}: ${member.name}`} aria-pressed={selectedId === member.userId} onClick={() => select(selectedId === member.userId ? undefined : member)}>{selectedId === member.userId ? labels.viewing : labels.viewTasks}</Button></TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
              {pager('staff', staff.pageNumber, staff.total, staffLoading)}
            </>}
          </Box>
          <Box component='section' aria-label={labels.tasks}>
            <Typography variant='h6' className='mbe-2'>{labels.tasks}</Typography>
            {selectedId && <Alert severity='info' action={<Button color='inherit' onClick={() => select()}>{labels.clearFilter}</Button>}>
              {labels.filtering}{selected ? `: ${selected.name} (${selected.departmentName})` : '…'}<Typography variant='caption' display='block'>{labels.allTasks}</Typography>
            </Alert>}
            {taskLoading && <Box role='status'><CircularProgress size={20} /> {labels.loadingTasks}</Box>}
            {taskError && <Alert severity='error'>{labels.failed}</Alert>}
            {taskResult && taskResult.taskState !== 'Connected' && <Alert severity='info'>{taskResult.taskState === 'TMS_TIMEOUT' ? labels.timeout : taskResult.taskState === 'TMS_CONTRACT_INVALID' ? labels.invalid : labels.unavailable} {labels.unknownCount}</Alert>}
            {taskList && <>
              <TableContainer sx={{ maxWidth: '100%', overflowX: 'auto' }}>
                <Table size='small' aria-label={labels.tasks} sx={{ tableLayout: 'fixed', minWidth: 580 }}>
                  <TableHead><TableRow>{[labels.taskTitle, labels.assignee, labels.status, labels.dueDate].map(label => <TableCell key={label}>{label}</TableCell>)}</TableRow></TableHead>
                  <TableBody>
                    {taskList.items.length === 0 ? <TableRow><TableCell colSpan={4}>{taskList.total === 0 ? labels.emptyTasks : labels.emptyTaskPage}</TableCell></TableRow> : taskList.items.map(task => (
                      <TableRow key={task.taskId}>
                        <TableCell sx={{ overflowWrap: 'anywhere', whiteSpace: 'normal' }}>{task.title}</TableCell>
                        <TableCell sx={{ overflowWrap: 'anywhere' }}><Typography variant='body2'>{task.assignee.name}</Typography><Typography variant='caption'>{task.assignee.departmentName}</Typography></TableCell>
                        <TableCell>{statusChip(task.status)}</TableCell>
                        <TableCell>{task.dueAt === null ? labels.noDueDate : new Date(task.dueAt).toLocaleDateString(isEn ? 'en-US' : 'vi-VN')}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
              {pager('tasks', taskList.pageNumber, taskList.total, taskLoading)}
            </>}
          </Box>
        </Stack>
      </CardContent>
    </Card>
  )
}
