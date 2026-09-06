using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "pdfs");
Directory.CreateDirectory(outputDir);

var expectedResults = new List<object>();

// ==========================================
// DANH SÁCH CƠ QUAN, LOẠI VĂN BẢN, V.V.
// ==========================================

var agencies = new (string FullName, string ShortName, string QuocHieu, string TinhThanh)[]
{
    ("THỦ TƯỚNG CHÍNH PHỦ", "TTg", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("VĂN PHÒNG CHÍNH PHỦ", "VPCP", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ GIÁO DỤC VÀ ĐÀO TẠO", "BGDĐT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ KHOA HỌC VÀ CÔNG NGHỆ", "BKHCN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ TÀI CHÍNH", "BTC", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ CÔNG AN", "BCA", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ Y TẾ", "BYT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ THÔNG TIN VÀ TRUYỀN THÔNG", "BTTTT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ TƯ PHÁP", "BTP", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("BỘ NÔNG NGHIỆP VÀ PHÁT TRIỂN NÔNG THÔN", "BNNPTNT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("ỦY BAN NHÂN DÂN THÀNH PHỐ HỒ CHÍ MINH", "UBND", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "TP. Hồ Chí Minh"),
    ("ỦY BAN NHÂN DÂN THÀNH PHỐ HÀ NỘI", "UBND", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("ỦY BAN NHÂN DÂN THÀNH PHỐ ĐÀ NẴNG", "UBND", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Đà Nẵng"),
    ("ỦY BAN NHÂN DÂN THÀNH PHỐ CẦN THƠ", "UBND", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Cần Thơ"),
    ("ỦY BAN NHÂN DÂN TỈNH BẮC NINH", "UBND", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Bắc Ninh"),
    ("ỦY BAN NHÂN DÂN TỈNH BÌNH DƯƠNG", "UBND", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Bình Dương"),
    ("SỞ GIÁO DỤC VÀ ĐÀO TẠO TP. HỒ CHÍ MINH", "SGDĐT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "TP. Hồ Chí Minh"),
    ("SỞ GIÁO DỤC VÀ ĐÀO TẠO HÀ NỘI", "SGDĐT-HN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("SỞ KHOA HỌC VÀ CÔNG NGHỆ TP. HỒ CHÍ MINH", "SKHCN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "TP. Hồ Chí Minh"),
    ("SỞ KHOA HỌC VÀ CÔNG NGHỆ ĐÀ NẴNG", "SKHCN-ĐN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Đà Nẵng"),
    ("SỞ KHOA HỌC VÀ CÔNG NGHỆ CẦN THƠ", "SKHCN-CT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Cần Thơ"),
    ("SỞ TÀI NGUYÊN VÀ MÔI TRƯỜNG HÀ NỘI", "STNMT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("TẬP ĐOÀN BƯU CHÍNH VIỄN THÔNG VIỆT NAM", "VNPT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("TẬP ĐOÀN ĐIỆN LỰC VIỆT NAM", "EVN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("TẬP ĐOÀN CÔNG NGHIỆP - VIỄN THÔNG QUÂN ĐỘI", "Viettel", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("CÔNG TY CỔ PHẦN FPT", "FPT", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("NGÂN HÀNG TMCP NGOẠI THƯƠNG VIỆT NAM", "VCB", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("NGÂN HÀNG TMCP ĐẦU TƯ VÀ PHÁT TRIỂN VIỆT NAM", "BIDV", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("TỔNG CÔNG TY HÀNG KHÔNG VIỆT NAM", "VNA", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("CÔNG TY CỔ PHẦN QUẢN TRỊ DỮ LIỆU & VĂN THƯ SỐ DAS", "DAS", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "TP. Hồ Chí Minh"),
    ("TRƯỜNG ĐẠI HỌC BÁCH KHOA TP. HỒ CHÍ MINH", "ĐHBK-HCM", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "TP. Hồ Chí Minh"),
    ("TRƯỜNG ĐẠI HỌC QUỐC GIA HÀ NỘI", "ĐHQGHN", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
    ("VIỆN HÀN LÂM KHOA HỌC VÀ CÔNG NGHỆ VIỆT NAM", "VAST", "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc", "Hà Nội"),
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
};

var subjects = new string[]
{
    "Hướng dẫn công tác chuẩn bị năm học mới 2026-2027",
    "Phối hợp triển khai Chương trình chuyển đổi số quốc gia đến năm 2030",
    "Tăng cường công tác phòng, chống dịch bệnh mùa hè năm 2026",
    "Triển khai Đề án phát triển ứng dụng dữ liệu về dân cư, định danh điện tử",
    "Đề nghị báo cáo kết quả thực hiện nhiệm vụ 6 tháng đầu năm 2026",
    "Phê duyệt Kế hoạch đào tạo, bồi dưỡng cán bộ, công chức năm 2026",
    "Hội thảo khoa học Ứng dụng Trí tuệ nhân tạo trong quản lý hành chính công",
    "Quy chế quản lý và sử dụng chữ ký số trong giao dịch điện tử",
    "Khen thưởng tập thể và cá nhân có thành tích xuất sắc trong công tác cải cách hành chính",
    "Về việc rà soát, đánh giá thực trạng hạ tầng công nghệ thông tin",
    "Triển khai Nghị định 30/2020/NĐ-CP về công tác văn thư",
    "Đẩy mạnh thực hiện Đề án 06 về phát triển ứng dụng dữ liệu dân cư",
    "Tổ chức tập huấn nghiệp vụ quản lý tài chính, ngân sách nhà nước",
    "Hướng dẫn đánh giá, xếp loại viên chức năm 2026",
    "Tăng cường quản lý nhà nước về an toàn thực phẩm",
    "Triển khai hệ thống quản lý văn bản và điều hành trực tuyến",
    "Phối hợp tổ chức Ngày Chuyển đổi số quốc gia 10/10/2026",
    "Về việc cử cán bộ tham gia Đoàn công tác tại Nhật Bản",
    "Ban hành Quy định về tiêu chuẩn, điều kiện bổ nhiệm công chức lãnh đạo",
    "Xin ý kiến về dự thảo Thông tư hướng dẫn Luật Giao dịch điện tử",
    "Thông báo lịch nghỉ Tết Nguyên đán Ất Tỵ 2026",
    "Mời tham dự Hội nghị tổng kết công tác năm 2026 và triển khai nhiệm vụ năm 2027",
    "Về việc điều chỉnh mức lương cơ sở từ ngày 01/07/2026",
    "Triển khai ứng dụng công nghệ blockchain trong quản lý chứng thư số",
    "Hướng dẫn thực hiện chế độ báo cáo thống kê ngành Giáo dục năm 2026",
    "Đề xuất kinh phí nâng cấp hạ tầng mạng nội bộ và hệ thống bảo mật",
    "Phê duyệt danh sách sinh viên đủ điều kiện tốt nghiệp đợt tháng 8/2026",
    "Về việc tuyển dụng viên chức sự nghiệp Giáo dục năm 2026",
    "Thông báo kết quả thanh tra, kiểm tra công tác quản lý đầu tư xây dựng",
    "Đề nghị phối hợp cung cấp thông tin phục vụ giám sát an ninh mạng quốc gia",
    "Hướng dẫn thực hiện chương trình mục tiêu quốc gia giảm nghèo bền vững",
    "Về việc thẩm định thiết kế cơ sở dự án Khu đô thị mới phía Tây",
    "Triển khai Chương trình đào tạo kỹ năng số cho người lao động",
    "Mời báo giá cung cấp thiết bị CNTT cho dự án số hóa tài liệu lưu trữ",
    "Báo cáo tình hình thực hiện thu chi ngân sách quý III năm 2026",
    "Về việc thành lập Hội đồng thẩm định đề tài nghiên cứu khoa học cấp Bộ",
    "Chấp thuận chủ trương đầu tư dự án Nhà máy điện mặt trời Bình Thuận",
    "Quy định về bảo vệ dữ liệu cá nhân trong hệ thống thông tin nội bộ",
    "Hướng dẫn công tác bầu cử trưởng thôn, tổ trưởng tổ dân phố nhiệm kỳ 2026-2031",
    "Về việc xử lý vi phạm pháp luật về bảo vệ môi trường tại KCN Biên Hòa",
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
};

var rng = new Random(42);

Console.WriteLine("Generating test PDFs...");

for (int i = 0; i < 100; i++)
{
    var agency = agencies[i % agencies.Length];
    var docType = docTypes[i % docTypes.Length];
    var subject = subjects[i % subjects.Length];
    var signer = signers[i % signers.Length];

    var day = rng.Next(1, 29);
    var month = rng.Next(1, 13);
    var year = rng.Next(2023, 2027);
    var refNum = rng.Next(1, 2000);

    var refSuffix = agency.ShortName;
    if (docType.TypeName == "Quyết Định") refSuffix = $"QĐ-{agency.ShortName}";
    else if (docType.TypeName == "Thông Báo") refSuffix = $"TB-{agency.ShortName}";
    else if (docType.TypeName == "Tờ Trình") refSuffix = $"TTr-{agency.ShortName}";
    else if (docType.TypeName == "Kế Hoạch") refSuffix = $"KH-{agency.ShortName}";
    else if (docType.TypeName == "Chỉ Thị") refSuffix = $"CT-{agency.ShortName}";
    else if (docType.TypeName == "Giấy Mời") refSuffix = $"GM-{agency.ShortName}";
    else if (docType.TypeName == "Báo Cáo") refSuffix = $"BC-{agency.ShortName}";
    else if (docType.TypeName == "Nghị Quyết") refSuffix = $"NQ-{agency.ShortName}";
    else if (docType.TypeName == "Hợp Đồng") refSuffix = $"HĐ-{agency.ShortName}";
    else refSuffix = $"{agency.ShortName}-VP";

    var referenceNumber = $"{refNum}/{refSuffix}";
    var dateString = $"{day:D2}/{month:D2}/{year}";
    var fileName = $"{i+1:D3}_{docType.TypeName.Replace(" ", "_")}_{agency.ShortName}_{refNum}.pdf";
    fileName = fileName.Replace("/", "_").Replace("\\", "_");

    var filePath = Path.Combine(outputDir, fileName);

    try
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(40);
                page.MarginBottom(40);
                page.MarginLeft(60);
                page.MarginRight(40);

                page.Content().Column(col =>
                {
                    // HEADER: Cơ quan | Quốc hiệu
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(4).Column(leftCol =>
                        {
                            leftCol.Item().AlignCenter().Text(agency.FullName)
                                .FontSize(11).Bold();
                            leftCol.Item().AlignCenter().PaddingTop(3)
                                .Text("___________").FontSize(9);
                        });
                        row.RelativeItem(5).Column(rightCol =>
                        {
                            foreach (var line in agency.QuocHieu.Split('\n'))
                            {
                                rightCol.Item().AlignCenter().Text(line)
                                    .FontSize(11).Bold();
                            }
                            rightCol.Item().AlignCenter().PaddingTop(3)
                                .Text("___________").FontSize(9);
                        });
                    });

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem(4).AlignCenter().Text($"Số: {referenceNumber}")
                            .FontSize(11);
                        row.RelativeItem(5).AlignCenter().Text(
                            $"{agency.TinhThanh}, ngày {day} tháng {month} năm {year}")
                            .FontSize(11).Italic();
                    });

                    col.Item().PaddingTop(15);

                    // TYPE LABEL
                    if (docType.TypeLabel != "CÔNG VĂN")
                    {
                        col.Item().AlignCenter().Text(docType.TypeLabel)
                            .FontSize(14).Bold();
                        col.Item().PaddingTop(5);
                    }

                    // SUBJECT
                    if (docType.HasVv)
                    {
                        col.Item().AlignCenter().Text($"V/v: {subject}")
                            .FontSize(11).Italic();
                    }
                    else
                    {
                        col.Item().AlignCenter().Text(subject)
                            .FontSize(11).Italic();
                    }

                    col.Item().PaddingTop(15);

                    // KÍNH GỬI
                    col.Item().Text(text =>
                    {
                        text.Span("Kính gửi: ").FontSize(11).Bold();
                        text.Span("Thủ trưởng các đơn vị trực thuộc").FontSize(11);
                    });

                    col.Item().PaddingTop(10);

                    // BODY
                    col.Item().Text($"Thực hiện chỉ đạo của Lãnh đạo {agency.FullName.ToLower()} về {subject.ToLower()}, " +
                        $"nay {agency.FullName.ToLower()} thông báo/hướng dẫn các nội dung sau:")
                        .FontSize(11).LineHeight(1.5f);

                    col.Item().PaddingTop(8);
                    col.Item().Text("1. Mục đích, yêu cầu")
                        .FontSize(11).Bold();
                    col.Item().PaddingLeft(20).Text(
                        $"Triển khai thực hiện nghiêm túc, kịp thời, đúng quy định về {subject.ToLower()}. " +
                        "Đảm bảo sự phối hợp chặt chẽ giữa các cơ quan, đơn vị liên quan.")
                        .FontSize(11).LineHeight(1.5f);

                    col.Item().PaddingTop(8);
                    col.Item().Text("2. Nội dung triển khai")
                        .FontSize(11).Bold();
                    col.Item().PaddingLeft(20).Text(
                        "a) Các đơn vị xây dựng kế hoạch chi tiết và gửi về cơ quan chủ trì trước ngày " +
                        $"{Math.Min(day + 15, 28)}/{month:D2}/{year}.\n" +
                        "b) Bố trí nguồn lực thực hiện đảm bảo đúng tiến độ và chất lượng.\n" +
                        "c) Báo cáo kết quả thực hiện định kỳ hàng tháng.")
                        .FontSize(11).LineHeight(1.5f);

                    col.Item().PaddingTop(8);
                    col.Item().Text("3. Tổ chức thực hiện")
                        .FontSize(11).Bold();
                    col.Item().PaddingLeft(20).Text(
                        "Yêu cầu thủ trưởng các đơn vị nghiêm túc triển khai thực hiện. " +
                        "Trong quá trình thực hiện, nếu có khó khăn, vướng mắc kịp thời báo cáo " +
                        "để được hướng dẫn, giải quyết./.")
                        .FontSize(11).LineHeight(1.5f);

                    col.Item().PaddingTop(15);

                    // NƠI NHẬN + CHỮ KÝ
                    col.Item().Row(row =>
                    {
                        row.RelativeItem(5).Column(leftCol =>
                        {
                            leftCol.Item().Text("Nơi nhận:").FontSize(9).Bold().Italic();
                            leftCol.Item().Text("- Như trên;").FontSize(9).Italic();
                            leftCol.Item().Text($"- {agency.FullName} (để b/c);").FontSize(9).Italic();
                            leftCol.Item().Text("- Lưu: VT, VP.").FontSize(9).Italic();
                        });
                        row.RelativeItem(4).Column(rightCol =>
                        {
                            rightCol.Item().AlignCenter().Text(signer.Title)
                                .FontSize(11).Bold();
                            rightCol.Item().PaddingTop(40);
                            rightCol.Item().AlignCenter().Text(signer.Name)
                                .FontSize(11).Bold();
                        });
                    });
                });
            });
        }).GeneratePdf(filePath);

        expectedResults.Add(new
        {
            fileName,
            expected = new
            {
                referenceNumber,
                subject,
                documentDate = dateString,
                partnerName = agency.FullName,
                documentType = docType.TypeName,
                signer = signer.Name
            }
        });

        Console.WriteLine($"  [{i+1:D3}/100] {fileName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [{i+1:D3}/100] ERROR: {fileName} - {ex.Message}");
    }
}

// Save expected results
var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "expected_results.json");
var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
File.WriteAllText(jsonPath, JsonSerializer.Serialize(expectedResults, options));

Console.WriteLine($"\nDone! Generated {expectedResults.Count} PDFs in '{outputDir}'");
Console.WriteLine($"Expected results saved to '{jsonPath}'");
