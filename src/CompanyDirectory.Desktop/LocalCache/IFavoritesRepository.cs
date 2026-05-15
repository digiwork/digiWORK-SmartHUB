namespace CompanyDirectory_Desktop.LocalCache;

public interface IFavoritesRepository
{
    Task AddAsync(string login);
    Task RemoveAsync(string login);
    Task<List<string>> GetAllAsync();
    Task<bool> IsFavoriteAsync(string login);
}
