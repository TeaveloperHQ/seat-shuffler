using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SeatShuffler.Models;

namespace SeatShuffler.Services;

/// <summary>확정 기록을 history.json에 영속.</summary>
public sealed class HistoryStore
{
    public List<ConfirmedRecord> Load()
    {
        try
        {
            if (!File.Exists(AppPaths.HistoryFile)) return new List<ConfirmedRecord>();
            var json = File.ReadAllText(AppPaths.HistoryFile);
            var doc = JsonSerializer.Deserialize(json, AppJsonContext.Default.HistoryDocument);
            return doc?.Records ?? new List<ConfirmedRecord>();
        }
        catch
        {
            return new List<ConfirmedRecord>();
        }
    }

    public void Save(IEnumerable<ConfirmedRecord> records)
    {
        AppPaths.EnsureDir();
        var doc = new HistoryDocument { Records = new List<ConfirmedRecord>(records) };
        var json = JsonSerializer.Serialize(doc, AppJsonContext.Default.HistoryDocument);
        AtomicWrite.Write(AppPaths.HistoryFile, json);
    }
}
