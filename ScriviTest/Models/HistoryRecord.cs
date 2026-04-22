using System;

namespace ScriviTest.Models;

public class HistoryRecord
{
    public string ExamTitle { get; set; } = string.Empty;
    public string ExportDate { get; set; } = string.Empty;
    public string WhiteboardKey { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}