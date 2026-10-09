using AFCS.TOM.Sbme2Server.Services.TemporarilyModels;
using AFCS.TOM.SbmeDataLayer;
using Basket = AFCS.TOM.SbmeModels.Basket;
using BDL = AFCS.TOM.SbmeModels.BglDataLayer;
using Enums = AFCS.TOM.SbmeModels.Enums;
using Sales = AFCS.TOM.SbmeModels.OutputParameters.Sales;

namespace AFCS.TOM.Sbme2Server.Services.Bgl
{
    public interface ISalesService : IBglServiceBase
    {
        bool IsServiceEnabled { get; }

        Task<Guid> CommitSaleTransaction(SaleTransaction transaction);
        Task<Guid> CommitPtTransaction(PtConfirmTransaction transaction);
        Task<bool> CheckIfPtTransactionExists(PtConfirmTransaction transaction);
        IEnumerable<Basket.Article> GetSoldArticles(string serialNumber, bool inlcudeVTokens, int top);
        Task<List<Guid>?> AddCscContractArticleInfo(List<CscContractArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null);
        Task<List<Guid>?> AddCscContractRefundArticleInfo(List<CscContractRefundArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null);
        Task<List<Guid>?> AddMagneticTicketArticleInfo(List<MagneticArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null);
        Task<List<Guid>?> AddMagneticRefundArticleInfo(List<MagneticRefundArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null);
        Task<List<Guid>?> AddContactlessCardArticleInfo(List<ContactlessCardArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null);
        Task<List<Guid>?> AddProfileRenewalArticleInfo(List<ProfileRenewalArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null);
        Task<List<Guid>?> AddPtItemArticleInfo(List<PtItemArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null);
        Task<List<Guid>?> AddPtItemRefundArticleInfo(List<PtItemRefundArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null);
        Task AddSpoiledMagneticTicketArticleInfo(Article article, Guid deviceShiftId, DateTime time);
        Task<Guid[]> AddWhiteListContactlessCardArticleInfo(ICollection<Article> articles, Guid deviceShiftId, DateTime time);
        Task<Guid[]> AddContactlessCardExpirationExtensionArticleInfo(ICollection<Article> articles, Guid deviceShiftId, DateTime time);
        Task RegisterCanceledCscContracts(List<CanceledCscContract> contracts);
        Task AddPosDetails(List<PosDetail> details);
        Task AddPtBankTransferInfo(PtBankTransferInfo info);
        List<PosDetail>? GetPosDetails(Guid transactionId);
        Sales.PosRefundDetails? GetPosRefundDetails(Guid transactionId);
        Sales.CashRefundDetails? GetCashRefundDetails(Guid transactionId);
        BDL.Article? GetArticleByItsCscContractInfo(Guid cscContractInfoId);
        List<BDL.CscContractArticleInfo>? GetCscContractArticleInfos(Guid cscContractInfoId);
        List<CscContractArticleDetails> GetCscContractArticles(string cardSerialNumber);
        BDL.UndonableContract? GetUndonableContract(string cardSerialNumber, int shortCardModel, Guid? deviceShiftId, Guid? startFrom);
        List<BDL.UndonableContract>? GetUndonableContracts(string cardSerialNumber, int shortCardModel, Guid? deviceShiftId, Guid? startFrom, bool undone = false);
        Sales.MostlyUsedTariffs? GetMostlyUsedTariffs(Enums.ArticleType articleType, byte periodInDays);
        int GetTheLongestPeriodOfArticles(Enums.ArticleType articleType);
        Task<List<SaleTransaction>> GetMissingPtTransactionsAsync();
        Task<SaleTransaction?> GetSaleTransaction(Guid transactionId);
    }

    public class EmptySalesService : ISalesService
    {
        public bool IsServiceEnabled { get; }
     
        public Task<List<Guid>?> AddContactlessCardArticleInfo(List<ContactlessCardArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            throw new NotImplementedException();
        }

        public Task<List<Guid>?> AddCscContractArticleInfo(List<CscContractArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            throw new NotImplementedException();
        }

        public Task<List<Guid>?> AddCscContractRefundArticleInfo(List<CscContractRefundArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            throw new NotImplementedException();
        }

