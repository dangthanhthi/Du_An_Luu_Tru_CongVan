'use client'

import { useEffect, useState } from 'react'
import { usePathname, useRouter } from 'next/navigation'
import IconButton from '@mui/material/IconButton'
import classnames from 'classnames'
import { CommandDialog, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from 'cmdk'
import { Title, Description } from '@radix-ui/react-dialog'

import DefaultSuggestions from './DefaultSuggestions'
import NoResult from './NoResult'
import useVerticalNav from '@menu/hooks/useVerticalNav'
import { useSettings } from '@core/hooks/useSettings'
import { useDasLauncher } from '@/hooks/useDasLauncher'
import { filterDasLauncher, type DasLauncherItem } from '../dasLauncher'
import './styles.css'

const NavSearch = () => {
  const [open, setOpen] = useState(false)
  const [searchValue, setSearchValue] = useState('')
  const router = useRouter()
  const pathname = usePathname()
  const { settings } = useSettings()
  const { isBreakpointReached } = useVerticalNav()
  const { sections, refreshCapabilities, t } = useDasLauncher()
  const results = filterDasLauncher(sections, searchValue)
  const limit = results.length > 1 ? 3 : 5
  const fallback = sections.flatMap(section => section.items)
    .filter(item => ['dashboard', 'my-staff', 'incomplete-reports'].includes(item.id))

  const selectItem = (item: DasLauncherItem) => {
    router.push(item.url)
    setOpen(false)
  }

  useEffect(() => {
    const down = (event: KeyboardEvent) => {
      if (event.key.toLowerCase() === 'k' && (event.metaKey || event.ctrlKey)) {
        event.preventDefault()
        setOpen(value => !value)
      }
    }

    document.addEventListener('keydown', down)

    return () => document.removeEventListener('keydown', down)
  }, [])

  useEffect(() => {
    if (open) refreshCapabilities()
    else setSearchValue('')
  }, [open, refreshCapabilities])

  const trigger = (
    <IconButton aria-label={t.search.openMenu} className='text-textPrimary' onClick={() => setOpen(true)}>
      <i className='tabler-search text-2xl' />
    </IconButton>
  )

  return (
    <>
      {isBreakpointReached || settings.layout === 'horizontal' ? trigger : (
        <div className='flex items-center gap-2 cursor-pointer' onClick={() => setOpen(true)}>
          {trigger}
          <span className='whitespace-nowrap select-none text-textDisabled'>{t.search.placeholder}</span>
        </div>
      )}
      <CommandDialog open={open} onOpenChange={setOpen} shouldFilter={false}>
        <div className='flex items-center justify-between border-be pli-4 plb-3 gap-2'>
          <Title hidden>{t.search.openMenu}</Title>
          <Description hidden>{t.search.inputPlaceholder}</Description>
          <i className='tabler-search' />
          <CommandInput aria-label={t.search.inputPlaceholder} placeholder={t.search.inputPlaceholder}
            value={searchValue} onValueChange={setSearchValue} />
          <span className='text-textDisabled'>[esc]</span>
          <IconButton size='small' aria-label={t.search.closeMenu} onClick={() => setOpen(false)}>
            <i className='tabler-x' />
          </IconButton>
        </div>
        <CommandList>
          {searchValue.trim() ? results.length > 0 ? results.map(section => (
            <CommandGroup key={section.title} heading={section.title.toUpperCase()} className='text-xs'>
              {section.items.slice(0, limit).map(item => (
                <CommandItem key={item.id} value={item.id} onSelect={() => selectItem(item)}
                  className={classnames('mli-2 mbe-px last:mbe-0 rounded', { 'active-searchItem': pathname === item.url })}>
                  <i className={classnames('text-xl', item.icon)} />
                  {item.name}
                </CommandItem>
              ))}
            </CommandGroup>
          )) : (
            <CommandEmpty><NoResult searchValue={searchValue} items={fallback} setOpen={setOpen} /></CommandEmpty>
          ) : <DefaultSuggestions sections={sections} setOpen={setOpen} />}
        </CommandList>
        <div cmdk-footer=''>
          <span>↑ ↓ {t.search.navigate}</span>
          <span>↵ {t.search.open}</span>
          <span>esc {t.search.close}</span>
        </div>
      </CommandDialog>
    </>
  )
}

export default NavSearch
