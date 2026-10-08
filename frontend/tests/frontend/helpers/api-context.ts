import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createContext, Script } from 'node:vm'
import { webcrypto } from 'node:crypto'
import ts from 'typescript'
import type { FixtureLocks } from './session-fixture'
import { MemoryStorage } from './session-fixture'

const serviceRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../../src/services')
type Api = typeof import('../../../src/services/api')

// Real service modules, isolated module caches; no auth/API implementation mocks.
export function apiContext(storage: Storage, locks: FixtureLocks | null, fetch: typeof globalThis.fetch, globals: Record<string, unknown> = {}, entry = 'api'): Api {
  const context = createContext({ window: {}, navigator: { locks }, localStorage: storage, sessionStorage: new MemoryStorage(), crypto: webcrypto, process: { env: {} }, fetch,
    Headers, Response, FormData, Blob, Uint8Array, AbortController, AbortSignal, DOMException, setTimeout, clearTimeout, Event, ...globals })
  const modules = new Map<string, { exports: unknown }>()
  const load = (path: string): any => {
    assert.ok(path.startsWith(serviceRoot + '\\') || path.startsWith(serviceRoot + '/'), 'Fixture modules stay inside service root')
    if (modules.has(path)) return modules.get(path)!.exports
    const module = { exports: {} }; modules.set(path, module)
    const code = ts.transpileModule(readFileSync(path, 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText
    const run = new Script(`(function(exports, require, module) { ${code}\n })`, { filename: path }).runInContext(context)
    run(module.exports, (name: string) => { assert.ok(name.startsWith('.')); return load(resolve(dirname(path), name + '.ts')) }, module)
    return module.exports
  }
  return load(resolve(serviceRoot, entry + '.ts'))
}
