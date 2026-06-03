using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using SeatShuffler.Models;

namespace SeatShuffler.Services;

public sealed class RosterImportResult
{
    public List<Student> Students { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool Ok { get; set; } = true;
}

/// <summary>
/// 명단 가져오기: 구분자 텍스트(클립보드/CSV)와 .xlsx(ClosedXML).
/// 헤더(학번/이름/성별)를 감지하거나 열 순서(학번, 이름, 성별)를 가정한다.
/// </summary>
public sealed class RosterImportService
{
    private static readonly string[] NumberHeaders = { "학번", "번호", "no", "id" };
    private static readonly string[] NameHeaders = { "이름", "성명", "name" };
    private static readonly string[] GenderHeaders = { "성별", "gender", "sex" };

    public RosterImportResult ParseDelimited(string? text)
    {
        var result = new RosterImportResult();
        if (string.IsNullOrWhiteSpace(text))
        {
            result.Ok = false;
            result.Warnings.Add("붙여넣을 내용이 없습니다.");
            return result;
        }

        var rows = text
            .Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Where(l => l.Trim().Length > 0)
            .Select(SplitLine)
            .ToList();

        return BuildFromRows(rows, result);
    }

    public RosterImportResult ParseXlsx(Stream stream)
    {
        var result = new RosterImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.FirstOrDefault();
            if (ws is null)
            {
                result.Ok = false;
                result.Warnings.Add("워크시트가 없습니다.");
                return result;
            }

            var rows = new List<string[]>();
            foreach (var row in ws.RangeUsed()?.RowsUsed() ?? Enumerable.Empty<IXLRangeRow>())
            {
                var cells = row.Cells().Select(c => c.GetString().Trim()).ToArray();
                if (cells.All(string.IsNullOrEmpty)) continue;
                rows.Add(cells);
            }
            return BuildFromRows(rows, result);
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Warnings.Add($"엑셀 파일을 읽지 못했습니다: {ex.Message}");
            return result;
        }
    }

    private static string[] SplitLine(string line)
    {
        // 탭이 있으면 탭 기준, 없으면 쉼표 기준.
        var sep = line.Contains('\t') ? '\t' : ',';
        return line.Split(sep).Select(c => c.Trim()).ToArray();
    }

    private RosterImportResult BuildFromRows(List<string[]> rows, RosterImportResult result)
    {
        if (rows.Count == 0)
        {
            result.Ok = false;
            result.Warnings.Add("인식된 행이 없습니다.");
            return result;
        }

        // 헤더 감지
        int numCol = -1, nameCol = -1, genderCol = -1;
        int startRow = 0;
        if (IsHeaderRow(rows[0]))
        {
            var header = rows[0];
            for (int c = 0; c < header.Length; c++)
            {
                var h = header[c].ToLowerInvariant();
                if (numCol < 0 && NumberHeaders.Any(k => h.Contains(k))) numCol = c;
                else if (nameCol < 0 && NameHeaders.Any(k => h.Contains(k))) nameCol = c;
                else if (genderCol < 0 && GenderHeaders.Any(k => h.Contains(k))) genderCol = c;
            }
            startRow = 1;
        }

        // 헤더가 없거나 매핑 실패 시 열 순서 가정: 학번, 이름, 성별 (또는 이름만).
        bool mapped = nameCol >= 0;
        if (!mapped)
        {
            if (rows.Max(r => r.Length) >= 3) { numCol = 0; nameCol = 1; genderCol = 2; }
            else if (rows.Max(r => r.Length) == 2) { nameCol = 0; genderCol = 1; }
            else { nameCol = 0; }
        }

        var seenKeys = new HashSet<string>();
        for (int r = startRow; r < rows.Count; r++)
        {
            var cols = rows[r];
            string num = numCol >= 0 && numCol < cols.Length ? cols[numCol] : "";
            string name = nameCol >= 0 && nameCol < cols.Length ? cols[nameCol] : "";
            string genderTok = genderCol >= 0 && genderCol < cols.Length ? cols[genderCol] : "";

            if (string.IsNullOrWhiteSpace(name))
            {
                result.Warnings.Add($"{r + 1}행: 이름이 비어 건너뜀.");
                continue;
            }

            var gender = NormalizeGender(genderTok);
            if (!string.IsNullOrWhiteSpace(genderTok) && gender == Gender.Unspecified)
                result.Warnings.Add($"{r + 1}행: 성별 '{genderTok}' 인식 실패 → 미지정.");

            var student = new Student { StudentNumber = num, Name = name, Gender = gender };
            if (!seenKeys.Add(student.Key))
                result.Warnings.Add($"{r + 1}행: 중복 식별키 '{student.Key}'.");

            result.Students.Add(student);
        }

        if (result.Students.Count == 0)
        {
            result.Ok = false;
            result.Warnings.Add("추가된 학생이 없습니다.");
        }
        return result;
    }

    private static bool IsHeaderRow(string[] row)
    {
        var joined = string.Join(" ", row).ToLowerInvariant();
        return NumberHeaders.Concat(NameHeaders).Concat(GenderHeaders).Any(joined.Contains);
    }

    public static Gender NormalizeGender(string token)
    {
        var t = token.Trim().ToLowerInvariant();
        return t switch
        {
            "남" or "남자" or "m" or "male" or "1" or "b" => Gender.Male,
            "여" or "녀" or "여자" or "f" or "female" or "2" or "g" => Gender.Female,
            _ => Gender.Unspecified,
        };
    }
}
