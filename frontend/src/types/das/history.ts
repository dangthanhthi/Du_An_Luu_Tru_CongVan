export type HistoryQuery = { pageNumber?: number; pageSize?: number; throughVersion?: string }
export type HistoryEvent = { id: string; actorUserId: string; version: string; occurredAt: string }
export type PartnerAuditEvent = HistoryEvent & { action: 'Create' | 'Update' | 'Delete' | 'Restore'; changesAvailability: 'NotRecorded' }
export type DocumentLifecycleEvent = HistoryEvent & { action: 'Distribute' | 'Cancel' | 'Restore'; fromStatus: 'InProgress' | 'Distributed' | 'Cancelled'; toStatus: 'InProgress' | 'Distributed' | 'Cancelled'; cancellationReason: string | null }
export type HistoryPage<T extends HistoryEvent> = { throughVersion: string; items: T[]; totalCount: number; pageNumber: number; pageSize: number }
export type PartnerAuditPage = HistoryPage<PartnerAuditEvent> & { partnerId: string }
export type DocumentLifecyclePage = HistoryPage<DocumentLifecycleEvent> & { documentId: string; coverage: 'V2LifecycleOnly' }
