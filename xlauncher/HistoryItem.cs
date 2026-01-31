public class HistoryItem
{
    public string FullPath { get; set; }
    public string FileName { get; set; }
    public string Alias { get; set; }
    public DateTime LastRun { get; set; }
    public int RunCount { get; set; }
    public bool IsFolder { get; set; }
}