'use client'

import { useCallback, useEffect, useState } from 'react'
import { useParams } from 'next/navigation'

import { tokenManager } from '@/services/api'
import { i18n } from '@/configs/i18n'
import { useAppDictionary } from './useDictionary'
import { buildDasLauncher, buildDasShortcuts, getCachedCapabilityHints } from '@/components/layout/shared/dasLauncher'

export const useCachedCapabilityHints = () => {
  const [capabilities, setCapabilities] = useState<string[]>([])
  const refreshCapabilities = useCallback(() => {
    try {
      const hasToken = Boolean(tokenManager.getToken()?.trim())

      setCapabilities(getCachedCapabilityHints(hasToken, hasToken ? tokenManager.getUser() : null))
    } catch { setCapabilities([]) }
  }, [])

  useEffect(() => {
    refreshCapabilities()
    window.addEventListener('storage', refreshCapabilities)
    window.addEventListener('focus', refreshCapabilities)

    return () => {
      window.removeEventListener('storage', refreshCapabilities)
      window.removeEventListener('focus', refreshCapabilities)
    }
  }, [refreshCapabilities])

  return { capabilities, refreshCapabilities }
}

export const useDasLauncher = () => {
  const params = useParams()
  const { t } = useAppDictionary()
  const { capabilities, refreshCapabilities } = useCachedCapabilityHints()
  const lang = typeof params.lang === 'string' ? params.lang : i18n.defaultLocale
  const sections = buildDasLauncher(lang, capabilities, t.nav)

  return { sections, shortcuts: buildDasShortcuts(sections), refreshCapabilities, t }
}
