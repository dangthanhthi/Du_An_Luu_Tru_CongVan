'use client'

// Next Imports
import Link from 'next/link'

// Third-party Imports
import classnames from 'classnames'

// Hook Imports
import useHorizontalNav from '@menu/hooks/useHorizontalNav'

// Util Imports
import { horizontalLayoutClasses } from '@layouts/utils/layoutClasses'

const FooterContent = () => {
  // Hooks
  const { isBreakpointReached } = useHorizontalNav()

  return (
    <div
      className={classnames(horizontalLayoutClasses.footerContent, 'flex items-center justify-between flex-wrap gap-4')}
    >
      <p>
        <span className='font-semibold text-textPrimary'>DAS — Document Administration System</span>
        <span className='text-textSecondary'>{` © ${new Date().getFullYear()}`}</span>
      </p>
      {!isBreakpointReached && (
        <div className='flex items-center gap-4 text-xs text-textSecondary'>
          <span>
            Template by{' '}
            <Link href='https://pixinvent.com/' target='_blank' className='text-primary uppercase font-medium'>
              Pixinvent
            </Link>
          </span>
          <Link href='https://themeforest.net/licenses/standard' target='_blank' className='text-primary'>
            License
          </Link>
        </div>
      )}
    </div>
  )
}

export default FooterContent
