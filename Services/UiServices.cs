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

    /// <summary>저장 위치를 물어본 뒤 <paramref name="write"/>로 내용을 쓰고, 저장된 로컬 경로를 돌려준다(취소 시 null).</summary>
    Task<string?> SaveSpreadsheetAsync(string suggestedFileName, Action<Stream> write);
}

/// <summary>좌석표를 그릴 때 필요한 옵션 묶음(미리보기와 인쇄가 같은 값을 쓴다).</summary>
public sealed record ChartRenderOptions(
    ChartSnapshot Snapshot, ChartSkin Skin, bool Flip,
    Bitmap? Background, Bitmap? MaleCell, Bitmap? FemaleCell,
    bool TransparentCells, bool ShowBoard, bool GenderColors, string? FontFamily);

public interface IExportService
{
    /// <summary>인쇄 대화상자를 띄워 좌석표를 인쇄한다. 보낸 프린터 이름(취소하면 null)을 돌려준다.</summary>
    string? PrintSeatChart(string title, ChartRenderOptions options);

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

    /// <summary>좌석표 제목 — 미리보기와 인쇄물이 같아야 하므로 한 곳에서 관리.</summary>
    public const string ChartTitle = "자리 배치표";

    /// <summary>글꼴을 고르지 않았을 때 쓰는 기본 글꼴(설치된 것 중 먼저 잡히는 순서).</summary>
    public static readonly FontFamily DefaultChartFont =
        new("굴림, Gulim, Malgun Gothic, Noto Sans CJK KR, Nanum Gothic, sans-serif");

