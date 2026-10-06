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

const DocumentChart = () => {
  const theme = useTheme()
  const { t } = useAppDictionary()

  const isDark = theme.palette.mode === 'dark'
  const textDisabled = isDark ? '#7d8299' : '#a8aaae'
  const textSecondary = isDark ? '#a1a0b5' : '#6f6b7d'
  const borderColor = isDark ? '#3b3f5c' : '#dbdade'

  const options: any = {
    chart: {
      parentHeightOffset: 0,
      toolbar: { show: false }
    },
    plotOptions: {
      bar: {
        borderRadius: 4,
        columnWidth: '40%'
      }
    },
    xaxis: {
      categories: t.charts.months,
      labels: {
        style: { colors: textDisabled }
      },
      axisBorder: { show: false },
      axisTicks: { show: false }
    },
    yaxis: {
      labels: {
        style: { colors: textDisabled }
      }
    },
    colors: ['#7367f0', '#28c76f'],
    dataLabels: { enabled: false },
    legend: {
      position: 'top',
      horizontalAlign: 'left',
      labels: { colors: textSecondary },
      markers: {
        radius: 12
      }
    },
    stroke: { show: false },
    grid: {
      borderColor: borderColor,
      xaxis: {
        lines: { show: false }
      }
    }
  }

  const series = [
    {
      name: t.documents.incoming,
      data: [120, 150, 180, 140, 200, 250, 220, 190, 210, 230, 260, 280]
    },
    {
      name: t.documents.outgoing,
      data: [80, 100, 120, 90, 140, 170, 150, 130, 160, 180, 200, 220]
    }
  ]

  return (
    <Card>
      <CardHeader title={t.charts.monthlyStats} />
      <CardContent>
        <AppReactApexCharts type='bar' height={350} options={options} series={series} />
      </CardContent>
    </Card>
  )
}

export default DocumentChart
