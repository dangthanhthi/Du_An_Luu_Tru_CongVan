'use client'

import { useContext } from 'react'

import { captureSessionIntent } from '@/services/api'
import { SessionIntentContext } from '@/contexts/sessionIntentContext'

const unavailableIntent = captureSessionIntent(null)

// New descendants inherit the displayed account, even before a queued storage
// event disposes their old parent. Standalone consumers have no authenticated owner.
export const useSessionIntent = () => useContext(SessionIntentContext) ?? unavailableIntent
