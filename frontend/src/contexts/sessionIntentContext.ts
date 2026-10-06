'use client'

import { createContext } from 'react'
import type { SessionIntent } from '@/services/api'

export const SessionIntentContext = createContext<SessionIntent | null>(null)
