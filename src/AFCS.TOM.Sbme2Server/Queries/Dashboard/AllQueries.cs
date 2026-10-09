namespace AFCS.TOM.Sbme2Server.Dashboard
{
    static partial class Queries
    {
        private static string _ps(string query) => QueryHelper.PutSchemes(query);

        public static string GetDeviceList() => _ps(string.Format(_getDeviceList));
    }
}