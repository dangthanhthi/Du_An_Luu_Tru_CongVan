using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AiOcrService.Models;
using AiOcrService.Services.FuzzyCorrection;

namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Bóc tách Cơ quan ban hành (PartnerName). Toàn bộ logic Bước 5 giữ NGUYÊN VẸN. BƯỚC 6 chèn
    /// 1 khối fallback DUY NHẤT: nếu sau tất cả các nhánh if/else + regex dò header mà PartnerName
    /// VẪN rỗng, mới thử rule động từ DB - đặt TRƯỚC bước _fuzzyCorrector.CorrectText() cuối cùng
    /// để giá trị lấy từ rule động cũng được hưởng lợi từ việc sửa lỗi chính tả tự động, giống hệt
    /// giá trị lấy từ logic tĩnh.
    /// </summary>
    public sealed class IssuingAgencyExtractor : IIssuingAgencyExtractor
    {
        private readonly IFuzzyTextCorrector _fuzzyCorrector;

        public IssuingAgencyExtractor(IFuzzyTextCorrector fuzzyCorrector)
        {
            _fuzzyCorrector = fuzzyCorrector;
        }

        public void Extract(string headerText, string fullText, ExtractedDocumentData result, IReadOnlyList<OcrPatternRule>? dynamicRules = null)
        {
            var refUpper = (result.ReferenceNumber ?? "").ToUpperInvariant();

            if (refUpper.Contains("/TTG") || refUpper.Contains("TTG-") || refUpper.Contains("/QĐ-TTG") || refUpper.Contains("/CT-TTG") || refUpper.Contains("/QD-TTG")) result.PartnerName = "Thủ tướng Chính phủ";
            else if (refUpper.Contains("/VPCP") || refUpper.Contains("VPCP-")) result.PartnerName = "Văn phòng Chính phủ";
            else if (refUpper.Contains("/CP") || refUpper.Contains("/NQ-CP") || refUpper.Contains("/NĐ-CP") || refUpper.Contains("/ND-CP")) result.PartnerName = "Chính phủ";
            else if (refUpper.Contains("/BTTTT") || refUpper.Contains("BTTTT-")) result.PartnerName = "Bộ Thông tin và Truyền thông";
            else if (refUpper.Contains("/BYT") || refUpper.Contains("BYT-") || refUpper.Contains("/QĐ-BYT")) result.PartnerName = "Bộ Y tế";
            else if (refUpper.Contains("/BGDĐT") || refUpper.Contains("/BGDDT") || refUpper.Contains("BGDĐT-")) result.PartnerName = "Bộ Giáo dục và Đào tạo";
            else if (refUpper.Contains("/BCA") || refUpper.Contains("BCA-")) result.PartnerName = "Bộ Công an";
            else if (refUpper.Contains("/BQP") || refUpper.Contains("BQP-")) result.PartnerName = "Bộ Quốc phòng";
            else if (refUpper.Contains("/BNG") || refUpper.Contains("BNG-")) result.PartnerName = "Bộ Ngoại giao";
            else if (refUpper.Contains("/BNV") || refUpper.Contains("BNV-")) result.PartnerName = "Bộ Nội vụ";
            else if (refUpper.Contains("/BTP") || refUpper.Contains("BTP-")) result.PartnerName = "Bộ Tư pháp";
            else if (refUpper.Contains("/BTC") || refUpper.Contains("BTC-")) result.PartnerName = "Bộ Tài chính";
            else if (refUpper.Contains("/BCT") || refUpper.Contains("BCT-")) result.PartnerName = "Bộ Công Thương";
            else if (refUpper.Contains("/BKHCN") || refUpper.Contains("BKHCN-")) result.PartnerName = "Bộ Khoa học và Công nghệ";
            else if (refUpper.Contains("/BGTVT") || refUpper.Contains("BGTVT-")) result.PartnerName = "Bộ Giao thông Vận tải";
            else if (refUpper.Contains("BNNMT") || headerText.Contains("Nông nghiệp và Môi trường", StringComparison.OrdinalIgnoreCase) || fullText.Contains("Nông nghiệp và Môi trường", StringComparison.OrdinalIgnoreCase)) result.PartnerName = "Bộ Nông nghiệp và Môi trường";
            else if (refUpper.Contains("/BNN") || refUpper.Contains("BNN-") || refUpper.Contains("-BNN") || refUpper.Contains("BNNPTNT")) result.PartnerName = "Bộ Nông nghiệp và Phát triển nông thôn";
            else if (refUpper.Contains("/BXD") || refUpper.Contains("BXD-")) result.PartnerName = "Bộ Xây dựng";
            else if (refUpper.Contains("/BLĐTBXH") || refUpper.Contains("/BLDTBXH")) result.PartnerName = "Bộ Lao động - Thương binh và Xã hội";
            else if (refUpper.Contains("/BVHTTDL") || refUpper.Contains("BVHTTDL-")) result.PartnerName = "Bộ Văn hóa, Thể thao và Du lịch";
            else if (refUpper.Contains("/BTNMT") || refUpper.Contains("BTNMT-")) result.PartnerName = "Bộ Tài nguyên và Môi trường";
            else if (refUpper.Contains("/TTCP") || refUpper.Contains("TTCP-")) result.PartnerName = "Thanh tra Chính phủ";
            else if (refUpper.Contains("/VPCP") || refUpper.Contains("VPCP-")) result.PartnerName = "Văn phòng Chính phủ";
            else if (refUpper.Contains("/KH-SYT") || refUpper.Contains("/QĐ-SYT") || refUpper.Contains("/SYT-") || refUpper.Contains("/SYT") || refUpper.Contains("-SYT"))
            {
                result.PartnerName = "Sở Y tế Thành phố Hồ Chí Minh";
            }
            else if (refUpper.Contains("/STP-") || refUpper.Contains("/STP") || refUpper.Contains("-STP"))
            {
                result.PartnerName = "Sở Tư pháp Thành phố Hồ Chí Minh";
            }
            else if (refUpper.Contains("/VNPT") || refUpper.Contains("-VNPT") || refUpper.Contains("VNPT-"))
            {
                result.PartnerName = "Tập đoàn Bưu chính Viễn thông Việt Nam";
            }
            else if (refUpper.Contains("/ĐHQGHN") || refUpper.Contains("/DHQGHN") || refUpper.Contains("-ĐHQGHN") || refUpper.Contains("-DHQGHN"))
            {
                result.PartnerName = "Đại học Quốc gia Hà Nội";
            }
            else if (refUpper.Contains("/HĐPH") || refUpper.Contains("/HDPH") || refUpper.Contains("-HĐPH"))
            {
                result.PartnerName = "Hội đồng phối hợp phổ biến giáo dục pháp luật TPHCM";
            }
            else if (refUpper.Contains("/VPUBND") || refUpper.Contains("/VP-TH") || refUpper.Contains("/VP-UBND"))
            {
                result.PartnerName = "Văn phòng Ủy ban nhân dân Thành phố Hồ Chí Minh";
            }
            else if (refUpper.Contains("/UBND-") || refUpper.Contains("/UBND") || refUpper.Contains("-UBND") || refUpper.EndsWith("UBND") || refUpper.Contains("UBNDBN"))
            {
                if (headerText.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase) || fullText.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase)) result.PartnerName = "Ủy ban nhân dân thành phố Hà Nội";
                else if (headerText.Contains("Bắc Ninh", StringComparison.OrdinalIgnoreCase) || refUpper.Contains("UBNDBN") || refUpper.Contains("UBND-BN")) result.PartnerName = "Ủy ban nhân dân tỉnh Bắc Ninh";
                else result.PartnerName = "Ủy ban nhân dân Thành phố Hồ Chí Minh";
            }

            if (string.IsNullOrWhiteSpace(result.PartnerName))
            {
                var normHeader = ExtractionTextUtils.RemoveDiacritics(headerText).ToUpperInvariant();
                if (normHeader.Contains("THU TUONG CHINH PHU") || normHeader.Contains("THU TUONG CHINHPHU") || normHeader.Contains("THU TUONG"))
                {
                    result.PartnerName = "Thủ tướng Chính phủ";
                }
                else if (normHeader.Contains("VAN PHONG CHINH PHU") || normHeader.Contains("VAN PHONG CHINHPHU") || normHeader.Contains("CONG THONG TIN DIEN TU CHINH PHU"))
                {
                    result.PartnerName = "Văn phòng Chính phủ";
                }
                else if (normHeader.Contains("DAI HOC CONG THUONG") || normHeader.Contains("TRUONG DAI HOC CONG THUONG"))
                {
                    result.PartnerName = "Trường Đại học Công thương TP.HCM";
                }
                else if (normHeader.Contains("DAI HOC QUOC GIA") || normHeader.Contains("DHQGHN"))
                {
                    result.PartnerName = "Đại học Quốc gia Hà Nội";
                }
                else if (normHeader.Contains("BUU CHINH VIEN THONG") || normHeader.Contains("VNPT"))
                {
                    result.PartnerName = "Tập đoàn Bưu chính Viễn thông Việt Nam";
                }
                else if (normHeader.Contains("SO Y TE") || normHeader.Contains("SOY TE") || normHeader.Contains("SOYTE") || normHeader.Contains("SỞ Y TẾ"))
                {
                    result.PartnerName = "Sở Y tế Thành phố Hồ Chí Minh";
                }
                else if (normHeader.Contains("SO TU PHAP") || normHeader.Contains("SOTP"))
                {
                    result.PartnerName = "Sở Tư pháp Thành phố Hồ Chí Minh";
                }
                else if (normHeader.Contains("SO TAI CHINH"))
                {
                    result.PartnerName = "Sở Tài chính Thành phố Hồ Chí Minh";
                }
                else if (normHeader.Contains("SO KHOA HOC VA CONG NGHE") || normHeader.Contains("SO KHCN"))
                {
                    result.PartnerName = "Sở Khoa học và Công nghệ Thành phố Hồ Chí Minh";
                }
                else if (normHeader.Contains("SO GIAO DUC VA DAO TAO") || normHeader.Contains("SO GDDT"))
                {
                    result.PartnerName = "Sở Giáo dục và Đào tạo Thành phố Hồ Chí Minh";
                }
                else if (normHeader.Contains("UY BAN NHAN DAN") || normHeader.Contains("UBND"))
                {
                    if (normHeader.Contains("HA NOI")) result.PartnerName = "Ủy ban nhân dân thành phố Hà Nội";
                    else if (normHeader.Contains("BAC NINH")) result.PartnerName = "Ủy ban nhân dân tỉnh Bắc Ninh";
                    else result.PartnerName = "Ủy ban nhân dân Thành phố Hồ Chí Minh";
                }
            }

            if (string.IsNullOrWhiteSpace(result.PartnerName))
            {
                var agencyMatch = Regex.Match(headerText,
                    @"(?:\b|^)((?:[ỦU]Y\s+)?BAN\s+NH[ÂA]N\s+D[ÂA]N[\p{L}\s.\-_/]{0,80}|ỦY BAN[\p{L}\s.\-_/]{2,80}|UBND[\p{L}\s.\-_/]{2,80}|SỞ\s+[\p{L}\s.\-_/]{2,80}|BỘ\s+[\p{L}\s.\-_/]{2,80}|CỤC\s+[\p{L}\s.\-_/]{2,80}|TỔNG CỤC\s+[\p{L}\s.\-_/]{2,80}|(?<![ỦUuYy\s])BAN\s+(?!NH[ÂA]N\s+D[ÂA]N)[\p{L}\s.\-_/]{2,80}|VĂN PHÒNG CHÍNH PHỦ|VĂN PHÒNG[\p{L}\s.\-_/]{2,80}|THANH TRA CHÍNH PHỦ|THỦ TƯỚNG CHÍNH PHỦ|CHÍNH PHỦ|NGÂN HÀNG[\p{L}\s.\-_/]{2,80}|TẬP ĐOÀN[\p{L}\s.\-_/]{2,80}|TỔNG CÔNG TY[\p{L}\s.\-_/]{2,80}|CÔNG TY[\p{L}\s.\-_/]{2,80}|BỆNH VIỆN[\p{L}\s.\-_/]{2,80}|TRƯỜNG ĐẠI HỌC[\p{L}\s.\-_/]{2,80}|ĐẠI HỌC QUỐC GIA[\p{L}\s.\-_/]{0,80}|TRƯỜNG[\p{L}\s.\-_/]{2,80}|VIỆN[\p{L}\s.\-_/]{2,80}|HỌC VIỆN[\p{L}\s.\-_/]{2,80})(?=(?:\r?\n\s*\r?\n|CONG HOA|CỘNG HÒA|Độc lập|DOC LAP|Số:|So:|Số\s*\/|___|===|\n\s*Số|\n\s*Kính gửi|$))",
                    RegexOptions.IgnoreCase);

                if (agencyMatch.Success)
                {
                    var pName = agencyMatch.Groups[1].Value.Replace("\r", " ").Replace("\n", " ").Trim();
                    pName = Regex.Replace(pName, @"\s+", " ");
                    pName = Regex.Replace(pName, @"(?:\s*-\s*|\s*Số:|\s*CONG HOA|\s*CỘNG HÒA|\s*ĐỘC LẬP).*$", "", RegexOptions.IgnoreCase).Trim();
                    pName = Regex.Replace(pName, @"(?<=[A-ZĐ]{2,})(?:BAN|PHÒNG|TRUNG TÂM)\s+.*$", "", RegexOptions.IgnoreCase).Trim();
                    pName = Regex.Replace(pName, @"\bBAN\s+TỔ\s+CHỨC.*$", "", RegexOptions.IgnoreCase).Trim();
                    pName = Regex.Replace(pName, @"^[_\\/.\-—\s,]+|[_\\/.\-—\s,=]+$", "").Trim();

                    if (Regex.IsMatch(pName, @"^(?:[ỦU]Y\s+)?BAN\s+NH[ÂA]N\s+D[ÂA]N", RegexOptions.IgnoreCase))
                    {
                        if (pName.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase)) pName = "Ủy ban nhân dân thành phố Hà Nội";
                        else if (pName.Contains("Bắc Ninh", StringComparison.OrdinalIgnoreCase)) pName = "Ủy ban nhân dân tỉnh Bắc Ninh";
                        else pName = "Ủy ban nhân dân Thành phố Hồ Chí Minh";
                    }

                    if (pName.Length >= 4)
                    {
                        result.PartnerName = pName;
                    }
                }
            }

            // ===== BƯỚC 6: FALLBACK RULE ĐỘNG (chỉ chạy khi toàn bộ logic tĩnh ở trên KHÔNG tìm ra gì) =====
            // Đặt TRƯỚC bước fuzzy-correct cuối cùng để giá trị lấy từ rule động cũng được sửa lỗi chính tả.
            if (string.IsNullOrWhiteSpace(result.PartnerName))
            {
                var dynamicVal = DynamicRuleFallbackApplier.TryExtract(fullText, OcrRuleTypes.IssuingAgency, dynamicRules);
                if (!string.IsNullOrWhiteSpace(dynamicVal))
                {
                    result.PartnerName = dynamicVal;
                }
            }

            if (!string.IsNullOrWhiteSpace(result.PartnerName))
            {
                result.PartnerName = _fuzzyCorrector.CorrectText(result.PartnerName);
            }
        }
    }
}
