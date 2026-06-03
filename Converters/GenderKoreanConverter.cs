using System;
using System.Globalization;
using Avalonia.Data.Converters;
using SeatShuffler.Models;

namespace SeatShuffler.Converters;

/// <summary>Gender enum ↔ 한글 표기(남/녀/미지정).</summary>
public sealed class GenderKoreanConverter : IValueConverter
{
    public static readonly GenderKoreanConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Gender g ? g.ToKorean() : "미지정";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            "남" => Gender.Male,
            "녀" => Gender.Female,
            _ => Gender.Unspecified,
        };
}
