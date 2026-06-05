using CommunityToolkit.Mvvm.ComponentModel;
using SeatShuffler.Services;

namespace SeatShuffler.ViewModels;

/// <summary>3탭(명단·배정·기록)을 호스팅하는 셸. 공유 AppState를 자식 VM에 주입.</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppState _state;

    /// <summary>클립보드/파일선택용. View가 attach될 때 Owner(TopLevel)를 주입한다.</summary>
    public UiServices Ui { get; }

    public RosterViewModel Roster { get; }
    public SeatSetupViewModel SeatSetup { get; }
    public ConstraintsViewModel Constraints { get; }
    public AssignmentViewModel Assignment { get; }
    public HistoryViewModel History { get; }
    public InfoViewModel Info { get; }

    // 탭 순서: 명단(0) · 좌석 설정(1) · 제약(2) · 배정(3) · 기록(4) · 정보(5)
    private const int SeatSetupTabIndex = 1;
    private const int ConstraintsTabIndex = 2;
    private const int AssignmentTabIndex = 3;

    [ObservableProperty] private int _selectedTabIndex;

    partial void OnSelectedTabIndexChanged(int value)
    {
        // 제약 탭을 벗어나면 즉시 다시 잠근다(학생 앞 노출 방지).
        if (value != ConstraintsTabIndex)
            Constraints.Lock();
        // 좌석 설정/배정 진입 시 공유 설정을 다시 반영.
        if (value == SeatSetupTabIndex)
            SeatSetup.Refresh();
        else if (value == AssignmentTabIndex)
            Assignment.Refresh();
    }

    public MainWindowViewModel(AppState state)
    {
        _state = state;
        Ui = new UiServices();
        Roster = new RosterViewModel(state, Ui, Ui);
        SeatSetup = new SeatSetupViewModel(state);
        Constraints = new ConstraintsViewModel(state);
        Assignment = new AssignmentViewModel(state, new SeatAssignmentService());
        History = new HistoryViewModel(state);
        Info = new InfoViewModel(state, Ui);
    }

    // 디자인타임용
    public MainWindowViewModel() : this(new AppState()) { }

    public void Save() => _state.Save();
}
