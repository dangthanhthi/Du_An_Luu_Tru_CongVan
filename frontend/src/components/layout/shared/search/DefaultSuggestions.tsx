import Link from 'next/link'
import classnames from 'classnames'

import type { DasLauncherSection } from '../dasLauncher'

const DefaultSuggestions = ({ sections, setOpen }: {
  sections: DasLauncherSection[]; setOpen: (value: boolean) => void
}) => (
  <div className='flex grow flex-wrap gap-x-[48px] gap-y-8 plb-14 pli-16 overflow-y-auto overflow-x-hidden bs-full'>
    {sections.map(section => (
      <div key={section.title} className='flex flex-col overflow-x-hidden gap-4 basis-full sm:basis-[calc((100%-3rem)/2)]'>
        <p className='text-xs uppercase text-textDisabled tracking-[0.8px]'>{section.title}</p>
        <ul className='flex flex-col gap-4'>
          {section.items.map(item => (
            <li key={item.id} className='flex'>
              <Link href={item.url} onClick={() => setOpen(false)}
                className='flex items-center overflow-x-hidden gap-2 hover:text-primary focus-visible:text-primary'>
                <i className={classnames(item.icon, 'flex text-xl shrink-0')} />
                <p className='text-[15px] truncate' title={item.name}>{item.name}</p>
              </Link>
            </li>
          ))}
        </ul>
      </div>
    ))}
  </div>
)

export default DefaultSuggestions
