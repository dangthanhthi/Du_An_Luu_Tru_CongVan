import assert from 'node:assert/strict'
import { test } from 'node:test'
import { POST as login } from '../../src/app/api/login/route'
import { POST as emailTest } from '../../src/app/api/email/test/route'
import { POST as emailScan } from '../../src/app/api/email/scan/route'
import { POST as ocr } from '../../src/app/api/ocr/analyze/route'

test('Template login cannot authenticate sample accounts when backend is unavailable', async () => {
  const original = globalThis.fetch
  let calls = 0
  globalThis.fetch = async () => { calls++; throw new TypeError('synthetic unavailable backend') }
  try {
    const response = await login(new Request('http://localhost/api/login', { method: 'POST', body: JSON.stringify({ username: 'admin', password: 'password' }) }))
    assert.equal(response.status, 410)
    assert.equal(calls, 0)
    const body = await response.json()
    assert.equal(body.code, 'TEMPLATE_LOGIN_DISABLED')
    assert.equal(body.accessToken, undefined)
    assert.equal(body.role, undefined)
    assert.equal(response.headers.get('set-cookie'), null)
  } finally { globalThis.fetch = original }
})

for (const [name, handler, status, code] of [
  ['template login', login, 410, 'TEMPLATE_LOGIN_DISABLED'],
  ['email test', emailTest, 503, 'INTEGRATION_DEFERRED'],
  ['email scan', emailScan, 503, 'INTEGRATION_DEFERRED'],
  ['OCR', ocr, 503, 'INTEGRATION_DEFERRED']
] as const) {
  test(`${name} rejects before consuming untrusted request payload`, async () => {
    let reads = 0
    const request = { json: async () => { reads++; throw new Error('synthetic payload must not be read') }, formData: async () => { reads++; throw new Error('must not be read') } }
    const response = await handler(request as unknown as Request)
    assert.equal(response.status, status)
    assert.equal((await response.json()).code, code)
    assert.equal(reads, 0)
    assert.equal(response.headers.get('cache-control'), 'no-store')
  })
}
