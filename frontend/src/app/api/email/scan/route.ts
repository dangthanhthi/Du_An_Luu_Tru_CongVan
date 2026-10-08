import { NextResponse } from 'next/server'
import { GoogleGenAI, Type, Schema } from '@google/genai'

// MOCK: A tiny sample PDF (blank) in base64 to simulate an email attachment.
// In production, this comes from IMAP/Gmail API.
const DUMMY_PDF_BASE64 = "JVBERi0xLjcKCjEgMCBvYmogICUgZW50cnkgcG9pbnQKPDwKICAvVHlwZSAvQ2F0YWxvZwogIC9QYWdlcyAyIDAgUgo+PgplbmRvYmoKCjIgMCBvYmoKPDwKICAvVHlwZSAvUGFnZXMKICAvTWVkaWFCb3ggWyAwIDAgMjAwIDIwMCBdCiAgL0NvdW50IDEKICAvS2lkcyBbIDMgMCBSIF0KPj4KZW5kb2JqCgozIDAgb2JqCjwwCiAgL1R5cGUgL1BhZ2UKICAvUGFyZW50IDIgMCBSCiAgL1Jlc291cmNlcyA8PAogICAgL0ZvbnQgPDwKICAgICAgL0YxIDQgMCBSCgkgID4+CiAgPj4KICAvQ29udGVudHMgNSAwIFIKPj4KZW5kb2JqCgo0IDAgb2JqCjw8CiAgL1R5cGUgL0ZvbnQKICAvU3VidHlwZSAvVHlwZTExCiAgL0Jhc2VGb250IC9UaW1lcy1Sb21hbgo+PgplbmRvYmoKCjUgMCBvYmoKPDwgL0xlbmd0aCA1MSA+PgpzdHJlYW0KQlQKICAvRjEgMTggVGYKICA1IDUwIFRkCiAgKFRoaXMgaXMgYSBkZW1vIGRvY3VtZW50KSAKICBUagoKRVQKZW5kc3RyZWFtCmVuZG9iagoKeHJlZgowIDYKMDAwMDAwMDAwMCA2NTUzNSBmIAowMDAwMDAwMDEwIDAwMDAwIG4gCjAwMDAwMDAwNjggMDAwMDAgbiAKMDAwMDAwMDE2NyAwMDAwMCBuIAowMDAwMDAwMjg3IDAwMDAwIG4gCjAwMDAwMDAzNzYgMDAwMDAgbiAKdHJhaWxlcgo8PAogIC9TaXplIDYKICAvUm9vdCAxIDAgUgo+PgpzdGFydHhyZWYKNDc3CiUlRU9GCg==";

export async function POST(request: Request) {
  try {
    const settings = await request.json()
    // Normally we use settings.email to connect to IMAP.

    const ai = new GoogleGenAI({ apiKey: process.env.GEMINI_API_KEY || 'fake-key' });
    
    // Check if API key is real, else return a realistic mocked OCR response
    let extractedData = {
      subject: "Báo cáo tiến độ dự án tuần 3",
      originator: "Công ty TNHH Phần Mềm HL",
      issuedDate: new Date().toISOString().split('T')[0],
      refNumber: "CV-2026/012-HL"
    };

    if (process.env.GEMINI_API_KEY) {
      try {
        const response = await ai.models.generateContent({
            model: 'gemini-2.5-flash',
            contents: [
                {
                    role: 'user',
                    parts: [
                        { inlineData: { data: DUMMY_PDF_BASE64, mimeType: 'application/pdf' } },
                        { text: `Extract the document details. Return a JSON object with:
                        - Subject (string): A short summary of the document (Trích yếu)
                        - Originator (string): The company/organization who sent the document (Nơi gửi)
                        - IssuedDate (string): ISO format YYYY-MM-DD
                        - DocumentNumber (string): The document reference number if any
                        ` }
                    ]
                }
            ],
            config: {
                responseMimeType: 'application/json',
                responseSchema: {
                    type: Type.OBJECT,
                    properties: {
                        Subject: { type: Type.STRING },
                        Originator: { type: Type.STRING },
                        IssuedDate: { type: Type.STRING },
                        DocumentNumber: { type: Type.STRING }
                    },
                    required: ['Subject', 'Originator', 'IssuedDate']
                }
            }
        });

        const parsedData = JSON.parse(response.text || '{}');
        extractedData = {
            subject: parsedData.Subject || extractedData.subject,
            originator: parsedData.Originator || extractedData.originator,
            issuedDate: parsedData.IssuedDate || extractedData.issuedDate,
            refNumber: parsedData.DocumentNumber || extractedData.refNumber
        };
      } catch (geminiErr) {
        console.warn("Gemini OCR failed or quota exceeded, falling back to mock", geminiErr);
      }
    }

    return NextResponse.json({
        success: true,
        items: [
            {
                messageId: `demo-email-${Date.now()}`,
                sender: "partner@example.com",
                subject: "Email đính kèm công văn mới",
                date: new Date().toISOString(),
                hasPdf: true,
                attachment: "VanBan_TienDo_HL.pdf",
                extractedRefNumber: extractedData.refNumber,
                extractedPartner: extractedData.originator,
                extractedTitle: extractedData.subject,
                extractedDate: extractedData.issuedDate
            }
        ]
    });
  } catch (error: any) {
    console.error('Email Scan Error:', error);
    return NextResponse.json(
      { success: false, message: error.message || 'Lỗi quét email hoặc OCR' },
      { status: 500 }
    );
  }
}
