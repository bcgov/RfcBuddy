using Microsoft.Extensions.Logging;
using RfcBuddy.App.Objects;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace RfcBuddy.App.Services;

public interface IRfcArchiveService
{
    /// <summary>
    /// Upserts observed RFCs into the shared archive and prunes entries older than the retention window.
    /// </summary>
    void UpdateArchive(IEnumerable<Rfc> observedRfcs);

    /// <summary>
    /// Returns completed RFCs from the archive for the last 5 weeks.
    /// </summary>
    List<Rfc> GetCompletedRfcs();
}

public sealed class RfcArchiveService : IRfcArchiveService
{
    private const int retentionDays = 35;
    private const int maxRecords = 20_000;
    private const long maxFileSizeBytes = 50L * 1024L * 1024L;
    private const int maxAttempts = 3;

    private static readonly ConcurrentDictionary<string, Mutex> lockRegistry = new(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger<RfcArchiveService> _logger;
    private readonly string _dataFolder;
    private readonly string _archiveFilePath;
    private readonly Mutex _writeMutex;

    public RfcArchiveService(IAppSettingsService appSettingsService, ILogger<RfcArchiveService> logger)
        : this(appSettingsService.AppSettings.DataFolder, logger)
    {
    }

    public RfcArchiveService(string dataFolder, ILogger<RfcArchiveService> logger, string archiveFileName = "archived-rfcs.json")
    {
        _logger = logger;
        _dataFolder = Path.GetFullPath(dataFolder);
        _archiveFilePath = Path.Combine(_dataFolder, archiveFileName);
        Directory.CreateDirectory(_dataFolder);
        _writeMutex = lockRegistry.GetOrAdd(_archiveFilePath, static path => new Mutex(false, "Global\\RfcBuddyArchive_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(path)))));
    }

    public void UpdateArchive(IEnumerable<Rfc> observedRfcs)
    {
        _writeMutex.WaitOne();
        try
        {
            List<Rfc> merged = LoadArchiveRecords();
            DateTime now = DateTime.Now;

            foreach (Rfc observedRfc in observedRfcs)
            {
                if (string.IsNullOrWhiteSpace(observedRfc.RfcNumber))
                {
                    continue;
                }

                Rfc? existing = merged.FirstOrDefault(x => string.Equals(x.RfcNumber, observedRfc.RfcNumber, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    merged.Add(CloneRfc(observedRfc));
                }
                else if (observedRfc.EndDate > existing.EndDate)
                {
                    int index = merged.IndexOf(existing);
                    merged[index] = CloneRfc(observedRfc);
                }
            }

            DateTime pruneCutoff = now.AddDays(-retentionDays);
            merged = [.. merged.Where(x => x.EndDate != default && x.EndDate >= pruneCutoff).OrderBy(x => x.RfcNumber, StringComparer.OrdinalIgnoreCase)];

            if (merged.Count > maxRecords)
            {
                _logger.LogWarning("Archive record limit exceeded. Records={RecordCount}", merged.Count);
                throw new InvalidOperationException($"The RFC archive record limit of {maxRecords} was exceeded.");
            }

            string json = JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true });
            if (Encoding.UTF8.GetByteCount(json) > maxFileSizeBytes)
            {
                _logger.LogWarning("Archive size limit exceeded. Bytes={Bytes}", Encoding.UTF8.GetByteCount(json));
                throw new InvalidOperationException($"The RFC archive size limit of {maxFileSizeBytes} bytes was exceeded.");
            }

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    string tempFilePath = Path.Combine(_dataFolder, $"archived-rfcs.json.tmp-{Guid.NewGuid():N}");
                    using (var stream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(json);
                    }
                    File.Move(tempFilePath, _archiveFilePath, true);
                    return;
                }
                catch (IOException ex) when (attempt < maxAttempts)
                {
                    _logger.LogWarning(ex, "Archive write retry. Attempt={Attempt}", attempt);
                    Thread.Sleep(TimeSpan.FromMilliseconds(100 * attempt));
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Archive file is corrupted. Resetting archive file.");
                    if (File.Exists(_archiveFilePath))
                    {
                        File.Delete(_archiveFilePath);
                    }
                    throw;
                }
            }

            _logger.LogError("Archive write exhausted retries. Path={ArchivePath}", _archiveFilePath);
            throw new IOException($"Unable to write archive after {maxAttempts} attempts.");
        }
        finally
        {
            _writeMutex.ReleaseMutex();
        }
    }

    public List<Rfc> GetCompletedRfcs()
    {
        List<Rfc> archivedRfcs = LoadArchiveRecords();
        DateTime now = DateTime.Now;
        DateTime cutoff = now.AddDays(-retentionDays);
        return [.. archivedRfcs.Where(x => x.EndDate != default && x.EndDate >= cutoff && x.EndDate <= now).OrderByDescending(x => x.EndDate).ThenByDescending(x => x.StartDate)];
    }

    private List<Rfc> LoadArchiveRecords()
    {
        if (!File.Exists(_archiveFilePath))
        {
            return [];
        }

        var fileInfo = new FileInfo(_archiveFilePath);

        if (fileInfo.Length > maxFileSizeBytes)

        {

            _logger.LogWarning("Archive size limit exceeded on read. Bytes={Bytes}", fileInfo.Length);

            File.Delete(_archiveFilePath);

            return [];

        }

        try
        {
            string json = File.ReadAllText(_archiveFilePath);
            List<Rfc>? records = JsonSerializer.Deserialize<List<Rfc>>(json);
            if (records is null)
            {
                return [];
            }
            return records;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Archive file is corrupted. Resetting archive file.");
            File.Delete(_archiveFilePath);
            return [];
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Archive read failed. Path={ArchivePath}", _archiveFilePath);
            return [];
        }
    }

    private static Rfc CloneRfc(Rfc source) => new(source.RfcNumber)
    {
        ApprovalStatus = source.ApprovalStatus,
        Platform = source.Platform,
        AssetTags = source.AssetTags,
        StartDate = source.StartDate,
        EndDate = source.EndDate,
        Description = source.Description,
        RiskAssessment = source.RiskAssessment,
        Keywords = []
    };
}
