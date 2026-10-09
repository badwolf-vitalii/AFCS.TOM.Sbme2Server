namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _signatureIdByHolderID = @"SELECT
            ATTACHEMENTID
            FROM #SCHEME_SBME2_GESTOWN#.ATTACHMENTS
            WHERE HOLDERID = :holderID
            AND ATTACHMENTTYPEID = 1
            AND ATTACHMENT_REF_TYPE = 3";
    }
}
