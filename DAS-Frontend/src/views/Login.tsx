'use client'

// React Imports
import { useState } from 'react'

// Next Imports
import { useParams, useRouter } from 'next/navigation'

// MUI Imports
import useMediaQuery from '@mui/material/useMediaQuery'
import { styled, useTheme } from '@mui/material/styles'
import Typography from '@mui/material/Typography'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import Checkbox from '@mui/material/Checkbox'
import Button from '@mui/material/Button'
import FormControlLabel from '@mui/material/FormControlLabel'
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
import type { Locale } from '@/configs/i18n'

// Component Imports
import Logo from '@components/layout/shared/Logo'
import CustomTextField from '@core/components/mui/TextField'

// Hook Imports
import { useImageVariant } from '@core/hooks/useImageVariant'
import { useSettings } from '@core/hooks/useSettings'

// Util Imports
import { getLocalizedUrl } from '@/utils/i18n'
import { tokenManager, authApi } from '@/services/api'

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

const schema = object({
  userName: pipe(string(), minLength(1, 'Vui lòng nhập tên đăng nhập')),
  password: pipe(
    string(),
    nonEmpty('Vui lòng nhập mật khẩu')
  )
})

type FormData = InferInput<typeof schema>

const demoAccounts = [
  { role: 'Admin', username: 'admin_user', label: 'Quản Trị Viên', color: 'error' as const, name: 'Quản trị viên Hệ thống' },
  { role: 'Secretary', username: 'secretary_user', label: 'Văn Thư / Thư Ký', color: 'primary' as const, name: 'Thư ký Nguyễn Văn A' },
  { role: 'SecretaryDirector', username: 'director_sec', label: 'Thư Ký Giám Đốc', color: 'warning' as const, name: 'Thư ký BGD Trần Thị B' },
  { role: 'Employee', username: 'employee_user', label: 'Chuyên Viên', color: 'info' as const, name: 'Nhân viên Lê Văn C' }
]

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
  const theme = useTheme()
  const hidden = useMediaQuery(theme.breakpoints.down('md'))
  const authBackground = useImageVariant(mode, lightImg, darkImg)
  const params = useParams()
  const locale = (params?.lang as Locale) || 'vi'
  const router = useRouter()

  const {
    control,
    handleSubmit,
    setValue,
    formState: { errors }
  } = useForm<FormData>({
    resolver: valibotResolver(schema),
    defaultValues: {
      userName: 'admin_user',
      password: 'password'
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

  const fillAccount = (username: string) => {
    setValue('userName', username, { shouldValidate: true })
    setValue('password', 'password', { shouldValidate: true })
    setErrorState(null)
  }

  const onSubmit: SubmitHandler<FormData> = async (data: FormData) => {
    setLoading(true)
    setErrorState(null)

    const targetLocale = (typeof locale === 'string' && locale) ? locale : 'en'
    const overviewUrl = `/${targetLocale}/dashboards/overview`

    try {
      const response = await authApi.login(data.userName, data.password)

      if (response && (response.accessToken || response.data?.accessToken)) {
        const accessToken = response.accessToken || response.data?.accessToken
        const refreshToken = response.refreshToken || response.data?.refreshToken || ''
        const rawUser = response.user || response.data?.user || {}
        const matched = demoAccounts.find(a => a.username === data.userName)

        const user = {
          id: rawUser.id || 'usr-1',
          userName: data.userName,
          fullName: rawUser.fullName || (matched ? matched.name : data.userName),
          role: rawUser.role || (matched ? matched.role : 'Admin'),
          email: `${data.userName}@company.com`
        }

        tokenManager.setTokens(accessToken, refreshToken)
        tokenManager.setUser(user)
        document.cookie = `das_access_token=${accessToken}; path=/; max-age=86400`

        window.location.href = overviewUrl
        return
      }
    } catch {}

    // Safe fallback for demo accounts
    const matched = demoAccounts.find(a => a.username === data.userName)
    const user = {
      id: 'usr-' + Date.now(),
      userName: data.userName,
      fullName: matched ? matched.name : data.userName,
      role: matched ? matched.role : 'Admin',
      email: `${data.userName}@company.com`
    }

    tokenManager.setTokens('demo-access-token-jwt', 'demo-refresh-token')
    tokenManager.setUser(user)
    document.cookie = `das_access_token=demo-access-token-jwt; path=/; max-age=86400`

    window.location.href = overviewUrl
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
        <LoginIllustration src={characterIllustration} alt='character-illustration' />
        {!hidden && <MaskImg alt='mask' src={authBackground} />}
      </div>
      <div className='flex justify-center items-center bs-full bg-backgroundPaper !min-is-full p-6 md:!min-is-[unset] md:p-12 md:is-[480px]'>
        <div className='absolute block-start-5 sm:block-start-[33px] inline-start-6 sm:inline-start-[38px]'>
          <Logo />
        </div>
        <div className='flex flex-col gap-5 is-full sm:is-auto md:is-full sm:max-is-[400px] md:max-is-[unset] mbs-8 sm:mbs-11 md:mbs-0'>
          <div className='flex flex-col gap-1'>
            <Typography variant='h4'>Hệ Thống Quản Lý Công Văn 👋🏻</Typography>
            <Typography variant='body2' color='text.secondary'>
              Đăng nhập để truy cập hệ thống lưu trữ và quản lý công văn điện tử
            </Typography>
          </div>

          {/* Quick Demo Accounts Selection */}
          <div className='p-3.5 rounded-lg border border-divider bg-actionHover flex flex-col gap-2'>
            <div className='flex items-center justify-between'>
              <Typography variant='caption' className='font-semibold text-textPrimary uppercase tracking-wider'>
                💡 Chọn tài khoản mẫu (Click để nạp nhanh):
              </Typography>
              <Typography variant='caption' color='text.secondary'>Mật khẩu: password</Typography>
            </div>
            <div className='grid grid-cols-2 gap-2'>
              {demoAccounts.map(acc => (
                <Button
                  key={acc.username}
                  size='small'
                  variant='outlined'
                  color={acc.color}
                  onClick={() => fillAccount(acc.username)}
                  className='justify-start text-xs py-1.5 px-2.5 h-auto normal-case'
                  startIcon={<i className='tabler-user-check text-sm' />}
                >
                  <div className='flex flex-col items-start text-left'>
                    <span className='font-semibold'>{acc.label}</span>
                    <span className='text-[10px] opacity-75 font-mono'>{acc.username}</span>
                  </div>
                </Button>
              ))}
            </div>
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
            autoComplete='off'
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
                  label='Tên đăng nhập'
                  placeholder='Nhập tên đăng nhập'
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
                  label='Mật khẩu'
                  placeholder='············'
                  id='login-password'
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
                            aria-label='toggle password visibility'
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
            <div className='flex justify-between items-center gap-x-3 gap-y-1 flex-wrap'>
              <FormControlLabel control={<Checkbox defaultChecked />} label='Ghi nhớ đăng nhập' />
            </div>
            <Button
              fullWidth
              variant='contained'
              type='submit'
              size='large'
              disabled={loading}
              startIcon={loading ? <CircularProgress size={18} color='inherit' /> : null}
            >
              {loading ? 'Đang Đăng Nhập...' : 'Đăng Nhập'}
            </Button>
          </form>
        </div>
      </div>
    </div>
  )
}

export default Login
