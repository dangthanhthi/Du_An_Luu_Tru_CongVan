using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AiOcrService.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("================================================================================");
Console.WriteLine("     BỘ ĐÁNH GIÁ & KIỂM THỬ OCR HÀNH CHÍNH VIỆT NAM (1,000 VĂN BẢN THỰC TẾ)      ");
Console.WriteLine("================================================================================\n");

var baseDir = AppDomain.CurrentDomain.BaseDirectory;
var pdfDir = Path.Combine(Directory.GetCurrentDirectory(), "pdfs_1000");
var resultsDir = Path.Combine(Directory.GetCurrentDirectory(), "results");
Directory.CreateDirectory(pdfDir);
Directory.CreateDirectory(resultsDir);

// ==========================================
// BƯỚC 1: TẠO 1000 TỆP PDF CÔNG VĂN MẪU ĐA DẠNG
// ==========================================
Console.WriteLine(">>> [BƯỚC 1]: Sinh 1,000 tệp PDF công văn thực tế đa dạng cơ quan & thể thức...");

var agencies = new (string FullName, string ShortName, string QuocHieu, string TinhThanh)[]
{
    ("THỦ TƯỚNG CHÍNH PHỦ", "TTg", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("VĂN PHÒNG CHÍNH PHỦ", "VPCP", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ GIÁO DỤC VÀ ĐÀO TẠO", "BGDĐT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ KHOA HỌC VÀ CÔNG NGHỆ", "BKHCN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ TÀI CHÍNH", "BTC", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ CÔNG AN", "BCA", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ QUỐC PHÒNG", "BQP", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ Y TẾ", "BYT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ THÔNG TIN VÀ TRUYỀN THÔNG", "BTTTT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ TƯ PHÁP", "BTP", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ CÔNG THƯƠNG", "BCT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ XÂY DỰNG", "BXD", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ GIAO THÔNG VẬN TẢI", "BGTVT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ TÀI NGUYÊN VÀ MÔI TRƯỜNG", "BTNMT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ NÔNG NGHIỆP VÀ PHÁT TRIỂN NÔNG THÔN", "BNNPTNT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ LAO ĐỘNG - THƯƠNG BINH VÀ XÃ HỘI", "BLĐTBXH", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ VĂN HÓA, THỂ THAO VÀ DU LỊCH", "BVHTTDL", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ KẾ HOẠCH VÀ ĐẦU TƯ", "BKHDT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ NỘI VỤ", "BNV", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ NGOẠI GIAO", "BNG", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("NGÂN HÀNG NHÀ NƯỚC VIỆT NAM", "NHNN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("THANH TRA CHÍNH PHỦ", "TTCP", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("ỦY BAN NHÂN DÂN THÀNH PHỐ HỒ CHÍ MINH", "UBND-HCM", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "TP. Hồ Chí Minh"),
    ("ỦY BAN NHÂN DÂN THÀNH PHỐ HÀ NỘI", "UBND-HN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("ỦY BAN NHÂN DÂN THÀNH PHỐ ĐÀ NẴNG", "UBND-ĐN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Đà Nẵng"),
    ("ỦY BAN NHÂN DÂN THÀNH PHỐ CẦN THƠ", "UBND-CT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Cần Thơ"),
    ("ỦY BAN NHÂN DÂN THÀNH PHỐ HẢI PHÒNG", "UBND-HP", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hải Phòng"),
    ("ỦY BAN NHÂN DÂN TỈNH BẮC NINH", "UBND-BN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Bắc Ninh"),
    ("ỦY BAN NHÂN DÂN TỈNH BÌNH DƯƠNG", "UBND-BD", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Bình Dương"),
    ("ỦY BAN NHÂN DÂN TỈNH ĐỒNG NAI", "UBND-ĐNAI", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Đồng Nai"),
    ("ỦY BAN NHÂN DÂN TỈNH QUẢNG NINH", "UBND-QN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Quảng Ninh"),
    ("ỦY BAN NHÂN DÂN TỈNH NGHỆ AN", "UBND-NA", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Nghệ An"),
    ("ỦY BAN NHÂN DÂN TỈNH THỪA THIÊN HUẾ", "UBND-TTH", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Thừa Thiên Huế"),
    ("ỦY BAN NHÂN DÂN TỈNH KHÁNH HÒA", "UBND-KH", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Khánh Hòa"),
    ("ỦY BAN NHÂN DÂN TỈNH LÂM ĐỒNG", "UBND-LĐ", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Lâm Đồng"),
    ("SỞ GIÁO DỤC VÀ ĐÀO TẠO TP. HỒ CHÍ MINH", "SGDĐT-HCM", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "TP. Hồ Chí Minh"),
    ("SỞ GIÁO DỤC VÀ ĐÀO TẠO HÀ NỘI", "SGDĐT-HN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("SỞ GIÁO DỤC VÀ ĐÀO TẠO ĐÀ NẴNG", "SGDĐT-ĐN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Đà Nẵng"),
    ("SỞ KHOA HỌC VÀ CÔNG NGHỆ TP. HỒ CHÍ MINH", "SKHCN-HCM", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "TP. Hồ Chí Minh"),
    ("SỞ KHOA HỌC VÀ CÔNG NGHỆ ĐÀ NẴNG", "SKHCN-ĐN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Đà Nẵng"),
    ("SỞ KHOA HỌC VÀ CÔNG NGHỆ CẦN THƠ", "SKHCN-CT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Cần Thơ"),
    ("SỞ TÀI CHÍNH HÀ NỘI", "STC-HN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("SỞ TÀI CHÍNH TP. HỒ CHÍ MINH", "STC-HCM", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "TP. Hồ Chí Minh"),
    ("SỞ TÀI NGUYÊN VÀ MÔI TRƯỜNG HÀ NỘI", "STNMT-HN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("SỞ THÔNG TIN VÀ TRUYỀN THÔNG TP. HỒ CHÍ MINH", "STTTT-HCM", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "TP. Hồ Chí Minh"),
    ("TẬP ĐOÀN BƯU CHÍNH VIỄN THÔNG VIỆT NAM", "VNPT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("TẬP ĐOÀN ĐIỆN LỰC VIỆT NAM", "EVN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("TẬP ĐOÀN CÔNG NGHIỆP - VIỄN THÔNG QUÂN ĐỘI", "Viettel", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("TẬP ĐOÀN DẦU KHÍ VIỆT NAM", "PVN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("CÔNG TY CỔ PHẦN FPT", "FPT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("NGÂN HÀNG TMCP NGOẠI THƯƠNG VIỆT NAM", "VCB", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("NGÂN HÀNG TMCP ĐẦU TƯ VÀ PHÁT TRIỂN VIỆT NAM", "BIDV", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("NGÂN HÀNG TMCP QUÂN ĐỘI", "MBBank", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("TỔNG CÔNG TY HÀNG KHÔNG VIỆT NAM", "VNA", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("CÔNG TY CỔ PHẦN QUẢN TRỊ DỮ LIỆU & VĂN THƯ SỐ DAS", "DAS", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "TP. Hồ Chí Minh"),
    ("TRƯỜNG ĐẠI HỌC BÁCH KHOA TP. HỒ CHÍ MINH", "ĐHBK-HCM", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "TP. Hồ Chí Minh"),
    ("TRƯỜNG ĐẠI HỌC QUỐC GIA HÀ NỘI", "ĐHQGHN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("TRƯỜNG ĐẠI HỌC KINH TẾ QUỐC DÂN", "NEU", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("VIỆN HÀN LÂM KHOA HỌC VÀ CÔNG NGHỆ VIỆT NAM", "VAST", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỆNH VIỆN BẠCH MAI", "BVBM", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỆNH VIỆN CHỢ RẪY", "BVCR", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "TP. Hồ Chí Minh"),
};

var docTypes = new (string TypeName, string TypeLabel, bool HasVv)[]
{
    ("Công Văn", "CÔNG VĂN", true),
    ("Quyết Định", "QUYẾT ĐỊNH", false),
    ("Thông Báo", "THÔNG BÁO", false),
    ("Tờ Trình", "TỜ TRÌNH", true),
    ("Giấy Mời", "GIẤY MỜI", false),
    ("Chỉ Thị", "CHỈ THỊ", false),
    ("Kế Hoạch", "KẾ HOẠCH", false),
    ("Báo Cáo", "BÁO CÁO", true),
    ("Nghị Quyết", "NGHỊ QUYẾT", false),
    ("Hợp Đồng", "HỢP ĐỒNG", false),
    ("Biên Bản", "BIÊN BẢN", false),
    ("Quy Chế", "QUY CHẾ", false),
    ("Quy Định", "QUY ĐỊNH", false),
    ("Hướng Dẫn", "HƯỚNG DẪN", false),
};

var subjects = new string[]
{
    "Hướng dẫn công tác chuẩn bị năm học mới 2026-2027",
    "Phối hợp triển khai Chương trình chuyển đổi số quốc gia đến năm 2030",
    "Tăng cường công tác phòng chống dịch bệnh mùa hè năm 2026",
    "Triển khai Đề án phát triển ứng dụng dữ liệu về dân cư định danh điện tử",
    "Đề nghị báo cáo kết quả thực hiện nhiệm vụ 6 tháng đầu năm 2026",
    "Phê duyệt Kế hoạch đào tạo bồi dưỡng cán bộ công chức năm 2026",
    "Hội thảo khoa học Ứng dụng Trí tuệ nhân tạo trong quản lý hành chính công",
    "Quy chế quản lý và sử dụng chữ ký số trong giao dịch điện tử",
    "Khen thưởng tập thể và cá nhân có thành tích xuất sắc trong công tác cải cách hành chính",
    "Về việc rà soát đánh giá thực trạng hạ tầng công nghệ thông tin",
    "Triển khai Nghị định 30/2020/NĐ-CP về công tác văn thư",
    "Đẩy mạnh thực hiện Đề án 06 về phát triển ứng dụng dữ liệu dân cư",
    "Tổ chức tập huấn nghiệp vụ quản lý tài chính ngân sách nhà nước",
    "Hướng dẫn đánh giá xếp loại viên chức năm 2026",
    "Tăng cường quản lý nhà nước về an toàn thực phẩm",
    "Triển khai hệ thống quản lý văn bản và điều hành trực tuyến",
    "Phối hợp tổ chức Ngày Chuyển đổi số quốc gia 10/10/2026",
    "Về việc cử cán bộ tham gia Đoàn công tác tại Nhật Bản",
    "Ban hành Quy định về tiêu chuẩn điều kiện bổ nhiệm công chức lãnh đạo",
    "Xin ý kiến về dự thảo Thông tư hướng dẫn Luật Giao dịch điện tử",
    "Thông báo lịch nghỉ Tết Nguyên đán Ất Tỵ 2026",
    "Mời tham dự Hội nghị tổng kết công tác năm 2026 và triển khai nhiệm vụ năm 2027",
    "Về việc điều chỉnh mức lương cơ sở từ ngày 01/07/2026",
    "Triển khai ứng dụng công nghệ blockchain trong quản lý chứng thư số",
    "Hướng dẫn thực hiện chế độ báo cáo thống kê ngành Giáo dục năm 2026",
    "Đề xuất kinh phí nâng cấp hạ tầng mạng nội bộ và hệ thống bảo mật",
    "Phê duyệt danh sách sinh viên đủ điều kiện tốt nghiệp đợt tháng 8/2026",
    "Về việc tuyển dụng viên chức sự nghiệp Giáo dục năm 2026",
    "Thông báo kết quả thanh tra kiểm tra công tác quản lý đầu tư xây dựng",
    "Đề nghị phối hợp cung cấp thông tin phục vụ giám sát an ninh mạng quốc gia",
    "Hướng dẫn thực hiện chương trình mục tiêu quốc gia giảm nghèo bền vững",
    "Về việc thẩm định thiết kế cơ sở dự án Khu đô thị mới phía Tây",
    "Triển khai Chương trình đào tạo kỹ năng số cho người lao động",
    "Mời báo giá cung cấp thiết bị CNTT cho dự án số hóa tài liệu lưu trữ",
    "Báo cáo tình hình thực hiện thu chi ngân sách quý III năm 2026",
    "Về việc thành lập Hội đồng thẩm định đề tài nghiên cứu khoa học cấp Bộ",
    "Chấp thuận chủ trương đầu tư dự án Nhà máy điện mặt trời Bình Thuận",
    "Quy định về bảo vệ dữ liệu cá nhân trong hệ thống thông tin nội bộ",
    "Hướng dẫn công tác bầu cử trưởng thôn tổ trưởng tổ dân phố nhiệm kỳ 2026-2031",
    "Về việc xử lý vi phạm pháp luật về bảo vệ môi trường tại KCN Biên Hòa",
    "Thực hiện chính sách hỗ trợ phát triển kinh tế số và xã hội số",
    "Đảm bảo an toàn thông tin mạng trong dịp lễ Quốc khánh 2/9",
    "Triển khai thanh toán không dùng tiền mặt trong trường học và cơ sở y tế",
    "Phê duyệt quy hoạch phân khu xây dựng tỷ lệ 1/2000 Khu công nghệ cao",
    "Tăng cường công tác phòng cháy chữa cháy tại các tòa nhà cao tầng",
    "Hướng dẫn thi đua lập thành tích chào mừng Đại hội Đảng các cấp",
    "Về việc cung cấp số liệu phục vụ lập báo cáo kinh tế xã hội năm 2026",
    "Phối hợp tổ chức Diễn đàn Chuyển đổi số doanh nghiệp vừa và nhỏ",
    "Ban hành Kế hoạch thanh tra chuyên ngành giáo dục nghề nghiệp năm 2026",
    "Quy định về định mức tiêu hao nhiên liệu đối với xe ô tô công vụ"
};

var signers = new (string Name, string Title)[]
{
    ("Nguyễn Văn Anh", "TỔNG GIÁM ĐỐC"),
    ("Trần Thị Bích Ngọc", "GIÁM ĐỐC"),
    ("Phạm Minh Tuấn", "CHỦ TỊCH"),
    ("Lê Hoàng Dũng", "PHÓ CHỦ TỊCH"),
    ("Đỗ Quốc Hùng", "BỘ TRƯỞNG"),
    ("Vũ Thị Lan Hương", "THỨ TRƯỞNG"),
    ("Ngô Thanh Sơn", "PHÓ TỔNG GIÁM ĐỐC"),
    ("Hoàng Đức Thịnh", "TRƯỞNG PHÒNG"),
    ("Mai Thị Thu Hà", "PHÓ GIÁM ĐỐC"),
    ("Bùi Văn Khánh", "VIỆN TRƯỞNG"),
    ("Đinh Trọng Hưng", "HIỆU TRƯỞNG"),
    ("Nguyễn Thị Mai Chi", "PHÓ HIỆU TRƯỞNG"),
    ("Võ Quốc Thắng", "CHÁNH VĂN PHÒNG"),
    ("Trần Đức Thắng", "TRƯỞNG BAN"),
    ("Lê Thị Kim Oanh", "PHÓ CHỦ TỊCH"),
};

var testCases = new List<TestCase>();
var rng = new Random(12345);

const int TARGET_COUNT = 1000;

for (int i = 0; i < TARGET_COUNT; i++)
{
    var agency = agencies[i % agencies.Length];
    var docType = docTypes[i % docTypes.Length];
    var subject = subjects[i % subjects.Length];
    var signer = signers[i % signers.Length];

    var day = rng.Next(1, 29);
    var month = rng.Next(1, 13);
    var year = rng.Next(2021, 2027);
    var refNum = rng.Next(1, 9999);

    var cleanAgencyCode = agency.ShortName.Replace("-", "");
    var refSuffix = cleanAgencyCode;
    if (docType.TypeName == "Quyết Định") refSuffix = $"QĐ-{cleanAgencyCode}";
    else if (docType.TypeName == "Thông Báo") refSuffix = $"TB-{cleanAgencyCode}";
    else if (docType.TypeName == "Tờ Trình") refSuffix = $"TTr-{cleanAgencyCode}";
    else if (docType.TypeName == "Kế Hoạch") refSuffix = $"KH-{cleanAgencyCode}";
    else if (docType.TypeName == "Chỉ Thị") refSuffix = $"CT-{cleanAgencyCode}";
    else if (docType.TypeName == "Giấy Mời") refSuffix = $"GM-{cleanAgencyCode}";
    else if (docType.TypeName == "Báo Cáo") refSuffix = $"BC-{cleanAgencyCode}";
    else if (docType.TypeName == "Nghị Quyết") refSuffix = $"NQ-{cleanAgencyCode}";
    else if (docType.TypeName == "Hợp Đồng") refSuffix = $"HĐ-{cleanAgencyCode}";
    else if (docType.TypeName == "Biên Bản") refSuffix = $"BB-{cleanAgencyCode}";
    else if (docType.TypeName == "Quy Chế") refSuffix = $"QC-{cleanAgencyCode}";
    else if (docType.TypeName == "Quy Định") refSuffix = $"QĐ-{cleanAgencyCode}";
    else if (docType.TypeName == "Hướng Dẫn") refSuffix = $"HD-{cleanAgencyCode}";
    else refSuffix = $"{cleanAgencyCode}-VP";

    var referenceNumber = $"{refNum}/{refSuffix}";
    var dateString = $"{day:D2}/{month:D2}/{year}";
    var fileName = $"{i + 1:D4}_{docType.TypeName.Replace(" ", "_")}_{agency.ShortName}_{refNum}.pdf";
    fileName = fileName.Replace("/", "_").Replace("\\", "_");

    var filePath = Path.Combine(pdfDir, fileName);

    if (!File.Exists(filePath))
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(36);
                page.MarginBottom(36);
                page.MarginLeft(50);
                page.MarginRight(40);

                page.Content().Column(col =>
                {
                    // HEADER
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(4).Column(leftCol =>
                        {
                            leftCol.Item().AlignCenter().Text(agency.FullName).FontSize(10).Bold();
                            leftCol.Item().AlignCenter().PaddingTop(2).Text("___________").FontSize(8);
                        });
                        row.RelativeItem(5).Column(rightCol =>
                        {
                            foreach (var line in agency.QuocHieu.Split('\n'))
                            {
                                rightCol.Item().AlignCenter().Text(line).FontSize(10).Bold();
                            }
                            rightCol.Item().AlignCenter().PaddingTop(2).Text("___________").FontSize(8);
                        });
                    });

                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem(4).AlignCenter().Text($"Số: {referenceNumber}").FontSize(10);
                        row.RelativeItem(5).AlignCenter().Text($"{agency.TinhThanh}, ngày {day} tháng {month} năm {year}").FontSize(10).Italic();
                    });

                    col.Item().PaddingTop(12);

                    if (docType.TypeLabel != "CÔNG VĂN")
                    {
                        col.Item().AlignCenter().Text(docType.TypeLabel).FontSize(13).Bold();
                        col.Item().PaddingTop(4);
                    }

                    if (docType.HasVv)
                    {
                        col.Item().AlignCenter().Text($"V/v: {subject}").FontSize(10).Italic();
                    }
                    else
                    {
                        col.Item().AlignCenter().Text(subject).FontSize(10).Italic();
                    }

                    col.Item().PaddingTop(12);

                    col.Item().Text(text =>
                    {
                        text.Span("Kính gửi: ").FontSize(10).Bold();
                        text.Span("Thủ trưởng các cơ quan, đơn vị liên quan.").FontSize(10);
                    });

                    col.Item().PaddingTop(8);

                    col.Item().Text($"Thực hiện chỉ đạo của cấp có thẩm quyền về {subject.ToLower()}, " +
                        $"{agency.FullName.ToLower()} thông báo/hướng dẫn thực hiện các nội dung sau:")
                        .FontSize(10).LineHeight(1.4f);

                    col.Item().PaddingTop(6);
                    col.Item().Text("1. Mục đích, yêu cầu").FontSize(10).Bold();
                    col.Item().PaddingLeft(15).Text($"Triển khai nghiêm túc, đúng tiến độ và đạt hiệu quả cao về {subject.ToLower()}.")
                        .FontSize(10).LineHeight(1.4f);

                    col.Item().PaddingTop(6);
                    col.Item().Text("2. Nhiệm vụ trọng tâm").FontSize(10).Bold();
                    col.Item().PaddingLeft(15).Text("a) Xây dựng kế hoạch chi tiết trước ngày 15 hàng tháng.\nb) Phối hợp chặt chẽ với các đơn vị liên quan.\nc) Định kỳ báo cáo kết quả thực hiện.")
                        .FontSize(10).LineHeight(1.4f);

                    col.Item().PaddingTop(12);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem(5).Column(leftCol =>
                        {
                            leftCol.Item().Text("Nơi nhận:").FontSize(8).Bold().Italic();
                            leftCol.Item().Text("- Như trên;").FontSize(8).Italic();
                            leftCol.Item().Text($"- {agency.FullName} (để b/c);").FontSize(8).Italic();
                            leftCol.Item().Text("- Lưu: VT, VP.").FontSize(8).Italic();
                        });
                        row.RelativeItem(4).Column(rightCol =>
                        {
                            rightCol.Item().AlignCenter().Text(signer.Title).FontSize(10).Bold();
                            rightCol.Item().PaddingTop(35);
                            rightCol.Item().AlignCenter().Text(signer.Name).FontSize(10).Bold();
                        });
                    });
                });
            });
        }).GeneratePdf(filePath);
    }

    testCases.Add(new TestCase
    {
        FileName = fileName,
        FilePath = filePath,
        Expected = new ExpectedData
        {
            ReferenceNumber = referenceNumber,
            Subject = subject,
            DocumentDate = dateString,
            PartnerName = agency.FullName,
            DocumentType = docType.TypeName,
            Signer = signer.Name
        }
    });

    if ((i + 1) % 200 == 0 || i == TARGET_COUNT - 1)
    {
        Console.WriteLine($"  [Sinh tệp] Đã chuẩn bị {i + 1:D4}/{TARGET_COUNT} file PDF...");
    }
}

Console.WriteLine($"✔ Đã tạo/chuẩn bị thành công {testCases.Count} file PDF mẫu chuẩn tại: {pdfDir}\n");

// ==========================================
// BƯỚC 2: KHỞI TẠO PIPELINE OCR & TRÍCH XUẤT
// ==========================================
Console.WriteLine(">>> [BƯỚC 2]: Khởi tạo OCR Engine & DynamicFieldExtractor...");

var ruleService = new OcrRuleService();
var fieldExtractor = new DynamicFieldExtractor(ruleService);
var ocrEngine = new TesseractOcrEngine();

Console.WriteLine("✔ Khởi tạo Pipeline thành công.\n");

// ==========================================
// BƯỚC 3: CHẠY BENCHMARK TOÀN DIỆN 1,000 FILE
// ==========================================
Console.WriteLine($">>> [BƯỚC 3]: Chạy kiểm thử tự động trên toàn bộ {testCases.Count} tệp PDF...");

var stopwatch = Stopwatch.StartNew();
var results = new List<TestResult>();

int refCorrect = 0;
int subjCorrect = 0;
int dateCorrect = 0;
int typeCorrect = 0;
int partnerCorrect = 0;
int signerCorrect = 0;

for (int idx = 0; idx < testCases.Count; idx++)
{
    var tc = testCases[idx];
    using var stream = File.OpenRead(tc.FilePath);

    var itemSw = Stopwatch.StartNew();
    var extractedText = ocrEngine.ExtractTextFromPdfStream(stream);
    var extractedFields = await fieldExtractor.ExtractFieldsAsync(extractedText, tc.FileName);
    itemSw.Stop();

    // So sánh kết quả
    bool isRefMatch = CleanString(extractedFields.ReferenceNumber) == CleanString(tc.Expected.ReferenceNumber) ||
                      (extractedFields.ReferenceNumber != null && tc.Expected.ReferenceNumber != null && 
                       extractedFields.ReferenceNumber.Contains(tc.Expected.ReferenceNumber.Split('/')[0], StringComparison.OrdinalIgnoreCase));

    bool isSubjMatch = IsSubjectMatching(extractedFields.Subject, tc.Expected.Subject);

    bool isDateMatch = extractedFields.DocumentDateString == tc.Expected.DocumentDate ||
                      (extractedFields.DocumentDate.HasValue && extractedFields.DocumentDate.Value.ToString("dd/MM/yyyy") == tc.Expected.DocumentDate);

    bool isTypeMatch = CleanString(extractedFields.DocumentType) == CleanString(tc.Expected.DocumentType);

    bool isPartnerMatch = !string.IsNullOrWhiteSpace(extractedFields.PartnerName) &&
                         (CleanString(extractedFields.PartnerName).Contains(CleanString(tc.Expected.PartnerName.Substring(0, Math.Min(10, tc.Expected.PartnerName.Length)))) ||
                          CleanString(tc.Expected.PartnerName).Contains(CleanString(extractedFields.PartnerName)) ||
                          CleanString(extractedFields.PartnerName) == CleanString(tc.Expected.PartnerName));

    bool isSignerMatch = !string.IsNullOrWhiteSpace(extractedFields.Signer) &&
                        (CleanString(extractedFields.Signer).Contains(CleanString(tc.Expected.Signer)) || 
                         CleanString(tc.Expected.Signer).Contains(CleanString(extractedFields.Signer)));

    if (isRefMatch) refCorrect++;
    if (isSubjMatch) subjCorrect++;
    if (isDateMatch) dateCorrect++;
    if (isTypeMatch) typeCorrect++;
    if (isPartnerMatch) partnerCorrect++;
    if (isSignerMatch) signerCorrect++;

    results.Add(new TestResult
    {
        FileName = tc.FileName,
        ElapsedMs = itemSw.ElapsedMilliseconds,
        IsRefMatch = isRefMatch,
        IsSubjMatch = isSubjMatch,
        IsDateMatch = isDateMatch,
        IsTypeMatch = isTypeMatch,
        IsPartnerMatch = isPartnerMatch,
        IsSignerMatch = isSignerMatch,
        ExtractedRef = extractedFields.ReferenceNumber,
        ExpectedRef = tc.Expected.ReferenceNumber,
        ExtractedSubj = extractedFields.Subject,
        ExpectedSubj = tc.Expected.Subject,
        ExtractedDate = extractedFields.DocumentDateString,
        ExpectedDate = tc.Expected.DocumentDate,
        ExtractedPartner = extractedFields.PartnerName,
        ExpectedPartner = tc.Expected.PartnerName,
        ExtractedType = extractedFields.DocumentType,
        ExpectedType = tc.Expected.DocumentType,
        ExtractedSigner = extractedFields.Signer,
        ExpectedSigner = tc.Expected.Signer
    });

    if ((idx + 1) % 100 == 0 || idx == testCases.Count - 1)
    {
        Console.WriteLine($"  [Kiểm thử] Đã xử lý {idx + 1:D4}/{testCases.Count} file ({itemSw.ElapsedMilliseconds}ms/file)...");
    }
}

stopwatch.Stop();

// ==========================================
// BƯỚC 4: TỔNG HỢP VÀ BÁO CÁO KẾT QUẢ
// ==========================================
int total = testCases.Count;
double refAcc = (double)refCorrect / total * 100.0;
double subjAcc = (double)subjCorrect / total * 100.0;
double dateAcc = (double)dateCorrect / total * 100.0;
double typeAcc = (double)typeCorrect / total * 100.0;
double partnerAcc = (double)partnerCorrect / total * 100.0;
double signerAcc = (double)signerCorrect / total * 100.0;
double overallAcc = (refAcc + subjAcc + dateAcc + typeAcc + partnerAcc + signerAcc) / 6.0;

Console.WriteLine("\n================================================================================");
Console.WriteLine("                  KẾT QUẢ KIỂM THỬ BENCHMARK TOÀN DIỆN (1,000 FILE)              ");
Console.WriteLine("================================================================================");
Console.WriteLine($"Tổng số tệp kiểm thử: {total} file PDF");
Console.WriteLine($"Tổng thời gian xử lý: {stopwatch.ElapsedMilliseconds} ms (Trung bình: {(double)stopwatch.ElapsedMilliseconds / total:F1} ms/file)");
Console.WriteLine("--------------------------------------------------------------------------------");
Console.WriteLine($"1. Số ký hiệu (Reference Number) : {refCorrect,4}/{total}  ({refAcc:F1}%)  {(refAcc >= 90 ? "✅ ĐẠT CHUẨN" : "⚠️ CẦN CẢI TIẾN")}");
Console.WriteLine($"2. Trích yếu / Tiêu đề (Subject) : {subjCorrect,4}/{total}  ({subjAcc:F1}%)  {(subjAcc >= 90 ? "✅ ĐẠT CHUẨN" : "⚠️ CẦN CẢI TIẾN")}");
Console.WriteLine($"3. Ngày ban hành (Document Date) : {dateCorrect,4}/{total}  ({dateAcc:F1}%)  {(dateAcc >= 90 ? "✅ ĐẠT CHUẨN" : "⚠️ CẦN CẢI TIẾN")}");
Console.WriteLine($"4. Thể loại văn bản (Doc Type)   : {typeCorrect,4}/{total}  ({typeAcc:F1}%)  {(typeAcc >= 90 ? "✅ ĐẠT CHUẨN" : "⚠️ CẦN CẢI TIẾN")}");
Console.WriteLine($"5. Cơ quan ban hành (Partner)    : {partnerCorrect,4}/{total}  ({partnerAcc:F1}%)  {(partnerAcc >= 90 ? "✅ ĐẠT CHUẨN" : "⚠️ CẦN CẢI TIẾN")}");
Console.WriteLine($"6. Người ký & Chức vụ (Signer)   : {signerCorrect,4}/{total}  ({signerAcc:F1}%)  {(signerAcc >= 85 ? "✅ ĐẠT CHUẨN" : "⚠️ CẦN CẢI TIẾN")}");
Console.WriteLine("--------------------------------------------------------------------------------");
Console.WriteLine($"ĐỘ CHÍNH XÁC TỔNG THỂ (OVERALL ACCURACY): {overallAcc:F1}%");
Console.WriteLine("================================================================================\n");

// Lưu kết quả JSON chi tiết
var reportObj = new
{
    totalDocuments = total,
    totalElapsedMs = stopwatch.ElapsedMilliseconds,
    averageMsPerDoc = (double)stopwatch.ElapsedMilliseconds / total,
    metrics = new
    {
        referenceNumber = new { correct = refCorrect, total, accuracy = refAcc },
        subject = new { correct = subjCorrect, total, accuracy = subjAcc },
        documentDate = new { correct = dateCorrect, total, accuracy = dateAcc },
        documentType = new { correct = typeCorrect, total, accuracy = typeAcc },
        partnerName = new { correct = partnerCorrect, total, accuracy = partnerAcc },
        signer = new { correct = signerCorrect, total, accuracy = signerAcc },
        overallAccuracy = overallAcc
    },
    details = results
};

var reportJsonPath = Path.Combine(resultsDir, "benchmark_1000_report.json");
File.WriteAllText(reportJsonPath, JsonSerializer.Serialize(reportObj, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"✔ Đã lưu báo cáo chi tiết 1,000 file JSON tại: {reportJsonPath}");

// ==========================================
// HELPER FUNCTIONS
// ==========================================
static string CleanString(string? str)
{
    if (string.IsNullOrWhiteSpace(str)) return string.Empty;
    return str.Replace(" ", "").Replace("/", "").Replace("-", "").Replace(".", "").ToLowerInvariant();
}

static bool IsSubjectMatching(string? extracted, string? expected)
{
    if (string.IsNullOrWhiteSpace(extracted) || string.IsNullOrWhiteSpace(expected)) return false;
    var extClean = extracted.Trim().ToLowerInvariant();
    var expClean = expected.Trim().ToLowerInvariant();
    if (extClean == expClean || extClean.Contains(expClean) || expClean.Contains(extClean)) return true;

    // Word overlap ratio >= 70%
    var extWords = extClean.Split(new[] { ' ', ',', '.', '-', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
    var expWords = expClean.Split(new[] { ' ', ',', '.', '-', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
    if (expWords.Length == 0) return false;

    int common = expWords.Count(w => extWords.Contains(w));
    return (double)common / expWords.Length >= 0.70;
}

public class TestCase
{
    public string FileName { get; set; } = default!;
    public string FilePath { get; set; } = default!;
    public ExpectedData Expected { get; set; } = default!;
}

public class ExpectedData
{
    public string ReferenceNumber { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string DocumentDate { get; set; } = default!;
    public string PartnerName { get; set; } = default!;
    public string DocumentType { get; set; } = default!;
    public string Signer { get; set; } = default!;
}

public class TestResult
{
    public string FileName { get; set; } = default!;
    public long ElapsedMs { get; set; }
    public bool IsRefMatch { get; set; }
    public bool IsSubjMatch { get; set; }
    public bool IsDateMatch { get; set; }
    public bool IsTypeMatch { get; set; }
    public bool IsPartnerMatch { get; set; }
    public bool IsSignerMatch { get; set; }
    public string? ExtractedRef { get; set; }
    public string? ExpectedRef { get; set; }
    public string? ExtractedSubj { get; set; }
    public string? ExpectedSubj { get; set; }
    public string? ExtractedDate { get; set; }
    public string? ExpectedDate { get; set; }
    public string? ExtractedPartner { get; set; }
    public string? ExpectedPartner { get; set; }
    public string? ExtractedType { get; set; }
    public string? ExpectedType { get; set; }
    public string? ExtractedSigner { get; set; }
    public string? ExpectedSigner { get; set; }
}
