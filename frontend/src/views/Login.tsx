'use client'

// React Imports
import { useState } from 'react'

// Next Imports
import { useParams } from 'next/navigation'

// MUI Imports
import useMediaQuery from '@mui/material/useMediaQuery'
import { styled, useTheme } from '@mui/material/styles'
import Typography from '@mui/material/Typography'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import Button from '@mui/material/Button'
import Alert from '@mui/material/Alert'
import CircularProgress from '@mui/material/CircularProgress'

// Third-party Imports
import { Controller, useForm } from 'react-hook-form'
import { valibotResolver } from '@hookform/resolvers/valibot'
import { object, minLength, string, pipe, nonEmpty } from 'valibot'
import type { SubmitHandler } from 'react-hook-form'
import type { InferInput } from 'valibot'
import classnames from 'classnames'

// Type Imports
import type { SystemMode } from '@core/types'
import { i18n, type Locale } from '@/configs/i18n'

// Component Imports
import Logo from '@components/layout/shared/Logo'
import CustomTextField from '@core/components/mui/TextField'

// Hook Imports
import { useImageVariant } from '@core/hooks/useImageVariant'
import { useSettings } from '@core/hooks/useSettings'
import { useAppDictionary } from '@/hooks/useDictionary'

// Util Imports
import { getLocalizedUrl } from '@/utils/i18n'
import { authApi } from '@/services/api'
import { LegacyAuthError } from '@/services/legacyAuth'

// Styled Custom Components
const LoginIllustration = styled('img')(({ theme }) => ({
  zIndex: 2,
  blockSize: 'auto',
  maxBlockSize: 680,
  maxInlineSize: '100%',
  margin: theme.spacing(12),
  [theme.breakpoints.down(1536)]: {
    maxBlockSize: 550
  },
  [theme.breakpoints.down('lg')]: {
    maxBlockSize: 450
  }
}))

const MaskImg = styled('img')({
  blockSize: 'auto',
  maxBlockSize: 355,
  inlineSize: '100%',
  position: 'absolute',
  insetBlockEnd: 0,
  zIndex: -1
})

type ErrorType = {
  message: string
}

const createSchema = (messages: { usernameRequired: string; passwordRequired: string }) => object({
  userName: pipe(string(), minLength(1, messages.usernameRequired)),
  password: pipe(
    string(),
    nonEmpty(messages.passwordRequired)
  )
})

type FormData = InferInput<ReturnType<typeof createSchema>>

