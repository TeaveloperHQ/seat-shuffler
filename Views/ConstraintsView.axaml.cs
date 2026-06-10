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
        PriorityList.AddHandler(DragDrop.DragLeaveEvent, OnCardDragLeave);
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

    // 커서 아래 카드 본체(Tag="card") 컨트롤을 찾는다.
    private static Control? FindCardControl(object? source)
    {
        for (var v = source as Visual; v is not null; v = v.GetVisualParent())
            if (v is Control { Tag: "card" } c)
                return c;
        return null;
    }

    // 커서가 카드 위쪽 절반이면 앞에, 아래쪽이면 뒤에 삽입.
    private (ConstraintCardViewModel? Card, bool After) HitTest(DragEventArgs e)
    {
        var ctl = FindCardControl(e.Source);
        if (ctl?.DataContext is ConstraintCardViewModel vm)
            return (vm, e.GetPosition(ctl).Y > ctl.Bounds.Height / 2);
        return (null, false);
    }

    private async void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(PriorityList).Properties.IsLeftButtonPressed) return;
        if (!IsWithinHandle(e.Source)) return;          // 손잡이에서만 드래그 시작
        if (CardFrom(e.Source) is not { } card) return;

        var data = new DataObject();
        data.Set(DragFormat, card.Kind);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        (DataContext as ConstraintsViewModel)?.ClearDropIndicators();
    }

    private void OnCardDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;

        var vm = DataContext as ConstraintsViewModel;
        vm?.ClearDropIndicators();
        var (target, after) = HitTest(e);
        if (target is not null)
        {
            if (after) target.DropAfter = true;
            else target.DropBefore = true;
        }
    }

    private void OnCardDragLeave(object? sender, DragEventArgs e) =>
        (DataContext as ConstraintsViewModel)?.ClearDropIndicators();

    private void OnCardDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is ConstraintsViewModel vm && e.Data.Get(DragFormat) is ConstraintKind moved)
        {
            var (target, after) = HitTest(e);
            if (target is not null) vm.ReorderPriority(moved, target.Kind, after);
            vm.ClearDropIndicators();
        }
    }
}
