using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AiOcrService.Models;
using AiOcrService.Services.Glyphs;
using AiOcrService.Services.FuzzyCorrection;
using AiOcrService.Services.Extraction;

namespace AiOcrService.Services
{
    /// <summary>
    /// BƯỚC 6: Orchestrator giờ THỰC SỰ dùng IOcrRuleService (trước đó Bước 5 chỉ giữ tham số
    /// constructor để không phá DI, discard bằng "_ = ruleService;" - xem WIRING_NOTES_BUOC5.md
    /// mục 4 "Việc CHƯA làm"). Giờ gọi GetRulesAsync(isActive: true) ĐÚNG 1 LẦN cho mỗi văn bản
    /// (không phải 4 lần, mỗi Extractor 1 lần) rồi truyền danh sách xuống cho 4 Extractor cần
    /// rule động (ReferenceNumber, IssuingAgency, Subject, DocumentType) tự lọc theo RuleType.
    ///
    /// Nếu _ruleService là null (constructor 6-Extractor không được cấp IOcrRuleService) hoặc
    /// việc gọi DB lỗi/timeout, dynamicRules sẽ là null -> mọi Extractor tự động BỎ QUA fallback
    /// rule động (DynamicRuleFallbackApplier trả về null khi rules null) và hành vi giống hệt
    /// Bước 5 - đây là thiết kế "fail-safe by default", không có rule DB thì hệ thống chạy y hệt
    /// trước khi có Bước 6.
    /// </summary>
    public class DynamicFieldExtractor : IDynamicFieldExtractor
    {
        private readonly IReferenceNumberExtractor _referenceNumberExtractor;
        private readonly IIssuingAgencyExtractor _issuingAgencyExtractor;
        private readonly ISubjectExtractor _subjectExtractor;
        private readonly IDocumentDateExtractor _documentDateExtractor;
        private readonly ISignerExtractor _signerExtractor;
        private readonly IDocumentTypeExtractor _documentTypeExtractor;
        private readonly IOcrRuleService? _ruleService;

        /// <summary>
        /// Constructor tương thích ngược với DI hiện có (IOcrRuleService, IGlyphNormalizer?,
        /// IFuzzyTextCorrector?) - tự dựng 6 Extractor bên trong. BƯỚC 6: _ruleService giờ được
        /// GIỮ LẠI (không còn discard) để dùng làm nguồn rule động.
        /// </summary>
        public DynamicFieldExtractor(
            IOcrRuleService ruleService,
            IGlyphNormalizer? glyphNormalizer = null,
            IFuzzyTextCorrector? fuzzyCorrector = null)
        {
            _ruleService = ruleService;

            var glyph = glyphNormalizer ?? new HandwrittenGlyphNormalizer();
            var fuzzy = fuzzyCorrector ?? new TrieBkTreeFuzzyCorrector(VocabularySeed.FromLegacyAutoCorrectTable());

            _referenceNumberExtractor = new ReferenceNumberExtractor(glyph);
            _issuingAgencyExtractor = new IssuingAgencyExtractor(fuzzy);
            _subjectExtractor = new SubjectExtractor(fuzzy);
            _documentDateExtractor = new DocumentDateExtractor(glyph);
            _signerExtractor = new SignerExtractor();
            _documentTypeExtractor = new DocumentTypeExtractor();
        }

        /// <summary>
        /// Constructor khuyến nghị DÀI HẠN (xem WIRING_NOTES_BUOC5.md mục 2) - cho phép DI
        /// Container inject thẳng 6 Extractor, dễ mock từng cái khi unit test riêng lẻ.
        /// BƯỚC 6: thêm tham số "ruleService" (optional, mặc định null) - nếu không truyền,
        /// hệ thống chạy y hệt Bước 5 (không có fallback rule động), KHÔNG phá lời gọi cũ nào
        /// đang dùng constructor 6 tham số.
        /// </summary>
        public DynamicFieldExtractor(
            IReferenceNumberExtractor referenceNumberExtractor,
            IIssuingAgencyExtractor issuingAgencyExtractor,
            ISubjectExtractor subjectExtractor,
            IDocumentDateExtractor documentDateExtractor,
            ISignerExtractor signerExtractor,
            IDocumentTypeExtractor documentTypeExtractor,
            IOcrRuleService? ruleService = null)
        {
            _referenceNumberExtractor = referenceNumberExtractor;
            _issuingAgencyExtractor = issuingAgencyExtractor;
            _subjectExtractor = subjectExtractor;
            _documentDateExtractor = documentDateExtractor;
            _signerExtractor = signerExtractor;
            _documentTypeExtractor = documentTypeExtractor;
            _ruleService = ruleService;
        }

