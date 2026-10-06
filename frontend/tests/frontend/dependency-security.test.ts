import assert from 'node:assert/strict'
import { test } from 'node:test'
import { encode, getToken } from 'next-auth/jwt'
import { mergeAttributes } from '@tiptap/core'
import { DOMSerializer } from '@tiptap/pm/model'
import { createRequire } from 'node:module'
import { pathToFileURL } from 'node:url'
import { mkdtemp, writeFile, unlink, rmdir } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { loadConfigFromFile } from '@prisma/config'

for (const authorization of ['Bearer %', 'Bearer %E0%A4%A']) {
  test(`Malformed encoded Bearer token is rejected without throwing (${authorization})`, async () => {
    const req = { headers: { authorization }, cookies: {} }
    const result = await getToken({ req: req as never, secret: 'synthetic-test-secret' })
    assert.equal(result, null)
  })
}

test('NextAuth encrypted JWT still resolves the synthetic identity', async () => {
  const secret = 'synthetic-only-secret-with-sufficient-length'
  const token = await encode({ secret, token: { sub: 'synthetic-user', name: 'Fixture' }, maxAge: 60 })
  const result = await getToken({ secret, req: { headers: { authorization: `Bearer ${token}` }, cookies: {} } as never })
  assert.equal(result?.sub, 'synthetic-user')
  assert.equal(result?.name, 'Fixture')
})

test('Tiptap attribute merge cannot inherit executable attributes through __proto__', () => {
  const attributes = mergeAttributes(JSON.parse('{"__proto__":{"onclick":"synthetic()"},"class":"document-editor"}'))
  assert.equal(attributes.onclick, undefined)
  assert.equal(Object.getPrototypeOf(attributes), Object.prototype)
  assert.equal(attributes.class, 'document-editor')
  // The patched helper retains __proto__ as an inert own data property. Verify its consumer.
  const emitted = new Map<string, string>()
  const document = { createElement: () => ({ setAttribute: (name: string, value: string) => emitted.set(name, value) }) }
  DOMSerializer.renderSpec(document as unknown as Document, ['img', attributes])
  assert.equal(emitted.has('onclick'), false)
  assert.equal(emitted.get('class'), 'document-editor')
})

test('Tiptap class deduplication and style override keep editor behavior', () => {
  const attributes = mergeAttributes({ class: 'editor bold', style: 'color: red; margin: 0' }, { class: 'bold active', style: 'color: blue' })
  assert.equal(attributes.class, 'editor bold active')
  assert.equal(attributes.style, 'color: blue; margin: 0')
})

test('Prisma configuration merge dependency handles recursive records without stack exhaustion', async () => {
  const require = createRequire(import.meta.url)
  const configRequire = createRequire(require.resolve('@prisma/config'))
  const { deepmerge } = await import(pathToFileURL(configRequire.resolve('deepmerge-ts')).href)
  const left: Record<string, unknown> = { label: 'fixture' }
  const right: Record<string, unknown> = { enabled: true }
  left.self = left
  right.self = right
  const result = deepmerge(left, right)
  assert.equal(result.label, 'fixture')
  assert.equal(result.enabled, true)
  assert.equal(result.self, result)
})

test('Prisma consumer loads record-based config with the scoped merge override', async () => {
  const dir = await mkdtemp(join(tmpdir(), 'das-prisma-config-'))
  const file = join(dir, 'prisma.config.mjs')
  try {
    await writeFile(file, 'export default { schema: "./schema.prisma", migrations: { path: "./migrations" } }')
    const result = await loadConfigFromFile({ configRoot: dir, configFile: file })
    assert.equal(result.error, undefined)
    assert.equal(result.config?.schema, resolve(dir, 'schema.prisma'))
    assert.equal(result.config?.migrations?.path, resolve(dir, 'migrations'))
  } finally {
    await unlink(file)
    await rmdir(dir)
  }
})

test('Iconify Tools still cleans a local SVG without invoking archive download/extraction', async () => {
  const { SVG, cleanupSVG } = await import('@iconify/tools')
  const svg = new SVG('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16"><rect x="1" y="2" width="8" height="7" fill="#123456"/></svg>')
  cleanupSVG(svg)
  assert.match(svg.toString(), /viewBox="0 0 16 16"/)
  assert.match(svg.toString(), /#123456/)
})
