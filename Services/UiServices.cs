using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SeatShuffler.Models;
using SeatShuffler.ViewModels;

namespace SeatShuffler.Services;

public interface IClipboardService
{
    Task<string?> GetTextAsync();
}

public interface IFolderService
{
    Task OpenFolderAsync(string path);
}

public sealed record PickedFile(Stream Stream, string Name);

public interface IDialogService
{
    Task<PickedFile?> PickSpreadsheetAsync();
}

public interface IExportService
{
    /// <summary>좌석표를 스킨·교탁반전·배경/셀 이미지 적용해 PNG로 저장하고 기본 뷰어로 연다.</summary>
    Task ExportSeatChartAsync(string title, ChartSnapshot snapshot, ChartSkin skin, bool flip,
        Bitmap? background, Bitmap? maleCell, Bitmap? femaleCell, bool transparentCells, bool showBoard);

    /// <summary>커스텀 배경 이미지 파일을 선택해 로컬 경로를 돌려준다.</summary>
    Task<string?> PickImageAsync();
}

/// <summary>
/// 클립보드/파일선택은 TopLevel이 필요하므로, View가 attach된 뒤
/// <see cref="Owner"/>를 주입한다. ViewModel은 인터페이스만 의존(MVVM 유지).
/// </summary>
public sealed class UiServices : IClipboardService, IDialogService, IFolderService, IExportService
{
    public TopLevel? Owner { get; set; }

    private static readonly FontFamily ChartFont =
        new("굴림, Gulim, Malgun Gothic, Noto Sans CJK KR, Nanum Gothic, sans-serif");

