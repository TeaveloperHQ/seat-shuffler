using System.Collections.ObjectModel;
using SeatShuffler.Models;

namespace SeatShuffler.Services;

/// <summary>
/// 탭 간 공유되는 앱 상태(명단 + 기록). DI 컨테이너 대신 App에서 1개 생성해
/// 자식 ViewModel에 생성자 주입한다.
/// </summary>
public sealed class AppState
{
    private readonly RosterStore _rosterStore = new();
    private readonly HistoryStore _historyStore = new();

    public ObservableCollection<Student> Roster { get; } = new();
    public ObservableCollection<ConfirmedRecord> History { get; } = new();

    public AppState()
    {
        foreach (var s in _rosterStore.Load())
            Roster.Add(s);
        foreach (var r in _historyStore.Load())
            History.Add(r);
    }

    public void SaveRoster() => _rosterStore.Save(Roster);
    public void SaveHistory() => _historyStore.Save(History);

    public void Save()
    {
        SaveRoster();
        SaveHistory();
    }
}
