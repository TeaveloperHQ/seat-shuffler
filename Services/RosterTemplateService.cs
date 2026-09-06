using System.IO;
using ClosedXML.Excel;

namespace SeatShuffler.Services;

/// <summary>
/// 명단 업로드용 빈 양식(.xlsx) 생성. 헤더는 가져오기가 인식하는 학번/이름/성별이고,
/// 아래 예시 몇 줄은 지우고 반 명단을 채워 넣으면 된다.
/// </summary>
public static class RosterTemplateService
{
    public const string SuggestedFileName = "학생명단_양식.xlsx";

    private static readonly (string Number, string Name, string Gender)[] Samples =
    {
        ("10101", "김민준", "남"),
        ("10102", "이서연", "여"),
        ("10103", "박도윤", "남"),
        ("10104", "최서아", "여"),
    };

    public static void WriteXlsx(Stream stream)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("명단");

        ws.Cell(1, 1).Value = "학번";
        ws.Cell(1, 2).Value = "이름";
        ws.Cell(1, 3).Value = "성별";
        var header = ws.Range(1, 1, 1, 3);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDF1F7");

        for (int i = 0; i < Samples.Length; i++)
        {
            var (num, name, gender) = Samples[i];
            int r = i + 2;
            ws.Cell(r, 1).SetValue(num).Style.NumberFormat.Format = "@"; // 학번은 문자열로
            ws.Cell(r, 2).Value = name;
            ws.Cell(r, 3).Value = gender;
        }

        // 안내는 헤더 오른쪽 칸에 둔다 — 아래에 두면 가져올 때 빈 이름 행으로 경고가 뜬다.
        ws.Cell(1, 5).Value =
            "※ 예시 행을 지우고 실제 명단을 입력하세요 · 학번은 비워도 됩니다(이름으로 구분) · 성별은 남/여";
        ws.Cell(1, 5).Style.Font.FontColor = XLColor.Gray;
        ws.Cell(1, 5).Style.Font.Bold = false;

        ws.Column(1).Width = 12;
        ws.Column(2).Width = 14;
        ws.Column(3).Width = 8;
        ws.SheetView.FreezeRows(1);

        wb.SaveAs(stream);
    }
}
