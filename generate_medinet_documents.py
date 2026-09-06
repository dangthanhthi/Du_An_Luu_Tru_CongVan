import os
from reportlab.lib.pagesizes import A4
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors

pdfmetrics.registerFont(TTFont('Times', 'C:/Windows/Fonts/times.ttf'))
pdfmetrics.registerFont(TTFont('Times-Bold', 'C:/Windows/Fonts/timesbd.ttf'))
pdfmetrics.registerFont(TTFont('Times-Italic', 'C:/Windows/Fonts/timesi.ttf'))

os.makedirs("medinet_test_documents", exist_ok=True)

docs = [
    {
        "filename": "81qdttg19012021signed_4320218.pdf",
        "org": "THỦ TƯỚNG CHÍNH PHỦ",
        "ref": "Số: 81/QĐ-TTg",
        "date": "Hà Nội, ngày 19 tháng 01 năm 2021",
        "doc_type": "QUYẾT ĐỊNH",
        "title": "Về việc tiếp tục triển khai chương trình hỗ trợ pháp lý cho doanh nghiệp nhỏ và vừa năm 2021",
        "signer": "Phó Thủ tướng Trương Hòa Bình",
        "lines": [
            "Căn cứ Luật Tổ chức Chính phủ ngày 19 tháng 6 năm 2015;",
            "Căn cứ Luật Hỗ trợ doanh nghiệp nhỏ và vừa ngày 12 tháng 6 năm 2017;",
            "Xét đề nghị của Bộ trưởng Bộ Tư pháp,",
            "",
            "QUYẾT ĐỊNH:",
            "Điều 1. Phê duyệt việc tiếp tục triển khai chương trình hỗ trợ pháp lý cho doanh nghiệp",
            "nhỏ và vừa năm 2021 trên phạm vi toàn quốc.",
            "Điều 2. Giao Bộ Tư pháp chủ trì, phối hợp với các Bộ ngành, UBND các tỉnh,",
            "thành phố trực thuộc Trung ương tổ chức thực hiện hiệu quả chương trình.",
            "Điều 3. Quyết định này có hiệu lực thi hành kể từ ngày ký."
        ]
    },
    {
        "filename": "1241vpsigned_23220219.pdf",
        "org": "VĂN PHÒNG ỦY BAN NHÂN DÂN\nTHÀNH PHỐ HỒ CHÍ MINH",
        "ref": "Số: 1241/VP-TH",
        "date": "Thành phố Hồ Chí Minh, ngày 18 tháng 02 năm 2021",
        "doc_type": "CÔNG VĂN",
        "title": "Về công tác phát ngôn và cung cấp thông tin cho báo chí",
        "signer": "Chánh Văn phòng Đặng Quốc Toàn",
        "lines": [
            "Kính gửi: Thủ trưởng các Sở, ban, ngành; Chủ tịch UBND các quận, huyện, TP Thủ Đức.",
            "",
            "Nhằm nâng cao hiệu quả công tác phát ngôn và cung cấp thông tin định kỳ, đột xuất",
            "cho các cơ quan thông tấn, báo chí theo đúng quy định của Nghị định số 09/2017/NĐ-CP,",
            "Văn phòng Ủy ban nhân dân Thành phố đề nghị các cơ quan, đơn vị:",
            "1. Nghiêm túc thực hiện việc phân công người phát ngôn và cung cấp thông tin.",
            "2. Phối hợp chặt chẽ với Sở Thông tin và Truyền thông để xử lý kịp thời các vấn đề dư luận.",
            "Văn phòng UBND Thành phố thông báo đến các đơn vị biết, thực hiện."
        ]
    },
    {
        "filename": "852-kh-sytsigned_92202114.pdf",
        "org": "SỞ Y TẾ THÀNH PHỐ HỒ CHÍ MINH",
        "ref": "Số: 852/KH-SYT",
        "date": "Thành phố Hồ Chí Minh, ngày 09 tháng 02 năm 2021",
        "doc_type": "KẾ HOẠCH",
        "title": "Công tác phổ biến, giáo dục pháp luật năm 2021 của ngành Y tế trên địa bàn Thành phố Hồ Chí Minh",
        "signer": "Giám đốc PGS.TS.BS Tăng Chí Thượng",
        "lines": [
            "Căn cứ Kế hoạch số 450/KH-UBND của Ủy ban nhân dân Thành phố về công tác phổ biến,",
            "giáo dục pháp luật trên địa bàn Thành phố Hồ Chí Minh năm 2021;",
            "Sở Y tế Thành phố Hồ Chí Minh ban hành Kế hoạch công tác phổ biến, giáo dục pháp luật",
            "năm 2021 với các mục tiêu, nhiệm vụ trọng tâm sau:",
            "1. Tuyên truyền sâu rộng Luật Khám bệnh, chữa bệnh, Luật Dược, Luật Phòng chống dịch bệnh.",
            "2. Nâng cao ý thức tuân thủ pháp luật y tế cho toàn thể cán bộ, nhân viên y tế và nhân dân."
        ]
    },
    {
        "filename": "851-kh-sytsigned_92202114.pdf",
        "org": "SỞ Y TẾ THÀNH PHỐ HỒ CHÍ MINH",
        "ref": "Số: 851/KH-SYT",
        "date": "Thành phố Hồ Chí Minh, ngày 09 tháng 02 năm 2021",
        "doc_type": "KẾ HOẠCH",
        "title": "Công tác kiểm tra, rà soát, xử lý văn bản quy phạm pháp luật trong lĩnh vực y tế trên địa bàn Thành phố Hồ Chí Minh năm 2021",
        "signer": "Phó Giám đốc Nguyễn Hoài Nam",
        "lines": [
            "Nhằm bảo đảm tính hợp hiến, hợp pháp, tính thống nhất và đồng bộ của hệ thống văn bản",
            "quy phạm pháp luật trong lĩnh vực y tế trên địa bàn Thành phố Hồ Chí Minh,",
            "Sở Y tế ban hành Kế hoạch kiểm tra, rà soát văn bản quy phạm pháp luật năm 2021.",
            "Các phòng chuyên môn thuộc Sở, các bệnh viện công lập và tư nhân có trách nhiệm",
            "phối hợp rà soát các văn bản liên quan đến lĩnh vực hoạt động phụ trách."
        ]
    },
    {
        "filename": "850-kh-sytsigned_92202114.pdf",
        "org": "SỞ Y TẾ THÀNH PHỐ HỒ CHÍ MINH",
        "ref": "Số: 850/KH-SYT",
        "date": "Thành phố Hồ Chí Minh, ngày 09 tháng 02 năm 2021",
        "doc_type": "KẾ HOẠCH",
        "title": "Triển khai thực hiện công tác theo dõi tình hình thi hành pháp luật trong lĩnh vực Y tế năm 2021",
        "signer": "Giám đốc PGS.TS.BS Tăng Chí Thượng",
        "lines": [
            "Thực hiện Nghị định số 59/2012/NĐ-CP của Chính phủ về theo dõi tình hình thi hành pháp luật;",
            "Sở Y tế triển khai Kế hoạch theo dõi trọng tâm tình hình thi hành pháp luật trong lĩnh vực:",
            "1. Công tác phòng, chống bệnh truyền nhiễm và tiêm chủng mở rộng.",
            "2. Quản lý hành nghề khám bệnh, chữa bệnh tư nhân và kinh doanh dược phẩm.",
            "Thủ trưởng các đơn vị trực thuộc nghiêm túc triển khai thực hiện theo đúng tiến độ."
        ]
    },
    {
        "filename": "4855qdsigned_15120218.pdf",
        "org": "ỦY BAN NHÂN DÂN\nTHÀNH PHỐ HỒ CHÍ MINH",
        "ref": "Số: 4855/QĐ-UBND",
        "date": "Thành phố Hồ Chí Minh, ngày 15 tháng 01 năm 2021",
        "doc_type": "QUYẾT ĐỊNH",
        "title": "Về việc bãi bỏ các chỉ thị của Ủy ban nhân dân Thành phố Hồ Chí Minh",
        "signer": "Chủ tịch Nguyễn Thành Phong",
        "lines": [
            "Căn cứ Luật Ban hành văn bản quy phạm pháp luật ngày 22 tháng 6 năm 2015;",
            "Xét đề nghị của Giám đốc Sở Tư pháp tại Tờ trình số 150/TTr-STP,",
            "",
            "QUYẾT ĐỊNH:",
            "Điều 1. Bãi bỏ toàn bộ các Chỉ thị quy phạm pháp luật do Ủy ban nhân dân Thành phố",
            "ban hành từ năm 2010 đến 2020 không còn phù hợp với quy định hiện hành.",
            "Điều 2. Quyết định này có hiệu lực thi hành kể từ ngày ký."
        ]
    },
    {
        "filename": "8985-qd-sytsigned_5120218.pdf",
        "org": "SỞ Y TẾ THÀNH PHỐ HỒ CHÍ MINH",
        "ref": "Số: 8985/QĐ-SYT",
        "date": "Thành phố Hồ Chí Minh, ngày 31 tháng 12 năm 2020",
        "doc_type": "QUYẾT ĐỊNH",
        "title": "Quyết định ban hành quy định ký ủy quyền và thừa ủy quyền của Giám đốc Sở Y tế đối với các trưởng phòng chức năng, nghiệp vụ thuộc Sở Y tế",
        "signer": "Giám đốc GS.TS.BS Nguyễn Tấn Bỉnh",
        "lines": [
            "Căn cứ Quyết định số 28/2020/QĐ-UBND của Ủy ban nhân dân Thành phố quy định chức năng,",
            "nhiệm vụ, quyền hạn và cơ cấu tổ chức của Sở Y tế Thành phố Hồ Chí Minh;",
            "",
            "QUYẾT ĐỊNH:",
            "Điều 1. Ban hành kèm theo Quyết định này Quy định về việc ký ủy quyền và thừa ủy quyền (TUQ)",
            "của Giám đốc Sở Y tế cho Trưởng các phòng chuyên môn, nghiệp vụ thuộc Sở Y tế.",
            "Điều 2. Quyết định này có hiệu lực thi hành kể từ ngày 01 tháng 01 năm 2021."
        ]
    },
    {
        "filename": "13_du_thao_quy_che_18-11signed_61202115.pdf",
        "org": "BỘ Y TẾ",
        "ref": "Số: 4899/QĐ-BYT",
        "date": "Hà Nội, ngày 24 tháng 11 năm 2020",
        "doc_type": "QUYẾT ĐỊNH",
        "title": "Ban hành Quy chế tiếp nhận, xử lý, phát hành và quản lý sử dụng, khai thác Hệ thống Quản lý và điều hành văn bản điện tử Bộ Y tế",
        "signer": "Bộ trưởng GS.TS Nguyễn Thanh Long",
        "lines": [
            "Căn cứ Nghị định số 75/2017/NĐ-CP của Chính phủ quy định chức năng, nhiệm vụ của Bộ Y tế;",
            "Căn cứ Quyết định số 28/2018/QĐ-TTg của Thủ tướng Chính phủ về gửi nhận văn bản điện tử;",
            "",
            "QUYẾT ĐỊNH:",
            "Điều 1. Ban hành kèm theo Quyết định này Quy chế tiếp nhận, xử lý, phát hành và quản lý",
            "sử dụng, khai thác Hệ thống Quản lý văn bản điện tử của Bộ Y tế.",
            "Điều 2. Quyết định này có hiệu lực thi hành kể từ ngày ký ban hành."
        ]
    },
    {
        "filename": "3885_qd_hdph_21102020.pdf",
        "org": "HỘI ĐỒNG PHỐI HỢP PHỔ BIẾN\nGIÁO DỤC PHÁP LUẬT TPHCM",
        "ref": "Số: 3885/QĐ-HĐPH",
        "date": "Thành phố Hồ Chí Minh, ngày 21 tháng 10 năm 2020",
        "doc_type": "QUYẾT ĐỊNH",
        "title": "Ban hành quy chế hoạt động của Hội đồng phối hợp phổ biến giáo dục pháp luật của TPHCM",
        "signer": "Chủ tịch Hội đồng Ngô Minh Châu",
        "lines": [
            "Căn cứ Quyết định số 27/2013/QĐ-TTg của Thủ tướng Chính phủ quy định về Hội đồng phối hợp;",
            "Xét đề nghị của Cơ quan Thường trực Hội đồng - Sở Tư pháp Thành phố Hồ Chí Minh,",
            "",
            "QUYẾT ĐỊNH:",
            "Điều 1. Ban hành Quy chế hoạt động của Hội đồng phối hợp phổ biến giáo dục pháp luật TPHCM.",
            "Điều 2. Các thành viên Hội đồng và các cơ quan liên quan chịu trách nhiệm thi hành Quyết định này."
        ]
    }
]

