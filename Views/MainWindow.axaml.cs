using Avalonia.Controls;
using SeatShuffler.ViewModels;

namespace SeatShuffler.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);
        // 클립보드/파일선택은 TopLevel이 필요 → attach 후 주입.
        if (DataContext is MainWindowViewModel vm)
            vm.Ui.Owner = this;
    }

    protected override void OnClosed(System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.Save();
        base.OnClosed(e);
    }
}
