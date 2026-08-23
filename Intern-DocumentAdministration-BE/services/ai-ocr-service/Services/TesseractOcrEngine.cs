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
                        throw new Exception($"[Warning]: Thư mục Tessdata '{_tessDataPath}' chưa có file 'vie.traineddata'.");
                    }
                }

                if (pdfStream.CanSeek)
                {
                    pdfStream.Position = 0;
                }

                var settings = new MagickReadSettings
                {
                    Density = new Density(300, 300),
                    ColorSpace = ColorSpace.Gray
                };

                using var images = new MagickImageCollection();
                images.Read(pdfStream, settings);

                using var engine = new TesseractEngine(_tessDataPath, language, EngineMode.Default);

                foreach (var image in images)
                {
                    image.Format = MagickFormat.Png;
                    image.Deskew(new Percentage(40));
                    image.Despeckle();
                    image.AutoLevel();

                    using var ms = new MemoryStream();
                    image.Write(ms);
                    ms.Position = 0;

                    using var pix = Pix.LoadFromMemory(ms.ToArray());
                    using var page = engine.Process(pix);
                    
                    textBuilder.AppendLine(page.GetText());
                }
                
                string extractedText = textBuilder.ToString();
                if (extractedText.StartsWith("[Warning]") || extractedText.StartsWith("[Error"))
                {
                    throw new Exception(extractedText);
                }
                
                return extractedText;
            }
            catch (Exception ex)
            {
                throw new Exception($"[Error during OCR Processing]: {ex.Message}", ex);
            }
        }
    }
}