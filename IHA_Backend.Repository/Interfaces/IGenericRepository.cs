namespace IHA_Backend.Repository.Interfaces
{
    /// <summary>
    /// Tüm entity'ler için ortak veritabanı işlemlerini tanımlayan genel arayüz.
    /// T: Herhangi bir entity sınıfı (Aircraft, Airport, User vb.)
    /// </summary>
    public interface IGenericRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> GetByIdAsync(int id);
        Task AddAsync(T entity);
        void Update(T entity);
        void Delete(T entity);
        Task SaveChangesAsync();
    }
}
