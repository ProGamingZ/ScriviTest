using System;
using System.IO;

namespace ScriviTest.Services;

public static class AppPaths
{
    // 1. Ask the OS for the safe AppData (Windows) or Application Support (Mac) folder
    private static readonly string BaseAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    
    // 2. Create a dedicated root folder for your app (e.g., AppData/Roaming/ScriviTest)
    public static string RootAppFolder => Path.Combine(BaseAppDataPath, "ScriviTest");

    // 3. Define the sub-folders inside the safe root directory
    public static string DataDir => Path.Combine(RootAppFolder, "Data");
    public static string QuestionnairesDir => Path.Combine(DataDir, "Questionnaires");
    public static string AnswersDir => Path.Combine(DataDir, "Answers");
    
    // The hidden History Log file
    public static string HistoryFile => Path.Combine(DataDir, "creation_history.dat");

    public static void InitializeFolders()
    {
        // Make sure all levels of the directory tree exist!
        if (!Directory.Exists(RootAppFolder)) Directory.CreateDirectory(RootAppFolder);
        if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
        if (!Directory.Exists(QuestionnairesDir)) Directory.CreateDirectory(QuestionnairesDir);
        if (!Directory.Exists(AnswersDir)) Directory.CreateDirectory(AnswersDir);
    }
}