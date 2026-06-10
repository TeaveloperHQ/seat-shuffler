using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using SeatShuffler.Models;

namespace SeatShuffler.ViewModels;

/// <summary>출력/미리보기용 좌석표 빌더: 스냅샷 + 스킨 + 교탁반전 + 셀 이미지 → 표시 VM.</summary>
public static class ChartBuilder
{
    public static List<SeatSectionViewModel> Build(ChartSnapshot snap, ChartSkin skin, bool flip,
        Bitmap? maleCell = null, Bitmap? femaleCell = null)
    {
        var cfg = snap.Config;
        var byPos = new Dictionary<(int, int, int), ChartSeat>();
        foreach (var s in snap.Seats) byPos[(s.Section, s.Row, s.Col)] = s;

        IEnumerable<int> Order(int n) => flip ? Enumerable.Range(0, n).Reverse() : Enumerable.Range(0, n);

        var sections = new List<SeatSectionViewModel>();
        foreach (var s in Order(cfg.Sections))
        {
            var section = new SeatSectionViewModel { Index = s }; // 실제 분단 번호 유지
            foreach (var r in Order(cfg.Rows))
            {
                var row = new SeatRowViewModel();
                int displayIdx = 0;
                foreach (var c in Order(cfg.Cols))
                {
                    var pos = new SeatPosition(s, r, c);
                    double left = (displayIdx > 0 && displayIdx % 2 == 0) ? 14 : 3;
                    displayIdx++;
                    var margin = new Thickness(left, 3, 3, 3);

                    if (snap.Empties.Contains(pos))
                    {
                        row.Seats.Add(new SeatSlotViewModel
                        {
                            Position = pos, Name = "비움", SubText = "✕", IsBlocked = true,
                            Background = skin.VacantSeat, Foreground = skin.SeatSubForeground,
                            Border = skin.SeatBorder, Margin = margin,
                        });
                        continue;
                    }

                    Gender? zone = snap.GenderSeats.TryGetValue(pos, out var zg) ? zg : null;
                    if (byPos.TryGetValue((s, r, c), out var seat))
                    {
                        var cell = seat.Gender switch
                        {
                            Gender.Male => maleCell,
                            Gender.Female => femaleCell,
                            _ => null,
                        };
                        row.Seats.Add(new SeatSlotViewModel
                        {
                            Position = pos, Name = seat.Name, SubText = seat.SubText,
                            Background = skin.SeatBrush(seat.Gender, false),
                            Border = skin.SeatBorder, Foreground = skin.SeatForeground,
                            Margin = margin, CellImage = cell,
                        });
                    }
                    else
                    {
                        row.Seats.Add(new SeatSlotViewModel
                        {
                            Position = pos,
                            Name = zone switch { Gender.Male => "남자리", Gender.Female => "여자리", _ => "빈자리" },
                            SubText = "",
                            Background = zone is null ? skin.VacantSeat : skin.SeatBrush(zone.Value, false),
                            Border = skin.SeatBorder, Foreground = skin.SeatSubForeground,
                            Margin = margin,
                        });
                    }
                }
                section.Rows.Add(row);
            }
            sections.Add(section);
        }
        return sections;
    }
}
