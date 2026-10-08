import assert from 'node:assert/strict'
import { test } from 'node:test'
import { componentHarness, nodes } from './helpers/component-harness'

function fixture() {
  let focused = '', listener: any
  const writes: any[] = []
  const location = { href: '', search: '?kind=Internal&view=all&pageNumber=2' }
  const settings = { mode: 'light', skin: 'default', layout: 'vertical', primaryColor: '#7367F0', contentWidth: 'compact' }
  const ui = componentHarness(new URL('../../src/@core/components/customizer/index.tsx', import.meta.url), {
    'next/navigation': { usePathname: () => '/vi/apps/documents/list' }, 'next/link': { default: 'Link' },
    '@mui/material/styles': { useTheme: () => ({ breakpoints: { values: { lg: 1200 } } }) },
    classnames: { default: (...args: any[]) => args.filter(x => typeof x === 'string').join(' ') },
    'react-use': { useDebounce() {}, useMedia: () => false },
    'react-colorful': { HexColorPicker: 'HexColorPicker', HexColorInput: 'HexColorInput' },
    'react-perfect-scrollbar': { default: 'PerfectScrollbar' },
    '@configs/primaryColorConfig': { default: [{ main: '#7367F0' }, { main: '#0D9394' }] },
    '@core/hooks/useSettings': { useSettings: () => ({ settings, updateSettings: (v: any) => writes.push(v), resetSettings() {}, isSettingsChanged: false }) },
    './styles.module.css': { default: new Proxy({}, { get: (_, key) => String(key) }) },
    ...Object.fromEntries(['SkinDefault', 'SkinBordered', 'LayoutVertical', 'LayoutCollapsed', 'LayoutHorizontal', 'ContentCompact', 'ContentWide', 'DirectionLtr', 'DirectionRtl'].map(name => [`@core/svg/${name}`, { default: name }]))
  }, { document: { addEventListener: (_: string, fn: any) => listener = fn, removeEventListener() {}, activeElement: null }, window: { location } })
  const open = () => {
    const trigger = nodes(ui.render()).find(n => n.props['aria-controls'] === 'das-theme-customizer')
    assert.ok(trigger, 'The visible settings launcher must have a keyboard-accessible control')
    assert.equal(trigger.type, 'button')
    trigger.props.ref.current = { focus() { focused = 'launcher' } }
    trigger.props.onClick()
    const tree = ui.render()
    const close = nodes(tree).find(n => n.props['data-customizer-close'])!
    assert.ok(close, 'A labelled close button must be available')
    close.props.ref.current = { focus() { focused = 'close' } }
    nodes(tree).find(n => n.props['data-customizer-color'])!.props.ref.current = { focus() { focused = 'color' }, contains() { return false } }
    ui.commit()
    return tree
  }
  return { ui, open, writes, location, focus: () => focused, key: (key: string) => listener?.({ key, preventDefault() {}, stopPropagation() {} }) }
}

test('Customizer launcher opens a labelled region, focuses close and Escape returns to launcher', () => {
  const f = fixture(), tree = f.open()
  const region = nodes(tree).find(n => n.props.id === 'das-theme-customizer')!
  assert.ok(region.props['aria-labelledby'])
  assert.equal(region.props.inert, false)
  assert.equal(f.focus(), 'close')
  f.key('Escape')
  assert.equal(nodes(f.ui.render()).find(n => n.props.id === 'das-theme-customizer')?.props.inert, true)
  assert.equal(f.focus(), 'launcher')
  f.ui.unmount()
})

test('All customizer click targets are named native buttons, so keyboard activation reaches every setting', () => {
  const f = fixture(), clickable = nodes(f.open()).filter(n => n.props.onClick)
  assert.ok(clickable.length > 15)
  for (const n of clickable) {
    assert.equal(n.type, 'button', 'No setting may require mouse-only activation')
    assert.equal(n.props.type, 'button')
    assert.ok(n.props['aria-label'], 'Icon and preview controls must have an accessible name')
  }
  f.ui.unmount()
})

test('Palette and mode selections expose current state and update the correct setting', () => {
  const f = fixture(), tree = f.open()
  const controls = nodes(tree)
  const light = controls.find(n => n.props['data-setting'] === 'mode:light')!
  const dark = controls.find(n => n.props['data-setting'] === 'mode:dark')!
  const palette = controls.find(n => n.props['data-setting'] === 'primaryColor:#0D9394')!
  assert.equal(light?.props['aria-pressed'], true)
  assert.equal(dark?.props['aria-pressed'], false)
  dark.props.onClick(); palette.props.onClick()
  assert.deepEqual(JSON.parse(JSON.stringify(f.writes)), [{ mode: 'dark' }, { primaryColor: '#0D9394' }])
  f.ui.unmount()
})

test('Escape dismisses the color popup before closing the settings region', () => {
  const f = fixture(), tree = f.open()
  nodes(tree).find(n => n.props['data-customizer-color'])!.props.onClick()
  f.ui.render(); f.ui.commit()
  f.key('Escape')
  const after = f.ui.render()
  f.ui.commit()
  assert.equal(nodes(after).find(n => n.type === 'Popper')?.props.open, false)
  assert.equal(nodes(after).find(n => n.props.id === 'das-theme-customizer')?.props.inert, false)
  assert.equal(f.focus(), 'color', 'Closing only the popup must preserve focus on its own launcher')
  f.ui.unmount()
})

test('Customizer language navigation preserves the current document filters and page', () => {
  const f = fixture()
  nodes(f.open()).find(n => n.props['aria-label'] === 'English')!.props.onClick()
  assert.equal(f.location.href, '/en/apps/documents/list?kind=Internal&view=all&pageNumber=2')
  f.ui.unmount()
})
