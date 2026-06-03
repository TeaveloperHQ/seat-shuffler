using System.IO;

namespace SeatShuffler.Services;

/// <summary>임시 파일에 쓰고 교체하는 원자적 쓰기. 쓰기 중 중단 시 원본 보호.</summary>
public static class AtomicWrite
{
    public static void Write(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}
