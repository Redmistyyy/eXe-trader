

namespace executive;

public class ExeHelper
{
    public static List<string> GetFileNamesWithoutExtension(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory: {directoryPath} does not exist.");
        }

        string[] files = Directory.GetFiles(directoryPath);
        List<string> fileNames = [];


        foreach (string file in files)
        {
            fileNames.Add(Path.GetFileNameWithoutExtension(file));
        }

        return fileNames;
    }
}