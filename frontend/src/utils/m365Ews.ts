// Microsoft 365 Exchange Web Services (EWS) Client over HTTPS (Port 443)
// Spec: Uses OAuth2 ROPC with Microsoft Office Client to authenticate against Azure AD
// and queries Exchange Online over HTTPS (port 443).

export interface M365EmailItem {
  id: string
  subject: string
  from: string
  senderEmail: string
  date: string
  receivedAt: string
  hasAttachments: boolean
  attachments: Array<{
    id?: string
    name: string
    size: number
    isPdf: boolean
  }>
}

export const M365_DEFAULTS = {
  email: 't-online@hlhvjoc.com.vn',
  password: 'Pw@123456',
  tenantId: 'b9a125df-d5fd-4d4d-8d68-6b024ed43da3',
  clientId: 'd3590ed6-52b3-4102-aeff-aad2292ab01c', // Microsoft Office public client
  scope: 'https://outlook.office.com/EWS.AccessAsUser.All offline_access',
  ewsUrl: 'https://outlook.office365.com/EWS/Exchange.asmx'
}

/**
 * Acquires a JWT Bearer Access Token for Microsoft Exchange via Azure AD ROPC
 */
export async function acquireM365Token(
  email = M365_DEFAULTS.email,
  password = M365_DEFAULTS.password,
  tenantId = M365_DEFAULTS.tenantId
): Promise<string> {
  const tokenEndpoint = `https://login.microsoftonline.com/${tenantId}/oauth2/v2.0/token`

  const body = new URLSearchParams({
    client_id: M365_DEFAULTS.clientId,
    grant_type: 'password',
    username: email,
    password: password,
    scope: M365_DEFAULTS.scope
  })

  const res = await fetch(tokenEndpoint, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: body.toString(),
    cache: 'no-store'
  })

  if (!res.ok) {
    const errorText = await res.text()
    throw new Error(`Xác thực tài khoản Microsoft 365 thất bại: ${errorText}`)
  }

  const data = await res.json()
  if (!data?.access_token) {
    throw new Error('Không nhận được access token từ máy chủ xác thực Microsoft.')
  }

  return data.access_token as string
}

/**
 * Tests connection to Microsoft Exchange 365 via EWS GetFolder
 */
export async function testM365Connection(
  email = M365_DEFAULTS.email,
  password = M365_DEFAULTS.password
): Promise<{ success: boolean; message: string; folderCount?: number }> {
  const token = await acquireM365Token(email, password)

  const testSoap = `<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
               xmlns:m="http://schemas.microsoft.com/exchange/services/2006/messages"
               xmlns:t="http://schemas.microsoft.com/exchange/services/2006/types"
               xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Header>
    <t:RequestServerVersion Version="Exchange2013" />
  </soap:Header>
  <soap:Body>
    <m:GetFolder>
      <m:FolderShape>
        <t:BaseShape>Default</t:BaseShape>
      </m:FolderShape>
      <m:FolderIds>
        <t:DistinguishedFolderId Id="inbox"/>
      </m:FolderIds>
    </m:GetFolder>
  </soap:Body>
</soap:Envelope>`

  const res = await fetch(M365_DEFAULTS.ewsUrl, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'text/xml; charset=utf-8',
      'User-Agent': 'DAS-Intake/1.0'
    },
    body: testSoap,
    cache: 'no-store'
  })

  if (!res.ok) {
    throw new Error(`Kết nối máy chủ Exchange Web Services thất bại (HTTP ${res.status}).`)
  }

  const xml = await res.text()
  const totalItemsMatch = xml.match(/<t:TotalCount>(\d+)<\/t:TotalCount>/)
  const totalCount = totalItemsMatch ? parseInt(totalItemsMatch[1], 10) : 0

  return {
    success: true,
    message: `Kết nối thành công tới hòm thư Microsoft Exchange 365 (${email}) qua Web Service (HTTPS :443). Hộp thư có ${totalCount} email.`,
    folderCount: totalCount
  }
}

/**
 * Scans Inbox for latest messages and their attachments
 */
