namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _cardByProfileAndBlReason = @"SELECT tscstatus,
			BLREASONCODE,
			HOLDERID,
			TSCSERIALNO,
			SHORTCARDMODEL,
			SMARTCARDSN
			FROM #SCHEME_SBME2_GESTOWN#.TSCDOCUMENTS
			WHERE
				(
					:pfofile IS NULL
					OR MAINPROFILEID = :pfofile
					OR PROFILEID2    = :pfofile
					OR PROFILEID3    = :pfofile
				)
				AND
				(
					:blreason IS NULL
					OR BLREASONCODE = :blreason
				)
			ORDER BY tscstatus, blreasoncode, holderid";
    }
}
