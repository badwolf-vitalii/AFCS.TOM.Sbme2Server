using AFCS.TOM.SbmeModels.SBME;

namespace AFCS.TOM.Sbme2Server.Services.SBME
{
    public class AuthenticatorService : IAuthenticatorService
    {
        public async Task<AuthenticationToken> AutheticateByCardPinCodeAsync(string connectionString, string cardSerialNumber)
        {
            return new AuthenticationToken { Authenticated = true };
        }

        public async Task<AuthenticationToken> AutheticateByCredentialAsync(string connectionString, string userId, string password)
        {
            return new AuthenticationToken { Authenticated = true };
        }
    }
}
