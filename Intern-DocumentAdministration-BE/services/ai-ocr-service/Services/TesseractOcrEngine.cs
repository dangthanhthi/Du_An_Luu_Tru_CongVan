using Docnet.Core;
using Docnet.Core.Models;
using ImageMagick;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;

namespace AiOcrService.Services
{
    public class TesseractOcrEngine : IOcrEngine
    {
        private readonly string _tessDataPath;

        public TesseractOcrEngine()
        {
            // Tesseract C++ native DLL trên Windows không hỗ trợ đường dẫn có dấu tiếng Việt (Dự án -> D? n).
            // Luôn sao chép traineddata sang thư mục ASCII chuẩn trong Temp để đảm bảo native C++ load 100% thành công.
            var asciiTessData = Path.Combine(Path.GetTempPath(), "doc_admin_tessdata");
            Directory.CreateDirectory(asciiTessData);

            var sourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            if (!Directory.Exists(sourceDir))
            {
                sourceDir = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
            }

            if (Directory.Exists(sourceDir))
            {
                foreach (var file in Directory.GetFiles(sourceDir, "*.traineddata"))
                {
                    var dest = Path.Combine(asciiTessData, Path.GetFileName(file));
                    if (!File.Exists(dest) || new FileInfo(file).Length != new FileInfo(dest).Length)
                    {
                        File.Copy(file, dest, true);
                    }
                }
            }

            _tessDataPath = asciiTessData;
            Environment.SetEnvironmentVariable("TESSDATA_PREFIX", asciiTessData);
        }

        private static bool IsLowQualityText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (text.Length < 30) return true;

            // 1. Kiểm tra tỷ lệ nguyên âm tiếng Việt có dấu
            // Văn bản hành chính Việt Nam luôn có các ký tự có dấu: à, á, ả, ã, ạ, ê, ô, ư, đ...
            int vietnameseAccents = 0;
            foreach (var ch in text)
            {
                if ("àáảãạăắằẳẵặâấầẩẫậèéẻẽẹêếềểễệìíỉĩịòóỏõọôốồổỗộơớờởỡợùúủũụưứừửữựỳýỷỹỵđĐ".IndexOf(char.ToLower(ch)) >= 0)
                {
                    vietnameseAccents++;
                }
            }
            // Nếu độ dài > 80 nhưng tỷ lệ ký tự có dấu < 3% -> OCR máy scan cũ bị lỗi font không dấu / rác
            if (text.Length > 80 && (double)vietnameseAccents / text.Length < 0.03)
            {
                return true;
            }

            // 2. Chứa các từ rác đặc trưng của OCR máy photocopy cũ
            if (Regex.IsMatch(text, @"\b(UY NUAN|tvtrNu|rUpnAp|vd wa|nghiQp|ph6p|h6 trq)\b", RegexOptions.IgnoreCase))
            {
                return true;
            }

            // 3. Đếm các ký tự rác phổ biến của OCR cũ bị lỗi font: ~, \, |, ^, `, etc.
            int noiseCount = 0;
            foreach (var ch in text)
            {
                if (ch == '~' || ch == '\\' || ch == '|' || ch == '^' || ch == '`' || ch == '{' || ch == '}' || ch == '¤' || ch == '¥' || ch == '§')
                {
                    noiseCount++;
                }
            }

            if (noiseCount >= 3 || (double)noiseCount / text.Length > 0.015)
            {
                return true;
            }

            // 4. Kiểm tra các mẫu số lẫn chữ bất thường (d6i, m6'i, nu6'c, t~o, khCringhi~p)
            if (Regex.IsMatch(text, @"[a-z][0-9][a-z]|[a-z]~[a-z]|[a-z]\\[a-z]|\b[a-z]+[0-9]+[a-z]*\b", RegexOptions.IgnoreCase))
            {
                return true;
            }

            return false;
        }

