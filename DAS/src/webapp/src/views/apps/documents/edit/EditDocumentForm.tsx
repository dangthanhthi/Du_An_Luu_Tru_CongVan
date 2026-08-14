'use client'

// React Imports
import { useState, useEffect } from 'react'
import { useRouter, useParams } from 'next/navigation'

// MUI Imports
import Grid from '@mui/material/Grid'
import Card from '@mui/material/Card'
import CardHeader from '@mui/material/CardHeader'
import CardContent from '@mui/material/CardContent'
import Button from '@mui/material/Button'
import MenuItem from '@mui/material/MenuItem'
import Alert from '@mui/material/Alert'
import CircularProgress from '@mui/material/CircularProgress'

// Component Imports
import CustomTextField from '@core/components/mui/TextField'
import { getLocalizedUrl } from '@/utils/i18n'
import type { Locale } from '@configs/i18n'
import { documentApi } from '@/services/api'
import { useAppDictionary } from '@/hooks/useDictionary'

const EditDocumentForm = ({ id }: { id: string }) => {
  const router = useRouter()
  const { lang: locale } = useParams()
  const { t } = useAppDictionary()

  const [formData, setFormData] = useState({
    documentNumber: '',
    title: '',
    direction: 'incoming',
    issuedDate: '',
    partnerName: '',
    summary: ''
  })
  const [loading, setLoading] = useState(true)
  const [submitLoading, setSubmitLoading] = useState(false)
  const [alertInfo, setAlertInfo] = useState<{ type: 'success' | 'error'; message: string } | null>(null)

  useEffect(() => {
    const fetchDoc = async () => {
      setLoading(true)
      try {
        const res = await documentApi.getById(id)
        if (res?.success && res?.data) {
          const d = res.data
          setFormData({
            documentNumber: d.documentNumber || '',
            title: d.title || '',
            direction: d.direction || 'incoming',
            issuedDate: d.issuedDate || '',
            partnerName: d.partnerName || '',
            summary: d.summary || ''
          })
        }
      } catch {}
      setLoading(false)
    }
    if (id) {
      fetchDoc()
    }
  }, [id])

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitLoading(true)
    setAlertInfo(null)

    try {
      await documentApi.update(id, formData)
      setAlertInfo({ type: 'success', message: `${t.documents.edit}: #${formData.documentNumber}` })
      setTimeout(() => {
        router.push(getLocalizedUrl('/apps/documents/list', locale as Locale))
      }, 1000)
    } catch (err: any) {
      setAlertInfo({ type: 'error', message: err.message || 'Error updating document.' })
      setSubmitLoading(false)
    }
  }

  if (loading) {
    return (
      <div className='flex justify-center items-center min-bs-[300px]'>
        <CircularProgress />
      </div>
    )
  }

  return (
    <Card>
      <CardHeader title={`${t.documents.editTitle} #${formData.documentNumber || id}`} />
      <CardContent>
        {alertInfo && (
          <Alert severity={alertInfo.type} className='mbe-5' onClose={() => setAlertInfo(null)}>
            {alertInfo.message}
          </Alert>
        )}

        <form onSubmit={handleSubmit}>
          <Grid container spacing={5}>
            <Grid size={{ xs: 12, md: 6 }}>
              <CustomTextField
                fullWidth
                label={`${t.documents.docNumber} *`}
                value={formData.documentNumber}
                onChange={e => setFormData({ ...formData, documentNumber: e.target.value })}
                required
              />
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <CustomTextField
                fullWidth
                select
                label={`${t.documents.type} *`}
                value={formData.direction}
                onChange={e => setFormData({ ...formData, direction: e.target.value })}
                required
              >
                <MenuItem value='incoming'>{t.documents.incoming}</MenuItem>
                <MenuItem value='outgoing'>{t.documents.outgoing}</MenuItem>
              </CustomTextField>
            </Grid>
            <Grid size={{ xs: 12 }}>
              <CustomTextField
                fullWidth
                label={`${t.documents.title} *`}
                value={formData.title}
                onChange={e => setFormData({ ...formData, title: e.target.value })}
                required
              />
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <CustomTextField
                fullWidth
                type='date'
                label={`${t.documents.issuedDate} *`}
                InputLabelProps={{ shrink: true }}
                value={formData.issuedDate}
                onChange={e => setFormData({ ...formData, issuedDate: e.target.value })}
                required
              />
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <CustomTextField
                fullWidth
                label={t.documents.partner}
                value={formData.partnerName}
                onChange={e => setFormData({ ...formData, partnerName: e.target.value })}
              />
            </Grid>
            <Grid size={{ xs: 12 }}>
              <CustomTextField
                fullWidth
                multiline
                rows={5}
                label={t.documents.summary}
                value={formData.summary}
                onChange={e => setFormData({ ...formData, summary: e.target.value })}
              />
            </Grid>
            <Grid size={{ xs: 12 }} className='flex items-center gap-4'>
              <Button
                type='submit'
                variant='contained'
                disabled={submitLoading}
                startIcon={submitLoading ? <CircularProgress size={18} color='inherit' /> : <i className='tabler-device-floppy' />}
              >
                {submitLoading ? t.documents.savingDoc : t.documents.saveDoc}
              </Button>
              <Button variant='tonal' color='secondary' onClick={() => router.back()}>
                {t.documents.cancel}
              </Button>
            </Grid>
          </Grid>
        </form>
      </CardContent>
    </Card>
  )
}

export default EditDocumentForm