const Login = ({ mode }: { mode: SystemMode }) => {
  // States
  const [isPasswordShown, setIsPasswordShown] = useState(false)
  const [errorState, setErrorState] = useState<ErrorType | null>(null)
  const [loading, setLoading] = useState(false)

  // Vars
  const darkImg = '/images/pages/auth-mask-dark.png'
  const lightImg = '/images/pages/auth-mask-light.png'
  const darkIllustration = '/images/illustrations/auth/v2-login-dark.png'
  const lightIllustration = '/images/illustrations/auth/v2-login-light.png'
  const borderedDarkIllustration = '/images/illustrations/auth/v2-login-dark-border.png'
  const borderedLightIllustration = '/images/illustrations/auth/v2-login-light-border.png'

  // Hooks
  let skin = 'default'
  try {
    const settingsContext = useSettings()
    skin = settingsContext?.settings?.skin || 'default'
  } catch {}
  const { lang } = useParams()
  const locale: Locale = i18n.locales.includes(lang as Locale) ? lang as Locale : i18n.defaultLocale
  const { t } = useAppDictionary()
  const theme = useTheme()
  const hidden = useMediaQuery(theme.breakpoints.down('md'))
  const authBackground = useImageVariant(mode, lightImg, darkImg)

  const {
    control,
    handleSubmit,

    formState: { errors }
  } = useForm<FormData>({
    resolver: valibotResolver(createSchema(t.login)),
    defaultValues: {
      userName: '',
      password: ''
    }
  })

  const characterIllustration = useImageVariant(
    mode,
    lightIllustration,
    darkIllustration,
    borderedLightIllustration,
    borderedDarkIllustration
  )

  const handleClickShowPassword = () => setIsPasswordShown(show => !show)

  const onSubmit: SubmitHandler<FormData> = async (data: FormData) => {
    setLoading(true)
    setErrorState(null)

    try {
      await authApi.login(data.userName, data.password)
      window.location.href = getLocalizedUrl('/dashboards/overview', locale)
    } catch (error) {
      setErrorState({ message: error instanceof LegacyAuthError
        ? error.message : t.login.failed })
    } finally {
      setLoading(false)
    }
  }
  return (
    <div className='flex bs-full justify-center'>
      <div
        className={classnames(
          'flex bs-full items-center justify-center flex-1 min-bs-[100dvh] relative p-6 max-md:hidden',
          {
            'border-ie': skin === 'bordered'
          }
        )}
      >
        <LoginIllustration src={characterIllustration} alt='' />
        {!hidden && <MaskImg alt='' src={authBackground} />}
      </div>
      <div className='flex justify-center items-center bs-full bg-backgroundPaper !min-is-full p-6 md:!min-is-[unset] md:p-12 md:is-[480px]'>
        <div className='absolute block-start-5 sm:block-start-[33px] inline-start-6 sm:inline-start-[38px]'>
          <Logo />
        </div>
        <div className='flex flex-col gap-5 is-full sm:is-auto md:is-full sm:max-is-[400px] md:max-is-[unset] mbs-8 sm:mbs-11 md:mbs-0'>
          <div className='flex flex-col gap-1'>
            <Typography variant='h4'>{t.login.title}</Typography>
            <Typography variant='body2' color='text.secondary'>
              {t.login.description}
            </Typography>
          </div>


          {errorState && (
            <Alert icon={false} severity="error">
              <Typography variant='body2' color='error'>
                {errorState.message}
              </Typography>
            </Alert>
          )}

          <form
            noValidate
            onSubmit={handleSubmit(onSubmit)}
            className='flex flex-col gap-5'
          >
            <Controller
              name='userName'
              control={control}
              rules={{ required: true }}
              render={({ field }) => (
                <CustomTextField
                  {...field}
                  autoFocus
                  fullWidth
                  id='login-username'
                  autoComplete='username'
                  label={t.login.username}
                  placeholder={t.login.usernamePlaceholder}
                  onChange={e => {
                    field.onChange(e.target.value)
                    errorState !== null && setErrorState(null)
                  }}
                  {...((errors.userName) && {
                    error: true,
                    helperText: errors?.userName?.message
                  })}
                />
              )}
            />
            <Controller
              name='password'
              control={control}
              rules={{ required: true }}
              render={({ field }) => (
                <CustomTextField
                  {...field}
                  fullWidth
                  label={t.login.password}
                  placeholder='············'
                  id='login-password'
                  autoComplete='current-password'
                  type={isPasswordShown ? 'text' : 'password'}
                  onChange={e => {
                    field.onChange(e.target.value)
                    errorState !== null && setErrorState(null)
                  }}
                  slotProps={{
                    input: {
                      endAdornment: (
                        <InputAdornment position='end'>
                          <IconButton
                            edge='end'
                            onClick={handleClickShowPassword}
                            onMouseDown={e => e.preventDefault()}
                            aria-label={isPasswordShown ? t.login.hidePassword : t.login.showPassword}
                          >
                            <i className={isPasswordShown ? 'tabler-eye-off' : 'tabler-eye'} />
                          </IconButton>
                        </InputAdornment>
                      )
                    }
                  }}
                  {...((errors.password) && {
                    error: true,
                    helperText: errors?.password?.message
                  })}
                />
              )}
            />
            <Button
              fullWidth
              variant='contained'
              type='submit'
              size='large'
              disabled={loading}
              startIcon={loading ? <CircularProgress size={18} color='inherit' /> : null}
            >
              {loading ? t.login.submitting : t.login.submit}
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}

export default Login
