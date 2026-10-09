namespace AFCS.TOM.Sbme2Server.SBME2
{
    static partial class Queries
    {
        private const string _customerByHolderID = @"SELECT
            holderid,
            insertdate,
            firstname,
            familyname,
            birthday,
            birthplace,
            nationality,
            documenttype,
            documentcode,
            documentcode2,
            docendvaliditydate,
            doc2endvaliditydate,
            {0}
            formatphoto,
            fiscalcode,
            address,
            house,
            flat,
            town,
            zipcode,
            district,
            sex,
            phone1,
            phone2,
            email,
            operatorid,
            lastupdate as LastUpdateDateTime,
            title,
            fax,
            corporateno,
            organizationid,
            status,
            postdebitstatus,
            customertype,
            birthplacecode,
            nationalitycode,
            activityid,
            parentholderid,
            birthplacecodetype,
            towncode,
            towncodetype,
            nationalitycodetype
            FROM #SCHEME_SBME2_GESTOWN#.HOLDERS
            WHERE HOLDERID = :holderID";
    }
}
