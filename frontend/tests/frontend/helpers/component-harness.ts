import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { runInNewContext } from 'node:vm'
import ts from 'typescript'

export type Node = { type: unknown; props: Record<string, any> }
// Executes real component callbacks with deterministic hook commits. This is
// a component fixture, not a DOM, browser or accessibility verification.
export function componentHarness(source: URL, imports: Record<string, unknown>, globals: Record<string, unknown> = {}) {
  const slots: any[] = []
  let cursor = 0
  let pending: (() => void)[] = []
  const depsChanged = (a: unknown[] | undefined, b: unknown[] | undefined) => !a || !b || a.length !== b.length || a.some((v, i) => !Object.is(v, b[i]))
  const react = {
    useState(initial: any) {
      const index = cursor++
      if (!(index in slots)) slots[index] = typeof initial === 'function' ? initial() : initial
      return [slots[index], (next: any) => { slots[index] = typeof next === 'function' ? next(slots[index]) : next }]
    },
    useRef(initial: any) { const index = cursor++; return slots[index] ??= { current: initial } },
    useMemo(work: () => any, deps: unknown[]) {
      const index = cursor++
      if (depsChanged(slots[index]?.deps, deps)) slots[index] = { deps, value: work() }
      return slots[index].value
    },
    useCallback(work: any, deps: unknown[]) { return react.useMemo(() => work, deps) },
    useEffect(work: () => any, deps?: unknown[]) {
      const index = cursor++
      if (depsChanged(slots[index]?.deps, deps)) {
        const previous = slots[index]
        slots[index] = { deps, effect: true, cleanup: previous?.cleanup }
        pending.push(() => { previous?.cleanup?.(); slots[index].cleanup = work() })
      }
    }
  }
  const exports: any = {}
  const code = ts.transpileModule(readFileSync(source, 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX } }).outputText
  runInNewContext(code, { exports, AbortController, setTimeout, clearTimeout, setInterval, clearInterval, ...globals, require(name: string) {
    if (name === 'react') return react
    if (name === 'react/jsx-runtime') return { jsx: (type: unknown, props: any) => ({ type, props }), jsxs: (type: unknown, props: any) => ({ type, props }), Fragment: 'Fragment' }
    if (name in imports) return imports[name]
    if (name === '@mui/material') return new Proxy({}, { get: (_, key) => key })
    if (name.startsWith('@mui/material/') || name.startsWith('@mui/lab/')) return { default: name.split('/').at(-1) }
    assert.fail('Unexpected component import ' + name)
  } })
  return {
    render: (props: any = {}): Node => { cursor = 0; return exports.default(props) },
    commit() { const effects = pending; pending = []; effects.forEach(effect => effect()) },
    unmount() { pending = []; slots.filter(slot => slot?.effect).forEach(slot => slot.cleanup?.()) },
  }
}
export function nodes(root: any): Node[] {
  if (Array.isArray(root)) return root.flatMap(nodes)
  if (!root || typeof root !== 'object' || !root.props) return []
  return [root, ...nodes(root.props.children)]
}
export function textContent(root: any): string {
  if (Array.isArray(root)) return root.map(textContent).join(' ')
  if (root == null || typeof root === 'boolean') return ''
  if (typeof root !== 'object') return String(root)
  return textContent(root.props?.children)
}
