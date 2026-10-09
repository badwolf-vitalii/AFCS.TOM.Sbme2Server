using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.CCDRequests.Enums;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.OutputParameters.Customer;
using AFCS.TOM.SbmeModels.SBME;
using AFCS.TOM.SbmeModels.SBME.InputParameters;
using NLog;
using Renci.SshNet;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using DSDE_CC_ITRO2 = AFCS.TOM.SbmeModels.CCDRequests.DSDE_CC_ITRO2;
using DSDE_CC_UPRO = AFCS.TOM.SbmeModels.CCDRequests.DSDE_CC_UPRO;

namespace AFCS.TOM.Sbme2Server.Services.SBME
{
    public class CustomerService : ICustomerService
    {
        private static NLog.Logger GetLogger() => LogManager.GetLogger("Sbme2Server");
        private static bool IsSftpSenderThreadRunning;
        private NLog.Logger _logger = GetLogger();

        private string ConvertMediaDeliveryToXML(MediaDelivery mediaDelivery)
        {
            var msg = new DSDE_CC_ITRO2.MSG
            {
                name = CCD_MsgType.DSDE_CC_ITRO2,
                MessageITRO2 = new DSDE_CC_ITRO2.MessageITRO2
                {
                    TSCIssuingDate = mediaDelivery.TSCIssuingDate,
                    OperatorID = mediaDelivery.OperatorId,
                    DeviceClassID = mediaDelivery.DeviceClassId,
                    DeviceCodeID = mediaDelivery.DeviceCode,
                    TSC_REQ = new DSDE_CC_ITRO2.MSGTSC_REQ
                    {
                        ShortCardModelID = mediaDelivery.ShortCardModel,
                        TSCSerialNo = (uint)mediaDelivery.PhysicalSerialNumber,
                        SaleDevice_ID = mediaDelivery.SaleDeviceId,
                        SerialNo = mediaDelivery.LogicalSerialNumber,
                        Output_Status = mediaDelivery.OutputStatus,
                        Error_Code = mediaDelivery.ErrorCode,
                        TSC_Req_ID = mediaDelivery.TscReqId.ToString(),
                        ShiftNo = mediaDelivery.ShiftNo,
                        ShiftOpenDateString = mediaDelivery.ShiftOpenDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                        TSCValidityEndDate = mediaDelivery.TSCEndValidityDate,
                        Contracts = mediaDelivery.Contracts?.Select(p => new DSDE_CC_ITRO2.Contract
                        {
                            SaleDevice_ID = p.SaleDeviceID,
                            SerialNo = p.SerialNo
                        }).ToArray()
                    }
                }
            };
            var xml = string.Empty;
            var xsSubmit = new XmlSerializer(typeof(DSDE_CC_ITRO2.MSG));
            using (var sww = new TOM.SbmeModels.StringWriter(Encoding.UTF8))
            {
                var ns = new XmlSerializerNamespaces();
                ns.Add("", "");
                var xmlWriterSettings = new XmlWriterSettings { OmitXmlDeclaration = true };
                using var writer = XmlWriter.Create(sww, xmlWriterSettings);
                xsSubmit.Serialize(writer, msg, ns);
                xml = sww.ToString().Replace(":q1", string.Empty).Replace("q1:", string.Empty);
            }
            _logger?.Debug($"Card issuing/reissuing confirm message : {xml}");
            Console.WriteLine();
            Console.WriteLine(xml.Replace("><", ">\r\n<"));
            Console.WriteLine();
            return xml;
        }

