'use client'

// MUI Imports
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Typography from '@mui/material/Typography'
import Grid from '@mui/material/Grid'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'

// Component Imports
import CustomAvatar from '@core/components/mui/Avatar'
import { useAppDictionary } from '@/hooks/useDictionary'

const StatCards = () => {
  const { t } = useAppDictionary()

  const stats = [
    {
      title: t.dashboard.incomingDocs,
      value: '1,245',
      icon: 'tabler-mail-down',
      color: 'primary',
      trend: '+12%',
    },
    {
      title: t.dashboard.outgoingDocs,
      value: '842',
      icon: 'tabler-mail-up',
      color: 'success',
      trend: '+5%',
    },
    {
      title: t.dashboard.pendingDocs,
      value: '143',
      icon: 'tabler-clock',
      color: 'warning',
      trend: '-2%',
    },
    {
      title: t.documents.overdue,
      value: '12',
      icon: 'tabler-alert-triangle',
      color: 'error',
      trend: '+1%',
    }
  ]

  return (
    <Grid container spacing={6}>
      {stats.map((stat, index) => (
        <Grid size={{ xs: 12, sm: 6, md: 3 }} key={index}>
          <Card>
            <CardContent>
              <Box display='flex' alignItems='center' justifyContent='space-between' mb={4}>
                <CustomAvatar color={stat.color as any} variant='rounded' size={42}>
                  <i className={stat.icon} />
                </CustomAvatar>
                <Chip size='small' label={stat.trend} color={stat.color as any} variant='tonal' />
              </Box>
              <Typography variant='h4' mb={1}>{stat.value}</Typography>
              <Typography variant='body2' color='text.secondary'>{stat.title}</Typography>
            </CardContent>
          </Card>
        </Grid>
      ))}
    </Grid>
  )
}

export default StatCards