        private static string PreprocessDocumentLayout(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var normalized = Regex.Replace(text, @"(?<=[\p{L}\d\-_])(?=CỘNG HÒA|Độc lập|QUYẾT ĐỊNH|QUYÉT|THÔNG BÁO|GIẤY MỜI|TỜ TRÌNH|CHỈ THỊ|KẾ HOẠCH|NGHỊ QUYẾT|BÁO CÁO|HỢP ĐỒNG|BIÊN BẢN|QUY CHẾ|QUY ĐỊNH|CÔNG VĂN|HƯỚNG DẪN|HUÓNG DN|HU'ÓNG DN|HUONG DAN|TÀI LIỆU HƯỚNG DẪN|V[\/\.]\s*v|Về việc|Ve viec|V\s+vic|Vv|Trích yếu|TRÍCH YẾU|Về\s+[A-ZÀ-Ỹa-z]|CHỦ TỊCH|CH TCH|PHÓ CHỦ TỊCH|Kính gửi|K[ií]nh|Căn cứ|Can cu|Điều \d|Nơi nhận|Nhằm|TM\.|KT\.|TRƯỞNG BAN|BỘ TRƯỞNG|THỨ TRƯỞNG|TỔNG GIÁM ĐỐC|GIÁM ĐỐC|PHÓ GIÁM ĐỐC|HIỆU TRƯỞNG|PHÓ HIỆU TRƯỞNG|CHÁNH VĂN PHÒNG|PHÓ CHÁNH VĂN PHÒNG|THỦ TƯỚNG|PHÓ THỦ TƯỚNG|CỔNG THÔNG TIN|Lưu:|Lưu\s+VT)", "\n", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"(?<=[\p{L}\d\-_])(?=CỘNG|CONG)", "\n", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"(?<=GIẤY MỜI|TỜ TRÌNH|QUYẾT ĐỊNH|QUYÉT ĐINH|THÔNG BÁO|KẾ HOẠCH|CÔNG VĂN|HƯỚNG DẪN|HUÓNG DN|BIÊN BẢN|CHỈ THỊ)(?=[\p{L}\d])", "\n", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"(?<=[\p{L}\d])(?=Số:|So:|Số\s*\/\s*No|Số\s+)", "\n", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"(?<=[\p{L}\d])(?=Hà Nội|TP\.|TP\s+Hồ Chí Minh|Đà Nẵng|Bắc Ninh|Hải Phòng|Cần Thơ|ngày\s+\d)", "\n", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"(?<=HIỆU TRƯỞNG|BỘ TRƯỞNG|THỦ TƯỚNG|GIÁM ĐỐC|CHỦ TỊCH|CHÁNH VĂN PHÒNG)(?=[A-ZÀ-Ỹ][a-zà-ỹ])", "\n", RegexOptions.IgnoreCase);

            return normalized;
        }

