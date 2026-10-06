import assert from 'node:assert/strict'
import { test } from 'node:test'
import { resolve } from 'node:path'
import { pathToFileURL } from 'node:url'

const routes = [
  'apps/academy', 'apps/user-list', 'apps/invoice', 'apps/permissions', 'apps/ecommerce', 'apps/logistics',
  'pages/faq', 'pages/widget-examples', 'pages/pricing', 'pages/profile'
]

for (const route of routes) {
  test(`Template GET ${route} returns a disabled boundary without sample data`, async () => {
    const handler = await import(pathToFileURL(resolve(`src/app/api/${route}/route.ts`)).href)
    const response: Response = await handler.GET()

    assert.equal(response.status, 410)
    assert.equal(response.headers.get('cache-control'), 'no-store')
    assert.deepEqual(await response.json(), { code: 'TEMPLATE_DATA_DISABLED' })
    assert.equal(response.headers.get('set-cookie'), null)
  })
}
