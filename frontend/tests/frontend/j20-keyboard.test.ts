import assert from 'node:assert/strict'
import { test } from 'node:test'
import { componentHarness, nodes } from './helpers/component-harness'

const dictionary = { userMenu: { openMenu: 'User menu', signedOut: 'Signed out', unknownUser: 'Unknown', dashboard: 'Dashboard', signIn: 'Sign in' }, search: { openShortcuts: 'Shortcuts', shortcuts: 'Shortcuts' } }
function expanded(ui: any, props: any) {
  const tree = ui.render(props), pop = nodes(tree).find(n => n.type === 'Popper')!
  return { tree, pop, inside: pop.props.children({ TransitionProps: {}, placement: 'bottom-end' }) }
}
for (const component of ['ModeDropdown', 'LanguageDropdown', 'UserDropdown', 'ShortcutsDropdown']) {
  test(`${component} Escape closes from trigger and returns focus`, () => {
    const ui = componentHarness(new URL(`../../src/components/layout/shared/${component}.tsx`, import.meta.url), {
      'next/link': { default: 'Link' }, 'next/navigation': { useParams: () => ({ lang: 'en' }), usePathname: () => '/en/dashboards/overview', useSearchParams: () => new URLSearchParams(), useRouter: () => ({ push() {} }) },
      '@core/hooks/useSettings': { useSettings: () => ({ settings: { mode: 'light' }, updateSettings() {} }) },
      '@mui/material/styles': { styled: () => () => 'BadgeContentSpan' },
      '@/utils/i18n': { getLocalizedUrl: (s: string) => s }, '@/services/api': { tokenManager: { getEpoch: () => null, getToken: () => null }, authApi: {} },
      '@/services/browserSession': { subscribeBrowserSession: () => () => {} }, './userMenuIdentity': { getUserMenuIdentity: () => null },
      '@/hooks/useDictionary': { useAppDictionary: () => ({ t: dictionary }) },
      classnames: { default: () => '' }, 'react-perfect-scrollbar': { default: 'PerfectScrollbar' }, '@core/components/mui/Avatar': { default: 'CustomAvatar' }, '@configs/themeConfig': { default: { layoutPadding: 24 } },
      '@mui/material/useMediaQuery': { default: () => false }
    }, { URLSearchParams, window: { addEventListener() {}, removeEventListener() {}, innerHeight: 900 } })
    const props = { shortcuts: [{ url: '/en/dashboards/overview', title: 'Dashboard', subtitle: 'Overview', icon: 'x' }] }
    let result = expanded(ui, props)
    let trigger = nodes(result.tree).find(n => n.type === (component === 'UserDropdown' ? 'Avatar' : 'IconButton'))!
    let focused = 0
    trigger.props.ref.current = { focus() { focused++ }, contains() { return false } }
    if (component === 'UserDropdown') trigger.props.onKeyDown({ key: 'Enter', preventDefault() {} })
    else trigger.props.onClick()
    result = expanded(ui, props)
    assert.equal(result.pop.props.open, true)
    if (component === 'UserDropdown') {
      assert.equal(nodes(result.tree).find(n => n.type === 'Badge')?.props.ref, undefined, 'Only the keyboard trigger owns the return-focus ref')
      const menu = nodes(result.inside).find(n => n.type === 'MenuList')!
      assert.equal(nodes(menu.props.children)[0]?.type, 'MenuItem', 'Autofocus must target a menu item, not the account header')
    }
    trigger = nodes(result.tree).find(n => n.type === (component === 'UserDropdown' ? 'Avatar' : 'IconButton'))!
    assert.equal(typeof trigger.props.onKeyDown, 'function', 'Escape must be handled on the trigger as well as popup content')
    trigger.props.onKeyDown({ key: 'Escape', preventDefault() {}, stopPropagation() {} })
    result = expanded(ui, props)
    assert.equal(result.pop.props.open, false)
    assert.ok(focused > 0)
    ui.unmount()
  })
}
for (const breakpoint of [true, false]) test(`Navigation uses native button at breakpoint=${breakpoint}`, () => {
  let changed: any
  const ui = componentHarness(new URL('../../src/@menu/components/vertical-menu/NavCollapseIcons.tsx', import.meta.url), {
    '../../hooks/useVerticalNav': { default: () => ({ isCollapsed: false, isBreakpointReached: breakpoint, collapseVerticalNav: (v: any) => changed = v, toggleVerticalNav: (v: any) => changed = v }) },
    '../../svg/Close': { default: 'Close' }, '../../svg/RadioCircle': { default: 'Circle' }, '../../svg/RadioCircleMarked': { default: 'Marked' }
  })
  const control = nodes(ui.render({ 'aria-label': 'Navigation' })).find(n => n.props.onClick)!
  assert.equal(control.type, 'button', 'Native button provides Enter and Space activation')
  assert.equal(control.props.type, 'button')
  assert.equal(control.props['aria-label'], 'Navigation')
  control.props.onClick()
  assert.equal(changed, breakpoint ? false : true)
  ui.unmount()
})
test('Mobile navigation opener is labelled and keyboard accessible', () => {
  const ui = componentHarness(new URL('../../src/components/layout/vertical/NavToggle.tsx', import.meta.url), {
    '@menu/hooks/useVerticalNav': { default: () => ({ isBreakpointReached: true, isToggled: false, toggleVerticalNav() {} }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: false }) }
  })
  const button = nodes(ui.render()).find(n => n.type === 'IconButton')!
  assert.ok(button, 'Mobile opener must be a native MUI button')
  assert.ok(button.props['aria-label'])
  assert.equal(button.props['aria-expanded'], false)
  ui.unmount()
})

test('Open mobile navigation focuses content, traps Tab, closes with Escape and restores opener', () => {
  let listener: any, focused = '', toggled: any
  const first = { focus() { focused = 'first' }, getClientRects: () => [1] }, last = { focus() { focused = 'last' }, getClientRects: () => [1] }
  const nav = { querySelectorAll: () => [first, last] }
  const doc = { activeElement: last, getElementById: () => nav, addEventListener: (_: string, callback: any) => listener = callback, removeEventListener() {} }
  const ui = componentHarness(new URL('../../src/components/layout/vertical/NavToggle.tsx', import.meta.url), {
    '@menu/hooks/useVerticalNav': { default: () => ({ isBreakpointReached: true, isToggled: true, toggleVerticalNav: (value: any) => toggled = value }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: false }) }
  }, { document: doc })
  const button = nodes(ui.render()).find(n => n.type === 'IconButton')!
  assert.ok(button.props.ref, 'The opener must own a return-focus ref')
  button.props.ref.current = { focus() { focused = 'opener' } }
  ui.commit()
  assert.equal(focused, 'first')
  listener({ key: 'Tab', shiftKey: false, preventDefault() {} })
  assert.equal(focused, 'first')
  listener({ key: 'Escape', preventDefault() {} })
  assert.equal(toggled, false)
  ui.unmount()
  assert.equal(focused, 'opener')
})
