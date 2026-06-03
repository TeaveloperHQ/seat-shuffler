using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SeatShuffler.Services;
using SeatShuffler.ViewModels;
using SeatShuffler.Views;

namespace SeatShuffler;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 공유 상태 1개 생성(명단·기록 로드) → 자식 VM에 주입.
            var state = new AppState();
            var vm = new MainWindowViewModel(state);

            desktop.MainWindow = new MainWindow { DataContext = vm };
            desktop.ShutdownRequested += (_, _) => state.Save();
        }

        base.OnFrameworkInitializationCompleted();
    }
}