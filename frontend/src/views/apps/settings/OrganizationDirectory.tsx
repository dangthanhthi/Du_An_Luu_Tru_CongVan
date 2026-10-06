'use client'

import { useEffect, useRef, useState } from 'react'

import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import CardHeader from '@mui/material/CardHeader'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'
import Divider from '@mui/material/Divider'
import Grid from '@mui/material/Grid'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableContainer from '@mui/material/TableContainer'
import TableHead from '@mui/material/TableHead'
import TablePagination from '@mui/material/TablePagination'
import TableRow from '@mui/material/TableRow'
import Typography from '@mui/material/Typography'

import { useAppDictionary } from '@/hooks/useDictionary'
import type { DictionaryType } from '@/hooks/useDictionary'
import { directoryApi } from '@/services/das/directory'
import { V2ApiError } from '@/services/das/http'
import type { DirectoryPageDto, DirectoryUnitDto, DirectoryUserDto } from '@/types/das/directory'

const getErrorMessage = (error: unknown, labels: DictionaryType['directory']) => {
  const status = error instanceof V2ApiError ? error.status : undefined

  if (status === 401) return labels.signInRequired
  if (status === 403) return labels.forbidden
  if (status === 503) return labels.unavailable

  return labels.loadFailed
}

const OrganizationDirectory = () => {
  const { t } = useAppDictionary()
  const labels = t.directory
  const [units, setUnits] = useState<DirectoryPageDto<DirectoryUnitDto> | null>(null)
  const [unitsLoading, setUnitsLoading] = useState(true)
  const [unitsError, setUnitsError] = useState<string | null>(null)
  const [unitsPage, setUnitsPage] = useState(1)
  const [unitsPageSize, setUnitsPageSize] = useState(20)
  const [unitsRetry, setUnitsRetry] = useState(0)
  const [department, setDepartment] = useState<DirectoryUnitDto | null>(null)
  const [users, setUsers] = useState<DirectoryPageDto<DirectoryUserDto> | null>(null)
  const [usersLoading, setUsersLoading] = useState(false)
  const [usersError, setUsersError] = useState<string | null>(null)
  const [usersPage, setUsersPage] = useState(1)
  const [usersPageSize, setUsersPageSize] = useState(20)
  const [usersRetry, setUsersRetry] = useState(0)
  const unitsRequest = useRef<AbortController | null>(null)
  const usersRequest = useRef<AbortController | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    let current = true

    unitsRequest.current = controller
    setUnits(null)
    setUnitsLoading(true)
    setUnitsError(null)

    directoryApi.getDepartments({ includeGroups: true, pageNumber: unitsPage, pageSize: unitsPageSize }, controller.signal)
      .then(page => {
        if (current && !controller.signal.aborted) setUnits(page)
      })
      .catch((error: unknown) => {
        if (current && !controller.signal.aborted) setUnitsError(getErrorMessage(error, labels))
      })
      .finally(() => {
        if (current && !controller.signal.aborted) setUnitsLoading(false)
      })

    return () => {
      current = false
      controller.abort()
    }
  }, [unitsPage, unitsPageSize, unitsRetry, labels])

  const departmentId = department?.id

  useEffect(() => {
    const controller = new AbortController()
    let current = true

    usersRequest.current = controller
    setUsers(null)
    setUsersError(null)

    if (!departmentId) {
      setUsersLoading(false)

      return () => controller.abort()
    }

    setUsersLoading(true)

    directoryApi.getUsers({ departmentId, purpose: 'originator', pageNumber: usersPage, pageSize: usersPageSize }, controller.signal)
      .then(page => {
        if (current && !controller.signal.aborted) setUsers(page)
      })
      .catch((error: unknown) => {
        if (current && !controller.signal.aborted) setUsersError(getErrorMessage(error, labels))
      })
      .finally(() => {
        if (current && !controller.signal.aborted) setUsersLoading(false)
      })

    return () => {
      current = false
      controller.abort()
    }
  }, [departmentId, usersPage, usersPageSize, usersRetry, labels])

  const selectDepartment = (unit: DirectoryUnitDto) => {
    if (!unit.isDepartment || !unit.isActive || department?.id === unit.id) return

    usersRequest.current?.abort()
    setUsers(null)
    setUsersError(null)
    setUsersLoading(true)
    setUsersPage(1)
    setDepartment(unit)
  }

  const paginationLabels = {
    labelRowsPerPage: labels.rowsPerPage,
    labelDisplayedRows: ({ from, to, count }: { from: number; to: number; count: number }) =>
      `${from}–${to} ${labels.of} ${count}`,
    getItemAriaLabel: (type: 'first' | 'last' | 'next' | 'previous') =>
      type === 'next' ? labels.nextPage : labels.previousPage
  }

  return (
    <Grid container spacing={6}>
      <Grid size={{ xs: 12 }}>
        <Typography variant='h4'>{labels.title}</Typography>
        <Typography color='text.secondary' className='mbs-2'>{labels.managedAtEap}</Typography>
      </Grid>

      <Grid size={{ xs: 12, lg: 7 }}>
        <Card>
          <CardHeader title={labels.unitsTitle} />
          <Divider />
          <div aria-busy={unitsLoading}>
            {unitsLoading ? (
              <CardContent className='flex items-center gap-3' role='status'>
                <CircularProgress size={24} />
                <Typography>{labels.loadingUnits}</Typography>
              </CardContent>
            ) : unitsError ? (
              <CardContent>
                <Alert severity='error' action={<Button color='inherit' onClick={() => setUnitsRetry(value => value + 1)}>{labels.retry}</Button>}>
                  {unitsError}
                </Alert>
              </CardContent>
            ) : units && units.items.length > 0 ? (
              <>
                <TableContainer>
                  <Table aria-label={labels.unitsTitle}>
                    <TableHead>
                      <TableRow>
                        <TableCell>{labels.name}</TableCell>
                        <TableCell>{labels.code}</TableCell>
                        <TableCell>{labels.unitType}</TableCell>
                        <TableCell>{labels.status}</TableCell>
                        <TableCell><span className='sr-only'>{labels.viewStaff}</span></TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {units.items.map(unit => (
                        <TableRow key={unit.id} selected={department?.id === unit.id}>
                          <TableCell component='th' scope='row'>{unit.name}</TableCell>
                          <TableCell>{unit.code || '—'}</TableCell>
                          <TableCell>
                            <Chip
                              size='small'
                              variant='tonal'
                              color={unit.isDepartment ? 'primary' : 'info'}
                              label={unit.isDepartment ? (unit.parentId ? labels.department : labels.management) : labels.group}
                            />
                          </TableCell>
                          <TableCell>{unit.isActive ? labels.active : labels.inactive}</TableCell>
                          <TableCell>
                            {unit.isDepartment && unit.isActive && (
                              <Button
                                size='small'
                                variant='text'
                                onClick={() => selectDepartment(unit)}
                                aria-label={`${labels.viewStaff}: ${unit.name}`}
                                aria-pressed={department?.id === unit.id}
                              >
                                {labels.viewStaff}
                              </Button>
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              </>
            ) : (
              <CardContent role='status'><Typography color='text.secondary'>{labels.emptyUnits}</Typography></CardContent>
            )}
          </div>
          {units && !unitsLoading && !unitsError && (
            <TablePagination
              component='div'
              count={units.totalCount}
              page={units.pageNumber - 1}
              rowsPerPage={units.pageSize}
              rowsPerPageOptions={[10, 20, 50]}
              onPageChange={(_, page) => {
                unitsRequest.current?.abort()
                setUnits(null)
                setUnitsLoading(true)
                setUnitsPage(page + 1)
              }}
              onRowsPerPageChange={event => {
                unitsRequest.current?.abort()
                setUnits(null)
                setUnitsLoading(true)
                setUnitsPage(1)
                setUnitsPageSize(Number(event.target.value))
              }}
              {...paginationLabels}
            />
          )}
        </Card>
      </Grid>

      <Grid size={{ xs: 12, lg: 5 }}>
        <Card>
          <CardHeader title={labels.staffTitle} subheader={department ? `${labels.selectedDepartment}: ${department.name}` : undefined} />
          <Divider />
          <div aria-busy={usersLoading}>
            {!department ? (
              <CardContent><Typography color='text.secondary'>{labels.selectDepartment}</Typography></CardContent>
            ) : usersLoading ? (
              <CardContent className='flex items-center gap-3' role='status'>
                <CircularProgress size={24} />
                <Typography>{labels.loadingStaff}</Typography>
              </CardContent>
            ) : usersError ? (
              <CardContent>
                <Alert severity='error' action={<Button color='inherit' onClick={() => setUsersRetry(value => value + 1)}>{labels.retry}</Button>}>
                  {usersError}
                </Alert>
              </CardContent>
            ) : users && users.items.length > 0 ? (
              <TableContainer>
                <Table aria-label={labels.staffTitle}>
                  <TableHead><TableRow><TableCell>{labels.staffName}</TableCell></TableRow></TableHead>
                  <TableBody>
                    {users.items.map(user => (
                      <TableRow key={user.id}><TableCell>{user.displayName}</TableCell></TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            ) : (
              <CardContent role='status'><Typography color='text.secondary'>{labels.emptyStaff}</Typography></CardContent>
            )}
          </div>
          {users && !usersLoading && !usersError && (
            <TablePagination
              component='div'
              count={users.totalCount}
              page={users.pageNumber - 1}
              rowsPerPage={users.pageSize}
              rowsPerPageOptions={[10, 20, 50]}
              onPageChange={(_, page) => {
                usersRequest.current?.abort()
                setUsers(null)
                setUsersLoading(true)
                setUsersPage(page + 1)
              }}
              onRowsPerPageChange={event => {
                usersRequest.current?.abort()
                setUsers(null)
                setUsersLoading(true)
                setUsersPage(1)
                setUsersPageSize(Number(event.target.value))
              }}
              {...paginationLabels}
            />
          )}
        </Card>
      </Grid>
    </Grid>
  )
}

export default OrganizationDirectory
