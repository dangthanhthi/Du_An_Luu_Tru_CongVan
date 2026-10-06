import Link from 'next/link'
import classnames from 'classnames'

import { useAppDictionary } from '@/hooks/useDictionary'
import type { DasLauncherItem } from '../dasLauncher'

const NoResult = ({ searchValue, items, setOpen }: {
  searchValue: string; items: DasLauncherItem[]; setOpen: (value: boolean) => void
}) => {
  const { t } = useAppDictionary()

  return (
    <div className='flex items-center justify-center grow flex-wrap plb-14 pli-16 overflow-y-auto overflow-x-hidden bs-full'>
      <div className='flex flex-col items-center'>
        <i className='tabler-file-alert text-[64px] mbe-2.5' />
        <p className='text-lg font-medium mbe-6'>{t.search.noResult}: “{searchValue}”</p>
        <p className='text-[15px] mbe-4 text-textDisabled'>{t.search.suggestions}</p>
        <ul className='flex flex-col self-start gap-[18px]'>
          {items.map(item => (
            <li key={item.id} className='flex items-center'>
              <Link href={item.url} onClick={() => setOpen(false)}
                className='flex items-center gap-2 hover:text-primary focus-visible:text-primary'>
                <i className={classnames(item.icon, 'text-xl shrink-0')} />
                <p className='text-[15px] truncate'>{item.name}</p>
              </Link>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}

export default NoResult
