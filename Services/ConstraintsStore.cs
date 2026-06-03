using System.IO;
using System.Text.Json;

namespace SeatShuffler.Services;

/// <summary>제약 + 배정 설정을 constraints.json에 영속.</summary>
public sealed class ConstraintsStore
{
    public ConstraintsDocument Load()
    {
        try
        {
            if (!File.Exists(AppPaths.ConstraintsFile)) return new ConstraintsDocument();
            var json = File.ReadAllText(AppPaths.ConstraintsFile);
            return JsonSerializer.Deserialize(json, AppJsonContext.Default.ConstraintsDocument)
                   ?? new ConstraintsDocument();
        }
        catch
        {
            return new ConstraintsDocument();
        }
    }

    public void Save(ConstraintsDocument doc)
    {
        AppPaths.EnsureDir();
        var json = JsonSerializer.Serialize(doc, AppJsonContext.Default.ConstraintsDocument);
        AtomicWrite.Write(AppPaths.ConstraintsFile, json);
    }
}
