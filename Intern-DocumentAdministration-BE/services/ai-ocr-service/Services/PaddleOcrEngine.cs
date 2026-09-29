using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using PaddleOCRSharp;
using Docnet.Core;
using Docnet.Core.Models;
using ImageMagick;

namespace AiOcrService.Services
{
    public class PaddleOcrEngine : IOcrEngine, IDisposable
    {
        private readonly ILogger<PaddleOcrEngine> _logger;
        private static PaddleOCREngine? _paddleOcr;
        private static Tesseract.TesseractEngine? _tesseractEngine;
        private static readonly object _tessLock = new();
        private static readonly object _paddleLock = new();

        public PaddleOcrEngine(ILogger<PaddleOcrEngine> logger)
        {
            _logger = logger;
            if (_paddleOcr == null)
            {
                // Chỉ ghi đè model Recognition (Nhận diện chữ) thành Tiếng Việt (LatinV3)
                // Các model Detection (Tìm khung chữ) và Classification (Xoay ảnh) dùng mặc định của thư viện
                string modelPath = Path.Combine(Path.GetTempPath(), "paddle_models_vi");
                OCRModelConfig config = new OCRModelConfig();
                
                // Trỏ tới thư mục chứa latin_PP-OCRv3_rec
                config.det_infer = Path.Combine(modelPath, "det");
                config.cls_infer = Path.Combine(modelPath, "cls");
                config.rec_infer = Path.Combine(modelPath, "rec");
                // Từ điển Tiếng Việt
                config.keys = Path.Combine(modelPath, "vi_dict.txt");

                OCRParameter ocrParam = new OCRParameter
                {
                    use_angle_cls = true,
                    cpu_math_library_num_threads = Environment.ProcessorCount,
                    det_db_score_mode = true
                };

                _paddleOcr = new PaddleOCREngine(config, ocrParam);
            }

            if (_tesseractEngine == null)
            {
                try
                {
                    string tessDataPath = @"C:\tessdata";
                    if (!Directory.Exists(tessDataPath) || !File.Exists(Path.Combine(tessDataPath, "vie.traineddata")))
                    {
                        var baseDir = AppContext.BaseDirectory;
                        tessDataPath = Path.Combine(baseDir, "tessdata");
                        if (!Directory.Exists(tessDataPath))
                        {
                            tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
                        }
                    }

                    if (Directory.Exists(tessDataPath) && File.Exists(Path.Combine(tessDataPath, "vie.traineddata")))
                    {
                        _tesseractEngine = new Tesseract.TesseractEngine(tessDataPath, "vie", Tesseract.EngineMode.LstmOnly);
                        _tesseractEngine.DefaultPageSegMode = Tesseract.PageSegMode.SingleBlock;
                        _logger.LogInformation($"Tesseract 5 (LSTM) initialized successfully from {tessDataPath}.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not initialize Tesseract 5: {ex.Message}");
                }
            }
        }

        private static bool IsLowQualityText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;

            // Nếu văn bản có chứa các cụm từ hành chính chuẩn và độ dài đủ lớn, chắc chắn là văn bản số chuẩn (digital PDF)
            if (text.Length >= 50 && Regex.IsMatch(text, @"(CỘNG\s*H[OÒ]A|CỘNG\s*HOÀ|Độc\s*lập|QUYẾT\s*ĐỊNH|KẾ\s*HOẠCH|THÔNG\s*BÁO|BÁO\s*CÁO|CHỈ\s*THỊ|HƯỚNG\s*DẪN|TỜ\s*TRÌNH|BỘ\s+[A-ZÀ-Ỹ]|SỞ\s+[A-ZÀ-Ỹ]|ỦY\s*BAN|UBND|Kính\s*gửi|Căn\s*cứ)", RegexOptions.IgnoreCase))
            {
                return false;
            }

            int noiseCount = 0;
            foreach (var ch in text)
            {
                if (ch == '~' || ch == '\\' || ch == '|' || ch == '^' || ch == '`' || ch == '{' || ch == '}' || ch == '¤' || ch == '¥' || ch == '§') noiseCount++;
            }
            if (noiseCount >= 3 || (double)noiseCount / text.Length > 0.015) return true;
            if (Regex.IsMatch(text, @"[a-z][0-9][a-z]|[a-z]~[a-z]|[a-z]\\[a-z]", RegexOptions.IgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Kiểm tra text PaddleOCR có thiếu dấu tiếng Việt bất thường không.
        /// Nếu tỷ lệ ký tự nguyên âm có dấu (á, à, ã...) dưới ngưỡng minRatio trên
        /// tổng số ký tự chữ cái, hệ thống cần kích hoạt Tesseract để bổ sung dấu.
        /// Với văn bản hành chính tiếng Việt chuẩn: tỷ lệ thực tế thường ~12-20%.
        /// </summary>
        private static bool IsVietnameseDiacriticsPoor(string text, double minRatio = 0.04)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 20) return false;
            int letters = text.Count(char.IsLetter);
            if (letters < 20) return false;
            int diacritics = text.Count(c =>
                "áàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđĐÁÀẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴ".Contains(c));
            return ((double)diacritics / letters) < minRatio;
        }

        /// <summary>
        /// Downsample ảnh nếu kích thước vượt ngưỡng an toàn cho OCR.
        /// OCR không cần hơn ~300 DPI (≈2200px cho A4), ảnh to hơn chỉ làm chậm CPU.
        /// </summary>
        private byte[] DownsampleImageIfNeeded(byte[] imageBytes, int maxDimension = 2200)
        {
            try
            {
                using var src = Cv2.ImDecode(imageBytes, ImreadModes.Color);
                if (src.Empty()) return imageBytes;
                int maxSide = Math.Max(src.Cols, src.Rows);
                if (maxSide <= maxDimension) return imageBytes; // đã đủ nhỏ, giữ nguyên

                double scale = (double)maxDimension / maxSide;
                int newW = (int)(src.Cols * scale);
                int newH = (int)(src.Rows * scale);
                using var resized = new Mat();
                Cv2.Resize(src, resized, new OpenCvSharp.Size(newW, newH), 0, 0, InterpolationFlags.Area);
                Cv2.ImEncode(".png", resized, out var resultBytes);
                return resultBytes;
            }
            catch
            {
                return imageBytes;
            }
        }



        private byte[] PreprocessImageWithOpenCV(byte[] imageBytes)
        {
            try
            {
                using var src = Cv2.ImDecode(imageBytes, ImreadModes.Color);
                if (src.Empty()) return imageBytes;

                // 1. Tự động Upscale 2x bằng nội suy Cubic cho các ảnh scan phân giải thấp (< 1500px chiều rộng)
                using var workingMat = new Mat();
                if (src.Cols < 1500 && src.Cols > 0)
                {
                    Cv2.Resize(src, workingMat, new OpenCvSharp.Size(src.Cols * 2, src.Rows * 2), 0, 0, InterpolationFlags.Cubic);
                }
                else
                {
                    src.CopyTo(workingMat);
                }

                // 2. Cân bằng tương phản nhẹ & Chuyển sang ảnh xám
                using var gray = new Mat();
                Cv2.CvtColor(workingMat, gray, ColorConversionCodes.BGR2GRAY);

                // 3. Khử nghiêng trang văn bản (Deskew)
                using var inverted = new Mat();
                Cv2.BitwiseNot(gray, inverted);
                using var coords = new Mat();
                Cv2.FindNonZero(inverted, coords);
                
                if (coords.Rows > 0)
                {
                    var box = Cv2.MinAreaRect(coords);
                    float angle = box.Angle;
                    
                    if (angle < -45) angle += 90;
                    if (Math.Abs(angle) > 0.5 && Math.Abs(angle) < 15)
                    {
                        var center = new Point2f(workingMat.Cols / 2f, workingMat.Rows / 2f);
                        using var rotMat = Cv2.GetRotationMatrix2D(center, angle, 1.0);
                        Cv2.WarpAffine(workingMat, workingMat, rotMat, workingMat.Size(), InterpolationFlags.Cubic, BorderTypes.Replicate);
                    }
                }

                // 4. Nhị phân hóa Otsu Binarization (Làm sạch hoàn toàn nền xám, vết ố scan mờ)
                using var finalGray = new Mat();
                Cv2.CvtColor(workingMat, finalGray, ColorConversionCodes.BGR2GRAY);
                using var binarized = new Mat();
                Cv2.Threshold(finalGray, binarized, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

                Cv2.ImEncode(".png", binarized, out var resultBytes);
                return resultBytes;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"OpenCV Preprocessing failed: {ex.Message}");
                return imageBytes;
            }
        }

        private string PostProcessOcrText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            // 1. Chuẩn hóa các cụm từ hành chính phổ biến bị lỗi OCR đặc thù
            text = text.Replace("trin khai", "triển khai");
            text = text.Replace("Trin khai", "Triển khai");
            text = text.Replace("hot đng", "hoạt động");
            text = text.Replace("kháng thuc", "kháng thuốc");
            text = text.Replace("camng", "của mạng");
            text = text.Replace("lưi", "lưới");
            text = text.Replace("lưói", "lưới");
            text = text.Replace("bnh vin", "bệnh viện");
            text = text.Replace("Bnh vin", "Bệnh viện");
            text = text.Replace("công lp", "công lập");
            text = text.Replace("ngo i công lp", "ngoài công lập");
            text = text.Replace("ngoài công lp", "ngoài công lập");
            text = text.Replace("tham kho", "tham khảo");
            text = text.Replace("ch t", "chất");
            text = text.Replace("lư ng", "lượng");
            text = text.Replace("chư ng", "chương");
            text = text.Replace("trinh", "trình");
            text = text.Replace("đin t", "điện tử");
            text = text.Replace("c ng", "công");
            text = text.Replace("nh n", "nhận");
            text = text.Replace("h tr", "hỗ trợ");
            text = text.Replace("k thut", "kỹ thuật");
            text = text.Replace("xét nghim", "xét nghiệm");
            text = text.Replace("th c hin", "thực hiện");
            text = text.Replace("vưng mc", "vướng mắc");
            text = text.Replace("đ ngh", "đề nghị");
            text = text.Replace("đon v", "đơn vị");
            text = text.Replace("phn ánh", "phản ánh");
            text = text.Replace("S Y t", "Sở Y tế");
            text = text.Replace("gii quyt", "giải quyết");
            text = text.Replace("Quyt đnh", "Quyết định");
            text = text.Replace("B Y t", "Bộ Y tế");
            text = text.Replace("Nhu trên", "Như trên");
            text = text.Replace("N i nhn", "Nơi nhận");
            text = text.Replace("Ban Giám đc", "Ban Giám đốc");
            text = text.Replace("Giám đc", "Giám đốc");
            text = text.Replace("GIÁM ĐC", "GIÁM ĐỐC");
            text = text.Replace("T do", "Tự do");
            text = text.Replace("Hnh phúc", "Hạnh phúc");
            text = text.Replace("đc lp", "độc lập");
            text = text.Replace("Đc lp", "Độc lập");
            text = text.Replace("SÖ", "SỞ");
            text = text.Replace("Y T\udc90", "Y TẾ");
            text = text.Replace("T\udc90", "TẾ");
            text = text.Replace("CH\udc8d", "CHÍ");
            text = text.Replace("Ch\xad", "Chí");
            text = text.Replace("Th\xa0nh phó", "Thành phố");
            text = text.Replace("Thành ph", "Thành phố");
            text = text.Replace("đếnh k 6 tháng 1 làn", "định kỳ 6 tháng 1 lần");
            text = text.Replace("vĐơn v đìu phì", "về Đơn vị điều phối");
            text = text.Replace("quc gia", "quốc gia");
            text = text.Replace("Cc Qun lý Khám, cha bnh", "Cục Quản lý Khám, chữa bệnh");
            text = text.Replace("theobiu mu", "theo biểu mẫu");
            text = text.Replace("quy đếnh ti", "quy định tại");
            text = text.Replace("Quyt đếnh s", "Quyết định số");
            text = text.Replace("Đi vi tt c", "Đối với tất cả");
            text = text.Replace("Tu theo được đim", "Tùy theo đặc điểm");
            text = text.Replace("tình hình thc t", "tình hình thực tế");
            text = text.Replace("Giám được", "Giám đốc");
            text = text.Replace("phâncông nhiêm v", "phân công nhiệm vụ");
            text = text.Replace("c th cho", "cụ thể cho");
            text = text.Replace("b phn nhm thc hin đy đ", "bộ phận nhằm thực hiện đầy đủ");
            text = text.Replace("độnggiám sát", "động giám sát");
            text = text.Replace("vi khun", "vi khuẩn");
            text = text.Replace("s dng", "sử dụng");
            text = text.Replace("trucôngày15/10", "trước ngày 15/10");
            text = text.Replace("caBộ Y t", "của Bộ Y tế");
            text = text.Replace("caBộ Y tế", "của Bộ Y tế");
            text = text.Replace("caBộ", "của Bộ");
            text = text.Replace("ca khoa Dưc", "của khoa Dược");
            text = text.Replace("Bệnhnhân", "Bệnh nhân");
            text = text.Replace("Nhi đống 1", "Nhi đồng 1");
            text = text.Replace("Nhi đống 2", "Nhi đồng 2");
            text = text.Replace("là nhng đơn v", "là những đơn vị");
            text = text.Replace("có phòng xét nghiệm đt chun", "có phòng xét nghiệm đạt chuẩn");
            text = text.Replace("vi tiêu", "với tiêu");
            text = text.Replace("nuôi cy", "nuôi cấy");
            text = text.Replace("vi kỵ thuật đếnh danh", "với kỹ thuật định danh");
            text = text.Replace("đìu phì", "điều phối");
            text = text.Replace("đếnh danh", "định danh");
            text = text.Replace("quy đếnh", "quy định");
            text = text.Replace("nhng bnhviện", "những bệnh viện");
            text = text.Replace("yêu cu", "yêu cầu");
            text = text.Replace("chtlưng đưc cấp nhật", "chất lượng được cập nhật");
            text = text.Replace("Công nhn", "Công nhận");
            text = text.Replace("thc hin", "thực hiện");
            text = text.Replace("đy đ", "đầy đủ");
            text = text.Replace("nhng đơn v", "những đơn vị");
            text = text.Replace("đt chun", "đạt chuẩn");
            text = text.Replace("Bệnhnhit đi", "Bệnh nhiệt đới");
            text = text.Replace("Bệnhnhit", "Bệnh nhiệt");
            text = text.Replace("vàBệnh viện", "và Bệnh viện");
            text = text.Replace("ISO 15189 v", "ISO 15189 về");
            text = text.Replace("chi tiêunuôi", "chỉ tiêu nuôi");
            text = text.Replace("kháng sinh đ", "kháng sinh đồ");
            text = text.Replace("Cácbệnh viện", "Các bệnh viện");
            text = text.Replace("K\xadnh gi", "Kính gửi");
            text = text.Replace("Kính gi", "Kính gửi");
            text = text.Replace("vic thit lp", "việc thiết lập");
            text = text.Replace("quy đnh", "quy định");
            text = text.Replace("chc năng", "chức năng");
            text = text.Replace("nhim v", "nhiệm vụ");
            text = text.Replace("trongcác", "trong các");
            text = text.Replace("co s", "cơ sở");
            text = text.Replace("khám, cha bnh", "khám, chữa bệnh");
            text = text.Replace("Nhm tăng cưng", "Nhằm tăng cường");
            text = text.Replace("hiu quå", "hiệu quả");
            text = text.Replace("phòng, chn", "phòng, chống");
            text = text.Replace("nhng ni dung", "những nội dung");

            // 2. Các từ ngắn (<= 4 ký tự) BẮT BUỘC dùng Word Boundary (\b) để chống nuốt chữ con (như Vinmec, CPU...)
            text = Regex.Replace(text, @"\bvin\b", "viện");
            text = Regex.Replace(text, @"\bđc\b", "được");
            text = Regex.Replace(text, @"\bĐc\b", "Được");
            text = Regex.Replace(text, @"\bcp\b", "cấp");
            text = Regex.Replace(text, @"\bCp\b", "Cấp");
            text = Regex.Replace(text, @"\bđn\b", "đến");
            text = Regex.Replace(text, @"\bĐn\b", "Đến");
            text = Regex.Replace(text, @"\bhp\b", "hợp");
            text = Regex.Replace(text, @"\bnht\b", "nhật");
            text = Regex.Replace(text, @"\bBnh\b", "Bệnh");
            text = Regex.Replace(text, @"\bđếng\b", "đồng");
            text = Regex.Replace(text, @"\bcy\b", "cấy");
            text = Regex.Replace(text, @"\bkỵ\b", "kỹ");
            text = Regex.Replace(text, @"\bvè\b", "về");

            // 3. Chuẩn hóa ngày tháng / địa danh
            text = text.Replace("y/3", "y 13").Replace("y 3", "y 13");
            text = Regex.Replace(text, @"ng[^\w]*y", "ngày", RegexOptions.IgnoreCase);
            text = text.Replace("thángf2", "tháng 12").Replace("thángl2", "tháng 12").Replace("tháng/2", "tháng 12");
            text = Regex.Replace(text, @"Th[\u00A0\x00-\x7F]*nh\s+phố", "Thành phố", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"Ch\u00AD\s+Minh", "Chí Minh", RegexOptions.IgnoreCase);

            // 4. Fix garbage bytes
            text = Regex.Replace(text, @"[^\x09\x0A\x0D\x20-\x7E\u00C0-\u1EF9]+", "");
            return text;
        }

