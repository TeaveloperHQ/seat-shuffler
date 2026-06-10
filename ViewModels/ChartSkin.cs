using System.Collections.Generic;
using Avalonia.Media;
using SeatShuffler.Models;

namespace SeatShuffler.ViewModels;

/// <summary>출력 좌석표 스킨(프리셋). 색만 바꾸며 앱 화면엔 영향 없음.</summary>
public sealed class ChartSkin
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    public IBrush PageBackground { get; init; } = Brushes.White;
    public IBrush TitleColor { get; init; } = Brushes.Black;
    public IBrush SubTitleColor { get; init; } = new SolidColorBrush(Color.Parse("#888888"));
    public IBrush BoardBackground { get; init; } = new SolidColorBrush(Color.Parse("#2E3B2E"));
    public IBrush BoardForeground { get; init; } = new SolidColorBrush(Color.Parse("#CFE8CF"));
    public IBrush SectionTitle { get; init; } = new SolidColorBrush(Color.Parse("#888888"));

    public IBrush MaleSeat { get; init; } = new SolidColorBrush(Color.Parse("#DCECFB"));
    public IBrush FemaleSeat { get; init; } = new SolidColorBrush(Color.Parse("#FBE0EC"));
    public IBrush NeutralSeat { get; init; } = Brushes.White;
    public IBrush VacantSeat { get; init; } = new SolidColorBrush(Color.Parse("#F0F0F0"));
    public IBrush SeatBorder { get; init; } = new SolidColorBrush(Color.Parse("#E0E0E0"));
    public IBrush SeatForeground { get; init; } = Brushes.Black;
    public IBrush SeatSubForeground { get; init; } = new SolidColorBrush(Color.Parse("#777777"));

    public IBrush SeatBrush(Gender g, bool vacant) => vacant ? VacantSeat : g switch
    {
        Gender.Male => MaleSeat,
        Gender.Female => FemaleSeat,
        _ => NeutralSeat,
    };

    private static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex));

    public static readonly IReadOnlyList<ChartSkin> Presets = new[]
    {
        new ChartSkin { Id = "basic", Name = "기본" },

        new ChartSkin
        {
            Id = "pastel", Name = "파스텔",
            PageBackground = B("#FFFDF7"), TitleColor = B("#6B5B73"),
            BoardBackground = B("#8FB996"), BoardForeground = B("#FFFFFF"),
            MaleSeat = B("#D7E8F5"), FemaleSeat = B("#F7DCE8"), NeutralSeat = B("#FFF4D6"),
            SeatBorder = B("#EADFD0"),
        },

        new ChartSkin
        {
            Id = "chalk", Name = "칠판",
            PageBackground = B("#22311F"), TitleColor = B("#F4F4E8"), SubTitleColor = B("#A9C5A0"),
            SectionTitle = B("#CFE0C7"),
            BoardBackground = B("#0F1A0E"), BoardForeground = B("#EAF3E4"),
            MaleSeat = B("#37506B"), FemaleSeat = B("#6B3750"), NeutralSeat = B("#3A4A37"),
            VacantSeat = B("#2A3A28"), SeatBorder = B("#4A5A45"),
            SeatForeground = B("#F4F4E8"), SeatSubForeground = B("#BFD0B7"),
        },

        new ChartSkin
        {
            Id = "sky", Name = "하늘",
            PageBackground = B("#EEF6FF"), TitleColor = B("#234E70"),
            BoardBackground = B("#3E6E9E"), BoardForeground = B("#EAF3FF"),
            MaleSeat = B("#CFE6FF"), FemaleSeat = B("#FBD9E8"), NeutralSeat = B("#FFFFFF"),
            SeatBorder = B("#CBE0F2"),
        },

        new ChartSkin
        {
            Id = "warm", Name = "노을",
            PageBackground = B("#FFF6EE"), TitleColor = B("#8A4B2F"),
            BoardBackground = B("#C56A3F"), BoardForeground = B("#FFF0E6"),
            MaleSeat = B("#FCE3CF"), FemaleSeat = B("#FAD2D2"), NeutralSeat = B("#FFF7E8"),
            SeatBorder = B("#F0D9C4"),
        },

        new ChartSkin
        {
            Id = "mint", Name = "민트",
            PageBackground = B("#F0FAF6"), TitleColor = B("#1F6B57"),
            BoardBackground = B("#3FA083"), BoardForeground = B("#EAFBF4"),
            MaleSeat = B("#D6EFF8"), FemaleSeat = B("#FBE2EC"), NeutralSeat = B("#E6F7EF"),
            SeatBorder = B("#CBE9DD"),
        },

        new ChartSkin
        {
            Id = "lavender", Name = "라벤더",
            PageBackground = B("#F7F4FC"), TitleColor = B("#5B4B86"),
            BoardBackground = B("#8169B0"), BoardForeground = B("#F2ECFB"),
            MaleSeat = B("#DCE3F7"), FemaleSeat = B("#F3DCF0"), NeutralSeat = B("#EEE8F8"),
            SeatBorder = B("#E0D6F0"),
        },

        new ChartSkin
        {
            Id = "mono", Name = "모노",
            PageBackground = B("#FFFFFF"), TitleColor = B("#222222"), SubTitleColor = B("#999999"),
            SectionTitle = B("#777777"),
            BoardBackground = B("#333333"), BoardForeground = B("#EEEEEE"),
            MaleSeat = B("#ECECEC"), FemaleSeat = B("#DDDDDD"), NeutralSeat = B("#F5F5F5"),
            SeatBorder = B("#CCCCCC"), SeatForeground = B("#222222"),
        },

        new ChartSkin
        {
            Id = "forest", Name = "숲",
            PageBackground = B("#F1F6EC"), TitleColor = B("#3B5A2E"),
            BoardBackground = B("#5C7A3F"), BoardForeground = B("#EEF6E4"),
            MaleSeat = B("#DCEAF0"), FemaleSeat = B("#F2E2D6"), NeutralSeat = B("#E7F0DC"),
            SeatBorder = B("#D3E2C4"),
        },
    };

    public static ChartSkin ById(string? id)
    {
        foreach (var s in Presets) if (s.Id == id) return s;
        return Presets[0];
    }
}
