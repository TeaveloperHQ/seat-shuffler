using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SeatShuffler.Services;

/// <summary>
/// 윈도우 인쇄 대화상자를 띄우고, 고른 프린터로 그림 한 장을 인쇄한다(GDI).
/// 용지가 세로로 설정돼 있으면 그림을 90° 돌려 넣으므로 프린터 설정을 바꿀 필요가 없다.
/// </summary>
public static class WindowsPrinting
{
    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>기본 프린터 이름(없으면 null).</summary>
    public static string? DefaultPrinter()
    {
        if (!IsSupported) return null;
        var size = 0;
        GetDefaultPrinter(null, ref size);
        if (size <= 0) return null;
        var sb = new StringBuilder(size);
        return GetDefaultPrinter(sb, ref size) ? sb.ToString() : null;
    }

    /// <summary>미리보기에 쓸 용지 크기(pt). 프린터를 못 읽으면 A4 가로로 가정한다.</summary>
    public static (double WidthPt, double HeightPt) PaperSize(string? printer)
    {
        if (!IsSupported || string.IsNullOrWhiteSpace(printer))
            return (PageLayout.PageWidthPt, PageLayout.PageHeightPt);

        var hdc = CreateIC("WINSPOOL", printer, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero) return (PageLayout.PageWidthPt, PageLayout.PageHeightPt);
        try
        {
            int w = GetDeviceCaps(hdc, PHYSICALWIDTH), h = GetDeviceCaps(hdc, PHYSICALHEIGHT);
            int dpiX = GetDeviceCaps(hdc, LOGPIXELSX), dpiY = GetDeviceCaps(hdc, LOGPIXELSY);
            if (w <= 0 || h <= 0 || dpiX <= 0 || dpiY <= 0)
                return (PageLayout.PageWidthPt, PageLayout.PageHeightPt);
            return (w * 72.0 / dpiX, h * 72.0 / dpiY);
        }
        finally { DeleteDC(hdc); }
    }

    /// <summary>
    /// 윈도우 인쇄 대화상자를 띄워 프린터·매수·용지를 고르게 한 뒤 그림을 한 장으로 인쇄한다.
    /// 고른 프린터 이름을 돌려주고, 사용자가 취소하면 null.
    /// </summary>
    public static string? Print(byte[] rgb, int width, int height, string documentName, IntPtr ownerWindow)
    {
        if (!IsSupported) throw new PlatformNotSupportedException("윈도우에서만 인쇄할 수 있습니다.");

        var pd = new PRINTDLG
        {
            lStructSize = Marshal.SizeOf<PRINTDLG>(),
            hwndOwner = ownerWindow,
            // 대화상자에서 장치 컨텍스트를 받아 그대로 쓴다. 쪽 번호·선택 영역은 쓸 일이 없어 감춘다.
            Flags = PD_RETURNDC | PD_NOPAGENUMS | PD_NOSELECTION | PD_HIDEPRINTTOFILE,
            nCopies = 1,
        };
        if (!PrintDlg(ref pd))
        {
            var err = CommDlgExtendedError();
            if (err != 0) throw new InvalidOperationException($"인쇄 대화상자를 열지 못했습니다(코드 {err}).");
            return null; // 사용자가 취소
        }

        var printer = DeviceName(pd.hDevNames) ?? DefaultPrinter() ?? "프린터";
        var copies = Math.Max(1, (int)pd.nCopies);
        if (pd.hDevMode != IntPtr.Zero) GlobalFree(pd.hDevMode);
        if (pd.hDevNames != IntPtr.Zero) GlobalFree(pd.hDevNames);

        var hdc = pd.hDC;
        if (hdc == IntPtr.Zero) throw new InvalidOperationException($"프린터 '{printer}'를 열지 못했습니다.");

        PrintOnDc(hdc, rgb, width, height, documentName, copies);
        return printer;
    }

