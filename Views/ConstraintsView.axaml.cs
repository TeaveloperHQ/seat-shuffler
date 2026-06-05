using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SeatShuffler.Models;
using SeatShuffler.ViewModels;

namespace SeatShuffler.Views;

public partial class ConstraintsView : UserControl
{
    private const string DragFormat = "seatshuffler-constraint-kind";

    public ConstraintsView()
    {
        InitializeComponent();

        // 우선순위 카드 드래그 앤 드롭 재정렬.
        PriorityList.AddHandler(PointerPressedEvent, OnPriorityPointerPressed, RoutingStrategies.Tunnel);
        PriorityList.AddHandler(DragDrop.DragOverEvent, OnPriorityDragOver);
        PriorityList.AddHandler(DragDrop.DropEvent, OnPriorityDrop);
    }

    private static PriorityRow? RowFrom(object? source) =>
        (source as StyledElement)?.DataContext as PriorityRow;

    private async void OnPriorityPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (RowFrom(e.Source) is not { } row) return;
        if (!e.GetCurrentPoint(PriorityList).Properties.IsLeftButtonPressed) return;

        var data = new DataObject();
        data.Set(DragFormat, row.Kind);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
    }

    private void OnPriorityDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void OnPriorityDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not ConstraintsViewModel vm) return;
        if (e.Data.Get(DragFormat) is not ConstraintKind moved) return;
        if (RowFrom(e.Source) is not { } target) return;

        vm.ReorderPriority(moved, target.Kind);
    }
}
