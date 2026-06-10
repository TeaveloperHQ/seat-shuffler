using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
    public Thickness BorderThickness { get; init; } = new(1);
    public ICommand? ToggleCommand { get; init; }

    /// <summary>커스텀 셀 이미지(있으면 카드 배경 대신 사용). 꾸미기 출력 전용.</summary>
    public Bitmap? CellImage { get; init; }
    public bool HasCellImage => CellImage is not null;

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
    private static readonly IBrush DefaultBorder = new SolidColorBrush(Color.Parse("#E0E0E0"));

    private static readonly IBrush SelectedBorder = new SolidColorBrush(Color.Parse("#2D7DF6"));

    public static List<SeatSectionViewModel> Build(
        SeatGridConfig config,
        Func<SeatPosition, Student?> lookup,
        IReadOnlySet<SeatPosition>? emptySeats = null,
        ICommand? toggleCommand = null,
        IReadOnlyDictionary<SeatPosition, Gender>? genderSeats = null,
        SeatPosition? selected = null)
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
                    bool sel = selected.HasValue && selected.Value.Equals(pos);
                    var selThickness = sel ? new Thickness(3) : new Thickness(1);

                    if (emptySeats is not null && emptySeats.Contains(pos))
                    {
                        row.Seats.Add(new SeatSlotViewModel
                        {
                            Position = pos, Name = "비움", SubText = "✕", IsBlocked = true,
                            Background = BlockedBg, Foreground = BlockedFg,
                            Border = sel ? SelectedBorder : BlockedBg, BorderThickness = selThickness,
                            Margin = margin, ToggleCommand = toggleCommand,
                        });
                        continue;
                    }

                    var student = lookup(pos);
                    Gender? zone = null;
                    if (genderSeats is not null && genderSeats.TryGetValue(pos, out var zg)) zone = zg;

                    if (student is not null)
                    {
                        row.Seats.Add(new SeatSlotViewModel
                        {
                            Position = pos,
                            Name = student.Name,
                            SubText = string.IsNullOrWhiteSpace(student.StudentNumber)
                                ? student.Gender.ToKorean() : student.StudentNumber,
                            Background = SeatSlotViewModel.BrushFor(student.Gender, false),
                            Border = sel ? SelectedBorder
                                : (zone is null ? DefaultBorder : SeatSlotViewModel.BrushFor(zone.Value, false)),
                            BorderThickness = selThickness,
                            Foreground = Brushes.Black,
                            Margin = margin,
                            ToggleCommand = toggleCommand,
                        });
                        continue;
                    }

                    // 빈 좌석 — 남녀 자리 지정이면 존으로 표시.
                    row.Seats.Add(new SeatSlotViewModel
                    {
                        Position = pos,
                        Name = zone switch { Gender.Male => "남자리", Gender.Female => "여자리", _ => "빈자리" },
                        SubText = "",
                        Background = SeatSlotViewModel.BrushFor(zone ?? Gender.Unspecified, zone is null),
                        Border = sel ? SelectedBorder : DefaultBorder,
                        BorderThickness = selThickness,
                        Foreground = zone is null ? new SolidColorBrush(Color.Parse("#AAAAAA")) : new SolidColorBrush(Color.Parse("#555555")),
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
