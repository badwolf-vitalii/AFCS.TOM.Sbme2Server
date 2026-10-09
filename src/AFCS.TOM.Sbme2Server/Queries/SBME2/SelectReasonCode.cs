namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _selectReasonCode =
            @"SELECT REASONCODE FROM TSCBlacklist
            WHERE SHORTCARDMODEL = :SHORTCARDMODEL
            AND to_number(:LASTSERIALNO, 'XXXXXXXXXXXXXXXX') BETWEEN to_number(FIRSTTSCSERIALNO, 'XXXXXXXXXXXXXXXX') AND to_number(LASTTSCSERIALNO, 'XXXXXXXXXXXXXXXX')";
    }
}

