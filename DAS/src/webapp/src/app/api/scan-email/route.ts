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
        authTimeout: 10000,
        connTimeout: 15000,
        tlsOptions: { rejectUnauthorized: false }
      }
    };

    const connection = await imaps.connect(config);
    const box = await connection.openBox('INBOX');

    const total = (box.messages && box.messages.total) || 0;
    if (total === 0) {
      connection.end();
      return NextResponse.json({ success: true, documents: [] });
    }

    const startSeq = Math.max(1, total - 14);
    const searchCriteria = [`${startSeq}:${total}`];
    const fetchOptions = {
      bodies: ['HEADER', 'TEXT', ''],
      struct: true
    };

    const messages = await connection.search(searchCriteria, fetchOptions);
    
    // Sort messages by sequence number descending
    messages.sort((a, b) => b.attributes.uid - a.attributes.uid);
    const lastMessages = messages;

    const scannedDocs: any[] = [];

    for (const msg of lastMessages) {
      // Fetch message body
      const allPart = msg.parts.find(p => p.which === '');
      const rawEmail = allPart ? allPart.body : '';
      
      let subject = 'Không có chủ đề';
      let sender = 'Không rõ';
      let senderEmail = '';
      let dateStr = new Date().toLocaleDateString('vi-VN');
      let bodyText = '';
      let fileName = 'document.pdf';
      let hasPdf = false;

      if (rawEmail) {
        const parsed = await simpleParser(rawEmail);
        subject = parsed.subject || 'Không có chủ đề';
        
        if (parsed.from && parsed.from.value && parsed.from.value.length > 0) {
          const fromVal = parsed.from.value[0];
          sender = fromVal.name || fromVal.address || 'Không rõ';
          senderEmail = fromVal.address || '';
        } else {
          sender = parsed.from?.text || 'Không rõ';
        }
        
        dateStr = parsed.date ? new Date(parsed.date).toLocaleDateString('vi-VN') : new Date().toLocaleDateString('vi-VN');
        bodyText = parsed.text || parsed.html || '';

        // Check if there are attachments
        if (parsed.attachments && parsed.attachments.length > 0) {
          const pdfAttachment = parsed.attachments.find(att => 
            att.contentType === 'application/pdf' || 
            att.filename?.toLowerCase().endsWith('.pdf')
          );
          if (pdfAttachment) {
            fileName = pdfAttachment.filename || 'document.pdf';
            hasPdf = true;
          }
        }
      }

      // Check if this email looks like a document:
      // (1) Has a PDF attachment OR
      // (2) Subject or body contains common document headings like "CỘNG HÒA XÃ HỘI CHỦ NGHĨA", "TẬP ĐOÀN VNPT", "Số: ...", "V/v ..."
      const isDocText = subject.toLowerCase().includes('công văn') || 
                        subject.toLowerCase().includes('cv-') || 
                        bodyText.includes('CỘNG HÒA XÃ HỘI CHỦ NGHĨA') || 
                        bodyText.includes('TẬP ĐOÀN') ||
                        bodyText.includes('Kính gửi') ||
                        bodyText.includes('Số:') || 
                        bodyText.includes('V/v');

      if (hasPdf || isDocText) {
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
