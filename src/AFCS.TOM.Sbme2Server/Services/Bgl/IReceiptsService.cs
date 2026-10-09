using AFCS.TOM.SbmeDataLayer;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.OutputParameters.Sales;

namespace AFCS.TOM.Sbme2Server.Services.Bgl
{
    public interface IReceiptsService : IBglServiceBase
    {
        bool IsServiceEnabled { get; }

        AgentShift? GetAgentShift(Guid shiftId, bool includeSaleTransactions = false);
        List<AgentShift>? GetAgentShifts(int agentId, DateTime from, DateTime to, bool includeSaleTransactions = false);
        List<AgentShift>? GetAllAgentsShifts(DateTime from, DateTime to, bool includeSaleTransactions = false);
        byte[]? GetTransactionReceipt(Guid transactionId);
        byte[]? GetEndShiftReceipt(Guid agentShiftId);
        List<ReceiptDateAndContent>? GetReceipts(Guid agentShiftId, ReceiptTypeMask mask, StartEnd? startEnd, int rowsPerPage = 50, int pageNumber = 0);
        List<ReceiptDateAndContent>? GetReceiptsForMedia(string cardSerialNumber, int shortCardModel, ReceiptTypeMask mask, StartEnd? startEnd, int rowsPerPage = 50, int pageNumber = 0);
        byte[]? GetReceiptByTypeAndId(string id, ReceiptTypeMask mask);
        Guid? GetUndoneCscContractVtsReceiptId(Guid articleId);
        Guid? GetUndoneTicketVtsReceiptId(Guid articleId);
        Guid? GetUndonePtItemVtsReceiptId(Guid articleId);
    }

    public class EmptyReceiptsService : IReceiptsService
    {
        public bool IsServiceEnabled { get; }

        public AgentShift? GetAgentShift(Guid shiftId, bool includeSaleTransactions = false)
        {
            throw new NotImplementedException();
        }

        public List<AgentShift>? GetAgentShifts(int agentId, DateTime from, DateTime to, bool includeSaleTransactions = false)
        {
            throw new NotImplementedException();
        }

        public List<AgentShift>? GetAllAgentsShifts(DateTime from, DateTime to, bool includeSaleTransactions = false)
        {
            throw new NotImplementedException();
        }

        public byte[]? GetEndShiftReceipt(Guid agentShiftId)
        {
            throw new NotImplementedException();
        }

        public byte[]? GetTransactionReceipt(Guid transactionId)
        {
            throw new NotImplementedException();
        }

        public List<ReceiptDateAndContent>? GetReceipts(Guid agentShiftId, ReceiptTypeMask mask, StartEnd? startEnd, int rowsPerPage, int pageNumber)
        {
            throw new NotImplementedException();
        }

        public List<ReceiptDateAndContent>? GetReceiptsForMedia(string cardSerialNumber, int shortCardModel, ReceiptTypeMask mask, StartEnd? startEnd, int rowsPerPage, int pageNumber)
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

        public byte[]? GetReceiptByTypeAndId(string id, ReceiptTypeMask mask)
        {
            throw new NotImplementedException();
        }

        public Guid? GetUndoneCscContractVtsReceiptId(Guid articleId)
        {
            throw new NotImplementedException();
        }

        public Guid? GetUndoneTicketVtsReceiptId(Guid articleId)
        {
            throw new NotImplementedException();
        }

        public Guid? GetUndonePtItemVtsReceiptId(Guid articleId)
        {
            throw new NotImplementedException();
        }
    }
}