        private string ConvertProfileRenewalToXML(ProfileRenewal profileRenewal)
        {
            var msg = new DSDE_CC_UPRO.MSG
            {
                name = CCD_MsgType.DSDE_CC_UPRO2,
                MessageUPRO = new DSDE_CC_UPRO.MessageUPRO
                {
                    OperatorID = profileRenewal.OperatorId,
                    DeviceClassID = profileRenewal.DeviceClassId,
                    DeviceCode = profileRenewal.DeviceCode,
                    TSC_REQ = new DSDE_CC_UPRO.MSGTSC_REQ
                    {
                        Status = profileRenewal.Status,
                        Error_Code = profileRenewal.ErrorCode,
                        TSC_Req_ID = profileRenewal.TSCReqID,
                        ShiftNo = profileRenewal.ShiftNo,
                        Price = profileRenewal.Price,
                        PaymentType = profileRenewal.PaymentType,
                        ShiftOpenDateString = profileRenewal.ShiftOpenDate.ToString("yyyy-MM-ddTHH:mm:ss")
                    }
                }
            };
            var xml = string.Empty;
            var xsSubmit = new XmlSerializer(typeof(DSDE_CC_UPRO.MSG));
            using (var sww = new TOM.SbmeModels.StringWriter(Encoding.UTF8))
            {
                var ns = new XmlSerializerNamespaces();
                ns.Add("", "");
                var xmlWriterSettings = new XmlWriterSettings { OmitXmlDeclaration = true };
                using var writer = XmlWriter.Create(sww, xmlWriterSettings);
                xsSubmit.Serialize(writer, msg, ns);
                xml = sww.ToString().Replace(":q1", string.Empty).Replace("q1:", string.Empty);
            }
            _logger?.Debug($"Card issuing/reissuing confirm message : {xml}");
            Console.WriteLine();
            Console.WriteLine(xml.Replace("><", ">\r\n<"));
            Console.WriteLine();
            return xml;
        }

        private static string GenerateXmlFileName() => $"Finalization_{DateTime.Now.Ticks}.xml";

