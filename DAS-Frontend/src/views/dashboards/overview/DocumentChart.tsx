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
        style: { colors: theme.palette.text.disabled }
      },
      axisBorder: { show: false },
      axisTicks: { show: false }
    },
    yaxis: {
      labels: {
        style: { colors: theme.palette.text.disabled }
      }
    },
    colors: [theme.palette.primary.main, theme.palette.success.main],
    dataLabels: { enabled: false },
    legend: {
      position: 'top',
      horizontalAlign: 'left',
      labels: { colors: theme.palette.text.secondary },
      markers: {
        radius: 12
      }
    },
    stroke: { show: false },
    grid: {
      borderColor: theme.palette.divider,
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
