namespace AFCS.TOM.Sbme2Server.Services
{
    public interface IBglServiceBase
    {
        void SaveChanges();
        Task<int> SaveChangesAsync();
    }
}
