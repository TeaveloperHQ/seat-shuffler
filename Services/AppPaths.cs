using System;
using System.IO;

namespace SeatShuffler.Services;

/// <summary>사용자별 앱 데이터 경로. 윈도우 %APPDATA%, 리눅스 ~/.config.</summary>
public static class AppPaths
{
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SeatShuffler");

    public static string RosterFile => Path.Combine(DataDir, "roster.json");
    public static string HistoryFile => Path.Combine(DataDir, "history.json");
    public static string ConstraintsFile => Path.Combine(DataDir, "constraints.json");

    public static void EnsureDir() => Directory.CreateDirectory(DataDir);
}
