import os

base_dir = r"c:\Users\MSIIIIII\Desktop\Dự án taskmanager\DAS\src\webapp\public\documents\samples"

os.makedirs(base_dir, exist_ok=True)

def generate_minimal_pdf(title, filename):
    title_bytes = title.encode('utf-8')
    stream_content = (
        b"BT\n"
        b"/F1 20 Tf\n"
        b"70 700 Td\n"
        b"(" + title_bytes + b") Tj\n"
        b"0 -35 Td\n"
        b"/F1 12 Tf\n"
        b"(Document Archive System - DAS Sign Service Verified) Tj\n"
        b"0 -20 Td\n"
        b"(System Date: 2026-07-19) Tj\n"
        b"ET\n"
    )
    length = len(stream_content)
    
    obj5 = b"5 0 obj\n<</Length " + str(length).encode('utf-8') + b">>\nstream\n" + stream_content + b"endstream\nendobj\n"
    
    o1 = 9
    o2 = o1 + len(b"1 0 obj\n<</Type/Catalog/Pages 2 0 R>>\nendobj\n")
    o3 = o2 + len(b"2 0 obj\n<</Type/Pages/Kids[3 0 R]/Count 1>>\nendobj\n")
    o4 = o3 + len(b"3 0 obj\n<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]/Resources<</Font<</F1 4 0 R>>>>/Contents 5 0 R>>\nendobj\n")
    o5 = o4 + len(b"4 0 obj\n<</Type/Font/Subtype/Type1/BaseFont/Helvetica-Bold>>\nendobj\n")
    
    xref_str = (
        "xref\n"
        "0 6\n"
        "0000000000 65535 f\n"
        "{:010d} 00000 n\n"
        "{:010d} 00000 n\n"
        "{:010d} 00000 n\n"
        "{:010d} 00000 n\n"
        "{:010d} 00000 n\n"
    ).format(o1, o2, o3, o4, o5)
    
    xref = xref_str.encode('utf-8')
    startxref = o5 + len(obj5)
    
    pdf_data = (
        b"%PDF-1.4\n"
        b"1 0 obj\n<</Type/Catalog/Pages 2 0 R>>\nendobj\n"
        b"2 0 obj\n<</Type/Pages/Kids[3 0 R]/Count 1>>\nendobj\n"
        b"3 0 obj\n<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]/Resources<</Font<</F1 4 0 R>>>>/Contents 5 0 R>>\nendobj\n"
        b"4 0 obj\n<</Type/Font/Subtype/Type1/BaseFont/Helvetica-Bold>>\nendobj\n"
        + obj5 +
        xref +
        b"trailer\n<</Size 6/Root 1 0 R>>\n"
        b"startxref\n" + str(startxref).encode('utf-8') + b"\n"
        b"%%EOF\n"
    )
    
    with open(os.path.join(base_dir, filename), "wb") as f:
        f.write(pdf_data)
    # Print only ascii safe logs
    print("Generated PDF: " + filename)

# Generate samples
generate_minimal_pdf("CONG VAN DEN: Huong dan bao cao luu tru quy II", "incoming-sample.pdf")
generate_minimal_pdf("CONG VAN DI: Quyet dinh bo nhiem nhan su ke toan", "outgoing-sample.pdf")
generate_minimal_pdf("CONG VAN NOI BO: Thong bao lich nghi phep nam 2026", "internal-sample.pdf")
