import assert from 'node:assert/strict'
import { test } from 'node:test'
import { resolve } from 'node:path'
import { pathToFileURL } from 'node:url'

const actions = [
  'getEcommerceData', 'getAcademyData', 'getLogisticsData', 'getInvoiceData', 'getUserData',
  'getPermissionsData', 'getProfileData', 'getFaqData', 'getPricingData', 'getStatisticsData'
]

for (const action of actions) {
  test(`Template server action ${action} stops before returning sample data`, async () => {
    const handlers = await import(pathToFileURL(resolve('src/app/server/actions.ts')).href)

    await assert.rejects(handlers[action](), (error: unknown) =>
      error instanceof Error && (error as Error & { digest?: string }).digest === 'NEXT_HTTP_ERROR_FALLBACK;404')
  })
}
