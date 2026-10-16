'use client'

import { useLayoutEffect, useMemo, useState, useSyncExternalStore } from 'react'
import type { ReactNode } from 'react'

import { captureSessionIntent, tokenManager } from '@/services/api'
import { isBrowserSessionCleanupReady, observeBrowserSessionEpoch, subscribeBrowserSession } from '@/services/browserSession'
import { SessionIntentContext } from '@/contexts/sessionIntentContext'
import NotAuthorized from '@views/NotAuthorized'
import type { SystemMode } from '@core/types'

const subscribe = (onChange: () => void) => subscribeBrowserSession(onChange, tokenManager.getEpoch)
const serverSnapshot = () => null

export default function DasSessionBoundary({ children, locale, mode = 'light' }: { children: ReactNode; locale: string; mode?: SystemMode }) {
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

  useLayoutEffect(() => {
    // Wait for the client session check and cleanup; anonymous entries and
    // expired sessions must reach the login form without exposing children.
    if (hasMounted && cleanupReady && !epoch) window.location.replace(`/${locale}/login`)
  }, [hasMounted, cleanupReady, epoch, locale])

  const sessionStatus = (title: string, description: string, signedOut = false, busy = false) => (
    <NotAuthorized mode={mode} sessionStatus title={title} description={description} code={signedOut ? '401' : null}
      busy={busy} action={signedOut ? { label: locale === 'vi' ? 'Về trang chủ' : 'Back to home', href: `/${locale}` } : null}
      secondaryAction={signedOut ? { label: locale === 'vi' ? 'Đăng nhập' : 'Sign in', href: `/${locale}/login` } : undefined} />
  )

  // SSR cannot inspect the browser's session. Keep the gate closed while that
  // state is unknown instead of announcing a false signed-out state on reload.
  if (!hasMounted) return sessionStatus(locale === 'vi' ? 'Đang kiểm tra phiên…' : 'Checking session…', locale === 'vi' ? 'Vui lòng chờ trong giây lát.' : 'Please wait a moment.', false, true)
  if (!cleanupReady) return sessionStatus(locale === 'vi' ? 'Không thể cập nhật phiên' : 'Cannot update the session', locale === 'vi' ? 'Vui lòng kiểm tra quyền lưu trữ của trình duyệt rồi tải lại trang.' : 'Check browser storage permissions, then reload this page.')
  if (!epoch) return sessionStatus(locale === 'vi' ? 'Vui lòng đăng nhập để tiếp tục' : 'Please sign in to continue', locale === 'vi' ? 'Đang chuyển đến trang đăng nhập. Nếu trang chưa chuyển, hãy bấm nút bên dưới.' : 'Redirecting to sign in. If the page does not redirect, use the button below.', true, true)
  // React can detect a new snapshot during another render before any storage
  // event arrives. Clean private storage before mounting that account's children.
  if (cleanedEpoch !== epoch) return sessionStatus(locale === 'vi' ? 'Đang cập nhật phiên…' : 'Updating session…', locale === 'vi' ? 'Vui lòng chờ trong giây lát.' : 'Please wait a moment.', false, true)
  // Account changes dispose page state and effects; token rotation keeps drafts.
  return <SessionIntentContext.Provider key={epoch} value={sessionIntent}>{children}</SessionIntentContext.Provider>
}
