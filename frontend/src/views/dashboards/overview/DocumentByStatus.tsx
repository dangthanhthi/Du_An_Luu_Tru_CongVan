'use client'

// Next Imports
import dynamic from 'next/dynamic'

// MUI Imports
import Card from '@mui/material/Card'
import CardHeader from '@mui/material/CardHeader'
import CardContent from '@mui/material/CardContent'
import { useTheme } from '@mui/material/styles'

// Hook Imports
import { useAppDictionary } from '@/hooks/useDictionary'

// Dynamic Import
const AppReactApexCharts = dynamic(() => import('@/libs/styles/AppReactApexCharts'), { ssr: false })

const DocumentByStatus = () => {
  const theme = useTheme()
  const { t } = useAppDictionary()

  const isDark = theme.palette.mode === 'dark'
  const textSecondary = isDark ? '#a1a0b5' : '#6f6b7d'
  const textPrimary = isDark ? '#cfd3ec' : '#2f2b3d'

  const options: any = {
    chart: {
      parentHeightOffset: 0,
      sparkline: { enabled: false }
    },
    labels: t.charts.statusLabels,
    colors: ['#28c76f', '#ff9f43', '#00bad1', '#ea5455'],
    stroke: {
      width: 0,
      show: false
    },
    plotOptions: {
      pie: {
        donut: {
          size: '72%',
          labels: {
            show: true,
            name: {
              fontSize: '1.2rem',
              color: textSecondary,
              offsetY: -10
            },
            value: {
              fontSize: '1.5rem',
              fontWeight: 600,
              color: textPrimary,
              offsetY: 10,
              formatter: (val: string) => `${val}`
            },
            total: {
              show: true,
              fontSize: '1rem',
              fontWeight: 500,
              label: t.charts.total,
              color: textSecondary,
              formatter: () => '1,430'
            }
          }
        }
      }
    },
    dataLabels: {
      enabled: false
    },
    legend: {
      position: 'bottom',
      labels: { colors: textSecondary },
      markers: {
        radius: 12
      }
    }
  }

  const series = [850, 320, 210, 50]

  return (
    <Card>
      <CardHeader title={t.charts.statusBreakdown} />
      <CardContent>
        <AppReactApexCharts type='donut' height={350} options={options} series={series} />
      </CardContent>
    </Card>
  )
}

export default DocumentByStatus