    public async Task<string?> PickImageAsync()
    {
        var sp = Owner?.StorageProvider;
        if (sp is null) return null;
        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "배경 이미지 선택",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("이미지") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp" } },
                new("모든 파일") { Patterns = new[] { "*" } },
            },
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task ExportSeatChartAsync(string title, ChartSnapshot snapshot, ChartSkin skin, bool flip,
        Bitmap? background, Bitmap? maleCell, Bitmap? femaleCell, bool transparentCells, bool showBoard)
    {
        var sp = Owner?.StorageProvider;
        if (sp is null) return;

        // 스킨·교탁반전·배경·셀 이미지 적용한 좌석표를 별도 비주얼로 구성해 렌더.
        var sections = ChartBuilder.Build(snapshot, skin, flip, maleCell, femaleCell, transparentCells);
        var visual = BuildChart(title, sections, skin, flip, background, showBoard);
        visual.Measure(Size.Infinity);
        visual.Arrange(new Rect(visual.DesiredSize));
        var size = visual.DesiredSize;
        if (size.Width < 1 || size.Height < 1) return;

        const double scale = 2.0; // 인쇄용 선명도
        var px = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(size.Width * scale)),
            Math.Max(1, (int)Math.Ceiling(size.Height * scale)));
        using var rtb = new RenderTargetBitmap(px, new Vector(96 * scale, 96 * scale));
        rtb.Render(visual);

        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "자리표 PNG 저장",
            SuggestedFileName = $"자리배치_{DateTime.Now:yyyyMMdd_HHmm}.png",
            DefaultExtension = "png",
            FileTypeChoices = new[] { new FilePickerFileType("PNG 이미지") { Patterns = new[] { "*.png" } } },
        });
        if (file is null) return;

        await using (var stream = await file.OpenWriteAsync())
            rtb.Save(stream);

        var path = file.TryGetLocalPath();
        if (path is not null && Owner?.Launcher is { } launcher)
            await launcher.LaunchFileInfoAsync(new FileInfo(path));
    }

    public static Control BuildChart(string title, IReadOnlyList<SeatSectionViewModel> sections,
        ChartSkin skin, bool flip, Bitmap? background = null, bool showBoard = true)
    {
        var root = new StackPanel
        {
            Background = background is null ? skin.PageBackground : Brushes.Transparent,
            Margin = new Thickness(28),
            Spacing = 8,
        };
        Avalonia.Controls.Documents.TextElement.SetFontFamily(root, ChartFont); // 자식 텍스트에 상속
        root.Children.Add(new Border { Height = 1 }); // 첫 자식 렌더 누락 회피용 스페이서
        root.Children.Add(new TextBlock
        {
            Text = title, FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = skin.TitleColor,
        });
        root.Children.Add(new TextBlock
        {
            Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm") + (flip ? "  ·  교탁 기준" : ""),
            Foreground = skin.SubTitleColor, FontSize = 12, Margin = new Thickness(0, 0, 0, 6),
        });

        Border Board() => new()
        {
            Background = skin.BoardBackground,
            CornerRadius = new CornerRadius(4), Padding = new Thickness(6), Width = 240,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10),
            Child = new TextBlock
            {
                Text = "칠판 (앞)", Foreground = skin.BoardForeground,
                HorizontalAlignment = HorizontalAlignment.Center, FontSize = 12,
            },
        };
        if (showBoard && !flip) root.Children.Add(Board());   // 정방향: 칠판 위, 교탁반전: 칠판 아래

        var secPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var sec in sections)
        {
            var col = new StackPanel { Margin = new Thickness(10, 0) };
            col.Children.Add(new TextBlock
            {
                Text = sec.Title, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = skin.SectionTitle, FontSize = 12, Margin = new Thickness(0, 0, 0, 4),
            });
            foreach (var row in sec.Rows)
            {
                var rp = new StackPanel { Orientation = Orientation.Horizontal };
                foreach (var seat in row.Seats)
                {
                    var text = new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock { Text = seat.Name, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = seat.Foreground, HorizontalAlignment = HorizontalAlignment.Center },
                            new TextBlock { Text = seat.SubText, FontSize = 10, Foreground = seat.Foreground, HorizontalAlignment = HorizontalAlignment.Center },
                        },
                    };
                    Control content = text;
                    if (seat.CellImage is { } cell)
                    {
                        var cg = new Grid();
                        cg.Children.Add(new Image { Source = cell, Stretch = seat.CellStretch });
                        cg.Children.Add(text);
                        content = cg;
                    }
                    rp.Children.Add(new Border
                    {
                        Background = seat.Background, BorderBrush = seat.Border,
                        BorderThickness = seat.BorderThickness, CornerRadius = new CornerRadius(8),
                        Margin = seat.Margin, Width = 92, Height = 58, ClipToBounds = seat.CellClip,
                        Child = content,
                    });
                }
                col.Children.Add(rp);
            }
            secPanel.Children.Add(col);
        }
        root.Children.Add(secPanel);
        if (showBoard && flip) root.Children.Add(Board()); // 교탁반전: 칠판을 아래쪽에

        if (background is null) return root;

        // 커스텀 배경 위에 좌석표 합성.
        var grid = new Grid();
        grid.Children.Add(new Image { Source = background, Stretch = Avalonia.Media.Stretch.UniformToFill });
        grid.Children.Add(root);
        return grid;
    }

    public async Task OpenFolderAsync(string path)
    {
        var launcher = Owner?.Launcher;
        if (launcher is null) return;
        Directory.CreateDirectory(path);
        await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path));
    }

    public async Task<string?> GetTextAsync()
    {
        var clip = Owner?.Clipboard;
        if (clip is null) return null;
        return await clip.GetTextAsync();
    }

    public async Task<PickedFile?> PickSpreadsheetAsync()
    {
        var sp = Owner?.StorageProvider;
        if (sp is null) return null;

        var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "엑셀/CSV 파일 선택",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("스프레드시트") { Patterns = new[] { "*.xlsx", "*.csv" } },
                new("모든 파일") { Patterns = new[] { "*" } },
            },
        });

        var file = files.Count > 0 ? files[0] : null;
        if (file is null) return null;
        var stream = await file.OpenReadAsync();
        return new PickedFile(stream, file.Name);
    }
}
