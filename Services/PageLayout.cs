using System;

namespace SeatShuffler.Services;

/// <summary>
/// 인쇄 한 장의 지면 규격(A4 가로)과 여백. 미리보기·실제 인쇄가 같은 값을 써야
/// "보이는 대로 인쇄"가 성립하므로 여기 한 곳에서만 정한다.
/// </summary>
public static class PageLayout
{
    /// <summary>A4 가로 페이지 크기(pt, 1pt = 1/72인치). 210×297mm를 눕힌 값.</summary>
    public const double PageWidthPt = 841.89;
    public const double PageHeightPt = 595.28;

    /// <summary>좌우 여백(pt ≈ 17mm). 넉넉히 둬야 인쇄물이 답답해 보이지 않고 철할 자리도 남는다.</summary>
    public const double MarginXPt = 48;

    /// <summary>위아래 여백(pt ≈ 9mm). 가로 좌석표는 세로가 빠듯해서 좌우보다 좁게 둔다.</summary>
    public const double MarginYPt = 26;

    /// <summary>프린터로 보낼 그림의 해상도 — 인쇄 선명도를 좌우한다.</summary>
    public const double PrintDpi = 300;

    /// <summary>화면 좌표(1/96인치)를 지면 좌표(1/72인치)로.</summary>
    public const double DipToPt = 72.0 / 96.0;

    /// <summary>내용 크기(pt)를 여백 안쪽에 '딱 맞게' 넣기 위한 배율(작으면 키우고 크면 줄인다).</summary>
    public static double FitScale(double widthPt, double heightPt) =>
        FitScale(widthPt, heightPt, PageWidthPt, PageHeightPt, rotated: false);

    /// <summary>용지 크기를 직접 줄 때의 맞춤 배율. 돌려 넣으면 좌우/위아래 여백도 같이 돈다.</summary>
    public static double FitScale(double widthPt, double heightPt, double pageWidthPt, double pageHeightPt, bool rotated)
    {
        if (widthPt <= 0 || heightPt <= 0) return 1;
        double mx = rotated ? MarginYPt : MarginXPt, my = rotated ? MarginXPt : MarginYPt;
        return Math.Min(
            Math.Max(1, pageWidthPt - 2 * mx) / widthPt,
            Math.Max(1, pageHeightPt - 2 * my) / heightPt);
    }

    /// <summary>좌석표를 90° 돌려 넣는 쪽이 더 크게 나오는가(세로 용지에 가로 좌석표를 넣는 경우).</summary>
    public static bool PreferRotated(double contentWidth, double contentHeight, double pageWidthPt, double pageHeightPt) =>
        FitScale(contentHeight, contentWidth, pageWidthPt, pageHeightPt, rotated: true) >
        FitScale(contentWidth, contentHeight, pageWidthPt, pageHeightPt, rotated: false);
}
