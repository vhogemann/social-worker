using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UglyToad.PdfPig;

namespace SocialWorker.Api.Features.Sources;

public sealed class SourceExtractor
{
    public async Task<string> ExtractTextAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        using var stream = file.OpenReadStream();

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
