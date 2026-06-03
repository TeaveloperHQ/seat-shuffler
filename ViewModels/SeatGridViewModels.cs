using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using SeatShuffler.Models;

namespace SeatShuffler.ViewModels;

/// <summary>배정/기록 그리드의 좌석 1칸(표시 + 선택적 클릭).</summary>
public sealed class SeatSlotViewModel
{
    public SeatPosition Position { get; init; }
    public string Name { get; init; } = "";
    public string SubText { get; init; } = "";
    public bool IsBlocked { get; init; }     // 비워둔(배정 제외) 좌석
    public IBrush Background { get; init; } = Brushes.White;
    public IBrush Border { get; init; } = new SolidColorBrush(Color.Parse("#E0E0E0"));
    public IBrush Foreground { get; init; } = Brushes.Black;
    public Thickness Margin { get; init; }
    public ICommand? ToggleCommand { get; init; }

    public static IBrush BrushFor(Gender g, bool empty) => empty
        ? new SolidColorBrush(Color.Parse("#F0F0F0"))
        : g switch
        {
            Gender.Male => new SolidColorBrush(Color.Parse("#DCECFB")),
            Gender.Female => new SolidColorBrush(Color.Parse("#FBE0EC")),
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
    private static readonly IBrush BlockedBg = new SolidColorBrush(Color.Parse("#3A3A3A"));
    private static readonly IBrush BlockedFg = new SolidColorBrush(Color.Parse("#CCCCCC"));

    public static List<SeatSectionViewModel> Build(
        SeatGridConfig config,
        Func<SeatPosition, Student?> lookup,
        IReadOnlySet<SeatPosition>? emptySeats = null,
        ICommand? toggleCommand = null)
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
                    double left = (c > 0 && c % 2 == 0) ? 14 : 3;
                    var margin = new Thickness(left, 3, 3, 3);

                    if (emptySeats is not null && emptySeats.Contains(pos))
                    {
                        row.Seats.Add(new SeatSlotViewModel
                        {
                            Position = pos, Name = "비움", SubText = "✕", IsBlocked = true,
                            Background = BlockedBg, Foreground = BlockedFg,
                            Border = BlockedBg, Margin = margin, ToggleCommand = toggleCommand,
                        });
                        continue;
                    }

                    var student = lookup(pos);
                    bool vacant = student is null;
                    row.Seats.Add(new SeatSlotViewModel
                    {
                        Position = pos,
                        Name = vacant ? "빈자리" : student!.Name,
                        SubText = vacant ? "" : (string.IsNullOrWhiteSpace(student!.StudentNumber)
                            ? student.Gender.ToKorean()
                            : student.StudentNumber),
                        Background = SeatSlotViewModel.BrushFor(student?.Gender ?? Gender.Unspecified, vacant),
                        Foreground = vacant ? new SolidColorBrush(Color.Parse("#AAAAAA")) : Brushes.Black,
                        Margin = margin,
                        ToggleCommand = toggleCommand,
                    });
                }
                section.Rows.Add(row);
            }
            sections.Add(section);
        }
        return sections;
    }
}