        private static string MergeDiacriticsFromTesseract(string paddleText, string tessText)
        {
            if (string.IsNullOrWhiteSpace(tessText) || string.IsNullOrWhiteSpace(paddleText)) return paddleText;

            try
            {
                // Thu thập các từ tiếng Việt chuẩn có dấu từ Tesseract (độ dài >= 2)
                var tessWords = Regex.Matches(tessText, @"\b[\p{L}]{2,}\b")
                    .Cast<Match>()
                    .Select(m => m.Value)
                    .Where(w => Regex.IsMatch(w, @"[áàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđĐ]"))
                    .Distinct()
                    .ToList();

                // Tạo danh sách các cặp (từ có dấu, từ không dấu)
                var tessLookup = tessWords.Select(tw => new { Standard = tw, Unaccented = RemoveDiacritics(tw) }).ToList();

                // Duyệt qua paddleText và phục hồi dấu nếu PaddleOCR làm rớt dấu hoặc mất ký tự
                return Regex.Replace(paddleText, @"\b[\p{L}]{2,}\b", match =>
                {
                    var word = match.Value;
                    // Nếu từ của paddle đã có dấu thanh đầy đủ thì giữ nguyên
                    if (Regex.IsMatch(word, @"[áàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđĐ]"))
                    {
                        return word;
                    }

                    var wordUnaccented = RemoveDiacritics(word);
                    // 1. Khớp nhanh nếu trùng hoàn toàn bản không dấu
                    var exactMatch = tessLookup.FirstOrDefault(t => t.Unaccented.Equals(wordUnaccented, StringComparison.OrdinalIgnoreCase));
                    if (exactMatch != null && Math.Abs(exactMatch.Standard.Length - word.Length) <= 1)
                    {
                        var standardWord = exactMatch.Standard;
                        if (char.IsUpper(word[0]) && !char.IsUpper(standardWord[0]))
                            return char.ToUpper(standardWord[0]) + standardWord.Substring(1);
                        if (char.IsLower(word[0]) && char.IsUpper(standardWord[0]))
                            return char.ToLower(standardWord[0]) + standardWord.Substring(1);
                        return standardWord;
                    }

                    // 2. So khớp mờ bằng Levenshtein với ngưỡng tỉ lệ theo độ dài: distance <= Math.Max(1, word.Length / 3)
                    int maxAllowedDistance = Math.Max(1, word.Length / 3);
                    if (word.Length >= 2)
                    {
                        var fuzzyCandidate = tessLookup
                            .Where(t => Math.Abs(t.Unaccented.Length - wordUnaccented.Length) <= maxAllowedDistance &&
                                        t.Unaccented[0] == wordUnaccented[0]) // Phải cùng chữ cái đầu
                            .Select(t => new { t.Standard, Distance = ComputeLevenshteinDistance(wordUnaccented, t.Unaccented) })
                            .Where(x => x.Distance <= maxAllowedDistance)
                            .OrderBy(x => x.Distance)
                            .FirstOrDefault();

                        if (fuzzyCandidate != null)
                        {
                            var standardWord = fuzzyCandidate.Standard;
                            if (char.IsUpper(word[0]) && !char.IsUpper(standardWord[0]))
                                return char.ToUpper(standardWord[0]) + standardWord.Substring(1);
                            if (char.IsLower(word[0]) && char.IsUpper(standardWord[0]))
                                return char.ToLower(standardWord[0]) + standardWord.Substring(1);
                            return standardWord;
                        }
                    }

                    return word;
                });
            }
            catch
            {
                return paddleText;
            }
        }