    /// <summary>열어 둔 프린터 장치 컨텍스트에 그림을 한 장(매수만큼) 찍는다. 다 쓰면 컨텍스트를 닫는다.</summary>
    private static void PrintOnDc(IntPtr hdc, byte[] rgb, int width, int height, string documentName, int copies)
    {
        try
        {
            int paperW = GetDeviceCaps(hdc, PHYSICALWIDTH), paperH = GetDeviceCaps(hdc, PHYSICALHEIGHT);
            int offX = GetDeviceCaps(hdc, PHYSICALOFFSETX), offY = GetDeviceCaps(hdc, PHYSICALOFFSETY);
            int dpiX = GetDeviceCaps(hdc, LOGPIXELSX), dpiY = GetDeviceCaps(hdc, LOGPIXELSY);
            if (paperW <= 0 || paperH <= 0 || dpiX <= 0 || dpiY <= 0)
                throw new InvalidOperationException("프린터의 용지 크기를 알 수 없습니다.");

            // 돌려서 넣는 쪽이 더 크게 나오면 돌린다(세로 용지에 가로 좌석표를 넣는 경우).
            var upright = Fit(width, height, false);
            var turned = Fit(height, width, true);
            var best = turned.Scale > upright.Scale ? turned : upright;

            var bits = best.Rotated ? RotateClockwise(rgb, width, height) : rgb;
            int imgW = best.Rotated ? height : width, imgH = best.Rotated ? width : height;

            var doc = new DOCINFO { cbSize = Marshal.SizeOf<DOCINFO>(), lpszDocName = documentName };
            if (StartDoc(hdc, ref doc) <= 0) throw new InvalidOperationException("인쇄를 시작하지 못했습니다.");
            try
            {
                var dib = ToDib(bits, imgW, imgH, out var header);
                for (var copy = 0; copy < copies; copy++)
                {
                    if (StartPage(hdc) <= 0) throw new InvalidOperationException("인쇄 페이지를 시작하지 못했습니다.");
                    SetStretchBltMode(hdc, COLORONCOLOR);

                    // 종이 좌표 → DC 좌표(프린터가 못 찍는 가장자리만큼 원점이 안쪽에 있다).
                    var ok = StretchDIBits(hdc,
                        best.X - offX, best.Y - offY, best.Width, best.Height,
                        0, 0, imgW, imgH, dib, ref header, DIB_RGB_COLORS, SRCCOPY);
                    if (ok == 0) throw new InvalidOperationException("그림을 프린터로 보내지 못했습니다.");

                    EndPage(hdc);
                }
                EndDoc(hdc);
            }
            catch
            {
                AbortDoc(hdc);
                throw;
            }

            // 종이 안 여백 상자에 비율을 지켜 가운데로 맞춘다. 돌려 넣을 땐 좌우/위아래 여백도 같이 돈다.
            (double Scale, int X, int Y, int Width, int Height, bool Rotated) Fit(int w, int h, bool rotated)
            {
                var mx = (int)Math.Round((rotated ? PageLayout.MarginYPt : PageLayout.MarginXPt) / 72.0 * dpiX);
                var my = (int)Math.Round((rotated ? PageLayout.MarginXPt : PageLayout.MarginYPt) / 72.0 * dpiY);
                double availW = Math.Max(1, paperW - 2 * mx), availH = Math.Max(1, paperH - 2 * my);
                var scale = Math.Min(availW / w, availH / h);
                int dw = Math.Max(1, (int)Math.Round(w * scale)), dh = Math.Max(1, (int)Math.Round(h * scale));
                return (scale, (paperW - dw) / 2, (paperH - dh) / 2, dw, dh, rotated);
            }
        }
        finally
        {
            DeleteDC(hdc);
        }
    }

    /// <summary>시계 방향 90° 회전(가로 좌석표를 세로 용지에 넣을 때).</summary>
    private static byte[] RotateClockwise(byte[] rgb, int w, int h)
    {
        var dst = new byte[rgb.Length];
        for (var y = 0; y < h; y++)
        {
            var src = y * w * 3;
            for (var x = 0; x < w; x++, src += 3)
            {
                // (x, y) → 새 그림에서 (h-1-y, x)
                var o = (x * h + (h - 1 - y)) * 3;
                dst[o] = rgb[src]; dst[o + 1] = rgb[src + 1]; dst[o + 2] = rgb[src + 2];
            }
        }
        return dst;
    }

