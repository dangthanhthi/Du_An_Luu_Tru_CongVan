using ImageMagick;
using System;
using System.IO;
using System.Text;
using Tesseract;

namespace AiOcrService.Services
{
    public class TesseractOcrEngine : IOcrEngine
    {
        private readonly string _tessDataPath;

        public TesseractOcrEngine()
        {
            // Tìm đường dẫn chứa tesseract traineddata tương thích cả Windows lẫn Linux Docker
            var envPath = Environment.GetEnvironmentVariable("TESSDATA_PREFIX");
            if (!string.IsNullOrWhiteSpace(envPath) && Directory.Exists(envPath))
            {
                _tessDataPath = envPath;
            }
            else if (Directory.Exists(@"/usr/share/tessdata"))
            {
                _tessDataPath = @"/usr/share/tessdata";
            }
            else if (Directory.Exists(@"/usr/share/tesseract-ocr/4.00/tessdata"))
            {
                _tessDataPath = @"/usr/share/tesseract-ocr/4.00/tessdata";
            }
            else
            {
                // Môi trường Windows Local Dev: lấy từ thư mục ./tessdata ngay cạnh ứng dụng
                _tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                if (!Directory.Exists(_tessDataPath))
                {
                    Directory.CreateDirectory(_tessDataPath);
                }
            }
        }

        public string ExtractTextFromPdfStream(Stream pdfStream)
        {
            var textBuilder = new StringBuilder();

            try
            {
                // Kiểm tra xem traineddata tiếng Việt có tồn tại hay không
                var vieTrainedData = Path.Combine(_tessDataPath, "vie.traineddata");
                var engTrainedData = Path.Combine(_tessDataPath, "eng.traineddata");
                
                string language = "vie";
                if (!File.Exists(vieTrainedData))
                {
                    if (File.Exists(engTrainedData))
                    {
                        language = "eng";
                    }
                    else
                    {
                        // Nếu chưa có file traineddata, trả về thông báo lỗi hướng dẫn người dùng
                        return $"[Warning]: Thư mục Tessdata '{_tessDataPath}' chưa có file 'vie.traineddata'. Vui lòng tải file traineddata tiếng Việt từ Tesseract GitHub bỏ vào thư mục trên.";
                    }
                }

                if (pdfStream.CanSeek)
                {
                    pdfStream.Position = 0;
                }

                using var images = new MagickImageCollection();
                
                // Đọc luồng dữ liệu PDF / Image
                images.Read(pdfStream);

                using var engine = new TesseractEngine(_tessDataPath, language, EngineMode.Default);

                foreach (var image in images)
                {
                    // Tối ưu hóa ảnh bản fax/PDF trước khi nạp vào OCR
                    image.Density = new Density(300, 300); // Đặt độ phân giải 300 DPI giúp nhận diện chữ mờ tốt hơn
                    image.Format = MagickFormat.Png;
                    image.ColorSpace = ColorSpace.Gray;   // Chuyển sang ảnh xám để giảm nhiễu

                    using var ms = new MemoryStream();
                    image.Write(ms);
                    ms.Position = 0;

                    using var pix = Pix.LoadFromMemory(ms.ToArray());
                    using var page = engine.Process(pix);
                    
                    textBuilder.AppendLine(page.GetText());
                }
            }
            catch (Exception ex)
            {
                textBuilder.AppendLine($"[Error during OCR Processing]: {ex.Message}");
            }

            return textBuilder.ToString();
        }
    }
}