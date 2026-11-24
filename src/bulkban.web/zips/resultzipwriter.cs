using System.IO.Compression;
using bulkban.Core.Policies;

namespace bulkban.Web.Zips;

/// <summary>把构建产物写成一个 zip。</summary>
internal static class ResultZipWriter
{
    internal static byte[] Write(IReadOnlyList<OutputFile> files, DateTimeOffset timestamp)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Path, CompressionLevel.Optimal);
                entry.LastWriteTime = timestamp;
                using var stream = entry.Open();
                stream.Write(file.Content);
            }
        }

        return buffer.ToArray();
    }
}
