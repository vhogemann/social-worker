using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace SocialWorker.Api.Features.Sources;

public sealed class SourceExtractor
{
    public async Task<string> ExtractTextAsync(string fileName, Stream stream)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (ext == ".pdf")
        {
            return ExtractPdfText(stream);
        }
        else if (ext == ".txt" || ext == ".md")
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        else
        {
            throw new NotSupportedException("Unsupported file format. Supported: .pdf, .txt, .md");
        }
    }

    private static string ExtractPdfText(Stream stream)
    {
        using var pdf = PdfDocument.Open(stream);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        return sb.ToString().Trim();
    }
}
