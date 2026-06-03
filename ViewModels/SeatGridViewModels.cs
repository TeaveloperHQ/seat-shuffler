using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using SeatShuffler.Models;

namespace SeatShuffler.ViewModels;

/// <summary>배정/기록 그리드의 좌석 1칸(읽기 전용 표시).</summary>
public sealed class SeatSlotViewModel
{
    public string Name { get; init; } = "";
    public string SubText { get; init; } = "";
    public bool IsEmpty { get; init; }
    public IBrush Background { get; init; } = Brushes.White;
    public IBrush Border { get; init; } = new SolidColorBrush(Color.Parse("#E0E0E0"));
    public Thickness Margin { get; init; }

    public static IBrush BrushFor(Gender g, bool empty) => empty
        ? new SolidColorBrush(Color.Parse("#F0F0F0"))
        : g switch
        {
            Gender.Male => new SolidColorBrush(Color.Parse("#DCECFB")),   // 연한 파랑
            Gender.Female => new SolidColorBrush(Color.Parse("#FBE0EC")), // 연한 분홍
            _ => Brushes.White,
        };
}

public sealed class SeatRowViewModel
{
    public List<SeatSlotViewModel> Seats { get; init; } = new();
}

public sealed class SeatSectionViewModel
{
    public int Index { get; init; }
    public string Title => $"{Index + 1}분단";
    public List<SeatRowViewModel> Rows { get; init; } = new();
}

/// <summary>config + 좌석→학생 조회로 분단/행/열 표시 VM을 만든다(배정·기록 공용).</summary>
public static class SeatGridBuilder
{
    public static List<SeatSectionViewModel> Build(
        SeatGridConfig config, System.Func<SeatPosition, Student?> lookup)
    {
        var sections = new List<SeatSectionViewModel>();
        for (int s = 0; s < config.Sections; s++)
        {
            var section = new SeatSectionViewModel { Index = s };
            for (int r = 0; r < config.Rows; r++)
            {
                var row = new SeatRowViewModel();
                for (int c = 0; c < config.Cols; c++)
                {
                    var pos = new SeatPosition(s, r, c);
                    var student = lookup(pos);
                    bool empty = student is null;
                    // 짝 사이 간격: 새 짝(짝수 열, 첫 열 제외) 왼쪽에 여백.
                    double left = (c > 0 && c % 2 == 0) ? 14 : 3;
                    row.Seats.Add(new SeatSlotViewModel
                    {
                        Name = empty ? "빈자리" : student!.Name,
                        SubText = empty ? "" : (string.IsNullOrWhiteSpace(student!.StudentNumber)
                            ? student.Gender.ToKorean()
                            : student.StudentNumber),
                        IsEmpty = empty,
                        Background = SeatSlotViewModel.BrushFor(student?.Gender ?? Gender.Unspecified, empty),
                        Margin = new Thickness(left, 3, 3, 3),
                    });
                }
                section.Rows.Add(row);
            }
            sections.Add(section);
        }
        return sections;
    }
}
