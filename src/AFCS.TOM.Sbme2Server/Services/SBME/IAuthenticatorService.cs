using AFCS.TOM.SbmeModels.SBME;

namespace AFCS.TOM.Sbme2Server.Services.SBME
{
    public interface IAuthenticatorService
    {
        public Task<AuthenticationToken> AutheticateByCredentialAsync(string connectionString, string userId, string password);
        public Task<AuthenticationToken> AutheticateByCardPinCodeAsync(string connectionString, string cardSerialNumber);
    }

    public class EmptyAuthenticatorService : IAuthenticatorService
    {
        public Task<AuthenticationToken> AutheticateByCardPinCodeAsync(string connectionString, string cardSerialNumber)
        {
            throw new NotImplementedException();
        }

        public Task<AuthenticationToken> AutheticateByCredentialAsync(string connectionString, string userId, string password)
        {
            throw new NotImplementedException();
        }
    }
}
