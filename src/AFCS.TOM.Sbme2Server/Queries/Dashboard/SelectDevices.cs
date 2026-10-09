namespace AFCS.TOM.Sbme2Server.Dashboard
{
    static partial class Queries
    {
        private const string _getDeviceList =
            @"select dev.operatorid       as OperatorId,
                     dev.deviceclassid    as DeviceClassId,
                     dev.devicetypeid     as DeviceTypeId,
                     dev.devicecode       as DeviceCode,
                     dev.equipmentid      as EquipmentId,
                     dev.saledeviceid     as SaleDeviceId,
                     dev.sellinggroupid   as SellingGroupId,
                     dev.nodeid           as NodId,
                     attr.attribcharvalue as Label,
                     dev.hostname         as HostName,
                     des.description      as Description,
                     dev.lastcontactdate  as LastContactDate
            from #SCHEME_SBME2_TARIFFOWN_CONFOWN#.devices dev,
                 #SCHEME_SBME2_TARIFFOWN_CONFOWN#.deviceattributes attr,
                 #SCHEME_SBME2_TARIFFOWN_CONFOWN#.tariffdbdescriptions des
            where dev.deviceclassid   = attr.deviceclassid
            and   dev.devicecode      = attr.devicecode
            and   dev.nodeid          = des.descid 
            and   des.descclass       = 91
            and   des.languageid      = 1
            and   attr.attributecode  = 20003
            and   dev.deviceclassid   = 6
            and   dev.equipmentid     = 25";
    }
}