    /// <summary>고른 글꼴 뒤에 한글 대체 글꼴을 붙인다 — 한글 자소가 없는 글꼴도 깨지지 않게.</summary>
    public static FontFamily ResolveChartFont(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? DefaultChartFont
            : new FontFamily($"{name}, Malgun Gothic, Noto Sans CJK KR, Nanum Gothic, 굴림, sans-serif");

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

    /// <summary>미리보기와 같은 함수로 좌석표 비주얼을 만들고 크기를 확정한다 → 화면과 인쇄물이 일치한다.</summary>
    private static (Control Visual, Size Size)? LayoutChart(string title, ChartRenderOptions o)
    {
        var sections = ChartBuilder.Build(o.Snapshot, o.Skin, o.Flip, o.MaleCell, o.FemaleCell,
            o.TransparentCells, o.GenderColors);
        var visual = BuildChart(title, sections, o.Skin, o.Flip, o.Background, o.ShowBoard, o.FontFamily);
        visual.Measure(Size.Infinity);
        visual.Arrange(new Rect(visual.DesiredSize));
        var size = visual.DesiredSize;
        return size.Width < 1 || size.Height < 1 ? null : (visual, size);
    }

    private static RenderTargetBitmap Render(Control visual, Size size, double scale)
    {
        var px = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(size.Width * scale)),
            Math.Max(1, (int)Math.Ceiling(size.Height * scale)));
        var rtb = new RenderTargetBitmap(px, new Vector(96 * scale, 96 * scale));
        rtb.Render(visual);
        return rtb;
    }

    public string? PrintSeatChart(string title, ChartRenderOptions options)
    {
        if (LayoutChart(title, options) is not { } layout)
            throw new InvalidOperationException("인쇄할 좌석표가 없습니다.");

        // 어느 프린터를 고를지는 대화상자에서 정해지므로, 해상도는 기본 프린터 용지를 기준으로 잡는다.
        using var rtb = Render(layout.Visual, layout.Size, PrintScale(layout.Size, WindowsPrinting.DefaultPrinter()));
        var owner = Owner?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero; // 대화상자를 앱 창에 모달로 띄운다
        return WindowsPrinting.Print(ToRgbOverWhite(rtb), rtb.PixelSize.Width, rtb.PixelSize.Height, title, owner);
    }

    /// <summary>
    /// 용지 여백 안쪽에 '딱 맞게' 넣었을 때의 인쇄 크기를 먼저 구하고,
    /// 그 크기에서 300dpi가 나오도록 렌더 배율을 정한다 → 늘리든 줄이든 흐려지지 않는다.
    /// </summary>
    private static double PrintScale(Size size, string? printer)
    {
        var (pageW, pageH) = WindowsPrinting.PaperSize(printer);
        double w = size.Width * PageLayout.DipToPt, h = size.Height * PageLayout.DipToPt;
        var rotated = PageLayout.PreferRotated(w, h, pageW, pageH);
        var fit = rotated
            ? PageLayout.FitScale(h, w, pageW, pageH, rotated: true)
            : PageLayout.FitScale(w, h, pageW, pageH, rotated: false);
        return Math.Clamp(fit * PageLayout.DipToPt * PageLayout.PrintDpi / 72.0, 1.0, 8.0);
    }

    /// <summary>프린터로 보낼 그림은 투명도를 쓰지 않으므로 흰 종이 위에 합성해 RGB 3바이트로 편다.</summary>
    private static unsafe byte[] ToRgbOverWhite(RenderTargetBitmap bitmap)
    {
        int w = bitmap.PixelSize.Width, h = bitmap.PixelSize.Height, stride = w * 4;
        if ((long)stride * h > int.MaxValue) throw new InvalidOperationException("좌석표가 너무 커서 인쇄할 수 없습니다.");

        var bgra = new byte[stride * h];
        fixed (byte* p = bgra)
            bitmap.CopyPixels(new PixelRect(0, 0, w, h), (IntPtr)p, bgra.Length, stride);

        // 렌더 타깃은 BGRA·미리 곱해진 알파가 기본이지만, 플랫폼에 따라 다를 수 있어 실제 값을 본다.
        var rgbaOrder = bitmap.Format == Avalonia.Platform.PixelFormat.Rgba8888;
        var premultiplied = bitmap.AlphaFormat != Avalonia.Platform.AlphaFormat.Unpremul;

        var rgb = new byte[w * h * 3];
        for (int i = 0, o = 0; i < bgra.Length; i += 4, o += 3)
        {
            byte c0 = bgra[i], c1 = bgra[i + 1], c2 = bgra[i + 2], a = bgra[i + 3];
            byte r = rgbaOrder ? c0 : c2, g = c1, b = rgbaOrder ? c2 : c0;
            if (a != 255)
            {
                var bg = 255 - a;                                    // 흰 배경이 비치는 정도
                if (premultiplied)
                {
                    r = (byte)Math.Min(255, r + bg); g = (byte)Math.Min(255, g + bg); b = (byte)Math.Min(255, b + bg);
                }
                else
                {
                    r = (byte)((r * a + 255 * bg) / 255);
                    g = (byte)((g * a + 255 * bg) / 255);
                    b = (byte)((b * a + 255 * bg) / 255);
                }
            }
            rgb[o] = r; rgb[o + 1] = g; rgb[o + 2] = b;
        }
        return rgb;
    }

    public static Control BuildChart(string title, IReadOnlyList<SeatSectionViewModel> sections,
        ChartSkin skin, bool flip, Bitmap? background = null, bool showBoard = true, string? fontFamily = null)
    {
        var root = new StackPanel
        {
            Background = background is null ? skin.PageBackground : Brushes.Transparent,
            Margin = new Thickness(28),
            Spacing = 8,
        };
        Avalonia.Controls.Documents.TextElement.SetFontFamily(root, ResolveChartFont(fontFamily)); // 자식 텍스트에 상속
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

    public async Task<string?> SaveSpreadsheetAsync(string suggestedFileName, Action<Stream> write)
    {
        var sp = Owner?.StorageProvider;
        if (sp is null) return null;

        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "엑셀 양식 저장",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "xlsx",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("엑셀 통합 문서") { Patterns = new[] { "*.xlsx" } },
            },
        });
        if (file is null) return null;

        await using (var stream = await file.OpenWriteAsync())
        {
            if (stream.CanSeek) stream.SetLength(0); // 기존 파일 덮어쓸 때 잔여 바이트 제거
            write(stream);
        }
        return file.TryGetLocalPath();
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