        public Task<List<Guid>?> AddMagneticTicketArticleInfo(List<MagneticArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            throw new NotImplementedException();
        }

        public Task<List<Guid>?> AddMagneticRefundArticleInfo(List<MagneticRefundArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            throw new NotImplementedException();
        }

        public Task<List<Guid>?> AddProfileRenewalArticleInfo(List<ProfileRenewalArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            throw new NotImplementedException();
        }

        public Task AddPosDetails(List<PosDetail> details)
        {
            throw new NotImplementedException();
        }

        public Task AddPtBankTransferInfo(PtBankTransferInfo info)
        {
            throw new NotImplementedException();
        }

        public List<PosDetail>? GetPosDetails(Guid transactionId)
        {
            throw new NotImplementedException();
        }

        public Sales.PosRefundDetails? GetPosRefundDetails(Guid transactionId)
        {
            throw new NotImplementedException();
        }

        public Sales.CashRefundDetails? GetCashRefundDetails(Guid transactionId)
        {
            throw new NotImplementedException();
        }

        public Task<Guid> CommitSaleTransaction(SaleTransaction transaction)
        {
            throw new NotImplementedException();
        }

        public Task<Guid> CommitPtTransaction(PtConfirmTransaction transaction)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CheckIfPtTransactionExists(PtConfirmTransaction transaction)
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> CreateTransactionReceipt(Guid transactionId)
        {
            throw new NotImplementedException();
        }

        public BDL.Article? GetArticleByItsCscContractInfo(Guid cscContractInfoId)
        {
            throw new NotImplementedException();
        }

        public List<BDL.CscContractArticleInfo>? GetCscContractArticleInfos(Guid cscContractInfoId)
        {
            throw new NotImplementedException();
        }

        public List<CscContractArticleDetails> GetCscContractArticles(string cardSerialNumber)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Basket.Article> GetSoldArticles(string serialNumber, bool inlcudeVTokens, int top)
        {
            throw new NotImplementedException();
        }

        public BDL.UndonableContract? GetUndonableContract(string cardSerialNumber, int shortCardModel, Guid? deviceShiftId, Guid? startFrom)
        {
            throw new NotImplementedException();
        }

        public List<BDL.UndonableContract>? GetUndonableContracts(string cardSerialNumber, int shortCardModel, Guid? deviceShiftId, Guid? startFrom, bool undone = false)
        {
            throw new NotImplementedException();
        }

        public Sales.MostlyUsedTariffs? GetMostlyUsedTariffs(Enums.ArticleType articleType, byte periodInDays)
        {
            throw new NotImplementedException();
        }

        public int GetTheLongestPeriodOfArticles(Enums.ArticleType articleType)
        {
            throw new NotImplementedException();
        }

        public Task<List<Guid>?> AddPtItemArticleInfo(List<PtItemArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            throw new NotImplementedException();
        }

        public Task<List<Guid>?> AddPtItemRefundArticleInfo(List<PtItemRefundArticleInfo> infos, List<Basket.AdditionalPtInfo?>? apti = null)
        {
            throw new NotImplementedException();
        }

        public Task AddSpoiledMagneticTicketArticleInfo(Article article, Guid deviceShiftId, DateTime time)
        {
            throw new NotImplementedException();
        }

        public Task<Guid[]> AddWhiteListContactlessCardArticleInfo(ICollection<Article> articles, Guid deviceShiftId, DateTime time)
        {
            throw new NotImplementedException();
        }

        public Task<Guid[]> AddContactlessCardExpirationExtensionArticleInfo(ICollection<Article> articles, Guid deviceShiftId, DateTime time)
        {
            throw new NotImplementedException();
        }

        public Task RegisterCanceledCscContracts(List<CanceledCscContract> contracts)
        {
            throw new NotImplementedException();
        }

        public void SaveChanges()
        {
            throw new NotImplementedException();
        }

        public Task<int> SaveChangesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<List<SaleTransaction>> GetMissingPtTransactionsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<SaleTransaction?> GetSaleTransaction(Guid transactionId)
        {
            throw new NotImplementedException();
        }
    }
}
