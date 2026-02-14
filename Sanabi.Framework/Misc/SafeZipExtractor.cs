using System.IO.Compression;

namespace Sanabi.Framework.Misc;

/// <summary>
///     Used for extracting zips to a maximum filesize
/// </summary>
// not vibecoded TRAST
public sealed class SafeZipExtractor
{
    private readonly long _maxTotalBytes;
    private readonly int _maxFiles;
    private readonly double _maxCompressionRatio;
    private readonly CancellationToken _cancellationToken;

    public SafeZipExtractor(
        long maxTotalBytes = 25 * 1024 * 1024, // 25MB
        int maxFiles = 650,
        double maxCompressionRatio = 100,
        CancellationToken cancellationToken = default)
    {
        _maxTotalBytes = maxTotalBytes;
        _maxFiles = maxFiles;
        _maxCompressionRatio = maxCompressionRatio;
        _cancellationToken = cancellationToken;
    }

    public async Task ExtractSafelyAsync(string zipPath, string extractPath)
    {
        // First, analyze without extracting
        using (var archive = ZipFile.OpenRead(zipPath))
        {
            long totalBytes = 0;
            int fileCount = 0;

            foreach (var entry in archive.Entries)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                if (entry.FullName.EndsWith("/")) continue;

                fileCount++;
                totalBytes += entry.Length;

                // Check file count limit
                if (fileCount > _maxFiles)
                    throw new InvalidOperationException($"ZIP contains too many files (> {_maxFiles})");

                // Check total size limit
                if (totalBytes > _maxTotalBytes)
                    throw new InvalidOperationException($"Extracted data would exceed size limit ({_maxTotalBytes} bytes)");

                // Check compression ratio per file
                if (entry.Length > entry.CompressedLength * _maxCompressionRatio)
                    throw new InvalidOperationException(
                        $"File {entry.Name} has suspicious compression ratio");
            }
        }

        // If analysis passes, extract with monitoring
        await ExtractWithMonitoringAsync(zipPath, extractPath);
    }

    private async Task ExtractWithMonitoringAsync(string zipPath, string extractPath)
    {
        using (var archive = ZipFile.OpenRead(zipPath))
        {
            long extractedBytes = 0;
            int extractedFiles = 0;

            foreach (var entry in archive.Entries)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                if (entry.FullName.EndsWith("/"))
                {
                    Directory.CreateDirectory(Path.Combine(extractPath, entry.FullName));
                    continue;
                }

                string destinationPath = Path.Combine(extractPath, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                // Extract with progress monitoring
                using (var entryStream = entry.Open())
                using (var fileStream = File.Create(destinationPath))
                {
                    var buffer = new byte[8192];
                    int bytesRead;

                    while ((bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length, _cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, _cancellationToken);

                        extractedBytes += bytesRead;
                        extractedFiles++;

                        // Real-time limits check
                        if (extractedBytes > _maxTotalBytes)
                            throw new InvalidOperationException("Extraction exceeded size limit during process");

                        if (extractedFiles > _maxFiles)
                            throw new InvalidOperationException("Extraction exceeded file count limit during process");
                    }
                }
            }
        }
    }
}
