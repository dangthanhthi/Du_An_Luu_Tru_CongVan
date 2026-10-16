'use client'

// Next Imports
import Link from 'next/link'
import { useParams } from 'next/navigation'

// MUI Imports
import useMediaQuery from '@mui/material/useMediaQuery'
import { styled, useTheme } from '@mui/material/styles'
import Button from '@mui/material/Button'
import Typography from '@mui/material/Typography'
import CircularProgress from '@mui/material/CircularProgress'

// Third-party Imports
import classnames from 'classnames'

// Type Imports
import type { SystemMode } from '@core/types'
import type { Locale } from '@/configs/i18n'

// Hook Imports
import { useImageVariant } from '@core/hooks/useImageVariant'

// Util Imports
import { getLocalizedUrl } from '@/utils/i18n'

// Styled Components
const MaskImg = styled('img')({
  blockSize: 'auto',
  maxBlockSize: 355,
  inlineSize: '100%',
  position: 'absolute',
  insetBlockEnd: 0,
  zIndex: -1
})

type Props = {
  mode: SystemMode
  title?: string
  description?: string
  code?: string | null
  action?: { label: string; href: string } | null
  secondaryAction?: { label: string; href: string }
  busy?: boolean
  sessionStatus?: boolean
}

const NotAuthorized = ({ mode, title, description, code = '401', action, secondaryAction, busy = false, sessionStatus = false }: Props) => {
  // Vars
  const darkImg = '/images/pages/misc-mask-dark.png'
  const lightImg = '/images/pages/misc-mask-light.png'

  // Hooks
  const theme = useTheme()
  const { lang: locale } = useParams()
  const hidden = useMediaQuery(theme.breakpoints.down('md'))
  const miscBackground = useImageVariant(mode, lightImg, darkImg)
  const button = action === undefined ? { label: 'Back To Home', href: getLocalizedUrl('/', locale as Locale) } : action

  return (
    <div role={sessionStatus ? 'status' : undefined} aria-live={sessionStatus ? 'polite' : undefined} className='flex items-center justify-center min-bs-[100dvh] relative p-6 overflow-x-hidden'>
      <div className='flex items-center flex-col text-center'>
        <div className='flex flex-col gap-2 is-[90vw] sm:is-[unset] mbe-6'>
          {code && <Typography className='font-medium text-8xl' color='text.primary'>{code}</Typography>}
          <Typography variant='h4'>{title ?? 'You are not authorized! 🔐'}</Typography>
          <Typography className='max-is-[560px]'>{description ?? "You don't have permission to access this page. Go Home!"}</Typography>
        </div>
        {busy && <CircularProgress size={28} aria-label={title} />}
        {(button || secondaryAction) && <div className='flex flex-wrap items-center justify-center gap-4'>
          {button && <Button href={button.href} component={Link} variant={secondaryAction ? 'outlined' : 'contained'}>{button.label}</Button>}
          {secondaryAction && <Button href={secondaryAction.href} component={Link} variant='contained'>{secondaryAction.label}</Button>}
        </div>}
        <img
          alt={sessionStatus ? '' : 'error-401-illustration'}
          src='/images/illustrations/characters/3.png'
          className='object-contain max-is-full bs-auto max-bs-[300px] sm:bs-[350px] md:bs-[400px] mbs-8 md:mbs-10'
        />
      </div>
      {!hidden && (
        <MaskImg
          alt=''
          src={miscBackground}
          className={classnames({ 'scale-x-[-1]': theme.direction === 'rtl' })}
        />
      )}
    </div>
  )
}

export default NotAuthorized
