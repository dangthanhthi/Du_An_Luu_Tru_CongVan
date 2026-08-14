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

  const options = {
    chart: {
      parentHeightOffset: 0
    },
    labels: t.charts.statusLabels,
    colors: [
      theme.palette.success.main,
      theme.palette.warning.main,
      theme.palette.info.main,
      theme.palette.error.main
    ],
    plotOptions: {
      pie: {
        donut: {
          labels: {
            show: true,
            name: {
              fontSize: '1.5rem'
            },
            value: {
              fontSize: '1rem',
              color: theme.palette.text.secondary,
              formatter: (val: string) => `${val}`
            },
            total: {
              show: true,
              fontSize: '1.5rem',
              label: t.charts.total,
              color: theme.palette.text.primary,
              formatter: () => '1430'
            }
          }
        }
      }
    },
    dataLabels: {
      enabled: false
    },
    stroke: {
      show: false
    },
    legend: {
      position: 'bottom',
      labels: { colors: theme.palette.text.secondary },
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