    /// <summary>GDI가 요구하는 DIB(파랑·초록·빨강 순서, 줄마다 4바이트 배수)로 옮긴다.</summary>
    private static byte[] ToDib(byte[] rgb, int w, int h, out BITMAPINFOHEADER header)
    {
        var stride = (w * 3 + 3) & ~3;
        var dib = new byte[(long)stride * h <= int.MaxValue
            ? stride * h
            : throw new InvalidOperationException("그림이 너무 커서 인쇄할 수 없습니다.")];
        for (var y = 0; y < h; y++)
        {
            int s = y * w * 3, d = y * stride;
            for (var x = 0; x < w; x++, s += 3, d += 3)
            {
                dib[d] = rgb[s + 2]; dib[d + 1] = rgb[s + 1]; dib[d + 2] = rgb[s];
            }
        }
        header = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = w,
            biHeight = -h,          // 음수 = 위에서 아래로 저장된 그림
            biPlanes = 1,
            biBitCount = 24,
            biCompression = 0,      // BI_RGB
            biSizeImage = dib.Length,
        };
        return dib;
    }

    /// <summary>대화상자가 돌려준 DEVNAMES에서 고른 프린터 이름을 꺼낸다.</summary>
    private static string? DeviceName(IntPtr hDevNames)
    {
        if (hDevNames == IntPtr.Zero) return null;
        var p = GlobalLock(hDevNames);
        if (p == IntPtr.Zero) return null;
        try
        {
            // DEVNAMES: 드라이버·장치·출력 이름의 위치(문자 단위)가 앞쪽 WORD 세 개에 담겨 있다.
            var deviceOffset = (ushort)Marshal.ReadInt16(p, 2);
            return Marshal.PtrToStringUni(p + deviceOffset * 2);
        }
        finally { GlobalUnlock(hDevNames); }
    }

    private const int PD_NOSELECTION = 0x4, PD_NOPAGENUMS = 0x8, PD_RETURNDC = 0x100, PD_HIDEPRINTTOFILE = 0x100000;
    private const int PHYSICALWIDTH = 110, PHYSICALHEIGHT = 111, PHYSICALOFFSETX = 112, PHYSICALOFFSETY = 113;
    private const int LOGPIXELSX = 88, LOGPIXELSY = 90;
    private const int COLORONCOLOR = 3, DIB_RGB_COLORS = 0;
    private const uint SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOCINFO
    {
        public int cbSize;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszDocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszOutput;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszDatatype;
        public int fwType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    /// <summary>인쇄 대화상자에 넘기는 구조체(PRINTDLGW). 필드 순서·크기가 윈도우 정의와 같아야 한다.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PRINTDLG
    {
        public int lStructSize;
        public IntPtr hwndOwner, hDevMode, hDevNames, hDC;
        public int Flags;
        public ushort nFromPage, nToPage, nMinPage, nMaxPage, nCopies;
        public IntPtr hInstance, lCustData, lpfnPrintHook, lpfnSetupHook;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpPrintTemplateName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpSetupTemplateName;
        public IntPtr hPrintTemplate, hSetupTemplate;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "PrintDlgW")]
    private static extern bool PrintDlg(ref PRINTDLG pd);

    [DllImport("comdlg32.dll")] private static extern int CommDlgExtendedError();
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr h);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr h);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr h);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetDefaultPrinter(StringBuilder? buffer, ref int size);

    /// <summary>인쇄하지 않고 용지 정보만 물어볼 때 쓰는 가벼운 컨텍스트.</summary>
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateIC(string driver, string device, string? output, IntPtr devMode);

    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int index);
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern int StartDoc(IntPtr hdc, ref DOCINFO di);
    [DllImport("gdi32.dll")] private static extern int StartPage(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern int EndPage(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern int EndDoc(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern int AbortDoc(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int StretchDIBits(IntPtr hdc, int xDest, int yDest, int wDest, int hDest,
        int xSrc, int ySrc, int wSrc, int hSrc, byte[] bits, ref BITMAPINFOHEADER bmi, int usage, uint rop);
}
