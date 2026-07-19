using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Sources;

public sealed class FileSourceService : IFileSourceService
{
    private readonly AppDbContext _db;
    private readonly SummarizationService? _summarizer;

    public FileSourceService(AppDbContext db, SummarizationService? summarizer)
    {
        _db = db;
        _summarizer = summarizer;
    }

    public async Task<AddFileSourceResult> AddFileSourceAsync(
        Guid userId,
        Guid draftId,
        string fileName,
        Stream fileStream,
        SourceExtractor extractor,
        CancellationToken ct)
    {
        var draft = await _db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
            ?? throw new KeyNotFoundException("Draft not found or access denied.");

        using var sha256 = SHA256.Create();
        byte[] hashBytes;
        using (var tempStream = new MemoryStream())
        {
            await fileStream.CopyToAsync(tempStream, ct);
            tempStream.Position = 0;
            hashBytes = sha256.ComputeHash(tempStream);
        }
        var shaHashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        var existing = await _db.Sources.FirstOrDefaultAsync(s => s.Sha256 == shaHashStr, ct);
        if (existing != null)
        {
            var hasLink = await _db.DraftSources.AnyAsync(ds => ds.DraftId == draftId && ds.SourceId == existing.Id, ct);
            if (!hasLink)
            {
                _db.DraftSources.Add(new DraftSource
                {
                    DraftId = draftId,
                    SourceId = existing.Id,
                    LinkedAt = DateTime.UtcNow
                });
            }
            draft.Status = DraftStatus.Editing;
            await _db.SaveChangesAsync(ct);

            return new AddFileSourceResult(existing.Id, existing.Reference);
        }

        string extractedText;
        using (var tempStreamForExtract = new MemoryStream())
        {
            fileStream.Position = 0;
            await fileStream.CopyToAsync(tempStreamForExtract, ct);
            tempStreamForExtract.Position = 0;
            extractedText = await extractor.ExtractTextAsync(fileName, tempStreamForExtract);
        }

        string? summary = null;
        if (_summarizer != null && !string.IsNullOrWhiteSpace(extractedText))
        {
            summary = await _summarizer.SummarizeAsync(extractedText, ct);
        }

        var newSource = new Source
        {
            Kind = SourceKind.File,
            Reference = fileName,
            Title = fileName,
            Content = extractedText,
            Summary = summary,
            Sha256 = shaHashStr,
            AddedAt = DateTime.UtcNow
        };

        _db.Sources.Add(newSource);
        _db.DraftSources.Add(new DraftSource
        {
            Draft = draft,
            Source = newSource,
            LinkedAt = DateTime.UtcNow
        });
        draft.Status = DraftStatus.Editing;
        await _db.SaveChangesAsync(ct);

        return new AddFileSourceResult(newSource.Id, newSource.Reference);
    }
}
