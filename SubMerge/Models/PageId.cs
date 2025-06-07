namespace SubMerge.Models;

public class PageId
{
    public PageId(int pageNumInFile, string fileName)
    {
        PageNumInFile = pageNumInFile;
        FileName = fileName;
    }

    public string FileName { get; set; }
    public int PageNumInFile { get; set; }
}
