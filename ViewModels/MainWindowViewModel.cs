using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeatShuffler.Models;

namespace SeatShuffler.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly char[] Separators = { '\n', '\r', ',', '\t' };
    private readonly Random _random = new();

    [ObservableProperty]
    private string _namesText =
        "김민준\n이서연\n박도윤\n최지우\n정하준\n강서아\n조시우\n윤하은\n장지호\n임수아\n한예준\n오지민";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Columns))]
    private decimal _columnCount = 4;

    [ObservableProperty]
    private string _status = "이름을 입력하고 '자리 섞기'를 누르세요.";

    /// <summary>UniformGrid 열 수 (정수). ColumnCount 변경 시 함께 갱신.</summary>
    public int Columns => Math.Max(1, (int)ColumnCount);

    public ObservableCollection<Seat> Seats { get; } = new();

    [RelayCommand]
    private void Shuffle()
    {
        var names = NamesText
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .ToList();

        if (names.Count == 0)
        {
            Status = "학생 이름이 없습니다.";
            Seats.Clear();
            return;
        }

        // Fisher-Yates 셔플
        for (int i = names.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (names[i], names[j]) = (names[j], names[i]);
        }

        Seats.Clear();
        for (int i = 0; i < names.Count; i++)
        {
            Seats.Add(new Seat { Number = i + 1, Name = names[i] });
        }

        Status = $"{names.Count}명 배치 완료 · {Columns}열";
    }
}