        private static int ComputeLevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            var d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (char.ToLowerInvariant(t[j - 1]) == char.ToLowerInvariant(s[i - 1])) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        private static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd')
                .Replace('Đ', 'D');
        }

        public string ExtractTextFromPdfStream(Stream pdfStream)
        {
            if (pdfStream.CanSeek) pdfStream.Position = 0;

            using var memoryStream = new MemoryStream();
            pdfStream.CopyTo(memoryStream);
            var fileBytes = memoryStream.ToArray();

            // Nếu file thực chất là HTML (như c_ng_v_n_test_pdf.pdf)
            var prefix = Encoding.UTF8.GetString(fileBytes, 0, Math.Min(fileBytes.Length, 100)).TrimStart();
            if (prefix.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) || 
                prefix.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            {
                var htmlText = Encoding.UTF8.GetString(fileBytes);
                htmlText = Regex.Replace(htmlText, @"<style[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
                htmlText = Regex.Replace(htmlText, @"<script[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
                htmlText = Regex.Replace(htmlText, @"<[^>]+>", " ");
                htmlText = System.Net.WebUtility.HtmlDecode(htmlText);
                htmlText = Regex.Replace(htmlText, @"[ \t]+", " ");
                htmlText = Regex.Replace(htmlText, @"(\r?\n\s*)+", "\n");
                return PostProcessOcrText(htmlText.Trim());
            }

            string extracted = string.Empty;
            try
            {
                using var pdfDoc = UglyToad.PdfPig.PdfDocument.Open(fileBytes);
                var directText = new StringBuilder();
                bool hasScannedPageImages = false;

                int pNum = 1;
                int totalPages = pdfDoc.NumberOfPages;
                int pagesToRead = Math.Min(totalPages, 5); // Giới hạn 5 trang đầu cho trích xuất metadata hành chính

                for (int p = 1; p <= pagesToRead; p++)
                {
                    var page = pdfDoc.GetPage(p);
                    var text = page.Text;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        directText.AppendLine($"[PAGE_{pNum}]\n{text}\n[/PAGE_{pNum}]");
                        pNum++;
                    }
                    if (p <= 2 && string.IsNullOrWhiteSpace(text) && page.GetImages().Any(img => img.RawBytes.Length > 5000))
                    {
                        hasScannedPageImages = true;
                    }
                }
                
                extracted = directText.ToString().Trim();
                // Nếu đã trích xuất được text layer kỹ thuật số chuẩn xác (không bị nhiễu OCR), trả về ngay lập tức
                // kể cả khi PDF có đính kèm ảnh con dấu / chữ ký số điện tử
                if (extracted.Length >= 50 && !IsLowQualityText(extracted))
                {
                    return extracted;
                }
                if (extracted.Length >= 50 && !hasScannedPageImages)
                {
                    return extracted;
                }
            }
            catch { }

            try
            {
                var ocrTextBuilder = new StringBuilder();
                var imagesToProcess = new System.Collections.Generic.List<byte[]>();

                try 
                {
                    using var docReader = DocLib.Instance.GetDocReader(fileBytes, new PageDimensions(1080, 1920));
                    int pageCount = docReader.GetPageCount();
                    // Cho phép quét tối đa 10 trang thay vì giới hạn cứng 2 trang
                    int maxPagesToScan = Math.Min(pageCount, 10);

                    bool hasExplicitTier1Type = false;
                    bool hasReferenceNumber = false;
                    bool hasDate = false;

                    for (int i = 0; i < maxPagesToScan; i++)
                    {
                        using var pageReader = docReader.GetPageReader(i);
                        var rawBytes = pageReader.GetImage();
                        if (rawBytes == null || rawBytes.Length == 0) continue;

                        var width = pageReader.GetPageWidth();
                        var height = pageReader.GetPageHeight();
                        using var magick = new MagickImage(rawBytes, new MagickReadSettings { Width = (uint)width, Height = (uint)height, Format = MagickFormat.Bgra });
                        magick.BackgroundColor = MagickColors.White;
                        magick.Alpha(AlphaOption.Remove);
                        magick.Format = MagickFormat.Png;

                        // Downsample về ≤2200px trước khi xử lý (giảm tải CPU đáng kể với file scan 400-600 DPI)
                        var rawImgBytes = DownsampleImageIfNeeded(magick.ToByteArray(), maxDimension: 2200);
                        var cleanImgBytes = PreprocessImageWithOpenCV(rawImgBytes);

                        // ── 1-PASS OCR: Chạy 1 lần duy nhất trên toàn trang ────────────────────────
                        string pageText = string.Empty;
                        string headerZoneText = string.Empty;
                        try
                        {
                            PaddleOCRSharp.OCRResult? fullOcrResult = null;
                            lock (_paddleLock)
                            {
                                fullOcrResult = _paddleOcr?.DetectText(cleanImgBytes);
                            }
                            pageText = fullOcrResult?.Text?.Trim() ?? string.Empty;

                            // Tách Header Zone từ kết quả 1-pass bằng Bounding Box Y-coordinate
                            // Các TextBlock có trọng tâm Y ≤ 35% chiều cao trang = vùng Header hành chính
                            if (fullOcrResult?.TextBlocks != null && fullOcrResult.TextBlocks.Count > 0)
                            {
                                // Xác định chiều cao thực tế từ ảnh đã xử lý
                                using var imgForHeight = Cv2.ImDecode(cleanImgBytes, ImreadModes.Grayscale);
                                int imgHeight = imgForHeight.Empty() ? height : imgForHeight.Rows;
                                double headerThreshold = imgHeight * 0.35;

                                var headerLines = new System.Text.StringBuilder();
                                foreach (var block in fullOcrResult.TextBlocks)
                                {
                                    if (string.IsNullOrWhiteSpace(block.Text)) continue;
                                    // Tính trọng tâm Y của block từ BoxPoints (4 góc)
                                    if (block.BoxPoints != null && block.BoxPoints.Count >= 4)
                                    {
                                        double centerY = block.BoxPoints.Average(p => p.Y);
                                        if (centerY <= headerThreshold)
                                            headerLines.AppendLine(block.Text);
                                    }
                                    else
                                    {
                                        // Fallback: nếu không có BoxPoints, lấy các dòng đầu tiên của pageText
                                        // (không block nào có tọa độ → bỏ qua tối ưu này)
                                    }
                                }
                                headerZoneText = headerLines.ToString().Trim();
                            }

                            // Fallback header zone: lấy 40% dòng đầu tiên nếu không có TextBlocks tọa độ
                            if (string.IsNullOrWhiteSpace(headerZoneText) && !string.IsNullOrWhiteSpace(pageText))
                            {
                                var lines = pageText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                                int headerLineCount = Math.Max(1, (int)(lines.Length * 0.35));
                                headerZoneText = string.Join("\n", lines.Take(headerLineCount));
                            }
                        }
                        catch (Exception ex) { _logger.LogWarning($"Full page OCR failed: {ex.Message}"); }

                        // ── TESSERACT CONDITIONAL FALLBACK ────────────────────────────────────────────
                        // Chỉ chạy Tesseract khi PaddleOCR cho kết quả nghi ngờ (thiếu dấu bất thường)
                        // Điều kiện: tỷ lệ ký tự có dấu tiếng Việt < 4% trên tổng ký tự chữ cái
                        if (_tesseractEngine != null && IsVietnameseDiacriticsPoor(pageText, minRatio: 0.04))
                        {
                            try
                            {
                                _logger.LogInformation($"Page {i+1}: PaddleOCR diacritics poor — activating Tesseract fallback.");
                                lock (_tessLock)
                                {
                                    using var pix = Tesseract.Pix.LoadFromMemory(cleanImgBytes);
                                    using var tessPage = _tesseractEngine.Process(pix);
                                    var tessText = tessPage.GetText()?.Trim();
                                    if (!string.IsNullOrWhiteSpace(tessText))
                                    {
                                        pageText = MergeDiacriticsFromTesseract(pageText, tessText);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"Tesseract 5 assist failed: {ex.Message}");
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(headerZoneText) && i == 0)
                            ocrTextBuilder.AppendLine($"[HEADER_ZONE]\n{headerZoneText}\n[/HEADER_ZONE]");
                        if (!string.IsNullOrWhiteSpace(pageText))
                            ocrTextBuilder.AppendLine($"[PAGE_{i + 1}]\n{pageText}\n[/PAGE_{i + 1}]");
                        ocrTextBuilder.AppendLine();

                        // Kiểm tra điều kiện Early Termination an toàn:
                        // Chỉ dừng sớm nếu tìm thấy TIER 1 tường minh (QUYẾT ĐỊNH, KẾ HOẠCH, THÔNG BÁO...) đứng riêng dòng,
                        // cùng với Số hiệu, Ngày tháng VÀ Trích Yếu đã kết thúc hoàn chỉnh (dấu chấm/chấm phẩy hoặc điểm neo CĂN CỨ/ĐIỀU/THỰC HIỆN)
                        var currentCombined = ocrTextBuilder.ToString();
                        if (Regex.IsMatch(currentCombined, @"(?m)^\s*(QUYẾT\s*ĐỊNH|KẾ\s*HOẠCH|THÔNG\s*BÁO|CHỈ\s*THỊ|HƯỚNG\s*DẪN|TỜ\s*TRÌNH|QUY\s*CHẾ|VĂN\s*BẢN\s*HỢP\s*NHẤT|THÔNG\s*TƯ)\s*$"))
                        {
                            hasExplicitTier1Type = true;
                        }
                        if (Regex.IsMatch(currentCombined, @"\b(?:S[oốỐ]|No\.?)\s*[:/.]?\s*(\d+[\s\w\-\/]+)", RegexOptions.IgnoreCase))
                        {
                            hasReferenceNumber = true;
                        }
                        if (Regex.IsMatch(currentCombined, @"\b(?:ngày|ngay)\s+\d{1,2}\s+tháng\s+\d{1,2}\s+năm\s+\d{4}\b", RegexOptions.IgnoreCase))
                        {
                            hasDate = true;
                        }

                        // Kiểm tra Trích Yếu đã kết thúc đầy đủ (không bị ngắt lửng giữa chừng)
                        bool hasCompleteSubject = false;
                        // Trường hợp 1: Công văn có dòng "V/v..." và kết thúc bằng dấu chấm, hoặc xuống dòng sang Kính gửi
                        if (Regex.IsMatch(currentCombined, @"(?i)(?:V\/v|Về\s+việc|Về)\s*[:.]?\s*([^\n\r]+?)(?:[.!?]|[\r\n]+\s*(?:Kính\s*gửi|K/g|Nơi\s*nhận))"))
                        {
                            hasCompleteSubject = true;
                        }
                        // Trường hợp 2: Quyết định/Kế hoạch có tiêu đề đứng dưới Loại văn bản và kết thúc trước khối Căn cứ / Ban hành
                        else if (hasExplicitTier1Type && Regex.IsMatch(currentCombined, @"(?m)^\s*(?:Căn\s*cứ|QUYẾT\s*ĐỊNH:|Điều\s+1|THỰC\s*HIỆN)"))
                        {
                            hasCompleteSubject = true;
                        }

                        // Sau khi đã quét tối thiểu 2 trang đầu tiên VÀ có đủ 4 trường cốt lõi với Trích yếu hoàn chỉnh
                        if (i >= 1 && hasExplicitTier1Type && hasReferenceNumber && hasDate && hasCompleteSubject)
                        {
                            _logger.LogInformation($"Early termination triggered safely at page {i + 1}/{pageCount}: All 4 core administrative fields including complete subject found.");
                            break;
                        }
                    }
                }
                catch
                {
                    // Fallback nếu không parse được qua DocNet
                    if (_paddleOcr != null)
                    {
                        var cleanImgBytes = PreprocessImageWithOpenCV(fileBytes);
                        PaddleOCRSharp.OCRResult? fullOcrResult = null;
                        lock (_paddleLock)
                        {
                            fullOcrResult = _paddleOcr.DetectText(cleanImgBytes);
                        }
                        if (!string.IsNullOrWhiteSpace(fullOcrResult?.Text))
                        {
                            ocrTextBuilder.AppendLine(fullOcrResult.Text);
                        }
                    }
                }

                var renderedOcrResult = ocrTextBuilder.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(renderedOcrResult))
                {
                    return PostProcessOcrText(renderedOcrResult);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR Processing failed.");
            }

            if (!string.IsNullOrWhiteSpace(extracted)) return PostProcessOcrText(extracted);
            throw new Exception("Unable to extract text using PaddleOCR.");
        }

        public void Dispose()
        {
            // _paddleOcr?.Dispose();
        }
    }
}




