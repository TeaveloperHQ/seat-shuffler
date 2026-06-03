using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SeatShuffler.Models;

namespace SeatShuffler.Services;

/// <summary>학생 명단을 roster.json에 영속.</summary>
public sealed class RosterStore
{
    public List<Student> Load()
    {
        try
        {
            if (!File.Exists(AppPaths.RosterFile)) return new List<Student>();
            var json = File.ReadAllText(AppPaths.RosterFile);
            var doc = JsonSerializer.Deserialize(json, AppJsonContext.Default.RosterDocument);
            return doc?.Students
                .Select(d => new Student { StudentNumber = d.StudentNumber, Name = d.Name, Gender = d.Gender })
                .ToList() ?? new List<Student>();
        }
        catch
        {
            // 손상된 파일은 무시하고 빈 명단으로 시작.
            return new List<Student>();
        }
    }

    public void Save(IEnumerable<Student> students)
    {
        AppPaths.EnsureDir();
        var doc = new RosterDocument
        {
            Students = students
                .Select(s => new StudentDto { StudentNumber = s.StudentNumber, Name = s.Name, Gender = s.Gender })
                .ToList(),
        };
        var json = JsonSerializer.Serialize(doc, AppJsonContext.Default.RosterDocument);
        AtomicWrite.Write(AppPaths.RosterFile, json);
    }
}
