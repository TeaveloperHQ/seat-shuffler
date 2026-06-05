using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SeatShuffler.Models;
using SeatShuffler.ViewModels;

namespace SeatShuffler.Views;

public partial class ConstraintsView : UserControl
{
    private const string DragFormat = "seatshuffler-constraint-kind";

    public ConstraintsView()
    {
        InitializeComponent();

        // 제약 카드 우선순위: 손잡이(≡)를 잡고 드래그해 재정렬.
        PriorityList.AddHandler(PointerPressedEvent, OnCardPointerPressed, RoutingStrategies.Tunnel);
        PriorityList.AddHandler(DragDrop.DragOverEvent, OnCardDragOver);
        PriorityList.AddHandler(DragDrop.DropEvent, OnCardDrop);
    }

    private static ConstraintCardViewModel? CardFrom(object? source) =>
        (source as StyledElement)?.DataContext as ConstraintCardViewModel;

    // 눌린 지점이 드래그 손잡이(Tag="draghandle") 안인지 확인.
    private static bool IsWithinHandle(object? source)
    {
        for (var v = source as Visual; v is not null; v = v.GetVisualParent())
            if (v is Control { Tag: "draghandle" })
                return true;
        return false;
    }

    private async void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(PriorityList).Properties.IsLeftButtonPressed) return;
        if (!IsWithinHandle(e.Source)) return;          // 손잡이에서만 드래그 시작
        if (CardFrom(e.Source) is not { } card) return;

        var data = new DataObject();
        data.Set(DragFormat, card.Kind);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
    }

    private void OnCardDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void OnCardDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not ConstraintsViewModel vm) return;
        if (e.Data.Get(DragFormat) is not ConstraintKind moved) return;
        if (CardFrom(e.Source) is not { } target) return;

        vm.ReorderPriority(moved, target.Kind);
    }
}
