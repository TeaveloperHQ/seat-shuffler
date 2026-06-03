using System.Collections.ObjectModel;
using SeatShuffler.Models;

namespace SeatShuffler.Services;

/// <summary>
/// 탭 간 공유되는 앱 상태(명단 + 기록 + 제약/설정). DI 컨테이너 대신 App에서 1개
/// 생성해 자식 ViewModel에 생성자 주입한다.
/// </summary>
public sealed class AppState
{
    private readonly RosterStore _rosterStore = new();
    private readonly HistoryStore _historyStore = new();
    private readonly ConstraintsStore _constraintsStore = new();

    public ObservableCollection<Student> Roster { get; } = new();
    public ObservableCollection<ConfirmedRecord> History { get; } = new();
    public SeatConstraints Constraints { get; }
    public AssignmentSettings Settings { get; }

    public AppState()
    {
        foreach (var s in _rosterStore.Load())
            Roster.Add(s);
        foreach (var r in _historyStore.Load())
            History.Add(r);

        var doc = _constraintsStore.Load();
        Constraints = doc.Constraints;
        Settings = doc.Settings;
    }

    public void SaveRoster() => _rosterStore.Save(Roster);
    public void SaveHistory() => _historyStore.Save(History);

    public void SaveConstraints() =>
        _constraintsStore.Save(new ConstraintsDocument { Constraints = Constraints, Settings = Settings });

    public void Save()
    {
        SaveRoster();
        SaveHistory();
        SaveConstraints();
    }
}