        public async Task<ExtractedDocumentData> ExtractFieldsAsync(string extractedText, string? fileName = null, byte[]? pdfBytes = null)
        {
            var result = new ExtractedDocumentData();
            if (string.IsNullOrWhiteSpace(extractedText)) return result;

            var normalizedText = PreprocessDocumentLayout(extractedText);

            // Trích xuất text của Trang 1 (Page 1) - Chỉ quét số ký hiệu, cơ quan, ngày ban hành trên trang đầu
            string page1Text = normalizedText;
            var page1Match = Regex.Match(normalizedText, @"\[PAGE_1\]([\s\S]*?)\[/PAGE_1\]");
            if (page1Match.Success)
            {
                page1Text = page1Match.Groups[1].Value.Trim();
            }
            else
            {
                var page2Idx = normalizedText.IndexOf("[PAGE_2]", StringComparison.OrdinalIgnoreCase);
                if (page2Idx > 0)
                {
                    page1Text = normalizedText.Substring(0, page2Idx).Trim();
                }
            }

            int canCuIdx = -1;
            var canCuMatch = Regex.Match(page1Text, @"\b(?:Căn cứ|Can cu)\b", RegexOptions.IgnoreCase);
            if (canCuMatch.Success && canCuMatch.Index > 60) canCuIdx = canCuMatch.Index;

            int kínhGửiIdx = -1;
            var kgMatch = Regex.Match(page1Text, @"\bKính\s*gửi\b", RegexOptions.IgnoreCase);
            if (kgMatch.Success && kgMatch.Index > 60 && !page1Text.Substring(Math.Max(0, kgMatch.Index - 15), Math.Min(15, kgMatch.Index)).Contains("Như"))
            {
                kínhGửiIdx = kgMatch.Index;
            }

            int thucHienIdx = -1;
            var thMatch = Regex.Match(page1Text, @"\bThực\s*hiện\s*(?:Công|Quyết|Kế|Nghị|Chỉ)", RegexOptions.IgnoreCase);
            if (thMatch.Success && thMatch.Index > 60) thucHienIdx = thMatch.Index;

            int docTypeIdx = -1;
            var dtMatches = Regex.Matches(page1Text, @"(?m)^\s*(?:THÔNG\s*TƯ|QUYẾT\s*ĐỊNH|NGHỊ\s*QUYẾT|CHỈ\s*THỊ|KẾ\s*HOẠCH|THÔNG\s*BÁO|TỜ\s*TRÌNH|GIẤY\s*MỜI|BIÊN\s*BẢN|HƯỚNG\s*DẪN|CÔNG\s*VĂN|V\/v|Về\s+việc)\b", RegexOptions.IgnoreCase);
            foreach (Match dtm in dtMatches)
            {
                if (dtm.Index > 60)
                {
                    docTypeIdx = dtm.Index;
                    break;
                }
            }

            int headerCutoff = 1500;
            if (docTypeIdx > 0 && docTypeIdx < headerCutoff) headerCutoff = docTypeIdx;
            if (canCuIdx > 0 && canCuIdx < headerCutoff) headerCutoff = canCuIdx;
            if (kínhGửiIdx > 0 && kínhGửiIdx < headerCutoff) headerCutoff = kínhGửiIdx;
            if (thucHienIdx > 0 && thucHienIdx < headerCutoff) headerCutoff = thucHienIdx;
            headerCutoff = Math.Min(page1Text.Length, Math.Max(100, headerCutoff));
            string headerText = page1Text.Substring(0, headerCutoff);

            // ===== BƯỚC 6: Nạp rule động MỘT LẦN DUY NHẤT cho cả văn bản (không phải 1 lần/field) =====
            // Chủ động bọc try/catch: DB lỗi/timeout không được phép làm hỏng pipeline OCR chính,
            // chỉ đơn giản là các Extractor sẽ chạy như Bước 5 (không có fallback rule động).
            IReadOnlyList<OcrPatternRule>? dynamicRules = null;
            if (_ruleService != null)
            {
                try
                {
                    dynamicRules = await _ruleService.GetRulesAsync(isActive: true);
                }
                catch
                {
                    dynamicRules = null;
                }
            }

            // 1. BÓC TÁCH SỐ KÝ HIỆU VĂN BẢN (REFERENCE NUMBER) - CHỈ QUÉT TRANG ĐẦU (Page 1)
            _referenceNumberExtractor.Extract(headerText, page1Text, fileName, pdfBytes, result, dynamicRules);

            // 2. BÓC TÁCH CƠ QUAN BAN HÀNH (ISSUING AGENCY / PARTNER) - CHỈ QUÉT TRANG ĐẦU (Page 1)
            _issuingAgencyExtractor.Extract(headerText, page1Text, result, dynamicRules);

            // 3. BÓC TÁCH TIÊU ĐỀ / TRÍCH YẾU (SUBJECT)
            _subjectExtractor.Extract(headerText, normalizedText, result, dynamicRules);

            // 4. BÓC TÁCH NGÀY BAN HÀNH (DOCUMENT DATE) - CHỈ QUÉT TRANG ĐẦU (Page 1)
            _documentDateExtractor.Extract(headerText, page1Text, fileName, canCuIdx, pdfBytes, result);

            // 5. BÓC TÁCH NGƯỜI KÝ & CHỨC DANH (SIGNER) - Quét toàn văn (Người ký nằm ở cuối văn bản)
            _signerExtractor.Extract(normalizedText, result);

            // 6. SUY LUẬN LOẠI VĂN BẢN (DOCUMENT TYPE) - phụ thuộc ReferenceNumber + Subject đã trích ở trên
            _documentTypeExtractor.Extract(headerText, result, dynamicRules);

            return result;
        }
    }
}
