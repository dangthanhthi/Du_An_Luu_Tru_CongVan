import { NextResponse } from 'next/server';
import imaps from 'imap-simple';
import { simpleParser } from 'mailparser';

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const { imapServer, imapPort, useSsl, emailAccount, appPassword } = body;

    if (!emailAccount || !appPassword) {
      return NextResponse.json({ success: false, error: 'Tài khoản Email và Mật khẩu ứng dụng là bắt buộc.' });
    }

    const config = {
      imap: {
        user: emailAccount,
        password: appPassword,
        host: imapServer || 'imap.gmail.com',
        port: imapPort || 993,
        tls: useSsl !== false,
        authTimeout: 5000,
        connTimeout: 5000,
        tlsOptions: { rejectUnauthorized: false }
      }
    };

    const connection = await imaps.connect(config);
    const box: any = await connection.openBox('INBOX');

    const total = (box.messages && box.messages.total) || 0;
    if (total === 0) {
      connection.end();
      return NextResponse.json({ success: true, documents: [] });
    }

    const startSeq = Math.max(1, total - 14);
    const searchCriteria = [`${startSeq}:${total}`];
    const fetchOptions = {
      bodies: ['HEADER', 'TEXT'],
      struct: true
    };

    const messages = await connection.search(searchCriteria, fetchOptions);
    
    // Sort messages by sequence number descending
    messages.sort((a, b) => b.attributes.uid - a.attributes.uid);
    const lastMessages = messages;

    const scannedDocs: any[] = [];

    for (const msg of lastMessages) {
      const headerPart = msg.parts.find(p => p.which === 'HEADER');
      const textPart = msg.parts.find(p => p.which === 'TEXT');
      
      const headersObj = headerPart ? headerPart.body : {};
      const rawText = textPart ? textPart.body : '';

      // Reconstruct raw headers string
      let headerString = '';
      for (const [key, val] of Object.entries(headersObj)) {
        if (Array.isArray(val)) {
          val.forEach(v => {
            headerString += `${key}: ${v}\r\n`;
          });
        } else {
          headerString += `${key}: ${val}\r\n`;
        }
      }

      // Reconstruct lightweight email string (no attachments)
      const lightweightEmail = `${headerString}\r\n${rawText}`;
      const parsed = await simpleParser(lightweightEmail);

      let subject = parsed.subject || 'Không có chủ đề';
      let sender = 'Không rõ';
      let senderEmail = '';
      
      if (parsed.from && parsed.from.value && parsed.from.value.length > 0) {
        const fromVal = parsed.from.value[0];
        sender = fromVal.name || fromVal.address || 'Không rõ';
        senderEmail = fromVal.address || '';
      } else {
        sender = parsed.from?.text || 'Không rõ';
      }
      
      const dateStr = parsed.date ? new Date(parsed.date).toLocaleDateString('vi-VN') : new Date().toLocaleDateString('vi-VN');
      const bodyText = parsed.text || parsed.html || '';
      
      // Check if there are PDF attachments in metadata struct
      const parts = imaps.getParts(msg.attributes.struct || []);
      let fileName = 'document.pdf';
      let hasPdf = false;

      const pdfPart = parts.find((part: any) => {
        return part.disposition && 
               part.disposition.type.toUpperCase() === 'ATTACHMENT' && 
               (part.subtype.toUpperCase() === 'PDF' || 
                (part.params && part.params.name && part.params.name.toLowerCase().endsWith('.pdf')) ||
                (part.disposition.params && part.disposition.params.filename && part.disposition.params.filename.toLowerCase().endsWith('.pdf')));
      });

      if (pdfPart) {
        fileName = pdfPart.params?.name || pdfPart.disposition?.params?.filename || 'document.pdf';
        hasPdf = true;
      }

      // Strictly check if the email contains a valid official document
      const hasNationalMotto = bodyText.includes('CỘNG HÒA XÃ HỘI CHỦ NGHĨA') && 
                               bodyText.includes('Độc lập - Tự do - Hạnh phúc');
      
      const hasDocNumberPattern = /Số:\s*\d+\/[A-Za-z0-9\-]+/i.test(bodyText) || 
                                  /Số\s*hiệu:\s*\d+\/[A-Za-z0-9\-]+/i.test(bodyText);
      
      const hasFormalSubject = subject.toLowerCase().includes('công văn') || 
                               subject.toLowerCase().includes('cv-') ||
                               /cv\s*\d+/i.test(subject);

      // A document is suitable if:
      // (1) It contains a PDF attachment
      // (2) OR it contains the Vietnamese National Motto (copy-pasted document)
      // (3) OR it has a formal subject and a document number pattern in the body
      const isSuitableDoc = hasPdf || hasNationalMotto || (hasFormalSubject && hasDocNumberPattern);

      if (isSuitableDoc) {
        // Extract original reference number (Số công văn gốc)
        let originalNo = '';
        
        // Try searching in subject
        const matchSubject = subject.match(/\d+\/[A-Za-z0-9\-]+/);
        if (matchSubject) {
          originalNo = matchSubject[0];
        } else {
          // Try searching in body for "Số: 1025/VNPT-VP"
          const matchBody = bodyText.match(/Số:\s*([^\s\r\n]+)/i);
          if (matchBody) {
            originalNo = matchBody[1];
          } else {
            originalNo = `GMAIL-${msg.attributes.uid}`;
          }
        }

        const newDocId = `doc-scan-real-${Date.now()}-${msg.attributes.uid}`;
        const docNo = `CV-DEN-2026-${Math.floor(10000 + Math.random() * 90000)}`;

        const docContent = `Quét thực tế từ email (${emailAccount}).\nNgười gửi: ${sender} <${senderEmail}>\nTiêu đề: ${subject}\nNgày nhận: ${dateStr}\n\nNội dung email:\n${bodyText}`;

        scannedDocs.push({
          id: newDocId,
          docNo: docNo,
          subject: subject,
          sender: sender,
          originalNo: originalNo,
          date: dateStr,
          priority: subject.includes('Mật') || bodyText.includes('Mật') ? 'Mật' : 'Thường',
          status: 'Chờ xử lý',
          content: docContent,
          fileName: fileName || (hasPdf ? 'document.pdf' : 'Nội_dung_Email.pdf')
        });
      }
    }

    connection.end();

    return NextResponse.json({ success: true, documents: scannedDocs });
  } catch (error: any) {
    console.error('Lỗi quét email trên API Serverless:', error);
    return NextResponse.json({ success: false, error: error.message || 'Lỗi không xác định khi kết nối IMAP.' });
  }
}
