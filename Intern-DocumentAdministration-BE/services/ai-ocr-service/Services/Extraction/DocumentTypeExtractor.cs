using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AiOcrService.Models;

namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Suy luận Loại văn bản (DocumentType). Toàn bộ logic Bước 5 giữ NGUYÊN VẸN. BƯỚC 6 thêm
    /// 1 khối fallback DUY NHẤT ở cuối: nếu SAU CÙNG (sau cả nhánh dựa vào ReferenceNumber lẫn
    /// nhánh dò nhãn in hoa/V-v trong header) mà DocumentType vẫn rỗng, mới thử rule động từ DB.
    /// Ghi chú: dynamic rule cho DocumentType so khớp trên headerText (không có fullText trong
    /// chữ ký hàm gốc) - đủ dùng vì tên loại văn bản luôn nằm ở phần đầu công văn.
    /// </summary>
    public sealed class DocumentTypeExtractor : IDocumentTypeExtractor
    {
        public void Extract(string headerText, ExtractedDocumentData result, IReadOnlyList<OcrPatternRule>? dynamicRules = null)
        {
            if (!string.IsNullOrWhiteSpace(result.ReferenceNumber))
            {
                var refUpper = result.ReferenceNumber.ToUpperInvariant();
                if (refUpper.Contains("/QĐ-") || refUpper.Contains("-QĐ/") || refUpper.Contains("/QĐ") || refUpper.EndsWith("-QĐ")) result.DocumentType = "Quyết Định";
                else if (refUpper.Contains("/TB-") || refUpper.Contains("-TB/") || refUpper.Contains("/TB") || refUpper.EndsWith("-TB")) result.DocumentType = "Thông Báo";
                else if (refUpper.Contains("/CT-") || refUpper.Contains("-CT/") || refUpper.Contains("/CT") || refUpper.EndsWith("-CT")) result.DocumentType = "Chỉ Thị";
                else if (refUpper.Contains("/KH-") || refUpper.Contains("-KH/") || refUpper.Contains("/KH") || refUpper.EndsWith("-KH")) result.DocumentType = "Kế Hoạch";
                else if (refUpper.Contains("/BC-") || refUpper.Contains("-BC/") || refUpper.Contains("/BC") || refUpper.EndsWith("-BC")) result.DocumentType = "Báo Cáo";
                else if (refUpper.Contains("/NQ-") || refUpper.Contains("-NQ/") || refUpper.Contains("/NQ") || refUpper.EndsWith("-NQ")) result.DocumentType = "Nghị Quyết";
                else if (refUpper.Contains("/HĐ-") || refUpper.Contains("-HĐ/") || refUpper.Contains("/HĐ") || refUpper.EndsWith("-HĐ")) result.DocumentType = "Hợp Đồng";
                else if (refUpper.Contains("/GM-") || refUpper.Contains("-GM/") || refUpper.Contains("/GM") || refUpper.EndsWith("-GM")) result.DocumentType = "Giấy Mời";
                else if (refUpper.Contains("/TTR-") || refUpper.Contains("-TTR/") || refUpper.Contains("/TTR") || refUpper.EndsWith("-TTR")) result.DocumentType = "Tờ Trình";
                else if (refUpper.Contains("/BB-") || refUpper.Contains("-BB/") || refUpper.Contains("/BB") || refUpper.EndsWith("-BB")) result.DocumentType = "Biên Bản";
                else if (refUpper.Contains("/QC-") || refUpper.Contains("-QC/") || refUpper.Contains("/QC") || refUpper.EndsWith("-QC")) result.DocumentType = "Quy Chế";
                else if (refUpper.Contains("/HD-") || refUpper.Contains("-HD/") || refUpper.Contains("/HD") || refUpper.EndsWith("-HD")) result.DocumentType = "Hướng Dẫn";
                else if (refUpper.Contains("/CĐ-") || refUpper.Contains("-CĐ/") || refUpper.Contains("/CĐ") || refUpper.EndsWith("-CĐ")) result.DocumentType = "Công Điện";
                else if (refUpper.Contains("VBHN") || refUpper.Contains("/VBHN-")) result.DocumentType = "Văn Bản Hợp Nhất";
                else if (refUpper.Contains("/TT-") || refUpper.Contains("-TT/") || refUpper.Contains("/TT") || refUpper.EndsWith("-TT")) result.DocumentType = "Thông Tư";
                else
                {
                    var standaloneTypeMatch = Regex.Match(headerText,
                        @"(?<!V[\/\.]\s*v[^\n]{0,50}|Về\s*việc[^\n]{0,50}|Căn\s*cứ[^\n]{0,30})(?:\n|\r|^)\s*(VĂN\s*BẢN\s*HỢP\s*NHẤT|THÔNG\s*TƯ|QUYẾT\s*ĐỊNH|QUYÉT\s*ĐỊNH|THÔNG\s*BÁO|KẾ\s*HOẠCH|TỜ\s*TRÌNH|GIẤY\s*MỜI|BIÊN\s*BẢN|CHỈ\s*THỊ|NGHỊ\s*QUYẾT|QUY\s*CHẾ|QUY\s*ĐỊNH|HƯỚNG\s*DẪN)\s*(?:\n|\r|$)",
                        RegexOptions.IgnoreCase);

                    if (standaloneTypeMatch.Success)
                    {
                        var st = ExtractionTextUtils.RemoveDiacritics(standaloneTypeMatch.Groups[1].Value).ToUpperInvariant().Replace(" ", "");
                        if (st.Contains("VANBANHOPNHAT")) result.DocumentType = "Văn Bản Hợp Nhất";
                        else if (st.Contains("THONGTU")) result.DocumentType = "Thông Tư";
                        else if (st.Contains("QUYETDINH")) result.DocumentType = "Quyết Định";
                        else if (st.Contains("THONGBAO")) result.DocumentType = "Thông Báo";
                        else if (st.Contains("KEHOACH")) result.DocumentType = "Kế Hoạch";
                        else if (st.Contains("TOTRINH")) result.DocumentType = "Tờ Trình";
                        else if (st.Contains("GIAYMOI")) result.DocumentType = "Giấy Mời";
                        else if (st.Contains("BIENBAN")) result.DocumentType = "Biên Bản";
                        else if (st.Contains("CHITHI")) result.DocumentType = "Chỉ Thị";
                        else if (st.Contains("NGHIQUYET")) result.DocumentType = "Nghị Quyết";
                        else if (st.Contains("QUYCHE") || st.Contains("QUYDINH")) result.DocumentType = "Quy Chế";
                        else if (st.Contains("HUONGDAN")) result.DocumentType = "Hướng Dẫn";
                        else result.DocumentType = "Công Văn";
                    }
                    else
                    {
                        result.DocumentType = "Công Văn";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(result.DocumentType))
            {
                var labelMatch = Regex.Match(headerText, @"(?<!Căn\s*cứ[^\n]{0,30})(?:\b|\n)(QUYẾT ĐỊNH|QUY[ẾÉE]T[\s\S]{0,2}[ĐD][\s\S]{0,3}NH|QUYT[\s\S]{0,2}NH|THÔNG BÁO|TỜ TRÌNH|GIẤY MỜI|CHỈ THỊ|KẾ HOẠCH|BÁO CÁO|NGHỊ QUYẾT|HỢP ĐỒNG|BIÊN BẢN|QUY CHẾ|QUY ĐỊNH|HƯỚNG DẪN|TÀI LIỆU HƯỚNG DẪN|THƯ KÊU GỌI)(?!\s*số\b)", RegexOptions.IgnoreCase);
                if (labelMatch.Success)
                {
                    var t = labelMatch.Groups[1].Value.ToLowerInvariant();
                    if (t.Contains("quy") && (t.Contains("ết") || t.Contains("ét") || t.Contains("et") || t.Contains("yt"))) result.DocumentType = "Quyết Định";
                    else if (t.Contains("hướng dẫn")) result.DocumentType = "Hướng Dẫn";
                    else if (t.Contains("quy chế")) result.DocumentType = "Quy Chế";
                    else result.DocumentType = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(t);
                }
                else if ((result.Subject != null && (result.Subject.StartsWith("V/v", System.StringComparison.OrdinalIgnoreCase) ||
                                                result.Subject.StartsWith("Về việc", System.StringComparison.OrdinalIgnoreCase) ||
                                                result.Subject.StartsWith("Về ", System.StringComparison.OrdinalIgnoreCase))) ||
                    Regex.IsMatch(headerText, @"\b(?:V/v|Về việc|Về)\b", RegexOptions.IgnoreCase) ||
                    !string.IsNullOrWhiteSpace(result.ReferenceNumber))
                {
                    result.DocumentType = "Công Văn";
                }
            }

            // ===== BƯỚC 6: FALLBACK RULE ĐỘNG (chỉ chạy khi mọi nhánh tĩnh ở trên đều để trống) =====
            if (string.IsNullOrWhiteSpace(result.DocumentType))
            {
                var dynamicVal = DynamicRuleFallbackApplier.TryExtract(headerText, OcrRuleTypes.DocumentType, dynamicRules);
                if (!string.IsNullOrWhiteSpace(dynamicVal))
                {
                    result.DocumentType = dynamicVal;
                }
            }
        }
    }
}
