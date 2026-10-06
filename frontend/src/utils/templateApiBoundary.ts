import { NextResponse } from 'next/server'

export const disabledTemplateData = () =>
  NextResponse.json({ code: 'TEMPLATE_DATA_DISABLED' }, { status: 410, headers: { 'Cache-Control': 'no-store' } })
