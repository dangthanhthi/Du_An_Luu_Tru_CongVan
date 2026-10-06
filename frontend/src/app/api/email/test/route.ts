import { NextResponse } from 'next/server'

// Deferred by the project owner. Keep network/OCR processing out of active routes.
export async function POST(_request: Request) {
  return NextResponse.json(
    { success: false, code: 'INTEGRATION_DEFERRED', message: 'Tính năng này đang tạm hoãn.' },
    { status: 503, headers: { 'Cache-Control': 'no-store' } }
  )
}
