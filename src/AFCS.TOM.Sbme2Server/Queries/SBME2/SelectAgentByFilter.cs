namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _agentByFilter = @"SELECT
            AGENTID,
            OPERATORID,
            AGENTUSERNAME,
            AGENTUSERPASSW,
            MATRICROLL,
            FIRSTNAME,
            FAMILYNAME,
            AGENTSTATUS,
            PLANTID,
            SYNID,
            PINCODE,
            SHORTCARDMODEL,
            TSCSERIALNO,
            TSCSALEOPERATORID,
            PROOFDOCID,
            PROOFDOCSERIALNO,
            PSWCHANGEDATE,
            PSWCHANGEAGENTID,
            PSWEXPIRYDATE,
            PSWHASHMODE,
            PINCHANGEDATE,
            PINCHANGEAGENTID,
            PINEXPIRYDATE,
            PINHASHMODE,
            LOGINATTEMPTLEFT,
            EMAIL,
            PSWCHANGEENABLED,
            HOLDERID,
            FROM #SCHEME_SBME2_GESTOWN#.AGENTS ";
    }
}
