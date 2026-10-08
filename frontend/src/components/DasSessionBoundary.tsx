'use client'

import { useLayoutEffect, useMemo, useState, useSyncExternalStore } from 'react'
import type { ReactNode } from 'react'

import { captureSessionIntent, tokenManager } from '@/services/api'
import { isBrowserSessionCleanupReady, observeBrowserSessionEpoch, subscribeBrowserSession } from '@/services/browserSession'
import { SessionIntentContext } from '@/contexts/sessionIntentContext'

const subscribe = (onChange: () => void) => subscribeBrowserSession(onChange, tokenManager.getEpoch)
const serverSnapshot = () => null

export default function DasSessionBoundary({ children, locale }: { children: ReactNode; locale: string }) {
  const epoch = useSyncExternalStore(subscribe, tokenManager.getAuthenticatedEpoch, serverSnapshot)
  const sessionIntent = useMemo(() => captureSessionIntent(epoch), [epoch])
  const storageEpoch = tokenManager.getEpoch()
  const cleanupReady = isBrowserSessionCleanupReady(storageEpoch)
  const [cleanedEpoch, setCleanedEpoch] = useState(tokenManager.getEpoch)
  const [hasMounted, setHasMounted] = useState(false)

  useLayoutEffect(() => { setHasMounted(true) }, [])

  useLayoutEffect(() => {
    const ready = observeBrowserSessionEpoch(storageEpoch)
    const next = ready ? storageEpoch : null
    if (cleanedEpoch !== next) setCleanedEpoch(next)
  }, [cleanedEpoch, storageEpoch, cleanupReady])

  // SSR cannot inspect the browser's session. Keep the gate closed while that
  // state is unknown instead of announcing a false signed-out state on reload.
  if (!hasMounted) return <div role='status'>{locale === 'vi' ? 'Đang kiểm tra phiên…' : 'Checking session…'}</div>
  if (!cleanupReady) return <div role='status'>{locale === 'vi' ? 'Không thể cập nhật phiên. Vui lòng kiểm tra quyền lưu trữ của trình duyệt và thử lại.' : 'Cannot update the session. Check browser storage permissions and try again.'}</div>
  if (!epoch) return (
    <div role='status'>
      {locale === 'vi' ? 'Vui lòng đăng nhập để tiếp tục. ' : 'Please sign in to continue. '}
      <a href={`/${locale}/login`}>{locale === 'vi' ? 'Đăng nhập' : 'Sign in'}</a>
    </div>
  )
  // React can detect a new snapshot during another render before any storage
  // event arrives. Clean private storage before mounting that account's children.
  if (cleanedEpoch !== epoch) return <div role='status'>{locale === 'vi' ? 'Đang cập nhật phiên…' : 'Updating session…'}</div>
  // Account changes dispose page state and effects; token rotation keeps drafts.
  return <SessionIntentContext.Provider key={epoch} value={sessionIntent}>{children}</SessionIntentContext.Provider>
}
