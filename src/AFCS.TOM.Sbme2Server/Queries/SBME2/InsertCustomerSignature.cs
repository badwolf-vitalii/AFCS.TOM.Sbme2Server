namespace AFCS.TOM.Sbme2Server.SBME2
{
    public partial class Queries
    {
        private const string _insertHolderSignature = @"INSERT INTO #SCHEME_SBME2_GESTOWN#.ATTACHMENTS (
            ATTACHEMENTID,
            INSERTDATE,
            INSERTDATETIME,
            OPERATORID,
            HOLDERID,
            ATTACHMENTTYPEID,
            ATTACHMENTFORMAT,
            ATTACHMENT_REF_TYPE,
            ATTACHMENT
            )
            VALUES
            (
            ATTACHMENT_SEQ.nextval,
            TRUNC(SYSDATE),
            SYSDATE,
            :OPERATORID,
            :HOLDERID,
            1,
            2,
            3,
            :ATTACHMENT
            ) RETURNING ATTACHEMENTID INTO :ATTACHMENTID_RETURN";
    }
}
