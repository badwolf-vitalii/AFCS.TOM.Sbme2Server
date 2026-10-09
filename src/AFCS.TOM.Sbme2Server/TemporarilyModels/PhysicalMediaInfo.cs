using System.Globalization;

namespace AFCS.TOM.Sbme2Server.TemporarilyModels
{
    public class PhysicalMediaInfo
    {
        public string SerialNumber { get; set; }
        public int ShortCardModel { get; set; }

        public SbmeModels.SBME2.PhysicalMediaInfo AsSbme2PhMediaInfo =>
            new SbmeModels.SBME2.PhysicalMediaInfo {
                ShortCardModel = ShortCardModel,
                SerialNumber = uint.Parse(SerialNumber, NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            };
    }
}
