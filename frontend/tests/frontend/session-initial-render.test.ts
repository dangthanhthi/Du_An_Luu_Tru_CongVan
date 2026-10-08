import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { runInNewContext } from 'node:vm'
import { test } from 'node:test'
import * as React from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import ts from 'typescript'

// Real React SSR uses the boundary's server snapshot. Browser token state is
// deliberately unavailable to SSR; it must neither expose children nor claim
// the browser is signed out before the first client session check.
for (const locale of ['vi', 'en']) test(`Initial ${locale} session render is unknown, without private content or a premature sign-in link`, () => {
  const exports: any = {}
  const source = readFileSync(new URL('../../src/components/DasSessionBoundary.tsx', import.meta.url), 'utf8')
  const code = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX } }).outputText
  const context = React.createContext(null)
  runInNewContext(code, { exports, require(name: string) {
    if (name === 'react') return React
    if (name === 'react/jsx-runtime') return require('react/jsx-runtime')
    if (name === '@/services/api') return { tokenManager: { getEpoch: () => null, getAuthenticatedEpoch: () => null }, captureSessionIntent: () => null }
    if (name === '@/services/browserSession') return { isBrowserSessionCleanupReady: () => true, observeBrowserSessionEpoch: () => true, subscribeBrowserSession: () => () => {} }
    if (name === '@/contexts/sessionIntentContext') return { SessionIntentContext: context }
    throw new Error('Unexpected import ' + name)
  } })
  const html = renderToStaticMarkup(React.createElement(exports.default, { locale }, React.createElement('p', null, 'PRIVATE_QA_CHILDREN')))
  assert.equal(html.includes('PRIVATE_QA_CHILDREN'), false)
  assert.equal(html.includes(`href="/${locale}/login"`), false, 'Unknown client state must not be rendered as signed out')
  assert.ok(html.includes('role="status"'))
  assert.ok(html.includes(locale === 'vi' ? 'Đang kiểm tra phiên' : 'Checking session'))
})