        public string ExtractTextFromPdfStream(Stream pdfStream)
        {
            if (pdfStream.CanSeek)
            {
                pdfStream.Position = 0;
            }

            // Layer 1: Trích xuất trực tiếp bằng PdfPig (siêu nhanh, không cần Ghostscript ngoài)
            try
            {
                using var memoryStream = new MemoryStream();
                pdfStream.CopyTo(memoryStream);
                var pdfBytes = memoryStream.ToArray();

                using var pdfDoc = UglyToad.PdfPig.PdfDocument.Open(pdfBytes);
                var directText = new StringBuilder();
                bool hasScannedPageImages = false;

                foreach (var page in pdfDoc.GetPages())
                {
                    var text = page.Text;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        directText.AppendLine(text);
                    }
                    if (page.GetImages().Any(img => img.RawBytes.Length > 5000))
                    {
                        hasScannedPageImages = true;
                    }
                }

                var extracted = directText.ToString().Trim();

                // Nếu văn bản trực tiếp sạch và chất lượng cao (PDF gốc xuất từ Word/InDesign, không chứa ảnh scan)
                if (extracted.Length >= 50 && !hasScannedPageImages && !IsLowQualityText(extracted))
                {
                    return extracted;
                }

                // Nếu là file scan ảnh hoặc văn bản trực tiếp bị rác (lỗi font/OCR cũ):
                // Render toàn bộ trang ở độ nét cao 2000x2828 bằng Docnet (Google PDFium) và chạy Tesseract OCR chuẩn vie+eng
                try
                {
                    var language = ResolveLanguage();
                    using var engine = new TesseractEngine(_tessDataPath, language, EngineMode.Default);
                    using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(2000, 2828));
                    int pageCount = docReader.GetPageCount();
                    var ocrTextBuilder = new StringBuilder();

                    // Văn bản hành chính: Số hiệu, Cơ quan ban hành, Ngày tháng và Trích yếu ở trang 1-2;
                    // Chữ ký, Người ký và Con dấu luôn ở trang cuối cùng
                    var pagesToProcess = new System.Collections.Generic.List<int> { 0 };
                    if (pageCount > 1) pagesToProcess.Add(1);
                    if (pageCount > 2 && !pagesToProcess.Contains(pageCount - 1)) pagesToProcess.Add(pageCount - 1);

                    foreach (var i in pagesToProcess)
                    {
                        using var pageReader = docReader.GetPageReader(i);
                        var rawBytes = pageReader.GetImage();
                        var width = pageReader.GetPageWidth();
                        var height = pageReader.GetPageHeight();

                        if (rawBytes != null && rawBytes.Length > 0 && width > 0 && height > 0)
                        {
                            using var magick = new MagickImage(rawBytes, new MagickReadSettings
                            {
                                Width = (uint)width,
                                Height = (uint)height,
                                Format = MagickFormat.Bgra
                            });
                            magick.Format = MagickFormat.Png;
                            // Tiền xử lý ảnh để tăng độ chính xác OCR chữ viết tay
                            PreprocessImageForHandwriting(magick);

                            // Pass 1: OCR toàn trang (PSM Auto)
                            using var ms = new MemoryStream();
                            magick.Write(ms);
                            using var pix = Pix.LoadFromMemory(ms.ToArray());
                            using var ocrPage = engine.Process(pix);
                            var pageText = ocrPage.GetText() ?? string.Empty;

                            // Pass 2: OCR vùng header (top 25%) với PSM SingleLine để tăng độ chính xác số hiệu và ngày
                            string headerZoneText = string.Empty;
                            try
                            {
                                int headerH = Math.Max(1, (int)(height * 0.25));
                                using var headerMagick = new MagickImage(ms.ToArray());
                                headerMagick.Crop(new MagickGeometry(0, 0, (uint)width, (uint)headerH));
                                headerMagick.ResetPage();
                                using var headerMs = new MemoryStream();
                                headerMagick.Write(headerMs);
                                using var headerEngine = new TesseractEngine(_tessDataPath, language, EngineMode.Default);
                                headerEngine.DefaultPageSegMode = PageSegMode.SingleLine;
                                using var headerPix = Pix.LoadFromMemory(headerMs.ToArray());
                                using var headerOcrPage = headerEngine.Process(headerPix);
                                headerZoneText = headerOcrPage.GetText()?.Trim() ?? string.Empty;
                            }
                            catch { /* header crop optional, continue */ }

                            if (!string.IsNullOrWhiteSpace(pageText))
                            {
                                // Nhúng header zone vào đầu kết quả để DynamicFieldExtractor dùng
                                if (!string.IsNullOrWhiteSpace(headerZoneText))
                                    ocrTextBuilder.AppendLine($"[HEADER_ZONE]{headerZoneText}[/HEADER_ZONE]");
                                ocrTextBuilder.AppendLine(pageText.Trim());
                                ocrTextBuilder.AppendLine();
                            }
                        }
                    }

                    var renderedOcrResult = ocrTextBuilder.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(renderedOcrResult) && renderedOcrResult.Length >= 30)
                    {
                        return renderedOcrResult;
                    }
                }
                catch
                {
                    // Fallback
                }

                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    return extracted;
                }
            }
            catch
            {
                // Fallback xuống ImageMagick
            }

            if (pdfStream.CanSeek)
            {
                pdfStream.Position = 0;
            }

            var textBuilder = new StringBuilder();
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"ocr-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var language = ResolveLanguage();
                var settings = new MagickReadSettings
                {
                    Density = new Density(300, 300),
                    ColorSpace = ColorSpace.Gray
                };

                using var images = new MagickImageCollection();
                images.Read(pdfStream, settings);

                if (images.Count == 0)
                {
                    throw new Exception("The PDF does not contain any pages that can be processed.");
                }

                var pageIndex = 0;
                foreach (var image in images)
                {
                    pageIndex++;
                    if (pageIndex > 20) break;

                    image.Format = MagickFormat.Png;
                    try { image.Deskew(new Percentage(30)); } catch { }

                    var imagePath = Path.Combine(tempDirectory, $"page-{pageIndex:D3}.png");
                    image.Write(imagePath);

                    var pageText = RunTesseract(imagePath, language);
                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        textBuilder.AppendLine(pageText.Trim());
                        textBuilder.AppendLine();
                    }
                }

                return textBuilder.ToString().Trim();
            }
            catch (Exception ex)
            {
                throw new Exception($"[Error during OCR Processing]: {ex.Message}", ex);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
                }
                catch { }
            }
        }

        /// <summary>
        /// Tiền xử lý ảnh để tăng độ chính xác OCR chữ viết tay:
        /// - Grayscale để giảm nhiễu màu sắc
        /// - AdaptiveThreshold để xử lý ánh sáng không đều
        /// - UnsharpMask để làm nét nét chữ mờ
        /// - ReduceNoise để giảm hạt nhiễu máy scan cũ
        /// </summary>
        private static void PreprocessImageForHandwriting(MagickImage magick)
        {
            try
            {
                // 1. Chuyển về Grayscale
                magick.ColorSpace = ColorSpace.Gray;
                // 2. Cân bằng histogram cục bộ (adaptive) để xử lý vùng sáng/tối không đều
                magick.AdaptiveThreshold(15, 15, new Percentage(-10));
                // 3. Làm nét nét chữ viết tay
                magick.UnsharpMask(0, 1.0, 1.5, 0.05);
                // 4. Giảm nhiễu hạt
                magick.ReduceNoise();
            }
            catch
            {
                // Nếu một bước nào thất bại, bỏ qua và tiếp tục với ảnh hiện tại
            }
        }

        private string ResolveLanguage()
        {
            var vieTrainedData = Path.Combine(
                _tessDataPath,
                "vie.traineddata");

            var engTrainedData = Path.Combine(
                _tessDataPath,
                "eng.traineddata");

            var hasVie =
                File.Exists(vieTrainedData);

            var hasEng =
                File.Exists(engTrainedData);

            if (hasVie && hasEng)
            {
                return "vie+eng";
            }

            if (hasVie)
            {
                return "vie";
            }

            if (hasEng)
            {
                return "eng";
            }

            throw new Exception(
                $"Could not find vie.traineddata or eng.traineddata in '{_tessDataPath}'.");
        }

        private string RunTesseract(
            string imagePath,
            string language)
        {
            try
            {
                using var engine = new Tesseract.TesseractEngine(_tessDataPath, language, Tesseract.EngineMode.Default);
                using var img = Tesseract.Pix.LoadFromFile(imagePath);
                using var page = engine.Process(img);
                return page.GetText() ?? string.Empty;
            }
            catch (Exception ex)
            {
                // Fallback to CLI if native DLL fails
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "tesseract",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    startInfo.ArgumentList.Add(imagePath);
                    startInfo.ArgumentList.Add("stdout");
                    startInfo.ArgumentList.Add("-l");
                    startInfo.ArgumentList.Add(language);
                    startInfo.ArgumentList.Add("--tessdata-dir");
                    startInfo.ArgumentList.Add(_tessDataPath);
                    startInfo.ArgumentList.Add("--psm");
                    startInfo.ArgumentList.Add("6");

                    using var process = new Process { StartInfo = startInfo };
                    if (process.Start())
                    {
                        var outputTask = process.StandardOutput.ReadToEndAsync();
                        process.WaitForExit(30000);
                        return outputTask.Result;
                    }
                }
                catch
                {
                }

                throw new Exception($"Tesseract OCR failed: {ex.Message}", ex);
            }
        }
    }
}