export async function scanM365Inbox(
  email = M365_DEFAULTS.email,
  password = M365_DEFAULTS.password,
  maxItems = 15
): Promise<M365EmailItem[]> {
  const token = await acquireM365Token(email, password)

  const findItemSoap = `<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
               xmlns:m="http://schemas.microsoft.com/exchange/services/2006/messages"
               xmlns:t="http://schemas.microsoft.com/exchange/services/2006/types"
               xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Header>
    <t:RequestServerVersion Version="Exchange2013" />
  </soap:Header>
  <soap:Body>
    <m:FindItem Traversal="Shallow">
      <m:ItemShape>
        <t:BaseShape>IdOnly</t:BaseShape>
        <t:AdditionalProperties>
          <t:FieldURI FieldURI="item:Subject" />
          <t:FieldURI FieldURI="item:DateTimeReceived" />
          <t:FieldURI FieldURI="item:HasAttachments" />
          <t:FieldURI FieldURI="message:From" />
          <t:FieldURI FieldURI="message:IsRead" />
        </t:AdditionalProperties>
      </m:ItemShape>
      <m:IndexedPageItemView MaxEntriesReturned="${maxItems}" Offset="0" BasePoint="Beginning" />
      <m:SortOrder>
        <t:FieldOrder Order="Descending">
          <t:FieldURI FieldURI="item:DateTimeReceived" />
        </t:FieldOrder>
      </m:SortOrder>
      <m:ParentFolderIds>
        <t:DistinguishedFolderId Id="inbox" />
      </m:ParentFolderIds>
    </m:FindItem>
  </soap:Body>
</soap:Envelope>`

  const res = await fetch(M365_DEFAULTS.ewsUrl, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'text/xml; charset=utf-8',
      'User-Agent': 'DAS-Intake/1.0'
    },
    body: findItemSoap,
    cache: 'no-store'
  })

  if (!res.ok) {
    throw new Error(`Quét danh sách email thất bại (HTTP ${res.status}).`)
  }

  const xml = await res.text()
  const messageBlocks = xml.split(/<t:Message[^>]*>/).slice(1)
  const items: M365EmailItem[] = []

  for (const block of messageBlocks) {
    const itemIdMatch = block.match(/<t:ItemId\s+Id="([^"]+)"/)
    const subjectMatch = block.match(/<t:Subject>([^<]*)<\/t:Subject>/)
    const dateMatch = block.match(/<t:DateTimeReceived>([^<]*)<\/t:DateTimeReceived>/)
    const hasAttMatch = block.match(/<t:HasAttachments>([^<]*)<\/t:HasAttachments>/)
    const fromNameMatch = block.match(/<t:Mailbox>\s*<t:Name>([^<]*)<\/t:Name>/)
    const fromEmailMatch = block.match(/<t:EmailAddress>([^<]*)<\/t:EmailAddress>/)

    const itemId = itemIdMatch ? itemIdMatch[1] : ''
    const subject = subjectMatch ? subjectMatch[1] : '(Không có tiêu đề)'
    const rawDate = dateMatch ? dateMatch[1] : new Date().toISOString()
    const hasAtt = hasAttMatch ? hasAttMatch[1] === 'true' : false
    const senderName = fromNameMatch ? fromNameMatch[1] : ''
    const senderEmail = fromEmailMatch ? fromEmailMatch[1] : ''
    const from = senderName ? `${senderName} <${senderEmail}>` : senderEmail

    // Format readable date (DD/MM/YYYY HH:mm)
    let formattedDate = rawDate
    try {
      const d = new Date(rawDate)
      formattedDate = `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
    } catch {
      // keep rawDate
    }

    const attachments: Array<{ id?: string; name: string; size: number; isPdf: boolean }> = []

    if (hasAtt && itemId) {
      try {
        const getItemSoap = `<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
               xmlns:m="http://schemas.microsoft.com/exchange/services/2006/messages"
               xmlns:t="http://schemas.microsoft.com/exchange/services/2006/types"
               xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Header>
    <t:RequestServerVersion Version="Exchange2013" />
  </soap:Header>
  <soap:Body>
    <m:GetItem>
      <m:ItemShape>
        <t:BaseShape>IdOnly</t:BaseShape>
        <t:AdditionalProperties>
          <t:FieldURI FieldURI="item:Attachments" />
        </t:AdditionalProperties>
      </m:ItemShape>
      <m:ItemIds>
        <t:ItemId Id="${itemId}" />
      </m:ItemIds>
    </m:GetItem>
  </soap:Body>
</soap:Envelope>`

        const getRes = await fetch(M365_DEFAULTS.ewsUrl, {
          method: 'POST',
          headers: {
            Authorization: `Bearer ${token}`,
            'Content-Type': 'text/xml; charset=utf-8',
            'User-Agent': 'DAS-Intake/1.0'
          },
          body: getItemSoap,
          cache: 'no-store'
        })

        if (getRes.ok) {
          const getXml = await getRes.text()
          const fileBlocks = getXml.split(/<t:FileAttachment>/).slice(1)
          for (const fb of fileBlocks) {
            const attIdMatch = fb.match(/<t:AttachmentId\s+Id="([^"]+)"/)
            const nameMatch = fb.match(/<t:Name>([^<]*)<\/t:Name>/)
            const sizeMatch = fb.match(/<t:Size>([^<]*)<\/t:Size>/)

            const name = nameMatch ? nameMatch[1] : 'attachment.bin'
            const size = sizeMatch ? parseInt(sizeMatch[1], 10) : 0
            const isPdf = name.toLowerCase().endsWith('.pdf')

            attachments.push({
              id: attIdMatch ? attIdMatch[1] : undefined,
              name,
              size,
              isPdf
            })
          }
        }
      } catch (err) {
        console.error('Lỗi khi lấy tệp đính kèm của thư:', err)
      }
    }

    items.push({
      id: itemId,
      subject,
      from,
      senderEmail,
      date: formattedDate,
      receivedAt: rawDate,
      hasAttachments: hasAtt,
      attachments
    })
  }

  return items
}