for doc in docs:
    filepath = os.path.join("medinet_test_documents", doc["filename"])
    c = canvas.Canvas(filepath, pagesize=A4)
    width, height = A4

    # Header Left: Org & Ref
    org_lines = doc["org"].split("\n")
    y = height - 50
    c.setFont("Times-Bold", 10)
    for ol in org_lines:
        c.drawString(40, y, ol)
        y -= 14
    
    c.setFont("Times", 10)
    c.drawString(40, y - 2, doc["ref"])

    # Header Right: National Motto & Date
    c.setFont("Times-Bold", 10)
    c.drawRightString(width - 40, height - 50, "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")
    c.setFont("Times-Bold", 10)
    c.drawRightString(width - 40, height - 64, "Độc lập - Tự do - Hạnh phúc")
    c.setLineWidth(0.8)
    c.line(width - 200, height - 70, width - 40, height - 70)

    c.setFont("Times-Italic", 9.5)
    c.drawRightString(width - 40, height - 85, doc["date"])

    # Divider line
    c.setLineWidth(0.5)
    c.setStrokeColor(colors.gray)
    c.line(40, height - 105, width - 40, height - 105)

    # Document Type
    c.setFont("Times-Bold", 14)
    c.setFillColor(colors.black)
    c.drawCentredString(width / 2, height - 135, doc["doc_type"])

    # Title
    c.setFont("Times-Bold", 11)
    title_text = doc["title"]
    # Split title if too long
    if len(title_text) > 70:
        words = title_text.split(" ")
        mid = len(words) // 2
        line1 = " ".join(words[:mid])
        line2 = " ".join(words[mid:])
        c.drawCentredString(width / 2, height - 160, line1)
        c.drawCentredString(width / 2, height - 176, line2)
        content_y = height - 210
    else:
        c.drawCentredString(width / 2, height - 160, title_text)
        content_y = height - 195

    # Content
    c.setFont("Times", 11)
    for line in doc["lines"]:
        if line == "QUYẾT ĐỊNH:":
            c.setFont("Times-Bold", 11)
            c.drawCentredString(width / 2, content_y, line)
            c.setFont("Times", 11)
        else:
            c.drawString(50, content_y, line)
        content_y -= 18

    # Signer block
    c.setFont("Times-Bold", 10)
    c.drawRightString(width - 60, height - 680, "TM. CƠ QUAN BAN HÀNH")
    c.drawRightString(width - 60, height - 750, doc["signer"])

    # Recipient block
    c.setFont("Times-Bold", 9)
    c.drawString(40, height - 680, "Nơi nhận:")
    c.setFont("Times", 8.5)
    c.drawString(40, height - 694, "- Như Điều 3;")
    c.drawString(40, height - 706, "- Sở Y tế TPHCM (b/c);")
    c.drawString(40, height - 718, "- Lưu: VT, VP.")

    c.save()
    print(f"Generated Unicode PDF: {filepath}")

print("Successfully generated all 9 real Medinet Unicode PDFs!")
