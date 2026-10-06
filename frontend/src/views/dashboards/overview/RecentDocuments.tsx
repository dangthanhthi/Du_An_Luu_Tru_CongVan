'use client'

// MUI Imports
import Card from '@mui/material/Card'
import CardHeader from '@mui/material/CardHeader'
import Chip from '@mui/material/Chip'
import Typography from '@mui/material/Typography'

// Component Imports
import { useAppDictionary } from '@/hooks/useDictionary'

// Style Imports
import tableStyles from '@core/styles/table.module.css'

const documentData = [
  {
    id: 'CV-2154/BGDĐT',
    title: 'V/v Hướng dẫn triển khai chuyển đổi số và ứng dụng AI OCR vào lưu trữ',
    type: 'incoming',
    date: '2026-08-10',
    status: 'completed',
    partner: 'Bộ Giáo dục và Đào tạo'
  },
  {
    id: 'QĐ-890/UBND',
    title: 'Quyết định phê duyệt Đề án Số hóa và Lưu trữ hồ sơ điện tử',
    type: 'incoming',
    date: '2026-08-11',
    status: 'completed',
    partner: 'UBND TP Hà Nội'
  },
  {
    id: 'TB-145/VNPT',
    title: 'Thông báo nâng cấp kết nối AI OCR và bảo trì hạ tầng truyền dẫn',
    type: 'incoming',
    date: '2026-08-12',
    status: 'processing',
    partner: 'Tập đoàn VNPT'
  },
  {
    id: 'TTr-320/ĐHQGHN',
    title: 'Tờ trình xin phê duyệt chủ trương đầu tư Trung tâm Lưu trữ Dữ liệu',
    type: 'outgoing',
    date: '2026-08-13',
    status: 'pending',
    partner: 'Đại học Quốc gia Hà Nội'
  },
  {
    id: 'GM-58/ABCTECH',
    title: 'Giấy mời tham dự Hội thảo ứng dụng AI OCR trong quản trị văn phòng',
    type: 'incoming',
    date: '2026-08-14',
    status: 'pending',
    partner: 'Công ty CP Công nghệ ABC'
  }
]

const RecentDocuments = () => {
  const { t } = useAppDictionary()

  const getStatusInfo = (status: string) => {
    switch (status) {
      case 'completed': return { label: t.documents.completed, color: 'success' }
      case 'processing': return { label: t.documents.processing, color: 'warning' }
      case 'pending': return { label: t.documents.pending, color: 'info' }
      case 'rejected': return { label: t.documents.rejected, color: 'error' }
      default: return { label: t.documents.overdue, color: 'error' }
    }
  }

  return (
    <Card>
      <CardHeader title={t.dashboard.recentDocs} />
      <div className='overflow-x-auto'>
        <table className={tableStyles.table}>
          <thead>
            <tr>
              <th>{t.documents.docNumber}</th>
              <th>{t.documents.title}</th>
              <th>{t.documents.type}</th>
              <th>{t.documents.issuedDate}</th>
              <th>{t.documents.status}</th>
              <th>{t.documents.partner}</th>
            </tr>
          </thead>
          <tbody>
            {documentData.map((doc, index) => {
              const st = getStatusInfo(doc.status)
              return (
                <tr key={index}>
                  <td>
                    <Typography variant='body2' className='font-mono font-semibold'>{doc.id}</Typography>
                  </td>
                  <td>
                    <Typography variant='body2' fontWeight={500}>{doc.title}</Typography>
                  </td>
                  <td>
                    <Chip
                      label={doc.type === 'incoming' ? t.documents.incoming : t.documents.outgoing}
                      color={doc.type === 'incoming' ? 'primary' : 'success'}
                      size='small'
                      variant='tonal'
                    />
                  </td>
                  <td>
                    <Typography variant='body2'>{doc.date}</Typography>
                  </td>
                  <td>
                    <Chip
                      label={st.label}
                      color={st.color as any}
                      size='small'
                      variant='tonal'
                    />
                  </td>
                  <td>
                    <Typography variant='body2'>{doc.partner}</Typography>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </Card>
  )
}

export default RecentDocuments
