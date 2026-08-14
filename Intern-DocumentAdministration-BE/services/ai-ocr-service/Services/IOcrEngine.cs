using System.IO;

namespace AiOcrService.Services
{
    public interface IOcrEngine
    {
        string ExtractTextFromPdfStream(Stream pdfStream);
    }
}