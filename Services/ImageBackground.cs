using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SeatShuffler.Services;

/// <summary>셀 이미지의 단색 배경(모서리 색)을 투명화 — 불투명 이미지 누끼용.</summary>
public static class ImageBackground
{
    private const int Tolerance = 48; // 채널별 허용 오차

    /// <summary>
    /// 좌상단 픽셀이 불투명한 단색이면 그 색과 유사한 불투명 픽셀을 투명화한다.
    /// 이미 투명한 PNG(모서리 alpha 낮음)는 그대로 둔다.
    /// </summary>
    public static unsafe Bitmap RemoveCornerBackground(Bitmap src)
    {
        var size = src.PixelSize;
        if (size.Width <= 0 || size.Height <= 0) return src;

        var wb = new WriteableBitmap(size, src.Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var fb = wb.Lock();

        int stride = fb.RowBytes;
        src.CopyPixels(new PixelRect(size), fb.Address, stride * size.Height, stride);

        byte* p0 = (byte*)fb.Address;
        byte kb = p0[0], kg = p0[1], kr = p0[2], ka = p0[3];
        if (ka < 250) return wb; // 이미 투명 영역 있음 → 키 제거 안 함

        for (int y = 0; y < size.Height; y++)
        {
            byte* row = p0 + y * stride;
            for (int x = 0; x < size.Width; x++)
            {
                byte* p = row + x * 4;
                if (p[3] < 250) continue;
                if (Math.Abs(p[0] - kb) <= Tolerance &&
                    Math.Abs(p[1] - kg) <= Tolerance &&
                    Math.Abs(p[2] - kr) <= Tolerance)
                {
                    p[3] = 0; // 투명화
                }
            }
        }
        return wb;
    }
}
