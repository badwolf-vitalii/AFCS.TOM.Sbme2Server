namespace AFCS.TOM.Sbme2Server.SBME2
{
    public partial class Queries
    {
        private const string _insertAttachment = @"INSERT INTO #SCHEME_SBME2_GESTOWN#.ATTACHMENTS (
            HOLDERID,
            INSERTDATE,
            ) 
            VALUES 
            (
            holderid_seq.NEXTVAL,
            SYSDATE,
            ) RETURNING HOLDERID INTO :HOLDERID";
    }
}
