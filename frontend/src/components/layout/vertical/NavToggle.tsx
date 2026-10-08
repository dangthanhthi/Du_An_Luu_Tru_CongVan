'use client'
import { useEffect, useRef } from 'react'
import IconButton from '@mui/material/IconButton'
import useVerticalNav from '@menu/hooks/useVerticalNav'
import { useAppDictionary } from '@/hooks/useDictionary'

const NavToggle = () => {
  const { toggleVerticalNav, isBreakpointReached, isToggled } = useVerticalNav()
  const { isEn } = useAppDictionary()
  const opener = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (!isBreakpointReached || !isToggled) return
    const navigation = document.getElementById('das-navigation')
    const controls = () => Array.from(navigation?.querySelectorAll<HTMLElement>('a[href],button,[role="button"][tabindex="0"]') ?? [])
      .filter(element => element.getClientRects().length > 0)

    controls()[0]?.focus()
    const keyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        toggleVerticalNav(false)
      } else if (event.key === 'Tab') {
        const items = controls(), first = items[0], last = items.at(-1)
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus() }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus() }
      }
    }

    document.addEventListener('keydown', keyDown)
    return () => { document.removeEventListener('keydown', keyDown); opener.current?.focus() }
  }, [isBreakpointReached, isToggled, toggleVerticalNav])

  return isBreakpointReached ? <IconButton
    ref={opener}
    aria-controls='das-navigation'
    aria-label={isEn ? 'Open navigation' : 'Mở điều hướng'}
    aria-expanded={Boolean(isToggled)}
    onClick={() => toggleVerticalNav()}
  ><i className='tabler-menu-2' aria-hidden='true' /></IconButton> : null
}

export default NavToggle
