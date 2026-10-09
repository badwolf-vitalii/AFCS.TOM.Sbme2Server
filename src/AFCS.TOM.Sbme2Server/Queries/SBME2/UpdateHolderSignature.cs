namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _updateHolderSignature = @"UPDATE #SCHEME_SBME2_GESTOWN#.ATTACHMENTS
            SET INSERTDATE = TRUNC(SYSDATE),
            INSERTDATETIME = SYSDATE,
            OPERATORID = :OPERATORID,
            ATTACHMENT = :ATTACHMENT
            WHERE ATTACHEMENTID = :ATTACHMENTID";
    }
}
