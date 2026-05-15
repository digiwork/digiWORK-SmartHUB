namespace CompanyDirectory_Desktop.LocalCache;

public interface IRecentlyViewedRepository
{
    Task RecordViewAsync(string login);
    Task<List<string>> GetRecentAsync(int limit = 10);
}
