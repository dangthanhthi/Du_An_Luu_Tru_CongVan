import assert from 'node:assert/strict'
import { test } from 'node:test'
import { apiContext } from './helpers/api-context'
import { FixtureLocks, MemoryStorage, seedSession } from './helpers/session-fixture'

for (const code of ['PARTNER_DUPLICATE', 'PARTNER_VERSION_CONFLICT', 'PARTNER_STATE_CONFLICT']) {
  test(`API preserves ${code} from controller errors without relying on message text`, async () => {
    const storage = new MemoryStorage(); seedSession(storage)
    const api = apiContext(storage, new FixtureLocks(), async () => Response.json({ success: false, message: 'Localized message', errors: [{ field: '', code, message: 'Other text' }], traceId: 'fixture-trace' }, { status: 409 }))
    await assert.rejects(api.requestApiEnvelope('partner', '/fixture'), error => {
      assert.ok(error instanceof api.ApiRequestError)
      assert.equal((error as any).code, code)
      assert.equal(error.status, 409)
      assert.equal(error.traceId, 'fixture-trace')
      return true
    })
  })
}
test('API rejects invalid machine codes and suppresses server failure details', async () => {
  const storage = new MemoryStorage(); seedSession(storage)
  const api = apiContext(storage, new FixtureLocks(), async () => Response.json({ success: false, code: '<private diagnostic>', message: 'private diagnostic' }, { status: 503 }))
  await assert.rejects(api.requestApiEnvelope('partner', '/fixture'), error => {
    assert.ok(error instanceof api.ApiRequestError)
    assert.equal((error as any).code, undefined)
    assert.ok(!error.message.includes('private diagnostic'))
    return true
  })
})
test('different field error codes cannot be mistaken for one duplicate conflict', async () => {
  const storage = new MemoryStorage(); seedSession(storage)
  const api = apiContext(storage, new FixtureLocks(), async () => Response.json({ success: false, errors: [{ code: 'PARTNER_DUPLICATE' }, { code: 'VALIDATION_FAILED' }] }, { status: 409 }))
  await assert.rejects(api.requestApiEnvelope('partner', '/fixture'), error => {
    assert.ok(error instanceof api.ApiRequestError); assert.equal(error.code, undefined); return true
  })
})
