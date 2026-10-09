namespace AFCS.TOM.Sbme2Server.SBME2
{
    public partial class Queries
    {
        private const string _selectNextSeqVal = @"SELECT MAX({0}) + 1 Value FROM {1}.{2}";
    }
}
