import { ApiRequestError, documentApi } from '@/services/api'
import { documentsV2Api } from '@/services/das/documents'
import { incompleteReportsApi, getMyStaffMembers, getMyStaffTasks } from '@/services/das/reports-staff'
import type { IncompleteReport, MyStaffPage, MyStaffTasksResult } from '@/services/das/reports-staff'
import { notificationsApi } from '@/services/das/notifications'
import type { InAppNotification } from '@/services/das/notifications'
import type { DocumentKind, DocumentPage } from '@/types/das/documents'

export type DashboardScope = 'all' | 'mine' | 'department'
export type Section<T> = { state: 'ready'; data: T } | { state: 'error'; status: number }
type Documents = { page: DocumentPage; canRegister: boolean }
export type OverviewData = {
  Incoming: Documents; Outgoing: Documents; Internal: Documents
  report: IncompleteReport
  notifications: { items: InAppNotification[]; totalCount: number; unread: number }
  staff: { members: MyStaffPage; tasks: Section<MyStaffTasksResult> }
}
export type OverviewSections = { [K in keyof OverviewData]?: Section<OverviewData[K]> }
export type PublishSection = <K extends keyof OverviewData>(name: K, section: Section<OverviewData[K]>) => void
const sources = {
  documents: documentApi.getScopedList, options: documentsV2Api.options,
  report: incompleteReportsApi.list, notificationList: notificationsApi.list, unread: notificationsApi.unread,
  staff: getMyStaffMembers, staffTasks: getMyStaffTasks
}
async function read<T>(work: () => Promise<T>): Promise<Section<T>> {
  try { return { state: 'ready', data: await work() } }
  catch (error) { return { state: 'error', status: error instanceof ApiRequestError ? error.status : 0 } }
}
// Publish widgets independently. Exact counts come from server totals, never
// from the displayed page; report and document scopes remain separate.
export async function loadOverview(scope: DashboardScope, signal: AbortSignal, publish: PublishSection, api = sources) {
  if (signal.aborted) return
  async function section<K extends keyof OverviewData>(name: K, work: () => Promise<OverviewData[K]>) {
    const result = await read(work)
    if (!signal.aborted) publish(name, result)
  }
  await Promise.all([
    ...(['Incoming', 'Outgoing', 'Internal'] as DocumentKind[]).map(kind => section(kind, async () => {
      const [page, options] = await Promise.all([
        api.documents({ kind, view: scope, pageNumber: 1, pageSize: 6 }, signal), read(() => api.options(kind, signal))
      ])
      return { page, canRegister: options.state === 'ready' && options.data.canRegister }
    })),
    section('report', () => api.report({ includeRecent: false, pageNumber: 1, pageSize: 5 }, signal)),
    section('notifications', async () => {
      const [page, unread] = await Promise.all([api.notificationList(1, 5, signal), api.unread(signal)])
      return { items: page.items, totalCount: page.totalCount, unread }
    }),
    section('staff', async () => {
      const [members, tasks] = await Promise.all([api.staff(1, 5, signal), read(() => api.staffTasks({ pageNumber: 1, pageSize: 1 }, signal))])
      return { members, tasks }
    })
  ])
}
