import assert from 'node:assert/strict'
import { test } from 'node:test'
import { getUserMenuIdentity } from '../../src/components/layout/shared/userMenuIdentity'

test('An anonymous visitor has no fabricated account or Admin role', () => {
  assert.equal(getUserMenuIdentity(false, null), null)
})
test('A stale cached administrator without an access token is not shown as signed in', () => {
  assert.equal(getUserMenuIdentity(false, { fullName: 'Cached administrator', role: 'Admin' }), null)
})
test('An access token without identity metadata does not invent an administrator', () => {
  assert.equal(getUserMenuIdentity(true, null), null)
})
test('Malformed identity arrays and primitives have no display identity', () => {
  for (const value of [[], 'admin', 42, true]) assert.equal(getUserMenuIdentity(true, value), null)
})
test('Existing account name and known role remain presentation data', () => {
  assert.deepEqual(getUserMenuIdentity(true, { fullName: '  Registered operator  ', userName: 'operator', role: 'Secretary' }),
    { name: 'Registered operator', role: 'Secretary' })
})
test('An account with no role remains roleless', () => {
  assert.deepEqual(getUserMenuIdentity(true, { userName: 'operator', roles: [] }), { name: 'operator', role: null })
})
test('A blank full name uses the actual username', () => {
  assert.deepEqual(getUserMenuIdentity(true, { fullName: ' ', userName: 'operator', role: 'Staff' }), { name: 'operator', role: 'Staff' })
})
test('Missing or invalid name and role fields do not become Admin placeholders', () => {
  for (const value of [{ id: 'account' }, { fullName: 42, role: false }]) {
    assert.deepEqual(getUserMenuIdentity(true, value), { name: null, role: null })
  }
})
test('A custom role is preserved without conversion to a generic Admin', () => {
  assert.deepEqual(getUserMenuIdentity(true, { userName: 'operator', role: 'Team Lead' }), { name: 'operator', role: 'Team Lead' })
})