        public async Task CardIssuingConfirmAsync(MediaDelivery mediaDelivery, SFTPConfirmTSCRequestConfiguration sftpConfig)
        {
            if (!sftpConfig.SendXml ||
                !(sftpConfig.SendErrorXml || (OutputStatus)mediaDelivery.OutputStatus == OutputStatus.OK)) return;

            var xml = ConvertMediaDeliveryToXML(mediaDelivery);

            if (!string.IsNullOrWhiteSpace(sftpConfig.LocalRepoPath))
            {
                var name = string.Empty;
                while (string.IsNullOrWhiteSpace(name) || File.Exists(name))
                    name = GenerateXmlFileName();
                File.WriteAllText($"{sftpConfig.LocalRepoPath}/{name}", xml);
            }
            else
                try
                {
                    var responseToSend = Encoding.ASCII.GetBytes(xml);
                    using (var client = new SftpClient(sftpConfig.ServerURL, sftpConfig.Username, sftpConfig.Password))
                    {
                        client.Connect();
                        using (var memoryStream = new MemoryStream())
                        {
                            memoryStream.Write(responseToSend, 0, responseToSend.Length);
                            memoryStream.Position = 0;
                            client.UploadFile(memoryStream, $"{sftpConfig.Path}/{GenerateXmlFileName()}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Error(ex);
                    throw;
                }
        }

        public async Task ProfileRenewalConfirmAsync(ProfileRenewal profileRenewal, SFTPConfirmTSCRequestConfiguration sftpConfig)
        {
            if (!sftpConfig.SendXml ||
                !(sftpConfig.SendErrorXml || (OutputStatus)profileRenewal.Status == OutputStatus.OK)) return;

            var xml = ConvertProfileRenewalToXML(profileRenewal);

            if (!string.IsNullOrWhiteSpace(sftpConfig.LocalRepoPath))
                File.WriteAllText($"{sftpConfig.LocalRepoPath}/{GenerateXmlFileName()}", xml);
            else
                try
                {
                    var responseToSend = Encoding.ASCII.GetBytes(xml);
                    using (var client = new SftpClient(sftpConfig.ServerURL, sftpConfig.Username, sftpConfig.Password))
                    {
                        client.Connect();
                        using (var memoryStream = new MemoryStream())
                        {
                            memoryStream.Write(responseToSend, 0, responseToSend.Length);
                            memoryStream.Position = 0;
                            client.UploadFile(memoryStream, $"{sftpConfig.Path}/{GenerateXmlFileName()}");
                        }
                    }
                }
            catch (Exception ex)
            {
                _logger?.Error(ex);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="connectionStrings">SBME_GESTOWN and SG_GESTOWN</param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public async Task<ResultForIssuingChecks> ChecksForCardIssuing(Dictionary<ConnectionString, string> connectionStrings, SFTPConfirmTSCRequestConfiguration sftpConfig, TscIssuingParameters parameters)
        {
            // TOOD: CAR-1433
            //       INSERT TSC Request
            //       HOLDER.status = "ISSON"
            //       check modified rows count == 0
            //       ? generate an "Error XML" => exit
            //       :
            //       {
            //         1. Check if the card hast not been yet issued for another customer
            //            ? TSC_REQUEST.status = "ISSON" => write & print => media delivery operation => generate an "Result XML" (ok/nok) => exit
            //            : TSC_REQUEST.ResultOfChecks = false => generate an "Error XML" => exit
            //         2. Other checks
            //       }

            var sbmeGESTOWN = connectionStrings.FirstOrDefault(p => p.Key == ConnectionString.SBME_GESTOWN);
            var sgGESTOWN = connectionStrings.FirstOrDefault(p => p.Key == ConnectionString.SG_GESTOWN);
            
            if (sbmeGESTOWN.Key == ConnectionString.NOT_SPECIFIED) throw new Exception("No SBMEGESTOWN connection string specified."); // TODO: add a new exception class
            if (sgGESTOWN.Key == ConnectionString.NOT_SPECIFIED) throw new Exception("No SGGESTOWN connection string specified."); // TODO: add a new exception class

            ResultForIssuingChecks checkResult = null;

            #region Insert TSC Request
            var step = 0;
            try
            {
                // Insert TSC request without error code
                //parameters.Status = Constants.TSC_REQUEST_STATUS_CHECK_OK;
                parameters.Status = Constants.TSC_REQUEST_STATUS_ISSON;
                parameters.CheckCode = 0;
                parameters.IssueErrorCode = TscIssueErrorCode.OK;
                _logger?.Info($"InsertTSCRequest() for {parameters.HolderInfo?.HolderId} {parameters.HolderInfo?.HolderFirstName} {parameters.HolderInfo?.HolderLastName}");
                checkResult = await DBOracleManager.InsertTSCRequest(sgGESTOWN.Value, parameters);

                // Calculate the Build Validity Date by calling a stored procedure
                _logger?.Info($"UpdateTscBuildValidityDate({checkResult.TscReqId})");
                ++step;
                var updateTscProcedure = await DBOracleManager.UpdateTscBuildValidityDate(sgGESTOWN.Value, checkResult.TscReqId);
                checkResult.IssuingDate = updateTscProcedure.IssuingDate;
                checkResult.TSCEndValidityDate = updateTscProcedure.TSCValidityEndDate;

                // here, we beleave that the checkResult.ResultOfChecks is true
            }
            catch (Exception ex)
            {
                if (step == 0)
                {
                    _logger?.Info($"InsertTSCRequest() failed: {ex.Message}");
                }
                else
                {
                    _logger?.Info($"UpdateTscBuildValidityDate() failed: {ex.Message}");
                }
                return new ResultForIssuingChecks
                {
                    ResultOfChecks = false,
                    ReasonCodeOnFail = 0
                };
            }
            #endregion

            #region Update Profile Build Validity Date
            //var updatedProfileProcedure = await DBOracleManager.UpdateProfileBuildValidityDate(sgGESTOWN.Value, checkResult.TscReqId);
            //var dates = new[]
            //{
            //    new KeyValuePair<short?, DateTime?>(updatedProfileProcedure.HolderProfileId, updatedProfileProcedure.ProfileValidityEndDate),
            //    new KeyValuePair<short?, DateTime?>(updatedProfileProcedure.HolderProfileAux1Id, updatedProfileProcedure.ProfileAux1ValidityEndDate),
            //    new KeyValuePair<short?, DateTime?>(updatedProfileProcedure.HolderProfileAux2Id, updatedProfileProcedure.ProfileAux2ValidityEndDate),
            //}.Where(p => p.Value.HasValue).ToArray();
            var sbmeTARIFFOWN = connectionStrings.FirstOrDefault(p => p.Key == ConnectionString.SBME_TARIFFOWN);
            var profileId1 = (parameters.Profiles?.Length ?? 0) > 0 ? parameters.Profiles[0] : (short)0;
            var profileId2 = (parameters.Profiles?.Length ?? 0) > 1 ? parameters.Profiles[1] : (short)0;
            var profileId3 = (parameters.Profiles?.Length ?? 0) > 2 ? parameters.Profiles[2] : (short)0;
            _logger?.Info("CalculateProfileInfos()");
            checkResult.ProfileEndValidityDates = await CalculateProfileInfos(sbmeTARIFFOWN.Value, new CalculateProfileInfosParameters {
                HolderBirthday = parameters.HolderInfo.HolderBirthday.Value,
                Profile1 = profileId1,
                Profile2 = profileId2,
                Profile3 = profileId3,
                Operator = parameters.Operator,
            }, parameters.ProfilesEvd, parameters.IsReissuing);
            if ((checkResult.ProfileEndValidityDates?.Length ?? 0) == 0)
            {
                checkResult.ResultOfChecks = false;
                checkResult.ReqCheckDsdeCode = (int)ReqCheckDSDECode.WrongOperator;
                return checkResult;
            }
            checkResult.TotalPrice = parameters.Price;
            _logger?.Info("UpdateTscReqHolderProfileBuildValidityDate()");
            await DBOracleManager.UpdateTscReqHolderProfileBuildValidityDate(
                sgGESTOWN.Value,
                checkResult.ProfileEndValidityDates.Where(p => p.EndValidityDates.HasValue).Select(p => p.EndValidityDates.Value).ToArray(),
                checkResult.TscReqId);
            #endregion

            #region Final Checks
            try
            {
                var sgn = $"{(ReqCheckDSDEOperator)parameters.Operator}";
                _logger?.Info($"ChecksForCardIssuing({checkResult.TscReqId}, {parameters.HolderInfo.HolderFiscalCode})");
                checkResult.ReqCheckDsdeCode = await DBOracleManager.ChecksForCardIssuing(sbmeGESTOWN.Value, checkResult.TscReqId, 62, sgn, parameters.HolderInfo.HolderFiscalCode); // TODO: find the value of expireInterval (gap)
                if ((ReqCheckDSDECode)checkResult.ReqCheckDsdeCode < ReqCheckDSDECode.OK) throw new Exception($"ReqCheckDSDE failed with code {parameters.ReqCheckDsdeGap}");
            }
            catch (Exception ex)
            {
                _logger?.Info($"ChecksForCardIssuing() failed: {ex.Message}");
                // Update TSC request with an error code
                _logger?.Info($"UpdateTscRequestCheckCodeWithStatus({Constants.TSC_REQUEST_STATUS_CHECK_ERROR})");
                await DBOracleManager.UpdateTscRequestCheckCodeWithStatus(sgGESTOWN.Value, checkResult.TscReqId, -1, Constants.TSC_REQUEST_STATUS_CHECK_ERROR, TscIssueErrorCode.SbmeDbGenericError);
                checkResult.ResultOfChecks = false;

                //// Insert TSC request with an error code
                //parameters.Status = Constants.TSC_REQUEST_STATUS_CHECK_ERROR;
                //parameters.CheckCode = -1;
                //parameters.IssueErrorCode = TscIssueErrorCode.SbmeDbGenericError; // ???

                //var reqCheckDsdeCode = checkResult.ReqCheckDsdeCode;
                //checkResult = await DBOracleManager.InsertTSCRequest(sgGESTOWN.Value, parameters);
                //checkResult.ReqCheckDsdeCode = reqCheckDsdeCode;

                return checkResult;
            }
            #endregion

            return checkResult;
        }

        public async Task<ProfileInfo[]> CalculateProfileInfos(string connectionString, CalculateProfileInfosParameters parameters, DateTime[]? oldPevds = null, CardIssuingFlag issuingFlag = CardIssuingFlag.JustIssued)
        {
            var profiles = await GetHolderProfiles(connectionString, parameters.Profile1, parameters.Profile2, parameters.Profile3, parameters.Operator);
            var profile1 = profiles.FirstOrDefault(p => p.HolderProfileId == parameters.Profile1);
            var profile2 = profiles.FirstOrDefault(p => p.HolderProfileId == parameters.Profile2);
            var profile3 = profiles.FirstOrDefault(p => p.HolderProfileId == parameters.Profile3);
            var takeOld = issuingFlag > CardIssuingFlag.ReissuedExpired;
            takeOld = issuingFlag == CardIssuingFlag.Broken;// || issuingFlag == CardIssuingFlag.ReissuedExpired;

            var pevd = new[]
            {
                new KeyValuePair<HolderProfile, DateTime?>(
                    profile1 ?? new HolderProfile { HolderProfileId = parameters.Profile1 },
                    takeOld || profile1 != null && SlavaUcraini(parameters.Profile1)
                        ? (oldPevds?.Length ?? 0) > 0
                            ? (DateTime?)oldPevds[0]
                            : profile1 != null
                                ? CalculateProfileEVD(profile1, parameters.HolderBirthday)
                                : null
                        : (profile1 == null ? null : CalculateProfileEVD(profile1, parameters.HolderBirthday))),
                new KeyValuePair<HolderProfile, DateTime?>(
                    profile2 ?? new HolderProfile { HolderProfileId = parameters.Profile2 },
                    takeOld || profile2 != null && SlavaUcraini(parameters.Profile2)
                        ? (oldPevds?.Length ?? 0) > 1
                            ? (DateTime?)oldPevds[1]
                            : profile2 != null
                                ? CalculateProfileEVD(profile2, parameters.HolderBirthday)
                                : null
                        : (profile2 == null ? null : CalculateProfileEVD(profile2, parameters.HolderBirthday))),
                new KeyValuePair<HolderProfile, DateTime?>(
                    profile3 ?? new HolderProfile { HolderProfileId = parameters.Profile3 },
                    takeOld || profile3 != null && SlavaUcraini(parameters.Profile3)
                        ? (oldPevds?.Length ?? 0) > 2
                            ? (DateTime?)oldPevds[2]
                            : profile3 != null
                                ? CalculateProfileEVD(profile3, parameters.HolderBirthday)
                                : null
                        : (profile3 == null ? null : CalculateProfileEVD(profile3, parameters.HolderBirthday)))
            }.Where(p => p.Value != null && p.Value.HasValue).ToArray();
            var result = new ProfileInfo[pevd.Length];
            for (var i = 0; i < result.Length; ++i)
            {
                var p = profiles.FirstOrDefault(p => p.HolderProfileId == pevd[i].Key.HolderProfileId);
                result[i] = new ProfileInfo {
                    Id = p.HolderProfileId,
                    EndValidityDates = pevd[i].Value,
                    IssuingPrice = p.IssuePrice.HasValue ? p.IssuePrice.Value : 0,
                    ReissuingPrice = p.RenewPrice.HasValue ? p.RenewPrice.Value : 0
                };
            }
            return result;
        }

        public async Task<bool> UpdateCardState(string connectionString, UpdateCardStateParameters parameters) =>
            await DBOracleManager.UpdateCardState(connectionString, parameters);

        public async Task<bool> UpdateTscRequestState(string connectionString, UpdateTscRequestStateParameters parameters) =>
            await DBOracleManager.UpdateTscRequestStatus(connectionString, parameters);

        public async Task<bool> UnlockHolder(string connectionString, uint holderId) =>
            await DBOracleManager.UpdateHolderStatus(connectionString, holderId, Constants.HOLDER_STATUS_INSERT);

        public async Task<bool> LockHolder(string connectionString, uint holderId) =>
            await DBOracleManager.UpdateHolderStatus(connectionString, holderId, Constants.HOLDER_STATUS_ISSON);

        public async Task<IList<HolderProfile>> GetHolderProfiles(string connectionString, short profile1, short profile2, short profile3, byte? providerId = null) =>
            await DBOracleManager.GetHolderProfilesAsync(connectionString, profile1, profile2, profile3, providerId);

        public async Task<IList<HolderProfileDescription>> GetAllHolderProfilesDescriptions(string connectionString) =>
            await DBOracleManager.GetAllHolderProfilesDescriptionsAsync(connectionString);

        private static bool SlavaUcraini(short profileId) => new[] { 4, 5, 22, 23, 42, 43, 44, 61, 62, 63, 64, 65, 66, 67, /*75, 76,*/ 88, 93, 94, 95, 96, 97, 130, 131, 132, 133, 134, 135, 177, 178, 179, 180, 188 }.Contains(profileId);

        private DateTime? CalculateProfileEVD(HolderProfile profile, DateTime holderBirthday)
        {
            DateTime trunc(DateTime date) => new DateTime(date.Year, date.Month, date.Day);

            var profileValidityDuration = profile.ProfileValidityDuration;
            var profileValidityLimit = profile.ProfileValidityLimit;
            var profileValidityLimitAge = profile.ProfileValidityLimitAge;
            var profileYoungAge = profile.ProfileYoungAge;
            var profileYoungValDur = profile.ProfileYoungValDur;
            var tscValidityDuration = profile.TscValidityDuration;
            var tscValidityLimit = profile.TscValidityLimit;
            var today = DateTime.Today;

            var limitAgeBirthDay = trunc(holderBirthday.AddYears(profileValidityLimitAge).AddDays(-1));
            var pevd = limitAgeBirthDay < profileValidityLimit ? limitAgeBirthDay : profileValidityLimit;

            var youngLimitBirthDate = trunc(holderBirthday.AddYears(profileYoungAge).AddDays(-1));
            if (today < youngLimitBirthDate)
            {
                var youngLimitDate = trunc(today.AddDays(profileYoungValDur - 1));
                if (youngLimitDate > youngLimitBirthDate)
                {
                    var limitDate = trunc(today.AddDays(profileValidityDuration - 1));
                    youngLimitDate = (limitDate > youngLimitBirthDate) ? limitDate : youngLimitBirthDate;
                    
                    var limitDate2 = today.AddDays(tscValidityDuration);
                    var youngLimitDate2 = (limitDate2 > youngLimitBirthDate) ? limitDate2 : youngLimitBirthDate;
                    youngLimitDate2 = new DateTime(Math.Min(youngLimitDate2.Ticks, tscValidityLimit.Ticks));

                    youngLimitDate = new DateTime(Math.Min(youngLimitDate.Ticks, youngLimitDate2.Ticks));
                }
                pevd = new DateTime(Math.Min(pevd.Ticks, youngLimitDate.Ticks));
            }
            else
            {
                if (profileValidityDuration % 365 == 0)
                {
                    // this change has been made in a decision together with ATM
                    var limitDate = trunc(today.AddYears(profileValidityDuration / 365).AddDays(-1));
                    pevd = new DateTime(Math.Min(pevd.Ticks, limitDate.Ticks));
                }
                else
                {
                    var limitDate = trunc(today.AddDays(profileValidityDuration - 1));
                    pevd = new DateTime(Math.Min(pevd.Ticks, limitDate.Ticks));
                }
            }

            var Jen1st2000 = new DateTime(2000, 1, 1);
            if (pevd < Jen1st2000) pevd = Jen1st2000;

            return trunc(pevd);
        }

        public async Task<IList<CardLayout>> GetProfileLayoutAsync(string connectionString, int profileId) =>
            await DBOracleManager.GetProfileLayoutAsync(connectionString, profileId);

        public async Task<IList<ProfileRequestMap>> GetProfileRequestMapAsync(string connectionString, int profileReqId) =>
            await DBOracleManager.GetProfileRequestMapAsync(connectionString, profileReqId);

        public async Task<IList<ProfileRequestMap>> GetProfileRequestMapByProfileIdAsync(string connectionString, int profile1, int profile2, int profile3, char gender) =>
            await DBOracleManager.GetProfileRequestMapByProfileIdAsync(connectionString, profile1, profile2, profile3, gender);

        public async Task<IList<ProfileRequestCode>> GetProfileRequestCodeAsync(string tarifownConnectionString, string confownConnectionString, char gender, byte providerId) =>
            await DBOracleManager.GetProfileRequestCodeAsync(tarifownConnectionString, confownConnectionString, gender, providerId);

        public async Task<uint> CreateCustomerAsync(string connectionString, Customer customer) =>
            await DBOracleManager.CreateCustomerAsync(connectionString, customer);

        public async Task UpdateCustomerAsync(string connectionString, uint holderId, List<PredicateFilter> filter) =>
            await DBOracleManager.UpdateCustomerAsync(connectionString, holderId, filter);

        public async Task<IList<Customer>> GetCustomersByFilterAsync(string connectionString, string[] customerPar)
        {
            IList<Customer> customers = null;
            var predicateList = new List<PredicateFilter>();
            for (var nLcv = 3; nLcv < customerPar.Length - 1; nLcv += 3)
            {
                predicateList.Add(new PredicateFilter
                {
                    FieldName = customerPar[nLcv],
                    FieldValue = customerPar[nLcv + 1],
                    FieldType = customerPar[nLcv + 2],
                });
            }
            int nRow = int.Parse(customerPar[1]);
            customers = await DBOracleManager.GetCustomersByFilterAsync(connectionString, predicateList, nRow).ConfigureAwait(false);
            return customers;
        }

        public async Task<Customer> GetCustomerByHolderIDAsync(string connectionString, uint holderID) =>
            await DBOracleManager.GetCustomerByHoldderIDAsync(connectionString, holderID);

        public async Task<Card> GetCardAsync(string connectionString, int shortCardModel, uint chipId) =>
            await DBOracleManager.GetCardAsync(connectionString, shortCardModel, chipId);

        public async Task<IList<Card>> GetCardsByHolderIDAsync(string connectionString, uint holderID) =>
            await DBOracleManager.GetCardsByHoldderIDAsync(connectionString, holderID);

        public Customer UpdateCustomer(int id, Customer customerItem) =>
            throw new NotImplementedException();

        public async Task<int> GetShortCardModel(string connectionString, decimal manufacturedId) =>
            await DBOracleManager.GetShortCardModel(connectionString, manufacturedId);

        public async Task<bool> DeleteCustomerAsync(string connectionString, uint id) =>
            await DBOracleManager.DeleteCustomerAsync(connectionString, id);

        public async Task<uint> GetCustomerIdByCardSerialNumber(string connectionString, string sn, int shortCardModel, string saleDeviceid = null) =>
            await DBOracleManager.GetCustomerIdByCardSerialNumber(connectionString, sn, shortCardModel, saleDeviceid);

        public async Task<List<GetContractsResult>> GetContracts(string connectionString, GetContractsParameters parameters) =>
            await DBOracleManager.GetContracts(connectionString, parameters);

        public async Task ClearTDSDEContracts(string connectionString) =>
            await DBOracleManager.ClearTDSDEContracts(connectionString);

        public async Task<GetProfileExtensionDetails> GetProfileExtension(string connectionString, GetProfileExtensionParameters parameters) =>
            await DBOracleManager.GetProfileExtension(connectionString, parameters);

        public async Task<int> BlackListCard(string connectionString, StolenLoastParameters parameters) =>
            await DBOracleManager.BlackListCard(connectionString, parameters);

        public async Task<bool> UpdateCardExpirationDates(string connectionString, UpdateCardExpirationDatesParameters parameters) =>
            await DBOracleManager.UpdateCardExpirationDates(connectionString, parameters);
    }
}
