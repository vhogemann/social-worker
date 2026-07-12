using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Media;

public sealed class FileStorageProvider
{
    private const string UploadsRoot = "/app/uploads";

    public string GetFullPath(string relativePath)
    {
        return Path.Combine(UploadsRoot, relativePath);
    }

    public bool FileExists(string relativePath)
    {
        return File.Exists(GetFullPath(relativePath));
    }

    public async Task WriteFileAsync(string relativePath, byte[] data)
    {
        var fullPath = GetFullPath(relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (dir != null)
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllBytesAsync(fullPath, data);
    }

    public void DeleteFileAndEmptyFolder(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            try { File.Delete(fullPath); } catch { Console.Error.WriteLine($"Failed to delete file: {fullPath}"); }
        }

        var dir = Path.GetDirectoryName(fullPath);
        if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
        {
            try { Directory.Delete(dir); } catch { Console.Error.WriteLine($"Failed to delete empty directory: {dir}"); }
        }
    }
}
