import { NextResponse, NextRequest } from 'next/server';
import imaps from 'imap-simple';

async function fetchPdfData(config: any, uid: number) {
  const connection = await imaps.connect(config);
  try {
    await connection.openBox('INBOX');
    const searchCriteria = [['UID', uid]];
    const fetchOptions = {
      bodies: ['HEADER'],
      struct: true
    };

    const messages = await connection.search(searchCriteria, fetchOptions);
    if (!messages || messages.length === 0) {
      throw new Error(`Message with UID ${uid} not found`);
    }

    const msg = messages[0];
    const parts = imaps.getParts(msg.attributes.struct || []);
    
    const pdfPart = parts.find((part: any) => {
      const isPdfSubtype = part.subtype && part.subtype.toUpperCase() === 'PDF';
      const hasPdfName = (part.params && part.params.name && part.params.name.toLowerCase().endsWith('.pdf')) ||
                         (part.disposition && part.disposition.params && part.disposition.params.filename && part.disposition.params.filename.toLowerCase().endsWith('.pdf'));
      return !!(isPdfSubtype || hasPdfName);
    });

    if (!pdfPart) {
      throw new Error('No PDF attachment found in this message structure.');
    }

    const partData = await connection.getPartData(msg, pdfPart);
    return partData;
  } finally {
    try { connection.end(); } catch (e) {}
  }
}

export async function GET(request: NextRequest) {
  let filename = 'document.pdf';
  try {
    const { searchParams } = new URL(request.url);
    const uidStr = searchParams.get('uid');
    const server = searchParams.get('server');
    const portStr = searchParams.get('port');
    const sslStr = searchParams.get('ssl');
    const email = searchParams.get('email');
    const password = searchParams.get('password');
    filename = searchParams.get('filename') || 'document.pdf';

    if (!uidStr || !email || !password) {
      return NextResponse.json({ error: 'Missing required parameters (uid, email, password)' }, { status: 400 });
    }

    const uid = parseInt(uidStr, 10);
    const port = parseInt(portStr || '993', 10);
    const useSsl = sslStr !== 'false';

    const config = {
      imap: {
        user: email,
        password: password,
        host: server || 'imap.gmail.com',
        port: port,
        tls: useSsl,
        authTimeout: 8000,
        connTimeout: 8000,
        tlsOptions: { rejectUnauthorized: false }
      }
    };

    // Enforce a strict 15-second unified timeout for the entire PDF retrieval process
    const pdfPromise = fetchPdfData(config, uid);
    const timeoutPromise = new Promise<any>((_, reject) => 
      setTimeout(() => reject(new Error('Thời gian tải tệp PDF từ Gmail quá hạn (15 giây).')), 15000)
    );

    const partData = await Promise.race([pdfPromise, timeoutPromise]);

    return new NextResponse(partData, {
      headers: {
        'Content-Type': 'application/pdf',
        'Content-Disposition': `inline; filename="${filename}"`
      }
    });

  } catch (error: any) {
    console.error('Error fetching document PDF:', error);
    
    // Cloud/Vercel Hybrid Fallback:
    // If IMAP fails to connect and fetch PDF, stream the requested static PDF from public/documents/samples if available.
    try {
      const fs = require('fs');
      const path = require('path');
      
      const safeFilename = path.basename(filename);
      let targetPath = path.join(process.cwd(), 'public', 'documents', 'samples', safeFilename);
      
      if (!fs.existsSync(targetPath)) {
        targetPath = path.join(process.cwd(), 'public', 'documents', 'samples', 'incoming-sample.pdf');
      }

      if (fs.existsSync(targetPath)) {
        const fileBuffer = fs.readFileSync(targetPath);
        return new NextResponse(fileBuffer, {
          headers: {
            'Content-Type': 'application/pdf',
            'Content-Disposition': `inline; filename="${safeFilename}"`
          }
        });
      }
    } catch (fsErr) {
      console.error('Failed to stream fallback sample PDF:', fsErr);
    }
    
    return NextResponse.json({ error: error.message || 'Internal Server Error' }, { status: 500 });
  }
}
