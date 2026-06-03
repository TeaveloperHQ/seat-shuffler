using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace SeatShuffler.Services;

public interface IClipboardService
{
    Task<string?> GetTextAsync();
}

public sealed record PickedFile(Stream Stream, string Name);

public interface IDialogService
{
    Task<PickedFile?> PickSpreadsheetAsync();
}

/// <summary>
/// 클립보드/파일선택은 TopLevel이 필요하므로, View가 attach된 뒤
/// <see cref="Owner"/>를 주입한다. ViewModel은 인터페이스만 의존(MVVM 유지).
/// </summary>
public sealed class UiServices : IClipboardService, IDialogService
{
    public TopLevel? Owner { get; set; }

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
