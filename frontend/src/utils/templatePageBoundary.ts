import { notFound } from 'next/navigation'

// Unused upstream demonstrations are not DAS application functions.
export const disabledTemplatePage = (): never => notFound()
