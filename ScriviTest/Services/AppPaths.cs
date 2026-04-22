using System;
using System.IO;

namespace ScriviTest.Services;

public static class AppPaths
{
    // Gets the directory exactly where the .exe is running (or the bin/Debug folder in VS Code)
    public static string BaseDirectory => AppContext.BaseDirectory;
    
    public static string DataDir => Path.Combine(BaseDirectory, "Data");
    public static string QuestionnairesDir => Path.Combine(DataDir, "Questionnaires");
    public static string AnswersDir => Path.Combine(DataDir, "Answers");
    
    // The hidden History Log file
    public static string HistoryFile => Path.Combine(DataDir, "creation_history.dat");

    public static void InitializeFolders()
    {
        if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
        if (!Directory.Exists(QuestionnairesDir)) Directory.CreateDirectory(QuestionnairesDir);
        if (!Directory.Exists(AnswersDir)) Directory.CreateDirectory(AnswersDir);
    }
}