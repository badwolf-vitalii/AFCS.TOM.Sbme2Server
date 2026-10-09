using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.SBME;
using AFCS.TOM.Sbme2Server.TemporarilyModels;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.OutputParameters.Customer;
using AFCS.TOM.SbmeModels.SBME;
using AFCS.TOM.SbmeModels.SBME.InputParameters;
using Newtonsoft.Json;
using NLog;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Globalization;
using System.Text;

namespace AFCS.TOM.Sbme2Server
{
    public class DBOracleManager
    {
        private static NLog.Logger Logger = LogManager.GetLogger("Sbme2Server");

        private static async Task RollbackTransactionPreservingErrorAsync(OracleTransaction? transaction)
        {
            if (transaction == null)
                return;

            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackException)
            {
                LogHelper.Error(Logger, rollbackException);
            }
        }

        // Each batch is atomic within its own Oracle connection. Separate databases
        // cannot be committed atomically without a distributed transaction.
        private static async Task DeleteTscRecordsAsync(string connectionString, string databaseName, long tscSerial, params string[] queries)
        {
            OracleConnection? connection = null;
            OracleTransaction? transaction = null;
            var commitAttempted = false;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);

                transaction = connection.BeginTransaction();
                var serial = tscSerial.ToString(CultureInfo.InvariantCulture);
                foreach (var query in queries)
                {
                    using var cmd = new OracleCommand(query, connection)
                    {
                        CommandType = CommandType.Text,
                        BindByName = true
                    };
                    cmd.Parameters.Add("TscSerial", OracleDbType.Varchar2).Value = serial;
                    await cmd.ExecuteNonQueryAsync();
                }
                commitAttempted = true;
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                if (commitAttempted)
                    Logger?.Error(ex, $"Oracle TSC cleanup commit outcome is uncertain for {databaseName}; verify the records before retrying.");
                await RollbackTransactionPreservingErrorAsync(transaction);
                throw;
            }
            finally
            {
                transaction?.Dispose();
                if (connection != null)
                {
                    if (connection.State != ConnectionState.Closed)
                        await DBOracleHelper.CloseDBConnection(connection);
                    await connection.DisposeAsync();
                }
            }
        }

        private static async Task AddDataOperation(OracleConnection connection, SbmeDataOperation operation, string tableName, string rowId, string schema = "GESTOWN")
        {
            if (!string.IsNullOrWhiteSpace(schema) && schema.Last() != '.')
                schema += ".";
            else
                schema = string.Empty;

            using (var cmd = new OracleCommand($"INSERT INTO {schema}DATAOPERATIONS (OPDATE, TABNAME, TABROWID, OPCODE) VALUES (SYSDATE, '{tableName}', :TABROWID, '{(char)operation}')", connection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add(new OracleParameter("TABROWID", OracleDbType.Varchar2)).Value = rowId;
                var res = await cmd.ExecuteScalarAsync();
            }
        }

        public static async Task<bool> DeleteCustomerAsync(string connectionString, uint id)
        {
            OracleConnection connection = null;
            var retValue = false;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.DeleteHolder();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("HolderId", (int)id));
                    var row = await cmd.ExecuteNonQueryAsync();
                    retValue = row > 0;
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return retValue;
        }

        public static async Task<uint> GetNextCustomerIdAsync(string connectionString)
        {
            OracleConnection connection = null;
            var cmdString = string.Empty;
            var retValue = (uint)0;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.GetNextCustomerId();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    var scalar = await cmd.ExecuteScalarAsync();
                    retValue = scalar == null ? retValue : uint.Parse(scalar.ToString());
                }
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetNextCustomerIdAsync", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return retValue;
        }

        public static async Task<uint> CreateCustomerAsync(string connectionString, Customer customer)
        {
            OracleConnection connection = null;
            OracleTransaction tran = null;
            var retValue = (uint)0;
            try
            {
                var photoTmp = customer.HolderPhoto;
                var signatureTmp = customer.HolderSignature;
                if (customer.HolderPhoto != null) customer.HolderPhoto = new byte[] { 1 };
                if (customer.HolderSignature != null) customer.HolderSignature = new byte[] { 1 };
                Logger?.Debug($"Preparing an INSERT CUSTOMER query: {JsonConvert.SerializeObject(customer)}");
                customer.HolderPhoto = photoTmp;
                customer.HolderSignature = signatureTmp;

                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                tran = connection.BeginTransaction();
                var query = Queries.InsertHolder();

                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    //cmd.Parameters.Add(new OracleParameter("FlagCfAuto", byte.Parse(customer.FLAGCFAuto.ToString())));
                    cmd.Parameters.Add(new OracleParameter("HolderFiscalCode", customer.HolderFiscalCode));
                    cmd.Parameters.Add(new OracleParameter("HolderFirstName", customer.HolderFirstName));
                    cmd.Parameters.Add(new OracleParameter("HolderLastName", customer.HolderLastName));
                    cmd.Parameters.Add(new OracleParameter("HolderBirthday", OracleDbType.Date)).Value = customer.HolderBirthday;

                    OracleParameter prmPhoto = cmd.Parameters.Add("HolderPhoto", OracleDbType.Blob);
                    prmPhoto.Direction = ParameterDirection.Input;
                    prmPhoto.Value = customer.HolderPhoto;
                    OracleParameter prmSignature = cmd.Parameters.Add("HolderSignature", OracleDbType.Blob);
                    prmSignature.Direction = ParameterDirection.Input;
                    prmSignature.Value = customer.HolderSignature;

                    cmd.Parameters.Add(new OracleParameter("HolderBirthPlace", customer.HolderBirthPlace));
                    cmd.Parameters.Add(new OracleParameter("HolderNationality", customer.HolderNationality));
                    cmd.Parameters.Add(new OracleParameter("HolderSex", customer.HolderSex));
                    cmd.Parameters.Add(new OracleParameter("HolderAddress", customer.HolderAddress));
                    cmd.Parameters.Add(new OracleParameter("HolderTown", customer.HolderTown));
                    cmd.Parameters.Add(new OracleParameter("HolderZipCode", customer.HolderZipCode));
                    cmd.Parameters.Add(new OracleParameter("HolderProv", customer.HolderProv));
                    cmd.Parameters.Add(new OracleParameter("HolderPhone1", customer.HolderPhone1));
                    cmd.Parameters.Add(new OracleParameter("HolderPhone2", customer.HolderPhone2));
                    cmd.Parameters.Add(new OracleParameter("HolderPhone3", customer.HolderPhone3));
                    cmd.Parameters.Add(new OracleParameter("HolderPhone4", customer.HolderPhone4));
                    cmd.Parameters.Add(new OracleParameter("HolderEMail", customer.HolderEmail));
                    if (customer.OperationId.HasValue)
                    {
                        cmd.Parameters.Add(new OracleParameter("OPERATORID", customer.OperationId.Value));
                    }
                    else
                    {
                        cmd.Parameters.Add(new OracleParameter("OPERATORID", DBNull.Value));
                    }
                    cmd.Parameters.Add(new OracleParameter
                    {
                        ParameterName = "HOLDERID",
                        OracleDbType = OracleDbType.Decimal,
                        Direction = ParameterDirection.Output
                    });

                    Logger?.Debug("Inserting a new customer");
                    var row = await cmd.ExecuteNonQueryAsync();
                    retValue = uint.Parse(cmd.Parameters["HOLDERID"].Value.ToString());
                    Logger?.Debug($"New customer created with ID: {retValue}");
                }

                var holderRowId = string.Empty;
                using (var cmd = new OracleCommand($"SELECT ROWID FROM GESTOWN.HOLDERS WHERE HOLDERID={retValue}", connection))
                {
                    cmd.CommandType = CommandType.Text;
                    var res = await cmd.ExecuteScalarAsync();
                    holderRowId = res.ToString();
                }

                await AddDataOperation(connection, SbmeDataOperation.Insert, "HOLDERS", holderRowId);

                tran.Commit();
            }
            catch (Exception ex)
            {
                Logger?.Error($"Failed creating a new customer: {ex}");
                await RollbackTransactionPreservingErrorAsync(tran);
                throw;
            }
            finally
            {
                tran?.Dispose();
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
                if (connection != null)
                    await connection.DisposeAsync();
            }
            return retValue;
        }

        public static async Task UpdateCustomerAsync(string connectionString, uint holderId, List<PredicateFilter> filter)
        {
            OracleConnection connection = null;
            OracleTransaction tran = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                tran = connection.BeginTransaction();
                var query = Queries.UpdateHolder();
                for (int nLcv = 0; nLcv < filter.Count; nLcv++)
                {
                    PredicateFilter pF = filter[nLcv];
                    query += nLcv == filter.Count - 1 ? " " + pF.FieldName.ToUpper() + " = :" + pF.FieldName.ToUpper() : " " + pF.FieldName.ToUpper() + " = :" + pF.FieldName.ToUpper() + ",";
                }
                query += $" WHERE HOLDERID = {holderId}";
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    foreach (PredicateFilter pF in filter) cmd.Parameters.Add(DBOracleHelper.GetOracleParameter(pF));
                    await cmd.ExecuteNonQueryAsync();
                }

                var holderRowId = string.Empty;
                using (var cmd = new OracleCommand($"SELECT ROWID FROM GESTOWN.HOLDERS WHERE HOLDERID={holderId}", connection))
                {
                    cmd.CommandType = CommandType.Text;
                    var res = await cmd.ExecuteScalarAsync();
                    holderRowId = res.ToString();
                }

                await AddDataOperation(connection, SbmeDataOperation.Update, "HOLDERS", holderRowId);

                tran.Commit();
            }
            catch (Exception ex)
            {
                await RollbackTransactionPreservingErrorAsync(tran);
                throw;
            }
            finally
            {
                tran?.Dispose();
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
                if (connection != null)
                    await connection.DisposeAsync();
            }
        }

        public static async Task<IList<Customer>> GetCustomersByFilterAsync(string connectionString, IList<PredicateFilter> filter, int maxRow = 50)
        {
            OracleConnection connection = null;
            try
            {
                var customers = new List<Customer>();
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var sB = new StringBuilder();
                for (var nLcv = 0; nLcv < filter.Count; ++nLcv)
                {
                    if (filter[nLcv].FieldName == null) continue;
                    if (filter[nLcv].FieldType.ToString().ToUpper().Equals(typeof(int).ToString().ToUpper()) ||
                        filter[nLcv].FieldType.ToString().ToUpper().Equals(typeof(decimal).ToString().ToUpper()))
                    {
                        sB.Append((nLcv == 0 ? " WHERE ROWNUM <= " + maxRow + " AND " : " AND ") + $"{filter[nLcv].FieldName.ToUpper()} = :{filter[nLcv].FieldName.ToString().ToUpper()}");
                    }
                    else if (filter[nLcv].FieldType.ToString().ToUpper().Equals(typeof(string).ToString().ToUpper()))
                    {
                        sB.Append((nLcv == 0 ? " WHERE ROWNUM <= " + maxRow + " AND " : " AND ") + $"{filter[nLcv].FieldName.ToUpper()} Like :{filter[nLcv].FieldName.ToString().ToUpper()}");
                    }
                    else if (filter[nLcv].FieldType.ToString().ToUpper().Equals(typeof(DateTime).ToString().ToUpper()))
                    {
                        sB.Append((nLcv == 0 ? " WHERE ROWNUM <= " + maxRow + " AND " : " AND ") + $"{filter[nLcv].FieldName.ToUpper()} = TO_DATE(:{filter[nLcv].FieldName.ToString().ToUpper()},'DD/MM/YYYY')");
                    }
                }
                var query = Queries.GetCustomerByFilter(sB.ToString());
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    foreach (var cF in filter.Where(p => p.FieldName != null))
                    {
                        if (cF.FieldType.ToString().ToUpper().Equals(typeof(int).ToString().ToUpper()) ||
                            cF.FieldType.ToString().ToUpper().Equals(typeof(decimal).ToString().ToUpper()))
                        {
                            cmd.Parameters.Add(new OracleParameter($"{cF.FieldName.ToUpper()}", $"{cF.FieldValue}".ToUpper()));
                        }
                        else if (cF.FieldType.ToString().ToUpper().Equals(typeof(string).ToString().ToUpper()))
                        {
                            cmd.Parameters.Add(new OracleParameter($"{cF.FieldName.ToUpper()}", $"{cF.FieldValue}%".ToUpper()));
                        }
                        else if (cF.FieldType.ToString().ToUpper().Equals(typeof(DateTime).ToString().ToUpper()))
                        {
                            var parsed = DateTime.TryParseExact(cF.FieldValue.ToString(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDay)
                                || DateTime.TryParseExact(cF.FieldValue.ToString(), "dd/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out birthDay)
                                || DateTime.TryParseExact(cF.FieldValue.ToString(), "d/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out birthDay)
                                || DateTime.TryParseExact(cF.FieldValue.ToString(), "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out birthDay);
                            if (!parsed)
                                Logger?.Error($"FieldName: {cF.FieldName}, FieldValue: {cF.FieldValue}");
                            var value = $"{birthDay:dd/MM/yyyy}".ToUpper();
                            cmd.Parameters.Add(new OracleParameter($"{cF.FieldName.ToUpper()}", value));
                        }
                    }
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var customer = DBOracleHelper.GetInstanceOfType<Customer>(reader);
                            customers.Add(customer);
                        }
                    }
                }
                return customers.OrderBy(p => p.HolderLastName).ThenBy(p => p.HolderFirstName).ToList();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        private static async Task<IList<short>> GetLayoutsU(string confownConnectionString)
        {
            OracleConnection connection = null;
            List<short> result = new List<short>();
            try
            {
                var crpList = new List<ProfileRequestCode>();
                connection = await DBOracleHelper.OpenDBConnection(confownConnectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(confownConnectionString);
                var query = Queries.GetLayoutsU();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var scalar = reader.GetValue(0);
                            if (short.TryParse(scalar?.ToString(), out var value)) result.Add(value);
                        }
                    }
                }
                return result;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<IList<HolderProfile>> GetHolderProfilesAsync(string tarifownConnectionString, short profile1, short profile2, short profile3, byte? providerId = null)
        {
            OracleConnection? connection = null;
            try
            {
                var profiles = new List<HolderProfile>();
                connection = await DBOracleHelper.OpenDBConnection(tarifownConnectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(tarifownConnectionString);
                var query = Queries.GetHolderProfiles();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("PROFILE1", profile1));
                    cmd.Parameters.Add(new OracleParameter("PROFILE2", profile2));
                    cmd.Parameters.Add(new OracleParameter("PROFILE3", profile3));
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var profile = DBOracleHelper.GetInstanceOfType<HolderProfile>(reader);
                            profiles.Add(profile);
                        }
                    }
                }
                return providerId.HasValue
                    ? profiles?.Where(p => (p.SaleagentListId >> providerId.Value & 1) == 1)?.Select(p => p)?.ToList() ?? new List<HolderProfile>()
                    : profiles;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<IList<HolderProfileDescription>> GetAllHolderProfilesDescriptionsAsync(string tarifownConnectionString)
        {
            OracleConnection? connection = null;
            try
            {
                var profiles = new List<HolderProfileDescription>();
                connection = await DBOracleHelper.OpenDBConnection(tarifownConnectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(tarifownConnectionString);
                var query = Queries.GetAllHolderProfilesDescriptions();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var profile = DBOracleHelper.GetInstanceOfType<HolderProfileDescription>(reader);
                            profiles.Add(profile);
                        }
                    }
                }
                return profiles;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<IList<ProfileRequestCode>> GetProfileRequestCodeAsync(string tarifownConnectionString, string confownConnectionString, char gender, byte providerId)
        {
            //var groupedLayouts = await GetLayoutsU(confownConnectionString);
            //if (groupedLayouts == null) throw new Exception("Failed to get layouts");
            OracleConnection connection = null;
            try
            {
                var crpList = new List<ProfileRequestCode>();
                var crpsWithMaps = new List<ProfileRequestCodeWithMap>();
                connection = await DBOracleHelper.OpenDBConnection(tarifownConnectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(tarifownConnectionString);
                var query = Queries.GetProfileRequestsByProviderIdWithMaps();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("SEX", gender));
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var crpWithMap = DBOracleHelper.GetInstanceOfType<ProfileRequestCodeWithMap>(reader);
                            crpsWithMaps.Add(crpWithMap);
                        }
                    }
                    var good = crpsWithMaps.Where(p => (p.SaleAgentListId >> providerId & 1) == 1 /*&& groupedLayouts.Contains(p.HolderProfileId)*/).Select(p => p.HolderProfileId).Distinct();
                    foreach (var crp in crpsWithMaps.Where(p => good.Contains(p.HolderProfileId)))
                        crpList.Add(new ProfileRequestCode
                        {
                            ProfileRequestCodeId = crp.ProfileRequestCodeId,
                            ProfileRequestCodeDescr = crp.ProfileRequestCodeDescr
                        });
                }
                return crpList;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<IList<ProfileRequestMap>> GetProfileRequestMapAsync(string connectionString, int profileReqId)
        {
            OracleConnection connection = null;
            try
            {
                var profiles = new List<ProfileRequestMap>();
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.ProfileRequestMap();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("profilerequestcodeid", profileReqId));
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var profile = DBOracleHelper.GetInstanceOfType<ProfileRequestMap>(reader);
                            profiles.Add(profile);
                        }
                    }
                }
                return profiles;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<IList<ProfileRequestMap>> GetProfileRequestMapByProfileIdAsync(string connectionString, int profile1, int profile2, int profile3, char gender = (char)0)
        {
            OracleConnection connection = null;
            try
            {
                var profiles = new List<ProfileRequestMap>();
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.ProfileRequestMapByProfileId();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("profile1", profile1));
                    cmd.Parameters.Add(new OracleParameter("profile2", profile2));
                    cmd.Parameters.Add(new OracleParameter("profile3", profile3));
                    cmd.Parameters.Add(new OracleParameter("sex", gender == 0 ? 'X' : gender));
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var profile = DBOracleHelper.GetInstanceOfType<ProfileRequestMap>(reader);
                            profiles.Add(profile);
                        }
                    }
                }
                return profiles.OrderBy(p => p.ProfileRequestCodeType != 'P').ToList();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<ResultForIssuingChecks> InsertTSCRequest(string connectionString, TscIssuingParameters parameters)
        {
            if (HelperClasses.JsonSerializer.DebugConfig.LogInsertTSCRequest ?? false)
            {
                LogHelper.Debug(Logger, JsonConvert.SerializeObject(parameters, Formatting.Indented));
            }

            OracleConnection connection = null;
            try
            {
                // Dovrà Popolare La TSC_REQUEST e restituire un ResultForIssuingChecks
                var checkResult = new ResultForIssuingChecks
                {
                    TscReqId = 0,
                    ResultOfChecks = parameters.CheckCode == 0 && parameters.IssueErrorCode == TscIssueErrorCode.OK,
                    ReasonCodeOnFail = 0,
                    ShortCardModel = parameters.ShortCardModel,
                    SerialNumber = (uint)DateTime.Now.Subtract(new DateTime(1997, 1, 1, 0, 0, 0)).TotalSeconds,
                    TotalPrice = 0,
                    TSCEndValidityDate = null,
                    ProfileEndValidityDates = null
                };

                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.InsertTscRequest();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("TSCCORRELATIONID", parameters.TscCorrelationId)); // xxyyyzzzzz (x: operator, y: device class, z: device code)
                    cmd.Parameters.Add(new OracleParameter("HOLDERID", (int)parameters.HolderInfo.HolderId));
                    cmd.Parameters.Add(new OracleParameter("HOLDERFISCALCODE", parameters.HolderInfo.HolderFiscalCode));
                    cmd.Parameters.Add(new OracleParameter("TSCVALIDITYENDDATE", checkResult.TSCEndValidityDate));
                    cmd.Parameters.Add(new OracleParameter("FLG_ISREISSUING", (char)parameters.IsReissuing));
                    cmd.Parameters.Add(new OracleParameter("OUTPUTSTATUS", parameters.Status));
                    cmd.Parameters.Add(new OracleParameter("CHECKCODE", parameters.CheckCode));
                    cmd.Parameters.Add(new OracleParameter("ISSUEERRORCODE", (byte)parameters.IssueErrorCode));
                    cmd.Parameters.Add(new OracleParameter("FLG_ISSORIGIN", "D"));
                    cmd.Parameters.Add(new OracleParameter("REQDATE", OracleDbType.Date)).Value = DateTime.Now;
                    cmd.Parameters.Add(new OracleParameter("FLAGCFAUTO", 1));
                    cmd.Parameters.Add(new OracleParameter("FLGRESMILAN", 1));
                    cmd.Parameters.Add(new OracleParameter("FLGSENDDOM", 1));
                    cmd.Parameters.Add(new OracleParameter("FLGDISPQUEST", 1));
                    cmd.Parameters.Add(new OracleParameter("LAYOUTNAME", parameters.LayoutName));
                    cmd.Parameters.Add(new OracleParameter("PRICE", parameters.Price));
                    if (parameters.Crp == -1)
                        cmd.Parameters.Add(new OracleParameter("PROFILEREQUESTCODEID", DBNull.Value));
                    else
                        cmd.Parameters.Add(new OracleParameter("PROFILEREQUESTCODEID", parameters.Crp));
                    cmd.Parameters.Add(new OracleParameter("HOLDERPROFILEID", parameters.Profiles.Length > 0 ? parameters.Profiles[0] : 0));
                    cmd.Parameters.Add(new OracleParameter("HOLDERPROFILEAUX1ID", parameters.Profiles.Length > 1 ? parameters.Profiles[1] : (short?)null));
                    cmd.Parameters.Add(new OracleParameter("HOLDERPROFILEAUX2ID", parameters.Profiles.Length > 2 ? parameters.Profiles[2] : (short?)null));
                    cmd.Parameters.Add(new OracleParameter("OPERATORID", parameters.Operator));
                    cmd.Parameters.Add(new OracleParameter("DEVICECLASSID", parameters.DeviceClass));
                    cmd.Parameters.Add(new OracleParameter("DEVICECODE", parameters.DeviceCode));

                    cmd.Parameters.Add(new OracleParameter("HOLDERFIRSTNAME", parameters.HolderInfo.HolderFirstName));
                    cmd.Parameters.Add(new OracleParameter("HOLDERLASTNAME", parameters.HolderInfo.HolderLastName));
                    cmd.Parameters.Add(new OracleParameter("HOLDERBIRTHDAY", OracleDbType.Date)).Value = parameters.HolderInfo.HolderBirthday;
                    cmd.Parameters.Add(new OracleParameter("HOLDERBIRTHPLACE", parameters.HolderInfo.HolderBirthPlace));
                    cmd.Parameters.Add(new OracleParameter("HOLDERNATIONALITY", parameters.HolderInfo.HolderNationality));
                    cmd.Parameters.Add(new OracleParameter("HOLDERPHOTO", parameters.HolderInfo.HolderPhoto));
                    cmd.Parameters.Add(new OracleParameter("HOLDERSIGNATURE", parameters.HolderInfo.HolderSignature));
                    cmd.Parameters.Add(new OracleParameter("HOLDERADDRESS", parameters.HolderInfo.HolderAddress));
                    cmd.Parameters.Add(new OracleParameter("HOLDERTOWN", parameters.HolderInfo.HolderTown));
                    cmd.Parameters.Add(new OracleParameter("HOLDERZIPCODE", parameters.HolderInfo.HolderZipCode));
                    cmd.Parameters.Add(new OracleParameter("HOLDERPROV", parameters.HolderInfo.HolderProv));
                    cmd.Parameters.Add(new OracleParameter("HOLDERSEX", parameters.HolderInfo.HolderSex));
                    cmd.Parameters.Add(new OracleParameter("HOLDERPHONE1", parameters.HolderInfo.HolderPhone1));
                    cmd.Parameters.Add(new OracleParameter("HOLDERPHONE2", parameters.HolderInfo.HolderPhone2));
                    cmd.Parameters.Add(new OracleParameter("HOLDERPHONE3", parameters.HolderInfo.HolderPhone3));
                    cmd.Parameters.Add(new OracleParameter("HOLDERPHONE4", parameters.HolderInfo.HolderPhone4));
                    cmd.Parameters.Add(new OracleParameter("HOLDEREMAIL", parameters.HolderInfo.HolderEmail));
                    cmd.Parameters.Add(new OracleParameter("SCHOOLID", parameters.HolderInfo.SchoolId));
                    cmd.Parameters.Add(new OracleParameter("FLGAUTOCERTIF", parameters.HolderInfo.FLGAutoCertif));
                    cmd.Parameters.Add(new OracleParameter("SALARY", parameters.HolderInfo.Salary));
                    cmd.Parameters.Add(new OracleParameter("COMPANYID", parameters.HolderInfo.CompanyId));
                    cmd.Parameters.Add(new OracleParameter("FLGRID", parameters.HolderInfo.FLGRId));
                    cmd.Parameters.Add(new OracleParameter("BANKNAME", parameters.HolderInfo.BankName));
                    cmd.Parameters.Add(new OracleParameter("AGENCY", parameters.HolderInfo.Agency));
                    cmd.Parameters.Add(new OracleParameter("CIN_ABI", parameters.HolderInfo.CIN_ABI));
                    cmd.Parameters.Add(new OracleParameter("CAB", parameters.HolderInfo.CAB));
                    cmd.Parameters.Add(new OracleParameter("CCNUMBER", parameters.HolderInfo.CCNumber));
                    cmd.Parameters.Add(new OracleParameter("FLGADDCREDITCARD", parameters.HolderInfo.FLGADDCreditCard));
                    cmd.Parameters.Add(new OracleParameter("CREDITCARDNO", parameters.HolderInfo.CreditCardNo));
                    cmd.Parameters.Add(new OracleParameter("ENDVALDATECREDITCARD", OracleDbType.Date)).Value = parameters.HolderInfo.EndValDateCreditCard;
                    cmd.Parameters.Add(new OracleParameter("FLGSENDEC", parameters.HolderInfo.FLGSENDEC));
                    cmd.Parameters.Add(new OracleParameter("CREDITCARDTYPE", parameters.HolderInfo.CreditCardType));
                    cmd.Parameters.Add(new OracleParameter("FLGFATTURAZIONE", parameters.HolderInfo.FLGFatturazione));
                    cmd.Parameters.Add(new OracleParameter("RAGIONESOCIALE", parameters.HolderInfo.RagioneSociale));
                    cmd.Parameters.Add(new OracleParameter("PARTITAIVA", parameters.HolderInfo.PartitaIVA));
                    cmd.Parameters.Add(new OracleParameter("FATTFISCALCODE", parameters.HolderInfo.FattFiscalCode));
                    cmd.Parameters.Add(new OracleParameter("FATTADDRESS", parameters.HolderInfo.FattAddress));
                    cmd.Parameters.Add(new OracleParameter("FATTZIPCODE", parameters.HolderInfo.FattZipCode));
                    cmd.Parameters.Add(new OracleParameter("FATTTOWN", parameters.HolderInfo.FattTown));
                    cmd.Parameters.Add(new OracleParameter("FATTPROV", parameters.HolderInfo.FattProv));
                    cmd.Parameters.Add(new OracleParameter("MATRICOLA", parameters.HolderInfo.Matricola));
                    cmd.Parameters.Add(new OracleParameter("FLGPRVCY1", parameters.HolderInfo.FLGPrvcy1));
                    cmd.Parameters.Add(new OracleParameter("FLGPRVCY2", parameters.HolderInfo.FLGPrvcy2));

                    if (parameters.OldShortCardModelId.HasValue)
                        cmd.Parameters.Add(new OracleParameter("OLDSHORTCARDMODELID", parameters.OldShortCardModelId.Value));
                    else
                        cmd.Parameters.Add(new OracleParameter("OLDSHORTCARDMODELID", DBNull.Value));
                    if (parameters.OldTSCSerialNo.HasValue)
                        cmd.Parameters.Add(new OracleParameter("OLDTSCSERIALNO", parameters.OldTSCSerialNo.Value));
                    else
                        cmd.Parameters.Add(new OracleParameter("OLDTSCSERIALNO", DBNull.Value));
                    cmd.Parameters.Add(new OracleParameter("TSCSERIALNO", (long)parameters.CardId));

                    cmd.Parameters.Add(new OracleParameter
                    {
                        ParameterName = "TSCREQID",
                        OracleDbType = OracleDbType.Int32,
                        Direction = ParameterDirection.Output
                    });
                    await cmd.ExecuteNonQueryAsync();
                    checkResult.TscReqId = int.Parse(cmd.Parameters["TSCREQID"].Value.ToString());
                }
                return checkResult;
            }
            catch (Exception ex)
            {
                Logger?.Error(ex);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<bool> UpdateHolderStatus(string connectionString, uint holderId, string status)
        {
            OracleConnection connection = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.UpdateHolderStatus();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("STATUS", status));
                    cmd.Parameters.Add(new OracleParameter("HOLDERID", (int)holderId));
                    var rows = await cmd.ExecuteNonQueryAsync();
                    return rows > 0;
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<bool> UpdateTscRequestStatus(string connectionString, UpdateTscRequestStateParameters parameters)
        {
            OracleConnection connection = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.UpdateTscRequestStatus();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("OUTPUTSTATUS", parameters.NewState));
                    cmd.Parameters.Add(new OracleParameter("TSCREQID", parameters.RequestId));
                    var rows = await cmd.ExecuteNonQueryAsync();
                    return rows > 0;
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<int> UpdateTscRequestCheckCode(string connectionString, int requestId, short checkCode, TscIssueErrorCode errorCode)
        {
            OracleConnection connection = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.UpdateTscRequestCheckCode();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("CHECKCODE", checkCode));
                    cmd.Parameters.Add(new OracleParameter("ISSUEERRORCODE", (byte)errorCode));
                    cmd.Parameters.Add(new OracleParameter("TSCREQID", requestId));
                    var rows = await cmd.ExecuteNonQueryAsync();
                    return rows;
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<int> UpdateTscRequestCheckCodeWithStatus(string connectionString, int requestId, short checkCode, string status, TscIssueErrorCode errorCode)
        {
            OracleConnection connection = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.UpdateTscRequestCheckCodeWithStatus();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("CHECKCODE", checkCode));
                    cmd.Parameters.Add(new OracleParameter("ISSUEERRORCODE", (byte)errorCode));
                    cmd.Parameters.Add(new OracleParameter("OUTPUTSTATUS", status));
                    cmd.Parameters.Add(new OracleParameter("TSCREQID", requestId));
                    var rows = await cmd.ExecuteNonQueryAsync();
                    return rows;
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<int> ChecksForCardIssuing(string connectionString, int tscRequestId, int expireInterval, string sgn, string fiscalCode)
        {
            OracleConnection connection = null;
            var retValue = -3;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                //using var cmd = new OracleCommand("REQCHECKDSDE", connection)
                using var cmd = new OracleCommand("REQCHECKDSDE_FISCALCODE", connection);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new OracleParameter("ReqNum", tscRequestId));
                cmd.Parameters.Add("Sgn", OracleDbType.NVarchar2).Value = sgn;
                cmd.Parameters.Add(new OracleParameter("Gap", expireInterval));
                cmd.Parameters.Add("Fiscal ", OracleDbType.NVarchar2).Value = fiscalCode;
                cmd.Parameters.Add(new OracleParameter
                {
                    ParameterName = "Esito",
                    OracleDbType = OracleDbType.Int32,
                    Direction = ParameterDirection.Output
                });

                var row = await cmd.ExecuteNonQueryAsync();
                retValue = int.Parse(cmd.Parameters["Esito"].Value.ToString());
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return retValue;
        }

        public static async Task<bool> UpdateCardState(string connectionString, UpdateCardStateParameters parameters, bool verification = true)
        {
            OracleConnection connection = null;
            var result = false;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.UpdateCardStatus();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("STATUS", parameters.NewState));
                    cmd.Parameters.Add(new OracleParameter("SHORTCARDMODELID", parameters.ShortCardModel));
                    cmd.Parameters.Add(new OracleParameter("TSCSERIALNO", parameters.CardSerialNo));
                    cmd.Parameters.Add(new OracleParameter("HOLDERID", (int)parameters.HolderId));
                    var res = await cmd.ExecuteNonQueryAsync();
                    result = res > 0;
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return !verification || result;
        }

        public static async Task<IList<CardLayout>> GetProfileLayoutAsync(string connectionString, int profileId)
        {
            OracleConnection connection = null;
            try
            {
                var profiles = new List<CardLayout>();
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.Layouts();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("HOLDERPROFILEID", profileId));
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var layout = DBOracleHelper.GetInstanceOfType<CardLayout>(reader);
                            profiles.Add(layout);
                        }
                    }
                }
                return profiles;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<Customer> GetCustomerByHoldderIDAsync(string connectionString, uint holderID)
        {
            OracleConnection connection = null;
            try
            {
                var customer = new Customer();
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.GetCustomerByHolderID();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("holderID", (int)holderID));
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        var read = await reader.ReadAsync();
                        customer = read ? DBOracleHelper.GetInstanceOfType<Customer>(reader) : null;
                    }
                }
                return customer;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<Card> GetCardAsync(string connectionString, int shortCardModel, uint chipId)
        {
            OracleConnection connection = null;
            try
            {
                Card card = null;
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.GetCard();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("tscserialno", (decimal)chipId));
                    cmd.Parameters.Add(new OracleParameter("shortcardmodelid", shortCardModel));
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        await reader.ReadAsync();
                        card = DBOracleHelper.GetInstanceOfType<Card>(reader);
                    }
                }
                return card;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<IList<Card>> GetCardsByHoldderIDAsync(string connectionString, uint holderID)
        {
            OracleConnection connection = null;
            try
            {
                var cards = new List<Card>();
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.GetCardsByHolderID();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("holderID", (int)holderID));
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var card = DBOracleHelper.GetInstanceOfType<Card>(reader);
                            cards.Add(card);
                        }
                    }
                }
                return cards.ToList();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<int> GetShortCardModel(string connectionString, decimal manufacturedId)
        {
            //return 1014; // cardType is Calypso & cardManufacturedID == 9932162562 -> 1014; cardType is Mifare -> 1003
            OracleConnection connection = null;
            var retValue = 0;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.GetShortCardModel();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("MANUFACTURERID", manufacturedId));
                    retValue = (int)cmd.ExecuteScalar();
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return retValue;
        }

        public static async Task<TscBuildDatesProcedure> UpdateTscBuildValidityDate(string connectionString, decimal tscRequestId)
        {
            OracleConnection connection = null;
            TscBuildDatesProcedure retValue = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                using (var cmd = new OracleCommand("TSCBUILDVALIDITYDATE", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(new OracleParameter("ReqNum", tscRequestId));
                    cmd.ExecuteNonQuery();
                }
                var query = Queries.GetTSCDates();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.Parameters.Add(new OracleParameter("TSCReqID", tscRequestId));
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        await reader.ReadAsync();
                        retValue = DBOracleHelper.GetInstanceOfType<TscBuildDatesProcedure>(reader);
                    }
                }
                if (HelperClasses.JsonSerializer.DebugConfig.LogInsertTSCRequest ?? false)
                {
                    LogHelper.Debug(Logger, $"ReqID: {tscRequestId}");
                    if (retValue == null)
                    {
                        LogHelper.Debug(Logger, "TscBuildDates: null");
                    }
                    else
                    {
                        LogHelper.Debug(Logger, JsonConvert.SerializeObject(retValue, Formatting.Indented));
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return retValue;
        }

        public static async Task UpdateTscReqHolderProfileBuildValidityDate(string connectionString, DateTime[] profiles, int tscReqId)
        {
            OracleConnection connection = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var arrProfiles0 = new[]
                {
                    "PROFILEVALIDITYENDDATE",
                    "PROFILEAUX1VALIDITYENDDATE",
                    "PROFILEAUX2VALIDITYENDDATE"
                };
                var arrProfiles = arrProfiles0.ToArray();

                for (var i = 0; i < arrProfiles.Length; ++i)
                    arrProfiles[i] = i < profiles.Length
                        ? $"{arrProfiles[i]} = :{arrProfiles[i]}"
                        : string.Empty;

                arrProfiles = arrProfiles.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();

                if (arrProfiles.Length == 0)
                    throw new Exception("No profiles end validity dates specified");

                var prfls = "SET " + string.Join(", ", arrProfiles);

                var query = Queries.UpdateTscRequestHolderProfiles(prfls);
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    for (var i = 0; i < arrProfiles.Length; ++i)
                        cmd.Parameters.Add(new OracleParameter(arrProfiles0[i], OracleDbType.Date)).Value = profiles[i];
                    cmd.Parameters.Add(new OracleParameter("TSCREQID", tscReqId));
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<ProfileValidityEndDateProcedure> UpdateProfileBuildValidityDate(string connectionString, decimal tscRequestId)
        {
            OracleConnection connection = null;
            ProfileValidityEndDateProcedure retValue = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                using (var cmd = new OracleCommand("PROFILEBUILDVALIDITYDATE", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add(new OracleParameter("ReqNum", tscRequestId));
                    cmd.ExecuteNonQuery();
                }
                var query = Queries.GetProfileValidityEndDate();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.Parameters.Add(new OracleParameter("TSCReqID", tscRequestId));
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        await reader.ReadAsync();
                        retValue = DBOracleHelper.GetInstanceOfType<ProfileValidityEndDateProcedure>(reader);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return retValue;
        }

        /// <param name="saleDeviceid">if null or empty, searches by physical sn, otherwise searches by logical sn</param>
        public static async Task<uint> GetCustomerIdByCardSerialNumber(string connectionString, string sn, int shortCardModel, string saleDeviceid = null)
        {
            OracleConnection connection = null;
            var retValue = (uint)0;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var physical = string.IsNullOrWhiteSpace(saleDeviceid);
                var query = physical ? Queries.GetCustomerIdByPhysicalSerialNumber() : Queries.GetCustomerIdByLogicalSerialNumber();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    if (!physical) cmd.Parameters.Add(new OracleParameter("saledeviceid", int.Parse(saleDeviceid)));
                    cmd.Parameters.Add(new OracleParameter("serial", long.Parse(sn)));
                    cmd.Parameters.Add(new OracleParameter("shortcardmodelid", shortCardModel));
                    var scalar = cmd.ExecuteScalar();
                    if (uint.TryParse(scalar?.ToString(), out var value)) retValue = value;
                }
                return retValue;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<List<GetContractsResult>> GetContracts(string connectionString, GetContractsParameters parameters)
        {
            OracleConnection connection = null;
            var retValue = new List<GetContractsResult>();
            try
            {
                var query = Queries.GetTDSDEContracts();
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);

                // Call the procedure that prepares contracts info
                using (var cmd = new OracleCommand("GetContracts", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    var retParam = cmd.Parameters.Add("v_Return", OracleDbType.Int32, ParameterDirection.ReturnValue);
                    cmd.Parameters.Add(new OracleParameter("v_ShortCardModelID", parameters.ShortCardModel));
                    cmd.Parameters.Add(new OracleParameter("v_TSCSerialNo", parameters.TscSerialNumber));
                    await cmd.ExecuteNonQueryAsync();
                }

                // Get contracts info
                var queryDV = Queries.GetParametersDV();
                var querySV_RV = Queries.GetParametersSV_RV();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("SHORTCARDMODELID", parameters.ShortCardModel));
                    cmd.Parameters.Add(new OracleParameter("TSCSERIALNO", parameters.TscSerialNumber));
                    //cmd.Parameters.Add(new OracleParameter("DEVICE", parameters.DeviceId));
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var contract = DBOracleHelper.GetInstanceOfType<GetContractsResult>(reader);
                            contract.ParametersDV = new List<ParameterDV>();
                            contract.ParametersSV_RV = new List<ParameterSV_RV>();
                            var serialNo = contract.SerialNo;
                            // Get DV parameters
                            using (var cmdDV = new OracleCommand(queryDV, connection))
                            {
                                cmdDV.CommandType = CommandType.Text;
                                cmdDV.Parameters.Add(new OracleParameter("SERIALNO", contract.SerialNo));
                                cmdDV.Parameters.Add(new OracleParameter("SALEDEVICEID", contract.SaleDeviceId));

                                using var readerDV = await cmdDV.ExecuteReaderAsync();
                                while (await readerDV.ReadAsync())
                                {
                                    var parameter = DBOracleHelper.GetInstanceOfType<ParameterDV>(readerDV);
                                    contract.ParametersDV.Add(parameter);
                                }
                            }
                            // Get SV_RV parameters
                            using (var cmdSV_RV = new OracleCommand(querySV_RV, connection))
                            {
                                cmdSV_RV.CommandType = CommandType.Text;
                                cmdSV_RV.Parameters.Add(new OracleParameter("SERIALNO", contract.SerialNo));
                                cmdSV_RV.Parameters.Add(new OracleParameter("SALEDEVICEID", contract.SaleDeviceId));
                                using var readerSV_RV = await cmdSV_RV.ExecuteReaderAsync();
                                while (await readerSV_RV.ReadAsync())
                                {
                                    var parameter = DBOracleHelper.GetInstanceOfType<ParameterSV_RV>(readerSV_RV);
                                    contract.ParametersSV_RV.Add(parameter);
                                }
                            }
                            retValue.Add(contract);
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return retValue;
        }

        public static async Task ClearTDSDEContracts(string connectionString)
        {
            OracleConnection connection = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);

                var query = Queries.ClearTDSDEContracts();
                var count = await ExecuteNonQuery(query, connection);
                Logger?.Debug($"{count} records deneted from T_DSDE_CONTRACTS");

                query = Queries.ClearParamsDv();
                count = await ExecuteNonQuery(query, connection);
                Logger?.Debug($"{count} records deneted from PARAMETERDV");

                query = Queries.ClearParamsSvRv();
                count = await ExecuteNonQuery(query, connection);
                Logger?.Debug($"{count} records deneted from PARAMETERSV_RV");
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        private static async Task<int> ExecuteNonQuery(string commandText, OracleConnection connection, bool throwable = true)
        {
            var count = 0;
            using (var cmd = new OracleCommand(commandText, connection))
            {
                try
                {
                    cmd.CommandType = CommandType.Text;
                    count = await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    LogHelper.Error(Logger, ex);
                    LogHelper.Error(Logger, commandText);
                    if (throwable) throw;
                }
            }
            return count;
        }

        public static async Task<GetProfileExtensionDetails> GetProfileExtension(string connectionString, GetProfileExtensionParameters parameters)
        {
            OracleConnection connection = null;
            GetProfileExtensionDetails retValue = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                using var cmd = new OracleCommand("Profile_Extension", connection);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new OracleParameter("v_ShortCardModelID", parameters.ShortCardModel));
                cmd.Parameters.Add(new OracleParameter("v_TSCSerialNo", parameters.TSCSerialNo));
                cmd.Parameters.Add(new OracleParameter("v_HolderProfile1_ID", parameters.HolderProfile1));
                cmd.Parameters.Add(new OracleParameter("v_HolderProfile2_ID", parameters.HolderProfile2.HasValue ? parameters.HolderProfile2.Value : 0));
                cmd.Parameters.Add(new OracleParameter("v_HolderProfile3_ID", parameters.HolderProfile3.HasValue ? parameters.HolderProfile3.Value : 0));
                cmd.Parameters.Add(new OracleParameter("v_operatorid", parameters.Operator));
                cmd.Parameters.Add(new OracleParameter("v_devclass", parameters.DeviceClass));
                cmd.Parameters.Add(new OracleParameter("v_devcode", parameters.DeviceCode));
                cmd.Parameters.Add(new OracleParameter("v_data_1", OracleDbType.Date)).Value = parameters.Profile1ExpiryDate;
                cmd.Parameters.Add(new OracleParameter("v_data_2", OracleDbType.Date)).Value = parameters.Profile2ExpiryDate.HasValue ? parameters.Profile2ExpiryDate : DBNull.Value;
                cmd.Parameters.Add(new OracleParameter("v_data_3", OracleDbType.Date)).Value = parameters.Profile3ExpiryDate.HasValue ? parameters.Profile3ExpiryDate : DBNull.Value;
                cmd.Parameters.Add(new OracleParameter
                {
                    ParameterName = "v_prezzo_out",
                    OracleDbType = OracleDbType.Int32,
                    Direction = ParameterDirection.Output
                });
                cmd.Parameters.Add(new OracleParameter
                {
                    ParameterName = "v_req_id_out",
                    OracleDbType = OracleDbType.Int32,
                    Direction = ParameterDirection.Output
                });
                cmd.Parameters.Add(new OracleParameter
                {
                    ParameterName = "v_esito_out",
                    OracleDbType = OracleDbType.Int32,
                    Direction = ParameterDirection.Output
                });

                var row = await cmd.ExecuteNonQueryAsync();
                var requestId = int.Parse(cmd.Parameters["v_req_id_out"].Value.ToString());
                var result = int.Parse(cmd.Parameters["v_esito_out"].Value.ToString());
                retValue = result == 1
                    ? new GetProfileExtensionDetails
                    {
                        RequestId = requestId,
                        Price = int.Parse(cmd.Parameters["v_prezzo_out"].Value.ToString()),
                        Result = result
                    }
                    : new GetProfileExtensionDetails
                    {
                        RequestId = requestId,
                        Result = result
                    };
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return retValue;
        }

        public static async Task<int> BlackListCard(string connectionString, StolenLoastParameters parameters)
        {
            var resultCode = -1;
            OracleConnection connection = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                using var cmd = new OracleCommand("Stolen_lost", connection);
                cmd.CommandType = CommandType.StoredProcedure;

                var pRetval = new OracleParameter
                {
                    Direction = ParameterDirection.ReturnValue,
                    OracleDbType = OracleDbType.Int32
                };
                cmd.Parameters.Add(pRetval);
                
                cmd.Parameters.Add(new OracleParameter("v_ShortCardModelID", parameters.ShortCardModel));
                cmd.Parameters.Add(new OracleParameter("v_TSCSerialNo", parameters.TSCSerialNo));
                cmd.Parameters.Add(new OracleParameter("v_StolenDate", OracleDbType.Date)).Value = parameters.StolenDate;
                cmd.Parameters.Add(new OracleParameter("v_ReasonCode", parameters.ReasonCode));

                await cmd.ExecuteNonQueryAsync();
                resultCode = int.TryParse(pRetval.Value.ToString(), out var result) ? result : -2;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return resultCode;
        }

        public static async Task<bool> UpdateCardExpirationDates(string connectionString, UpdateCardExpirationDatesParameters parameters)
        {
            var result = false;
            OracleConnection? connection = null;
            OracleTransaction? tran = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                result = true;
                tran = connection.BeginTransaction();
                var query = Queries.UpdateCardEvd();
                var dates = string.Empty;
                if (parameters.CardEvd.HasValue)
                    dates += ", TSCVALIDITYENDDATE = :TSCVALIDITYENDDATE";
                if (parameters.Profile1Evd.HasValue)
                    dates += ", PROFILEVALIDITYENDDATE = :PROFILEVALIDITYENDDATE";
                if (parameters.Profile2Evd.HasValue)
                    dates += ", PROFILEAUX1VALIDITYENDDATE = :PROFILEAUX1VALIDITYENDDATE";
                if (parameters.Profile3Evd.HasValue)
                    dates += ", PROFILEAUX2VALIDITYENDDATE = :PROFILEAUX2VALIDITYENDDATE";
                query = query.Replace("#SPLIT#", dates);
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    if (parameters.CardEvd.HasValue)
                        cmd.Parameters.Add(new OracleParameter("TSCVALIDITYENDDATE", OracleDbType.Date)).Value = parameters.CardEvd.Value;
                    if (parameters.Profile1Evd.HasValue)
                        cmd.Parameters.Add(new OracleParameter("PROFILEVALIDITYENDDATE", OracleDbType.Date)).Value = parameters.Profile1Evd.Value;
                    if (parameters.Profile2Evd.HasValue)
                        cmd.Parameters.Add(new OracleParameter("PROFILEAUX1VALIDITYENDDATE", OracleDbType.Date)).Value = parameters.Profile2Evd.Value;
                    if (parameters.Profile3Evd.HasValue)
                        cmd.Parameters.Add(new OracleParameter("PROFILEAUX2VALIDITYENDDATE", OracleDbType.Date)).Value = parameters.Profile3Evd.Value;
                    cmd.Parameters.Add(new OracleParameter("SHORTCARDMODELID", parameters.ShortCardModel));
                    cmd.Parameters.Add(new OracleParameter("TSCSERIALNO", parameters.CardSerialNumber));
                    await cmd.ExecuteNonQueryAsync();
                }

                var tscDocumentRowId = string.Empty;
                using (var cmd = new OracleCommand($"SELECT ROWID FROM GESTOWN.TSC_DOCUMENTS WHERE SHORTCARDMODELID={parameters.ShortCardModel} AND TSCSERIALNO={parameters.CardSerialNumber}", connection))
                {
                    cmd.CommandType = CommandType.Text;
                    var res = await cmd.ExecuteScalarAsync();
                    tscDocumentRowId = res?.ToString();
                }

                await AddDataOperation(connection, SbmeDataOperation.Update, "TSC_DOCUMENTS", tscDocumentRowId);

                tran.Commit();
            }
            catch
            {
                await RollbackTransactionPreservingErrorAsync(tran);
                throw;
            }
            finally
            {
                tran?.Dispose();
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
                if (connection != null)
                    await connection.DisposeAsync();
            }
            return result;
        }


        public static async Task ForgetTscDocument(string connectionStringSbme, string connectionStringSg, string connectionStringSgUnsafe, long tscSerial)
        {
            if (string.IsNullOrWhiteSpace(connectionStringSbme) &&
                string.IsNullOrWhiteSpace(connectionStringSg) &&
                string.IsNullOrWhiteSpace(connectionStringSgUnsafe))
            {
                throw new ArgumentException("At least one Oracle connection string is required.");
            }

            var committedDatabases = new List<string>();
            try
            {
                if (!string.IsNullOrWhiteSpace(connectionStringSbme))
                {
                    await DeleteTscRecordsAsync(connectionStringSbme, "SBME", tscSerial,
                        "DELETE FROM GESTOWN.TSC_DOCUMENTS WHERE TSCSERIALNO = :TscSerial");
                    committedDatabases.Add("SBME");
                }

                if (!string.IsNullOrWhiteSpace(connectionStringSgUnsafe))
                {
                    await DeleteTscRecordsAsync(connectionStringSgUnsafe, "SG unsafe", tscSerial,
                        "DELETE FROM SG_dsdemh.TSC_DOCUMENTS WHERE TSCSERIALNO = :TscSerial",
                        "DELETE FROM SG_dsdemh.SBME_TSC_Contracts WHERE TSCSERIALNO = :TscSerial",
                        "DELETE FROM SG_dsdemh.SBME_TSC_Documents WHERE TSCSERIALNO = :TscSerial",
                        "DELETE FROM SG_dsdemh.SBME_TSC_BlackList WHERE LowSerialNo = :TscSerial",
                        "DELETE FROM SG_dsdemh.TARIFFREQUESTS WHERE TSCREQID IN (SELECT TSCREQID FROM SG_dsdemh.TSC_REQUESTS WHERE TSCSERIALNO = :TscSerial)",
                        "DELETE FROM SG_dsdemh.TSC_REQUESTS WHERE TSCSERIALNO = :TscSerial");
                    committedDatabases.Add("SG unsafe");
                }

                if (!string.IsNullOrWhiteSpace(connectionStringSg))
                {
                    await DeleteTscRecordsAsync(connectionStringSg, "SG", tscSerial,
                        "DELETE FROM SG_dsdemh.PARAMETERDV WHERE SERIALNO IN (SELECT SERIALNO FROM SG_dsdemh.T_DSDE_Contracts WHERE TSCSERIALNO = :TscSerial)",
                        "DELETE FROM SG_dsdemh.PARAMETERSV_RV WHERE SERIALNO IN (SELECT SERIALNO FROM SG_dsdemh.T_DSDE_Contracts WHERE TSCSERIALNO = :TscSerial)",
                        "DELETE FROM SG_dsdemh.T_DSDE_Contracts WHERE TSCSERIALNO = :TscSerial");
                    committedDatabases.Add("SG");
                }
            }
            catch (Exception ex)
            {
                if (committedDatabases.Count > 0)
                    Logger?.Error(ex, $"TSC cleanup for serial {tscSerial} stopped after committing: {string.Join(", ", committedDatabases)}. Manual database reconciliation may be required.");
                throw;
            }
        }

        public static async Task<string> AddParkingContract(string connectionStringSbme, string connectionStringSgUnsafe, int shortCardModel, long tscSerial)
        {
            OracleConnection? connection1 = null;
            //OracleConnection? connection2 = null;
            OracleTransaction? tran1 = null;
            var commitAttempted = false;
            //OracleTransaction? tran2 = null;
            try
            {
                var cs1 = !string.IsNullOrWhiteSpace(connectionStringSbme);
                var cs2 = !string.IsNullOrWhiteSpace(connectionStringSgUnsafe);
                if (cs1 && cs2)
                {
                    connection1 = await DBOracleHelper.OpenDBConnection(connectionStringSbme);
                    if (connection1 == null)
                        throw new DBConnectionOpeningException(connectionStringSbme);
                    //connection2 = await DBOracleHelper.OpenDBConnection(connectionStringSgUnsafe);
                    //if (connection2 == null)
                    //    throw new DBConnectionOpeningException(connectionStringSgUnsafe);
                    tran1 = connection1.BeginTransaction();
                    //tran2 = connection2.BeginTransaction();

                    var query = $"SELECT * FROM gestown.TSC_DOCUMENTS WHERE SHORTCARDMODELID={shortCardModel} AND TSCSERIALNO='{tscSerial}'";
                    
                    var found = false;
                    using (var cmd = new OracleCommand(query, connection1))
                    {
                        cmd.CommandType = CommandType.Text;
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                    if (!found) throw new Exception("Card not found in SBME");

                    //found = false;
                    //using (var cmd = new OracleCommand(query, connection2))
                    //{
                    //    cmd.CommandType = CommandType.Text;
                    //    using (var reader = await cmd.ExecuteReaderAsync())
                    //    {
                    //        while (await reader.ReadAsync())
                    //        {
                    //            found = true;
                    //            break;
                    //        }
                    //    }
                    //}
                    //if (!found) throw new Exception("Card not found in SG");

                    query = $"SELECT SERIALNO, PARKINGALLOWED, VALIDITYENDDATE FROM gestown.TSC_CONTRACTS WHERE SHORTCARDMODELID={shortCardModel} AND TSCSERIALNO='{tscSerial}' AND VALIDITYENDDATE >= sysdate";
                    
                    var count1 = 0;
                    using (var cmd = new OracleCommand(query, connection1))
                    {
                        cmd.CommandType = CommandType.Text;
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                ++count1;
                                if (count1 == 4) throw new Exception("Card already having 4 active contracts in SBME");
                                var contract = DBOracleHelper.GetInstanceOfType<CheatParkingContracts>(reader);
                                if (contract.ParkingAllowed?.Equals("1") ?? false) throw new Exception("Card already containing a parking contract in SBME");
                            }
                        }
                    }
                    
                    //var count2 = 0;
                    //using (var cmd = new OracleCommand(query, connection2))
                    //{
                    //    cmd.CommandType = CommandType.Text;
                    //    using (var reader = await cmd.ExecuteReaderAsync())
                    //    {
                    //        while (await reader.ReadAsync())
                    //        {
                    //            ++count2;
                    //            if (count2 == 4) throw new Exception("Card already having 4 active contracts in SG");
                    //            var contract = DBOracleHelper.GetInstanceOfType<CheatParkingContracts>(reader);
                    //            if (contract.ParkingAllowed?.Equals("1") ?? false) throw new Exception("Card already containing a parking contract in SG");
                    //        }
                    //    }
                    //}

                    var contractSerial = (long)DateTime.Now.Subtract(new DateTime(1997, 1, 1, 0, 0, 0)).TotalSeconds;

                    query = $"INSERT INTO gestown.TSC_CONTRACTS (" +
                        $" SERIALNO," +
                        $" SALEDEVICEID," +
                        $" SHORTCARDMODELID," +
                        $" TSCSERIALNO," +
                        $" TARIFFID," +
                        $" ISSUINGDATE," +
                        $" FLG_ISSORIGIN," +
                        $" ISSORIGINID," +
                        $" OPERATORID," +
                        $" DEVICECLASSID," +
                        $" DEVICECODE," +
                        $" PRICE," +
                        $" NUMBEROFUNITS," +
                        $" VALIDITYLIMITDATE," +
                        $" PAYMENTTYPE," +
                        $" CIRCULARCOUNTER," +
                        $" FLGCONFIRMTRANS," +
                        $" PARKINGALLOWED" +
                        $") VALUES (" +
                        $" :SERIALNO," +
                        $" :SALEDEVICEID," +
                        $" :SHORTCARDMODELID," +
                        $" :TSCSERIALNO," +
                        $" :TARIFFID," +
                        $" :ISSUINGDATE," +
                        $" :FLG_ISSORIGIN," +
                        $" :ISSORIGINID," +
                        $" :OPERATORID," +
                        $" :DEVICECLASSID," +
                        $" :DEVICECODE," +
                        $" :PRICE," +
                        $" :NUMBEROFUNITS," +
                        $" :VALIDITYLIMITDATE," +
                        $" :PAYMENTTYPE," +
                        $" :CIRCULARCOUNTER," +
                        $" :FLGCONFIRMTRANS," +
                        $" :PARKINGALLOWED" +
                        $" )";

                    using (var cmd = new OracleCommand(query, connection1))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("SERIALNO", contractSerial));
                        cmd.Parameters.Add(new OracleParameter("SALEDEVICEID", 10));
                        cmd.Parameters.Add(new OracleParameter("SHORTCARDMODELID", shortCardModel));
                        cmd.Parameters.Add(new OracleParameter("TSCSERIALNO", tscSerial));
                        cmd.Parameters.Add(new OracleParameter("TARIFFID", 4200));
                        cmd.Parameters.Add(new OracleParameter("ISSUINGDATE", OracleDbType.Date)).Value = new DateTime(2023, 7, 19, 11, 08, 1);
                        cmd.Parameters.Add(new OracleParameter("FLG_ISSORIGIN", "T"));
                        cmd.Parameters.Add(new OracleParameter("ISSORIGINID", (long)296727));
                        cmd.Parameters.Add(new OracleParameter("OPERATORID", (byte)1));
                        cmd.Parameters.Add(new OracleParameter("DEVICECLASSID", (short)206));
                        cmd.Parameters.Add(new OracleParameter("DEVICECODE", 4));
                        cmd.Parameters.Add(new OracleParameter("PRICE", (decimal)31400));
                        cmd.Parameters.Add(new OracleParameter("NUMBEROFUNITS", (long)1));
                        cmd.Parameters.Add(new OracleParameter("VALIDITYLIMITDATE", OracleDbType.Date)).Value = new DateTime(2025, 1, 31);
                        cmd.Parameters.Add(new OracleParameter("PAYMENTTYPE", (short)9));
                        cmd.Parameters.Add(new OracleParameter("CIRCULARCOUNTER", OracleDbType.Int16)).Value = 0;
                        cmd.Parameters.Add(new OracleParameter("FLGCONFIRMTRANS", (byte)1));
                        cmd.Parameters.Add(new OracleParameter("PARKINGALLOWED", OracleDbType.Int16)).Value = 1;
                        var res = await cmd.ExecuteNonQueryAsync();
                    }

                    //using (var cmd = new OracleCommand(query, connection2))
                    //{
                    //    cmd.CommandType = CommandType.Text;
                    //    cmd.Parameters.Add(new OracleParameter("SERIALNO", contractSerial));
                    //    cmd.Parameters.Add(new OracleParameter("SALEDEVICEID", 5054));
                    //    cmd.Parameters.Add(new OracleParameter("SHORTCARDMODELID", shortCardModel));
                    //    cmd.Parameters.Add(new OracleParameter("TSCSERIALNO", tscSerial));
                    //    cmd.Parameters.Add(new OracleParameter("TARIFFID", 7979));
                    //    cmd.Parameters.Add(new OracleParameter("ISSUINGDATE", OracleDbType.Date)).Value = new DateTime(2023, 1, 10, 8, 53, 0);
                    //    cmd.Parameters.Add(new OracleParameter("FLG_ISSORIGIN", "S"));
                    //    cmd.Parameters.Add(new OracleParameter("ISSORIGINID", (long)433820322));
                    //    cmd.Parameters.Add(new OracleParameter("OPERATORID", (byte)1));
                    //    cmd.Parameters.Add(new OracleParameter("DEVICECLASSID", (short)31));
                    //    cmd.Parameters.Add(new OracleParameter("DEVICECODE", 579));
                    //    cmd.Parameters.Add(new OracleParameter("PRICE", (decimal)1950));
                    //    cmd.Parameters.Add(new OracleParameter("NUMBEROFUNITS", (long)1));
                    //    cmd.Parameters.Add(new OracleParameter("VALIDITYLIMITDATE", OracleDbType.Date)).Value = new DateTime(2025, 1, 31);
                    //    cmd.Parameters.Add(new OracleParameter("PAYMENTTYPE", (short)1));
                    //    cmd.Parameters.Add(new OracleParameter("CIRCULARCOUNTER", OracleDbType.Int16)).Value = 0;
                    //    cmd.Parameters.Add(new OracleParameter("FLGCONFIRMTRANS", (byte)1));
                    //    cmd.Parameters.Add(new OracleParameter("PARKINGALLOWED", OracleDbType.Int16)).Value = 0;
                    //    cmd.Parameters.Add(new OracleParameter("LASTUPDATE", OracleDbType.Date)).Value = new DateTime(2023, 7, 11, 23, 51, 01);
                    //    cmd.Parameters.Add(new OracleParameter("PARENTEVENTDATE", OracleDbType.Date)).Value = new DateTime(2023, 1, 10, 6, 56, 0);
                    //    cmd.Parameters.Add(new OracleParameter("PARENTEVENTID", (long)1));
                    //    var res = await cmd.ExecuteNonQueryAsync();
                    //}

                    query = $"INSERT INTO gestown.TSC_COUNTERS (" +
                        $" SERIALNO," +
                        $" SALEDEVICEID," +
                        $" DATEANDTIME," +
                        $" ACTIVITYID," +
                        $" VALIDITYSTARTDATE," +
                        $" VALIDITYENDDATE," +
                        $" NOJOURNEYSLEFT" +
                        $") VALUES (" +
                        $" :SERIALNO," +
                        $" :SALEDEVICEID," +
                        $" :DATEANDTIME," +
                        $" :ACTIVITYID," +
                        $" :VALIDITYSTARTDATE," +
                        $" :VALIDITYENDDATE," +
                        $" :NOJOURNEYSLEFT" +
                        $" )";
                    
                    using (var cmd = new OracleCommand(query, connection1))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("SERIALNO", contractSerial));
                        cmd.Parameters.Add(new OracleParameter("SALEDEVICEID", 10));
                        cmd.Parameters.Add(new OracleParameter("DATEANDTIME", OracleDbType.Date)).Value = new DateTime(2023, 7, 19, 17, 28, 0);
                        cmd.Parameters.Add(new OracleParameter("ACTIVITYID", (long)352052785));
                        cmd.Parameters.Add(new OracleParameter("VALIDITYSTARTDATE", OracleDbType.Date)).Value = new DateTime(2023, 7, 19, 11, 08, 1);
                        cmd.Parameters.Add(new OracleParameter("VALIDITYENDDATE", OracleDbType.Date)).Value = new DateTime(2025, 12, 31);
                        cmd.Parameters.Add(new OracleParameter("NOJOURNEYSLEFT", 1023));
                        var res = await cmd.ExecuteNonQueryAsync();
                    }

                    //using (var cmd = new OracleCommand(query, connection2))
                    //{
                    //    cmd.CommandType = CommandType.Text;
                    //    cmd.Parameters.Add(new OracleParameter("SERIALNO", contractSerial));
                    //    cmd.Parameters.Add(new OracleParameter("SALEDEVICEID", 5054));
                    //    cmd.Parameters.Add(new OracleParameter("DATEANDTIME", OracleDbType.Date)).Value = new DateTime(2023, 1, 24, 17, 28, 0);
                    //    cmd.Parameters.Add(new OracleParameter("ACTIVITYID", (long)435028092));
                    //    cmd.Parameters.Add(new OracleParameter("VALIDITYSTARTDATE", OracleDbType.Date)).Value = new DateTime(2023, 1, 1);
                    //    cmd.Parameters.Add(new OracleParameter("VALIDITYENDDATE", OracleDbType.Date)).Value = new DateTime(2024, 12, 31);
                    //    cmd.Parameters.Add(new OracleParameter("NOJOURNEYSLEFT", 8));
                    //    var res = await cmd.ExecuteNonQueryAsync();
                    //}

                    if (tran1 != null)
                    {
                        commitAttempted = true;
                        await tran1.CommitAsync();
                    }
                    //if (tran2 != null)
                    //    await tran2.CommitAsync();

                    return "OK";
                }

                throw new Exception(string.Format("{0} connection string not found", cs1 ? "SBME" : "SG"));
            }
            catch (Exception ex)
            {
                if (commitAttempted)
                    Logger?.Error(ex, $"AddParkingContract commit outcome is uncertain for TSC {tscSerial}; verify before retrying.");
                await RollbackTransactionPreservingErrorAsync(tran1);
                //if (tran2 != null)
                //    await tran2.RollbackAsync();
                throw;
            }
            finally
            {
                tran1?.Dispose();
                if ((connection1?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection1);
                if (connection1 != null)
                    await connection1.DisposeAsync();
                //if ((connection2?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                //    await DBOracleHelper.CloseDBConnection(connection2);
            }
        }

        public static async Task<string> RemoveParkingContract(string connectionStringSbme, string connectionStringSgUnsafe, int shortCardModel, long tscSerial)
        {
            OracleConnection? connection1 = null;
            OracleConnection? connection2 = null;
            OracleTransaction? tran1 = null;
            OracleTransaction? tran2 = null;
            var sbmeCommitAttempted = false;
            var sbmeCommitted = false;
            var sgCommitAttempted = false;
            var sgCommitted = false;
            try
            {
                var cs1 = !string.IsNullOrWhiteSpace(connectionStringSbme);
                var cs2 = !string.IsNullOrWhiteSpace(connectionStringSgUnsafe);
                if (cs1 && cs2)
                {
                    connection1 = await DBOracleHelper.OpenDBConnection(connectionStringSbme);
                    if (connection1 == null)
                        throw new DBConnectionOpeningException(connectionStringSbme);
                    connection2 = await DBOracleHelper.OpenDBConnection(connectionStringSgUnsafe);
                    if (connection2 == null)
                        throw new DBConnectionOpeningException(connectionStringSgUnsafe);
                    tran1 = connection1.BeginTransaction();
                    tran2 = connection2.BeginTransaction();

                    var query = $"SELECT * FROM gestown.TSC_DOCUMENTS WHERE SHORTCARDMODELID={shortCardModel} AND TSCSERIALNO='{tscSerial}'";
                    
                    var found = false;
                    using (var cmd = new OracleCommand(query, connection1))
                    {
                        cmd.CommandType = CommandType.Text;
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                    if (!found) throw new Exception("Card not found in SBME");

                    found = false;
                    using (var cmd = new OracleCommand(query, connection2))
                    {
                        cmd.CommandType = CommandType.Text;
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                    if (!found) throw new Exception("Card not found in SG");

                    query = $"SELECT SERIALNO, PARKINGALLOWED, VALIDITYENDDATE FROM gestown.TSC_CONTRACTS WHERE SHORTCARDMODELID={shortCardModel} AND TSCSERIALNO='{tscSerial}'";

                    var serials = new List<uint>();

                    using (var cmd = new OracleCommand(query, connection1))
                    {
                        cmd.CommandType = CommandType.Text;
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var contract = DBOracleHelper.GetInstanceOfType<CheatParkingContracts>(reader);
                                if (contract != null && (contract.ParkingAllowed?.Equals("1") ?? false) && !serials.Contains(contract.SerialNo))
                                    serials.Add(contract.SerialNo);
                            }
                        }
                    }

                    using (var cmd = new OracleCommand(query, connection2))
                    {
                        cmd.CommandType = CommandType.Text;
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var contract = DBOracleHelper.GetInstanceOfType<CheatParkingContracts>(reader);
                                if (contract != null && (contract.ParkingAllowed?.Equals("1") ?? false) && !serials.Contains(contract.SerialNo))
                                    serials.Add(contract.SerialNo);
                            }
                        }
                    }

                    if (serials.Count == 0) throw new Exception("No parking contracts found");

                    query = $"DELETE FROM gestown.TSC_CONTRACTS WHERE SHORTCARDMODELID={shortCardModel} AND TSCSERIALNO='{tscSerial}' AND PARKINGALLOWED='1'";

                    using (var cmd = new OracleCommand(query, connection1))
                    {
                        cmd.CommandType = CommandType.Text;
                        var res = await cmd.ExecuteNonQueryAsync();
                    }

                    using (var cmd = new OracleCommand(query, connection2))
                    {
                        cmd.CommandType = CommandType.Text;
                        var res = await cmd.ExecuteNonQueryAsync();
                    }

                    query = $"DELETE FROM gestown.TSC_COUNTERS WHERE SERIALNO IN ({string.Join(", ", serials)})";

                    using (var cmd = new OracleCommand(query, connection1))
                    {
                        cmd.CommandType = CommandType.Text;
                        var res = await cmd.ExecuteNonQueryAsync();
                    }

                    using (var cmd = new OracleCommand(query, connection2))
                    {
                        cmd.CommandType = CommandType.Text;
                        var res = await cmd.ExecuteNonQueryAsync();
                    }

                    if (tran1 != null)
                    {
                        sbmeCommitAttempted = true;
                        await tran1.CommitAsync();
                        sbmeCommitted = true;
                    }
                    if (tran2 != null)
                    {
                        sgCommitAttempted = true;
                        await tran2.CommitAsync();
                        sgCommitted = true;
                    }

                    return "OK";
                }

                throw new Exception(string.Format("{0} connection string not found", cs1 ? "SBME" : "SG"));
            }
            catch (Exception ex)
            {
                if (sbmeCommitAttempted)
                    Logger?.Error(ex, $"RemoveParkingContract for TSC {tscSerial} may be partially committed. SBME confirmed: {sbmeCommitted}; SG commit attempted: {sgCommitAttempted}. Verify both databases before retrying.");
                if (!sbmeCommitted)
                    await RollbackTransactionPreservingErrorAsync(tran1);
                if (!sgCommitted)
                    await RollbackTransactionPreservingErrorAsync(tran2);
                throw;
            }
            finally
            {
                tran1?.Dispose();
                tran2?.Dispose();
                if ((connection1?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection1);
                if (connection1 != null)
                    await connection1.DisposeAsync();
                if ((connection2?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection2);
                if (connection2 != null)
                    await connection2.DisposeAsync();
            }
        }
    }
}
