namespace CompanyDirectory.Shared.Dtos;

public class SyncResultDto
{
    public int SyncedCount { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; set; } = [];
    public bool Success => Errors.Count == 0;
}
