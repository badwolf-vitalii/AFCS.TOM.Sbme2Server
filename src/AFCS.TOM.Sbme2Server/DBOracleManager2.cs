using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.SBME2;
using AFCS.TOM.Sbme2Server.TemporarilyModels;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.SBME2;
using NLog;
using NLog.Filters;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace AFCS.TOM.Sbme2Server
{
    public partial class DBOracleManager2
    {
        private static NLog.Logger Logger = LogManager.GetLogger("Sbme2Server");

        #region Customers
        private static async Task<uint> P_CreateCustomer(OracleTransaction transaction, Customer customer)
        {
            var holderId = (uint)0;
            var cmdString = string.Empty;
            try
            {
                var holderIdSpecified = customer.HolderId != 0;
                var query = Queries.InsertCustomer(holderIdSpecified);

                using (var cmd = new OracleCommand(query, transaction.Connection))
                {
                    cmd.CommandType = CommandType.Text;
                    if (holderIdSpecified)
                        cmd.Parameters.Add(new OracleParameter("HolderId", (int)customer.HolderId));
                    cmd.Parameters.Add(new OracleParameter("FirstName", customer.FirstName?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("FamilyName", customer.FamilyName?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("Birthday", OracleDbType.Date)).Value = customer.Birthday;
                    cmd.Parameters.Add(new OracleParameter("BirthPlace", customer.BirthPlace?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("Nationality", customer.Nationality?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("DocumentType", customer.DocumentType));
                    cmd.Parameters.Add(new OracleParameter("DocumentCode", customer.DocumentCode?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("DocumentCode2", customer.DocumentCode2?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("DocEndValidityDate", OracleDbType.Date)).Value = customer.DocEndValidityDate;
                    cmd.Parameters.Add(new OracleParameter("Doc2EndValidityDate", OracleDbType.Date)).Value = customer.Doc2EndValidityDate;
                    OracleParameter prmPhoto = cmd.Parameters.Add("Photo", OracleDbType.Blob);
                    prmPhoto.Direction = ParameterDirection.Input;
                    prmPhoto.Value = customer.Photo;
                    cmd.Parameters.Add(new OracleParameter("FormatPhoto", customer.FormatPhoto));
                    cmd.Parameters.Add(new OracleParameter("FiscalCode", customer.FiscalCode?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("Address", customer.Address?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("House", customer.House?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("Flat", customer.Flat?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("Town", customer.Town?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("ZipCode", customer.ZipCode?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("District", customer.District?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("Sex", customer.Sex?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("Phone1", customer.Phone1?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("Phone2", customer.Phone2?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("EMail", customer.EMail?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("OperatorID", customer.OperatorID));
                    cmd.Parameters.Add(new OracleParameter("Title", customer.Title?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("Fax", customer.Fax?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("CorporateNo", customer.CorporateNo));
                    cmd.Parameters.Add(new OracleParameter("OrganizationId", customer.OrganizationId));
                    cmd.Parameters.Add(new OracleParameter("Status", customer.Status ?? 1));
                    cmd.Parameters.Add(new OracleParameter("PostDebitStatus", customer.PostDebitStatus));
                    cmd.Parameters.Add(new OracleParameter("CustomerType", (short?)customer.CustomerType));
                    cmd.Parameters.Add(new OracleParameter("BirthPlaceCode", customer.BirthPlaceCode?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("NationalityCode", customer.NationalityCode?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("ActivityId", customer.ActivityId));
                    if (customer.ParentHolderId.HasValue && customer.ParentHolderId.Value > 0)
                        cmd.Parameters.Add(new OracleParameter("ParentHolderId", customer.ParentHolderId.Value));
                    else
                        cmd.Parameters.Add(new OracleParameter("ParentHolderId", DBNull.Value));
                    cmd.Parameters.Add(new OracleParameter("BirthPlaceCodeType", (short?)customer.BirthPlaceCodeType));
                    cmd.Parameters.Add(new OracleParameter("TownCode", customer.TownCode?.ToUpper() ?? string.Empty));
                    cmd.Parameters.Add(new OracleParameter("TownCodeType", (short?)customer.TownCodeType));
                    cmd.Parameters.Add(new OracleParameter("Agent_LastUpdate", customer.Agent_LastUpdate));
                    cmd.Parameters.Add(new OracleParameter("NationalityCodeType", (short?)customer.NationalityCodeType));
                    cmd.Parameters.Add(new OracleParameter {
                        ParameterName = "HOLDERID_RETURN",
                        OracleDbType = OracleDbType.Decimal,
                        Direction = ParameterDirection.Output
                    });
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    var row = await cmd.ExecuteNonQueryAsync();
                    if (row == 0) throw new Exception("customer creation failed");
                    holderId = uint.Parse(cmd.Parameters["HOLDERID_RETURN"].Value?.ToString() ?? string.Empty);
                }

                if (customer.Signature != null && customer.Signature.Length > 0)
                {
                    query = Queries.InsertHolderSignature();
                    using (var cmd = new OracleCommand(query, transaction.Connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("OPERATORID", customer.OperatorID)); // OPERATORID
                        cmd.Parameters.Add(new OracleParameter("HOLDERID", (int)holderId)); // HOLDERID
                        var prmSignature = cmd.Parameters.Add("HOLDERSIGNATURE", OracleDbType.Blob);
                        prmSignature.Direction = ParameterDirection.Input;
                        prmSignature.Value = customer.Signature;
                        cmdString = OracleCommandWrapper.ToString(cmd, query);
                        var ret = cmd.Parameters.Add(new OracleParameter {
                            ParameterName = "ATTACHMENTID_RETURN",
                            OracleDbType = OracleDbType.Decimal,
                            Direction = ParameterDirection.Output
                        });
                        var row = await cmd.ExecuteNonQueryAsync();
                        if (row == 0) throw new Exception("agent creation failed (signature)");
                    }
                }
            }
            catch (OracleException ex)
            {
                var end = ex.Message.IndexOf(":");
                if (end > 0)
                {
                    var code = ex.Message.Substring(0, end);
                    ExHelper.ThrowExceptionContainer(ex, "InsertCustomer", code);
                }
                else
                    ExHelper.ThrowExceptionContainer(ex, "InsertCustomer", cmdString);
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "InsertCustomer", cmdString);
                throw;
            }
            return holderId;
        }

        public static async Task<uint> CreateCustomer(string connectionString, RequestBase<Customer> customer)
        {
            OracleConnection connection = null;
            var holderId = (uint)0;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                using (var transaction = connection.BeginTransaction())
                {
                    holderId = await P_CreateCustomer(transaction, customer.Body);
                    if (holderId == 0)
                        transaction.Rollback();
                    else
                        transaction.Commit();
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "CreateCustomer");
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return holderId;
        }

        public static async Task<bool> UpdateCustomer(string connectionString, uint holderId, RequestBase<IList<SearchFilter>> filter)
        {
            if (filter?.Body == null || !filter.Body.Any())
            {
                return false;
            }

            var opId = filter.Body.FirstOrDefault(p => p.FieldName?.Equals("OPERATORID", StringComparison.InvariantCultureIgnoreCase) ?? false);
            if (opId == null || string.IsNullOrWhiteSpace(opId.FieldValue) ||
                !short.TryParse(opId.FieldValue, out var operatorId))
            {
                throw new OperatorNotSpecifiedException();
            }

            OracleConnection connection = null;
            var cmdString = string.Empty;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var signature = filter.Body.FirstOrDefault(p => p.FieldName?.Equals("HOLDERSIGNATURE", StringComparison.InvariantCultureIgnoreCase) ?? false);
                        if (signature != null)
                        {
                            filter.Body.Remove(signature);

                            //ATTACHMENTID=ATTACHMENT_SEQ.nextval
                            //INSERTDATE=data senza orario dell’inserimento
                            //INSERTDATETIME=data ed ora dell’inserimento
                            //OPERATORID=…
                            //HOLDERID=holderid
                            //ATTACHMENTTYPEID=t2c_AttType_Signature=1
                            //ATTACHMENTFORMAT=t2c_AttFormat_jpg=2
                            //ATTACHMENT_REF_TYPE=t2c_AttRefType_holder=3
                            //ATTACHMENT=foto della firma

                            long attachmentid = 0;

                            var query = Queries.GetHolderSignatureId();
                            using (var cmd = new OracleCommand(query, connection))
                            {
                                cmd.CommandType = CommandType.Text;
                                cmd.Parameters.Add(new OracleParameter("HOLDERID", (int)holderId)); // HOLDERID
                                cmdString = OracleCommandWrapper.ToString(cmd, query);
                                var res = (long?)await cmd.ExecuteScalarAsync();
                                if (res.HasValue)
                                {
                                    attachmentid = res.Value;
                                }
                            }

                            if (attachmentid > 0)
                            {
                                // UPDATE
                                query = Queries.UpdateHolderSignature();
                                using (var cmd = new OracleCommand(query, connection))
                                {
                                    cmd.CommandType = CommandType.Text;
                                    cmd.Parameters.Add(DBOracleHelper.GetOracleParameter(opId)); // OPERATORID
                                    cmd.Parameters.Add(DBOracleHelper.GetOracleParameter(signature)); // ATTACHMENT
                                    cmd.Parameters.Add(new OracleParameter("ATTACHMENTID", attachmentid)); // ATTACHMENTID
                                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }
                            else
                            {
                                // INSERT
                                query = Queries.InsertHolderSignature();
                                using (var cmd = new OracleCommand(query, connection))
                                {
                                    cmd.CommandType = CommandType.Text;
                                    cmd.Parameters.Add(new OracleParameter("OPERATORID", operatorId)); // OPERATORID
                                    cmd.Parameters.Add(new OracleParameter("HOLDERID", (int)holderId)); // HOLDERID
                                    var decoded = Convert.FromBase64String(signature.FieldValue!);
                                    var prmSignature = cmd.Parameters.Add("HOLDERSIGNATURE", OracleDbType.Blob);
                                    prmSignature.Direction = ParameterDirection.Input;
                                    prmSignature.Value = decoded;
                                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                                    var ret = cmd.Parameters.Add(new OracleParameter {
                                        ParameterName = "ATTACHMENTID_RETURN",
                                        OracleDbType = OracleDbType.Decimal,
                                        Direction = ParameterDirection.Output
                                    });
                                    var row = await cmd.ExecuteNonQueryAsync();
                                    if (row == 0) throw new Exception("agent creation failed (signature)");
                                    attachmentid = long.Parse(cmd.Parameters["ATTACHMENTID_RETURN"].Value?.ToString() ?? string.Empty);
                                }
                            }
                        }

                        if (filter.Body.Any())
                        {
                            var query = Queries.UpdateHolder();
                            for (int nLcv = 0; nLcv < filter.Body.Count; nLcv++)
                            {
                                var pF = filter.Body[nLcv];
                                query += nLcv == filter.Body.Count - 1
                                    ? " " + pF.FieldName.ToUpper() + " = :" + pF.FieldName.ToUpper()
                                    : " " + pF.FieldName.ToUpper() + " = :" + pF.FieldName.ToUpper() + ",";
                            }
                            query += $" WHERE HOLDERID = {holderId}";
                            using (var cmd = new OracleCommand(query, connection))
                            {
                                cmd.CommandType = CommandType.Text;
                                foreach (var pF in filter.Body)
                                    cmd.Parameters.Add(DBOracleHelper.GetOracleParameter(pF));
                                cmdString = OracleCommandWrapper.ToString(cmd, query);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "UpdateCustomer", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return true;
        }

        public static async Task<IList<Customer>> GetCustomersByFilter(string connectionString, RequestBase<IList<SearchFilter>> filters)
        {
            OracleConnection connection = null;
            var cmdString = string.Empty;
            try
            {
                if (filters?.Body != null)
                    foreach (var filter in filters.Body)
                        filter.FieldType = DBOracleHelper.NormalizeClassName(filter?.FieldType);

                var customers = new List<Customer>();
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var sB = new StringBuilder();
                var withPhoto = true;
                if (filters?.Body != null)
                    foreach (var filter in filters.Body.Where(p => p.FieldName != null || (p.FieldType?.ToUpper()?.Equals("NOPHOTO") ?? false)))
                    {
                        if (filter.FieldType?.ToUpper()?.Equals("NOPHOTO") ?? false)
                            withPhoto = false;
                        else if ((filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(int).ToString())?.ToUpper()) ?? false) ||
                            (filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(decimal).ToString())?.ToUpper()) ?? false))
                        {
                            sB.Append((filters.Body.First() == filter ? " WHERE ROWNUM <= " + filters.RowMax + " AND " : " AND ") + $"{filter.FieldName.ToUpper()} = :{filter.FieldName.ToUpper()}");
                        }
                        else if (filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(string).ToString())?.ToUpper()) ?? false)
                        {
                            sB.Append((filters.Body.First() == filter ? " WHERE ROWNUM <= " + filters.RowMax + " AND " : " AND ") + $"{filter.FieldName.ToUpper()} Like :{filter.FieldName.ToUpper()}");
                        }
                        else if (filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(DateTime).ToString())?.ToUpper()) ?? false)
                        {
                            sB.Append((filters.Body.First() == filter ? " WHERE ROWNUM <= " + filters.RowMax + " AND " : " AND ") + $"{filter.FieldName.ToUpper()} = TO_DATE(:{filter.FieldName.ToUpper()},'DD/MM/YYYY')");
                        }
                    }
                var query = Queries.GetCustomerByFilter(sB.ToString(), withPhoto);
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    if (filters?.Body != null)
                        foreach (var filter in filters.Body.Where(p => !string.IsNullOrWhiteSpace(p.FieldName)))
                        {
                            if ((filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(int).ToString())?.ToUpper()) ?? false) ||
                                (filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(decimal).ToString())?.ToUpper()) ?? false))
                            {
                                cmd.Parameters.Add(new OracleParameter($"{filter.FieldName.ToUpper()}", $"{filter.FieldValue}".ToUpper()));
                            }
                            else if (filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(string).ToString())?.ToUpper()) ?? false)
                            {
                                cmd.Parameters.Add(new OracleParameter($"{filter.FieldName.ToUpper()}", $"{filter.FieldValue}%".ToUpper()));
                            }
                            else if (filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(DateTime).ToString())?.ToUpper()) ?? false)
                            {
                                var parsed = DateTime.TryParseExact(filter.FieldValue.ToString(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDay)
                                || DateTime.TryParseExact(filter.FieldValue.ToString(), "dd/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out birthDay)
                                || DateTime.TryParseExact(filter.FieldValue.ToString(), "d/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out birthDay)
                                || DateTime.TryParseExact(filter.FieldValue.ToString(), "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out birthDay);
                                if (!parsed)
                                    Logger?.Error($"FieldName: {filter.FieldName}, FieldValue: {filter.FieldValue}");
                                var value = $"{birthDay:dd/MM/yyyy}".ToUpper();
                                cmd.Parameters.Add(new OracleParameter($"{filter.FieldName.ToUpper()}", value));
                            }
                        }
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var customer = DBOracleHelper.GetInstanceOfType<Customer>(reader);
                            customers.Add(customer);

                            long attachmentid = 0;
                            query = Queries.GetHolderSignatureId();
                            using (var cmd2 = new OracleCommand(query, connection))
                            {
                                cmd2.CommandType = CommandType.Text;
                                cmd2.Parameters.Add(new OracleParameter("HOLDERID", (int)customer.HolderId));
                                cmdString = OracleCommandWrapper.ToString(cmd2, query);
                                var res0 = await cmd2.ExecuteScalarAsync();
                                var res = (long?)await cmd2.ExecuteScalarAsync();
                                if (res.HasValue)
                                {
                                    attachmentid = res.Value;
                                }
                            }
                            try
                            {
                                query = Queries.GetHolderSignature();
                                using (var cmd2 = new OracleCommand(query, connection))
                                {
                                    cmd2.CommandType = CommandType.Text;
                                    cmd2.Parameters.Add(new OracleParameter("ATTACHEMENTID", attachmentid));
                                    cmdString = OracleCommandWrapper.ToString(cmd2, query);
                                    using (var reader2 = await cmd2.ExecuteReaderAsync())
                                    {
                                        if (await reader2.ReadAsync() && reader2.HasRows)
                                        {
                                            var value = reader2.GetValue(0);
                                            var relatedAttachment = value is DBNull
                                                ? null
                                                : (byte[]?)value;
                                            customer.Signature = relatedAttachment;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger?.Error($"Failed getting customer ({customer.HolderId}) signature from attachments ({attachmentid})");
                            }
                        }
                    }
                }
                return customers.OrderBy(p => p.FamilyName).ThenBy(p => p.FirstName).ToList();
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetCustomerByFilter", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        private static async Task<Customer> P_GetCustomerByHoldderIDAsync(OracleTransaction transaction, uint holderID, bool withPhoto = true)
        {
            LogHelper.Info(null, "var customer = new Customer();");
            var customer = new Customer();
            var cmdString = string.Empty;

            LogHelper.Info(null, "var query = Queries.GetCustomerByHolderID(withPhoto);");
            var query = Queries.GetCustomerByHolderID(withPhoto);
            LogHelper.Info(null, query);
            if (transaction == null)
                LogHelper.Info(null, "transaction is NULL");
            LogHelper.Info(null, "using (var cmd = new OracleCommand(query, transaction.Connection))");
            using (var cmd = new OracleCommand(query, transaction.Connection))
            {
                if (cmd == null)
                    LogHelper.Info(null, "cmd is NULL");
                LogHelper.Info(null, "cmd.CommandType = CommandType.Text;");
                cmd.CommandType = CommandType.Text;
                LogHelper.Info(null, @"cmd.Parameters.Add(new OracleParameter(""holderID"", holderID));");
                cmd.Parameters.Add(new OracleParameter("holderID", (int)holderID));
                cmdString = OracleCommandWrapper.ToString(cmd, query);
                LogHelper.Info(null, "using (var reader = await cmd.ExecuteReaderAsync())");
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (reader == null)
                        LogHelper.Info(null, "reader is NULL");
                    LogHelper.Info(null, "var read = await reader.ReadAsync();");
                    var read = await reader.ReadAsync();
                    if (read == null)
                        LogHelper.Info(null, "read is NULL");
                    LogHelper.Info(null, "customer = read ? DBOracleHelper.GetInstanceOfType<Customer>(reader) : null;");
                    customer = read ? DBOracleHelper.GetInstanceOfType<Customer>(reader) : null;
                }
            }

            if (customer == null)
            {
                return null;
            }

            long attachmentid = 0;
            query = Queries.GetHolderSignatureId();
            using (var cmd = new OracleCommand(query, transaction.Connection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add(new OracleParameter("HOLDERID", (int)holderID));
                cmdString = OracleCommandWrapper.ToString(cmd, query);
                var res = (long?)await cmd.ExecuteScalarAsync();
                if (res.HasValue)
                {
                    attachmentid = res.Value;
                }
            }
            query = Queries.GetHolderSignature();
            using (var cmd = new OracleCommand(query, transaction.Connection))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add(new OracleParameter("ATTACHEMENTID", attachmentid));
                cmdString = OracleCommandWrapper.ToString(cmd, query);
                try
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync() && reader.HasRows)
                        {
                            var value = reader.GetValue(0);
                            var relatedAttachment = value is DBNull
                                ? null
                                : (byte[]?)value;
                            customer.Signature = relatedAttachment;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger?.Error($"Failed getting customer ({customer?.HolderId ?? 0}) signature from attachments ({attachmentid})");
                }
            }
            return customer;
        }

        public static async Task<Customer> GetCustomerByHoldderIDAsync(string connectionString, uint holderID, bool withPhoto = true)
        {
            OracleConnection connection = null;
            var customer = new Customer();
            try
            {
                LogHelper.Info(null, "connection = await DBOracleHelper.OpenDBConnection(connectionString);");
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                {
                    LogHelper.Info(null, "connection is NULL");
                    throw new DBConnectionOpeningException(connectionString);
                }
                LogHelper.Info(null, "using (var transaction = connection.BeginTransaction())");
                using (var transaction = connection.BeginTransaction())
                {
                    if (transaction == null)
                        LogHelper.Info(null, "transaction is NULL");
                    LogHelper.Info(null, $"customer = await P_GetCustomerByHoldderIDAsync(transaction, {holderID}, {withPhoto});");
                    customer = await P_GetCustomerByHoldderIDAsync(transaction, holderID, withPhoto);
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetCustomerByHolderId");
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return customer;
        }

        public static async Task<int> GetShortCardModel(string connectionString, decimal manufacturedId)
        {
            OracleConnection connection = null;
            var retValue = 0;
            var cmdString = string.Empty;
            try
            {
                connection = DBOracleHelper.OpenDBConnection(connectionString).Result;
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.GetShortCardModel();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("MANUFACTURERID", manufacturedId.ToString().PadLeft(10, '0')));
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    retValue = (int)cmd.ExecuteScalar();
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetShortCardModel", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return retValue;
        }

        public static async Task<IList<RelatedCustomer>> GetRelatedCustomers(string connectionString, RequestBase<HolderIdRequest> filters)
        {
            OracleConnection connection = null;
            var cmdString = string.Empty;
            try
            {
                var relatedCustomers = new List<RelatedCustomer>();
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var sB = new StringBuilder();
                sB.Append(" WHERE ROWNUM <= " + filters.RowMax.ToString() + $" AND A.HolderId = :holderId");
                var query = Queries.GetRelatedCustomers(sB.ToString());
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("holderId", (int)filters.Body.HolderId));
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var relatedCustomer = DBOracleHelper.GetInstanceOfType<RelatedCustomer>(reader);
                            relatedCustomers.Add(relatedCustomer);
                        }
                    }
                }
                return relatedCustomers.OrderBy(p => p.FamilyName).ThenBy(p => p).ToList();
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetRelatedCustomers", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<uint> CreateCustomerAttachment(string connectionString, RequestBase<CustomerAttachment> attachment)
        {
            OracleConnection connection = null;
            var retValue = (uint)0;
            var cmdString = string.Empty;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.InsertCustomerAttachment();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    //cmd.Parameters.Add(new OracleParameter("FirstName", attachment.FirstName.ToUpper()));

                    //cmd.Parameters.Add(new OracleParameter("Birthday", OracleDbType.Date)).Value = customer.Birthday;

                    //OracleParameter prmPhoto = cmd.Parameters.Add("Photo", OracleDbType.Blob);
                    //prmPhoto.Direction = ParameterDirection.Input;
                    //prmPhoto.Value = customer.Photo;


                    //cmd.Parameters.Add(new OracleParameter {
                    //    ParameterName = "HOLDERID",
                    //    OracleDbType = OracleDbType.Decimal,
                    //    Direction = ParameterDirection.Output
                    //});
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    var row = await cmd.ExecuteNonQueryAsync();
                    if (row == 0) throw new Exception("Attachmnet creation failed");
                    retValue = uint.Parse(cmd.Parameters["HOLDERID"].Value.ToString());
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "CreateCustomerAttachment", cmdString);
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
            var cmdString = string.Empty;
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
                    if (!physical)
                    {
                        cmd.Parameters.Add(new OracleParameter("saledeviceid", int.Parse(saleDeviceid ?? "0")));
                        cmd.Parameters.Add(new OracleParameter("serial", long.Parse(sn)));
                        cmd.Parameters.Add(new OracleParameter("shortcardmodelid", shortCardModel));
                    }
                    else
                    {
                        cmd.Parameters.Add(new OracleParameter("serial", sn));
                        cmd.Parameters.Add(new OracleParameter("shortcardmodelid", shortCardModel));
                    }
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    var scalar = cmd.ExecuteScalar();
                    if (uint.TryParse(scalar?.ToString(), out var value)) retValue = value;
                }
                return retValue;
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetCustomerByCardSerialNumber", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        /// <summary>
        /// Agents primary key (AgentId) is not managed by a sequence.
        /// </summary>
        /// <param name="transaction"></param>
        /// <returns></returns>
        private static async Task<short> AgentIdNextVal(OracleTransaction transaction)
        {
            short retValue = 0;
            var cmdString = string.Empty;
            try
            {
                var query = Queries.AgentIdSeqNextVal();
                using (var cmd = new OracleCommand(query, transaction.Connection))
                {
                    cmd.CommandType = CommandType.Text;

                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        var read = await reader.ReadAsync();
                        if (read)
                            retValue = reader.GetInt16(0);
                    }
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "AgentIdNextVal", cmdString);
                throw;
            }
            finally
            {
            }
            return retValue;
        }

        private static async Task<short> P_CreateAgent(OracleTransaction transaction, Agent agent)
        {
            var agentId = (short)0;
            var cmdString = string.Empty;

            try
            {
                var holderIsSpecified = agent.HolderId.HasValue && agent.HolderId.Value != 0;

                var holderId = agent.HolderId.HasValue ? agent.HolderId.Value : (uint)0;
                var customer = holderIsSpecified
                    ? P_GetCustomerByHoldderIDAsync(transaction, holderId, false)
                        ?? throw new Exception($"missing referenced HolderId {holderId}")
                    : throw new Exception("missing HolderId reference");

                var agentIdSpecified = agent.AgentId != 0;
                if (agentIdSpecified)
                {
                    // se esiste già un agent con questo id la insert darà errore
                    // se non esiste lo inserisce
                    // questo ad es. se l'agentId viene impostato da SBME tramite la sua sequence
                }
                else
                {
                    // se AgentId non è valorizzato simulo la sequence
                    agent.AgentId = await AgentIdNextVal(transaction);
                }
                var query = Queries.InsertAgent(agentIdSpecified);
                using (var cmd = new OracleCommand(query, transaction.Connection))
                {
                    cmd.CommandType = CommandType.Text;
                    if (agentIdSpecified)
                        cmd.Parameters.Add(new OracleParameter("AgentId", agent.AgentId));
                    cmd.Parameters.Add(new OracleParameter("OperatorId", agent.OperatorId));
                    cmd.Parameters.Add(new OracleParameter("AgentUsername", agent.AgentUserName));
                    cmd.Parameters.Add(new OracleParameter("AgentUserPassw", agent.AgentUserPassw));
                    cmd.Parameters.Add(new OracleParameter("MatricRoll", agent.MatricRoll));
                    cmd.Parameters.Add(new OracleParameter("FirstName", agent.FirstName));
                    cmd.Parameters.Add(new OracleParameter("FamilyName", agent.LastName));
                    cmd.Parameters.Add(new OracleParameter("AgentStatus", (short)agent.AgentStatus));
                    if (agent.PlantId.HasValue && agent.PlantId.Value > 0)
                        cmd.Parameters.Add(new OracleParameter("PlantId", agent.PlantId));
                    else
                        cmd.Parameters.Add(new OracleParameter("PlantId", DBNull.Value));
                    if (agent.SynId.HasValue && agent.SynId.Value > 0)
                        cmd.Parameters.Add(new OracleParameter("SynId", agent.SynId));
                    else
                        cmd.Parameters.Add(new OracleParameter("SynId", DBNull.Value));
                    cmd.Parameters.Add(new OracleParameter("PinCode", agent.PinCode));
                    if (agent.ShortCardModel.HasValue)
                        cmd.Parameters.Add(new OracleParameter("ShortCardModel", agent.ShortCardModel));
                    else
                        cmd.Parameters.Add(new OracleParameter("ShortCardModel", DBNull.Value));
                    cmd.Parameters.Add(new OracleParameter("TSCSerialNo", agent.TSCSerialNo));
                    if (agent.TSCSaleOperatorId.HasValue)
                        cmd.Parameters.Add(new OracleParameter("TSCSaleOperatorId", agent.TSCSaleOperatorId));
                    else
                        cmd.Parameters.Add(new OracleParameter("TSCSaleOperatorId", DBNull.Value));
                    if (agent.TSCSaleOperatorId.HasValue)
                        cmd.Parameters.Add(new OracleParameter("ProofDocId", agent.TSCSaleOperatorId));
                    else
                        cmd.Parameters.Add(new OracleParameter("ProofDocId", DBNull.Value));
                    cmd.Parameters.Add(new OracleParameter("ProofDocSerialNo", agent.ProofDocSerialNo));
                    if (agent.PswChangeDate.HasValue)
                        cmd.Parameters.Add(new OracleParameter("PswChangeDate", agent.PswChangeDate));
                    else
                        cmd.Parameters.Add(new OracleParameter("PswChangeDate", DBNull.Value));
                    if (agent.PswChangeAgentId.HasValue)
                        cmd.Parameters.Add(new OracleParameter("PswChangeAgentId", agent.PswChangeAgentId));
                    else
                        cmd.Parameters.Add(new OracleParameter("PswChangeAgentId", DBNull.Value));
                    if (agent.PswExpiryDate.HasValue)
                        cmd.Parameters.Add(new OracleParameter("PswExpiryDate", agent.PinExpiryDate));
                    else
                        cmd.Parameters.Add(new OracleParameter("PswExpiryDate", DBNull.Value));
                    if (agent.PswHashMode.HasValue)
                        cmd.Parameters.Add(new OracleParameter("PswHashMode", agent.PswHashMode));
                    else
                        cmd.Parameters.Add(new OracleParameter("PswHashMode", DBNull.Value));
                    if (agent.PinChangeDate.HasValue)
                        cmd.Parameters.Add(new OracleParameter("PinChangeDate", agent.PinChangeDate));
                    else
                        cmd.Parameters.Add(new OracleParameter("PinChangeDate", DBNull.Value));
                    if (agent.PinChangeAgentId.HasValue)
                        cmd.Parameters.Add(new OracleParameter("PinChangeAgentId", agent.PinChangeAgentId));
                    else
                        cmd.Parameters.Add(new OracleParameter("PinChangeAgentId", DBNull.Value));
                    if (agent.PinExpiryDate.HasValue)
                        cmd.Parameters.Add(new OracleParameter("PinExpiryDate", agent.PinExpiryDate));
                    else
                        cmd.Parameters.Add(new OracleParameter("PinExpiryDate", DBNull.Value));
                    if (agent.PinHashMode.HasValue)
                        cmd.Parameters.Add(new OracleParameter("PinHashMode", agent.PinHashMode));
                    else
                        cmd.Parameters.Add(new OracleParameter("PinHashMode", DBNull.Value));
                    if (agent.LoginAttemptLeft.HasValue)
                        cmd.Parameters.Add(new OracleParameter("LoginAttemptLeft", agent.LoginAttemptLeft));
                    else
                        cmd.Parameters.Add(new OracleParameter("LoginAttemptLeft", DBNull.Value));
                    cmd.Parameters.Add(new OracleParameter("PswChangeEnabled", agent.PswChangeEnabled));
                    Debug.Assert(agent.HolderId.HasValue);
                    if (agent.HolderId.HasValue)
                        cmd.Parameters.Add(new OracleParameter("HolderId", (int?)agent.HolderId));
                    else
                        cmd.Parameters.Add(new OracleParameter("HolderId", DBNull.Value));

                    cmd.Parameters.Add(new OracleParameter {
                        ParameterName = "AGENTID_RETURN",
                        OracleDbType = OracleDbType.Int16,
                        Direction = ParameterDirection.Output
                    });

                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    var row = await cmd.ExecuteNonQueryAsync();
                    if (row == 0) throw new Exception("agent creation failed");
                    agentId = short.Parse(cmd.Parameters["AGENTID_RETURN"].Value?.ToString() ?? string.Empty);

                    if (agent.RoleList != null)
                    {
                        foreach (var role in agent.RoleList)
                        {
                            // create new entries in AGENT_AGENTROLE 
                            using (var cmd2 = new OracleCommand(Queries.InsertAgent_AgentRole(), transaction.Connection))
                            {
                                cmd2.CommandType = CommandType.Text;
                                cmd2.Parameters.Add(new OracleParameter("AgentId", agentId));
                                cmd2.Parameters.Add(new OracleParameter("AgentRole", role));
                                cmdString = OracleCommandWrapper.ToString(cmd2, query);
                                var row2 = await cmd2.ExecuteNonQueryAsync();
                                if (row2 == 0) throw new Exception($"agent role {role} creation failed for agentId {agent.AgentUserName}");
                            }
                        }
                    }
                    if (agent.SalePointIdList != null)
                    {
                        foreach (var id in agent.SalePointIdList)
                        {
                            // create new entries in AGENT_SALEPOINTS 
                            using (var cmd2 = new OracleCommand(Queries.InsertAgent_SalePoints(), transaction.Connection))
                            {
                                cmd2.CommandType = CommandType.Text;
                                cmd2.Parameters.Add(new OracleParameter("AgentId", agent.AgentId));
                                cmd2.Parameters.Add(new OracleParameter("SalePointId", id));
                                cmdString = OracleCommandWrapper.ToString(cmd2, query);
                                var row2 = await cmd2.ExecuteNonQueryAsync();
                                if (row2 == 0) throw new Exception($"relation to salepoint {id} creation failed for agentId {agent.AgentUserName}");
                            }
                        }
                    }
                    if (agent.DeviceIdList != null)
                    {
                        foreach (var deviceId in agent.DeviceIdList)
                        {
                            // create new entries in AGENT_DEVICES 
                            using (var cmd2 = new OracleCommand(Queries.InsertAgent_Devices(), transaction.Connection))
                            {
                                cmd2.CommandType = CommandType.Text;
                                cmd2.Parameters.Add(new OracleParameter("AgentId", agent.AgentId));
                                cmd2.Parameters.Add(new OracleParameter("OperatorId", deviceId.OperatorId));
                                cmd2.Parameters.Add(new OracleParameter("DeviceClassId", (short)deviceId.DeviceClassId));
                                cmd2.Parameters.Add(new OracleParameter("OperatorId", deviceId.DeviceCode));
                                cmdString = OracleCommandWrapper.ToString(cmd2, query);
                                var row2 = await cmd2.ExecuteNonQueryAsync();
                                if (row2 == 0) throw new Exception($"relation to deviceId {deviceId.OperatorId}/{deviceId.DeviceClassId}/{deviceId.DeviceCode} creation failed for agentId {agent.AgentUserName}");
                            }
                        }
                    }
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "InsertAgent", cmdString);
                throw;
            }
            return agentId;
        }

        private static async Task<int> P_GetAgentId(OracleTransaction transaction)
        {
            int agentId = 0;
            var cmdString = string.Empty;

            try
            {
                //CREATE SEQUENCE  "ATMPCONFOWN"."AGENTID_SEQ"  MINVALUE 1 MAXVALUE 99999 INCREMENT BY 1 START WITH 250 NOCACHE  NOORDER  CYCLE  NOKEEP  NOSCALE  GLOBAL ;
                var query = Queries.GetNextAgentId();
                using (var cmd = new OracleCommand(query, transaction.Connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    var scalar = await cmd.ExecuteScalarAsync();
                    agentId = scalar == null ? agentId : int.Parse(scalar.ToString());
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetAgentId", cmdString);
                throw;
            }
            return agentId;
        }

        private static async Task<bool> P_UpdateAgent(OracleTransaction transaction, Agent agent, RequestBase<IList<SearchFilter>> filters)
        {
            bool result = false;
            var cmdString = string.Empty;

            try
            {
                var agentIdSpecified = agent.AgentId != 0;
                if (agentIdSpecified == false)
                {
                    throw new Exception($"missing AgentId {agent.AgentId}");
                }

                var query = Queries.UpdateAgent();
                if (filters.Body.Count > 0)
                {
                    for (int nLcv = 0; nLcv < filters.Body.Count; nLcv++)
                    {
                        var pF = filters.Body[nLcv];
                        query += nLcv == filters.Body.Count - 1 ? " " + pF.FieldName.ToUpper() + " = :" + pF.FieldName.ToUpper() : " " + pF.FieldName.ToUpper() + " = :" + pF.FieldName.ToUpper() + ",";
                    }
                    query += $" WHERE AGENTID = {agent.AgentId}";
                    using (var cmd = new OracleCommand(query, transaction.Connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        foreach (var pF in filters.Body) cmd.Parameters.Add(DBOracleHelper.GetOracleParameter(pF));
                        cmdString = OracleCommandWrapper.ToString(cmd, query);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    Type t = agent.GetType();
                    string fieldName;
                    object propertyValue;

                    string whereCondition = " WHERE ";

                    // Use each property of the object passed in
                    foreach (PropertyInfo pi in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        // Get the name of the property
                        fieldName = pi.Name;

                        // Get the value of the property
                        propertyValue = pi.GetValue(agent, null);

                        if (fieldName == "AgentId")
                            whereCondition += " " + fieldName.ToUpper() + " = :" + fieldName.ToUpper() + ",";
                        else
                            query += " " + fieldName.ToUpper() + " = :" + fieldName.ToUpper() + ",";

                        //Debug.WriteLine(fieldName + ": " + (propertyValue == null ? "null" : propertyValue.ToString()));
                    }

                    query += whereCondition;

                    using (var cmd = new OracleCommand(query, transaction.Connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        foreach (PropertyInfo pi in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            // Get the name of the property
                            fieldName = pi.Name;

                            // Get the value of the property
                            propertyValue = pi.GetValue(agent, null);

                            switch (fieldName)
                            {
                                case "PlantId":
                                case "SynId":
                                    if (propertyValue != null && (int)(propertyValue) > 0)
                                        cmd.Parameters.Add(new OracleParameter(fieldName, propertyValue));
                                    else
                                        cmd.Parameters.Add(new OracleParameter(fieldName, DBNull.Value));
                                    break;
                                default:
                                    if (propertyValue != null)
                                        cmd.Parameters.Add(new OracleParameter(fieldName, propertyValue));
                                    else
                                        cmd.Parameters.Add(new OracleParameter(fieldName, DBNull.Value));
                                    break;
                                    //Debug.WriteLine(fieldName + ": " + (propertyValue == null ? "null" : propertyValue.ToString()));
                            }

                            cmdString = OracleCommandWrapper.ToString(cmd, query);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        if (agent.RoleList != null)
                        {
                            // delete ALL old entries in AGENT_AGENTROLE 
                            using (var cmd2 = new OracleCommand(Queries.DeleteAgent_AgentRole("WHERE AGENTID = :AgentId", null), transaction.Connection))
                            {
                                cmd2.CommandType = CommandType.Text;
                                cmd2.Parameters.Add(new OracleParameter("AgentId", agent.AgentId));
                                cmdString = OracleCommandWrapper.ToString(cmd2, query);
                                var row2 = await cmd2.ExecuteNonQueryAsync();
                                if (row2 == 0) throw new Exception($"AGENT_AGENTROLE DELETE failed for agentId {agent.AgentUserName}");
                            }

                            foreach (var role in agent.RoleList)
                            {
                                // create new entries in AGENT_AGENTROLE 
                                using (var cmd2 = new OracleCommand(Queries.InsertAgent_AgentRole(), transaction.Connection))
                                {
                                    cmd2.CommandType = CommandType.Text;
                                    cmd2.Parameters.Add(new OracleParameter("AgentId", agent.AgentId));
                                    cmd2.Parameters.Add(new OracleParameter("AgentRole", role));
                                    cmdString = OracleCommandWrapper.ToString(cmd2, query);
                                    var row2 = await cmd2.ExecuteNonQueryAsync();
                                    if (row2 == 0) throw new Exception($"agent role {role} creation failed for agentId {agent.AgentUserName}");
                                }
                            }
                        }
                        if (agent.SalePointIdList != null)
                        {
                            // delete ALL old entries in AGENT_SALEPOINTS 
                            using (var cmd2 = new OracleCommand(Queries.DeleteAgent_SalePoints("WHERE AGENTID = :AgentId", null), transaction.Connection))
                            {
                                cmd2.CommandType = CommandType.Text;
                                cmd2.Parameters.Add(new OracleParameter("AgentId", agent.AgentId));
                                cmdString = OracleCommandWrapper.ToString(cmd2, query);
                                var row2 = await cmd2.ExecuteNonQueryAsync();
                                if (row2 == 0) throw new Exception($"AGENT_SALEPOINTS DELETE failed for agentId {agent.AgentUserName}");
                            }

                            foreach (var id in agent.SalePointIdList)
                            {
                                // create new entries in AGENT_SALEPOINTS 
                                using (var cmd2 = new OracleCommand(Queries.InsertAgent_SalePoints(), transaction.Connection))
                                {
                                    cmd2.CommandType = CommandType.Text;
                                    cmd2.Parameters.Add(new OracleParameter("AgentId", agent.AgentId));
                                    cmd2.Parameters.Add(new OracleParameter("SalePointId", id));
                                    cmdString = OracleCommandWrapper.ToString(cmd2, query);
                                    var row2 = await cmd2.ExecuteNonQueryAsync();
                                    if (row2 == 0) throw new Exception($"relation to salepoint {id} creation failed for agentId {agent.AgentUserName}");
                                }
                            }
                        }
                        if (agent.DeviceIdList != null)
                        {
                            // delete ALL old entries in AGENT_DEVICES 
                            using (var cmd2 = new OracleCommand(Queries.DeleteAgent_Devices("WHERE AGENTID = :AgentId", null), transaction.Connection))
                            {
                                cmd2.CommandType = CommandType.Text;
                                cmd2.Parameters.Add(new OracleParameter("AgentId", agent.AgentId));
                                cmdString = OracleCommandWrapper.ToString(cmd2, query);
                                var row2 = await cmd2.ExecuteNonQueryAsync();
                                if (row2 == 0) throw new Exception($"AGENT_DEVICES DELETE failed for agentId {agent.AgentUserName}");
                            }

                            foreach (var deviceId in agent.DeviceIdList)
                            {
                                // create new entries in AGENT_DEVICES 
                                using (var cmd2 = new OracleCommand(Queries.InsertAgent_Devices(), transaction.Connection))
                                {
                                    cmd2.CommandType = CommandType.Text;
                                    cmd2.Parameters.Add(new OracleParameter("AgentId", agent.AgentId));
                                    cmd2.Parameters.Add(new OracleParameter("OperatorId", deviceId.OperatorId));
                                    cmd2.Parameters.Add(new OracleParameter("DeviceClassId", (short)deviceId.DeviceClassId));
                                    cmd2.Parameters.Add(new OracleParameter("OperatorId", deviceId.DeviceCode));
                                    cmdString = OracleCommandWrapper.ToString(cmd2, query);
                                    var row2 = await cmd2.ExecuteNonQueryAsync();
                                    if (row2 == 0) throw new Exception($"relation to deviceId {deviceId.OperatorId}/{deviceId.DeviceClassId}/{deviceId.DeviceCode} creation failed for agentId {agent.AgentUserName}");
                                }
                            }
                        }
                    }
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "UpdateAgent", cmdString);
                throw;
            }
            return true;
        }

        public static async Task<short> CreateAgent(string connectionString, RequestBase<Agent> agent)
        {
            OracleConnection connection = null;
            var agentId = (short)0;
            var cmdString = string.Empty;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                using (var transaction = connection.BeginTransaction())
                {
                    agentId = await P_CreateAgent(transaction, agent.Body);
                    if (agentId == 0)
                        transaction.Rollback();
                    else
                        transaction.Commit();
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "CreateAgent", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return agentId;
        }

        public static async Task<IList<Agent>> GetAgentsByFilter(string connectionString, RequestBase<IList<SearchFilter>> filters)
        {
            OracleConnection connection = null;
            var cmdString = string.Empty;
            try
            {
                if (filters?.Body != null)
                    foreach (var filter in filters.Body)
                        filter.FieldType = DBOracleHelper.NormalizeClassName(filter?.FieldType);

                var agents = new List<Agent>();
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var sB = new StringBuilder();
                if (filters?.Body != null)
                    foreach (var filter in filters.Body.Where(p => p.FieldName != null))
                    {
                        if ((filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(int).ToString())?.ToUpper()) ?? false) ||
                            (filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(decimal).ToString())?.ToUpper()) ?? false))
                        {
                            sB.Append((filters.Body.First() == filter ? " WHERE ROWNUM <= " + filters.RowMax + " AND " : " AND ") + $"{filter.FieldName.ToUpper()} = :{filter.FieldName.ToUpper()}");
                        }
                        else if (filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(string).ToString())?.ToUpper()) ?? false)
                        {
                            sB.Append((filters.Body.First() == filter ? " WHERE ROWNUM <= " + filters.RowMax + " AND " : " AND ") + $"{filter.FieldName.ToUpper()} Like :{filter.FieldName.ToUpper()}");
                        }
                        else if (filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(DateTime).ToString())?.ToUpper()) ?? false)
                        {
                            sB.Append((filters.Body.First() == filter ? " WHERE ROWNUM <= " + filters.RowMax + " AND " : " AND ") + $"{filter.FieldName.ToUpper()} = TO_DATE(:{filter.FieldName.ToUpper()},'DD/MM/YYYY')");
                        }
                    }
                var query = Queries.GetAgentByFilter(sB.ToString());
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    if (filters?.Body != null)
                        foreach (var filter in filters.Body.Where(p => !string.IsNullOrWhiteSpace(p.FieldName)))
                        {
                            if ((filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(int).ToString())?.ToUpper()) ?? false) ||
                                (filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(decimal).ToString())?.ToUpper()) ?? false))
                            {
                                cmd.Parameters.Add(new OracleParameter($"{filter.FieldName.ToUpper()}", $"{filter.FieldValue}".ToUpper()));
                            }
                            else if (filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(string).ToString())?.ToUpper()) ?? false)
                            {
                                cmd.Parameters.Add(new OracleParameter($"{filter.FieldName.ToUpper()}", $"{filter.FieldValue}%".ToUpper()));
                            }
                            else if (filter.FieldType?.ToUpper()?.Equals(DBOracleHelper.NormalizeClassName(typeof(DateTime).ToString())?.ToUpper()) ?? false)
                            {
                                DateTime.TryParse(filter.FieldValue, out DateTime birthDay);
                                cmd.Parameters.Add(new OracleParameter($"{filter.FieldName.ToUpper()}", $"{birthDay.ToString("dd/MM/yyyy")}".ToUpper()));
                            }
                        }

                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var agent = DBOracleHelper.GetInstanceOfType<Agent>(reader);
                            agents.Add(agent);
                        }
                    }
                }
                return agents.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToList();
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetAgentsByFilter", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }
        
        public static async Task<int> GetAgentId(string connectionString)
        {
            OracleConnection connection = null;
            int agentId = 0;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                using (var transaction = connection.BeginTransaction())
                {
                    agentId = await P_GetAgentId(transaction);
                    if (agentId == 0)
                        transaction.Rollback();
                    else
                        transaction.Commit();
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetAgentId");
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return agentId;
        }
        
        public static async Task<bool> UpdateAgent(string connectionString, Agent agent, RequestBase<IList<SearchFilter>> filters)
        {
            OracleConnection connection = null;
            bool result = false;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                using (var transaction = connection.BeginTransaction())
                {
                    result = await P_UpdateAgent(transaction, agent, filters);
                    if (result == false)
                    {
                        transaction.Rollback();
                        result = false;
                    }
                    else
                    {
                        transaction.Commit();
                        result = true;
                    }
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "UpdateAgent");
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return true;
        }

        public static async Task<List<AgentProfile>> GetAgentRoles(string connectionString, RequestBase<AgentProfileIdRequest> filters)
        {
            OracleConnection connection = null;
            var cmdString = string.Empty;
            try
            {
                var profiles = new List<AgentProfile>();
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                if (true)
                {
                    var sB = new StringBuilder();
                    sB.Append(" WHERE ROWNUM <= " + filters.RowMax.ToString() + $" AND Id = :profileId");
                    var query = Queries.GetAgentProfiles(filters.Body.AgentProfileId > 0 ? sB.ToString() : "");
                    using (var cmd = new OracleCommand(query, connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        if (filters.Body.AgentProfileId > 0)
                            cmd.Parameters.Add(new OracleParameter("profileId", filters.Body.AgentProfileId));
                        cmdString = OracleCommandWrapper.ToString(cmd, query);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var profile = DBOracleHelper.GetInstanceOfType<AgentProfile>(reader);
                                profiles.Add(profile);
                            }
                        }
                    }
                }
                foreach (var profile in profiles)
                {
                    var sB = new StringBuilder();
                    sB.Append(" AND ProfileId = :profileId");
                    string whereCondition = " WHERE LanguageId = :languageId ";
                    var query = Queries.GetAgentRoles(whereCondition + (profile.Id > 0 ? sB.ToString() : ""));
                    var roleList = new List<AgentRole>();
                    using (var cmd = new OracleCommand(query, connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("languageId", profile.LanguageId));
                        if (profile.Id > 0)
                            cmd.Parameters.Add(new OracleParameter("profileId", profile.Id));
                        cmdString = OracleCommandWrapper.ToString(cmd, query);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var role = DBOracleHelper.GetInstanceOfType<AgentRole>(reader);
                                roleList.Add(role);
                            }
                        }
                    }
                    if (roleList.Count > 0)
                        profile.RoleList = roleList.ToArray();
                }
                return profiles.ToList();
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetAgentRoles", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<List<HolderProfile>> GetAllHolderProfilesDescriptions(string connectionString)
        {
            OracleConnection connection = null;
            var cmdString = string.Empty;
            try
            {
                var profiles = new List<HolderProfile>();
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                if (true)
                {
                    var query = Queries.AllHolderProfilesDescriptions();
                    using (var cmd = new OracleCommand(query, connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmdString = OracleCommandWrapper.ToString(cmd, query);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var profile = DBOracleHelper.GetInstanceOfType<HolderProfile>(reader);
                                profiles.Add(profile);
                            }
                        }
                    }
                }
                return profiles.ToList();
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetHolderProfiles", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }
        #endregion

        #region Media
        public static async Task<IList<Media>> GetMedia(string connectionStringGestown, string connectionStringTariffown, RequestBase<HolderIdRequest> filters)
        {
            OracleConnection connection = null;
            var cmdString = string.Empty;
            try
            {
                var medias = new List<Media>();
                connection = await DBOracleHelper.OpenDBConnection(connectionStringGestown);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionStringGestown);
                var sB = new StringBuilder();
                sB.Append(" WHERE ROWNUM <= " + filters.RowMax.ToString() + $" AND HolderId = :holderId");
                var query = Queries.GetMediaByHolderId(sB.ToString());
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("holderId", (int)filters.Body.HolderId));
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var media = DBOracleHelper.GetInstanceOfType<Media>(reader);
                            medias.Add(media);
                        }
                    }
                }
                return medias.ToList();
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetMediaByHolderId", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<IList<Media>> GetMediaByHolderId(string connectionStringGestown, string connectionStringTariffown, RequestBase<HolderIdRequest> filters)
        {
            OracleConnection connectionGestown = null;
            OracleConnection connectionTariffown = null;
            var cmdString = string.Empty;
            try
            {
                var medias = new List<Media>();
                connectionGestown = await DBOracleHelper.OpenDBConnection(connectionStringGestown);
                if (connectionGestown == null)
                    throw new DBConnectionOpeningException(connectionStringGestown);
                var sB = new StringBuilder();
                sB.Append(" WHERE ROWNUM <= " + filters.RowMax.ToString() + $" AND HolderId = :holderId");
                var query = Queries.GetMediaByHolderId(sB.ToString());
                using (var cmd = new OracleCommand(query, connectionGestown))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("holderId", (int)filters.Body.HolderId));
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var media = DBOracleHelper.GetInstanceOfType<Media>(reader);
                            medias.Add(media);
                        }
                    }
                }
                await DBOracleHelper.CloseDBConnection(connectionGestown);

                var proofDocIds = medias.Where(p => p.ProofDocId.HasValue).Select(p => p.ProofDocId.Value).Distinct().ToArray();
                if (proofDocIds.Length > 0)
                {
                    connectionTariffown = await DBOracleHelper.OpenDBConnection(connectionStringTariffown);
                    if (connectionTariffown == null)
                        throw new DBConnectionOpeningException(connectionStringTariffown);
                    query = $"SELECT PROOFDOCID as ID, PROOFDOCTYPE as Type, PROOFDOCDESC as Description FROM PROOFDOCUMENTS WHERE PROOFDOCID IN ({string.Join(',', proofDocIds)})";
                    var proofDoctType = new List<ProofDoctType>();
                    using (var cmd = new OracleCommand(query, connectionTariffown))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmdString = OracleCommandWrapper.ToString(cmd, query);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var type = DBOracleHelper.GetInstanceOfType<ProofDoctType>(reader);
                                proofDoctType.Add(type);
                            }
                        }
                    }
                    await DBOracleHelper.CloseDBConnection(connectionTariffown);

                    var dict = proofDoctType.ToDictionary(p => p.ID, p => p);
                    foreach (var media in medias.Where(p => p.ProofDocId.HasValue))
                    {
                        if (dict.ContainsKey(media.ProofDocId.Value))
                        {
                            media.ProofDocType = dict[media.ProofDocId.Value].Type;
                            media.ProofDocTypeDescription = dict[media.ProofDocId.Value].Description;
                        }
                    }
                }
                return medias.ToList();
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetMediaByHolderId", cmdString);
                throw;
            }
            finally
            {
                if ((connectionGestown?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connectionGestown);
                if ((connectionTariffown?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connectionTariffown);
            }
        }

        public static async Task<IList<string>> GetMediaByProfileAndBlReason(string connectionStringGestown, int? profile, int? blreason)
        {
            if (!profile.HasValue && !blreason.HasValue)
            {
                return new List<string>();
            }

            OracleConnection connectionGestown = null;
            var cmdString = string.Empty;
            try
            {
                var medias = new List<string>();
                connectionGestown = await DBOracleHelper.OpenDBConnection(connectionStringGestown);
                if (connectionGestown == null)
                    throw new DBConnectionOpeningException(connectionStringGestown);
                var query = Queries.GetMediaByProfileAndBlReason();
                using (var cmd = new OracleCommand(query, connectionGestown))
                {
                    cmd.BindByName = true;
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add("pfofile", OracleDbType.Int32).Value =
                        profile.HasValue ? (object)profile.Value : DBNull.Value;
                    cmd.Parameters.Add("blreason", OracleDbType.Int32).Value =
                        blreason.HasValue ? (object)blreason.Value : DBNull.Value;
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var tscStatus = Convert.ToString(reader["TSCSTATUS"]);
                            var blReasonCode = Convert.ToString(reader["BLREASONCODE"]);
                            var holderId = Convert.ToString(reader["HOLDERID"]);
                            var tscSerialNo = Convert.ToString(reader["TSCSERIALNO"]);
                            var shortCardModel = Convert.ToString(reader["SHORTCARDMODEL"]);
                            var smartCardSn = Convert.ToString(reader["SMARTCARDSN"]);

                            if (!string.IsNullOrWhiteSpace(tscSerialNo) &&
                                ulong.TryParse(
                                    tscSerialNo,
                                    NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture,
                                    out var decimalValue))
                            {
                                tscSerialNo += $" ({decimalValue})";
                            }

                            medias.Add(string.Join(" | ", new[]
                            {
                                tscStatus,
                                blReasonCode,
                                holderId,
                                tscSerialNo,
                                shortCardModel,
                                smartCardSn
                            }));
                        }
                    }
                }
                await DBOracleHelper.CloseDBConnection(connectionGestown);
                return medias;
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetMediaByProfileAndBlReason", cmdString);
                throw;
            }
            finally
            {
                if ((connectionGestown?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connectionGestown);
            }
        }

        public static async Task ResetMediaBl(string connectionStringGestown, string tscSerial, int shortcardmodel)
        {
            OracleConnection connectionGestown = null;
            var cmdString = string.Empty;
            try
            {
                connectionGestown = await DBOracleHelper.OpenDBConnection(connectionStringGestown);
                if (connectionGestown == null)
                    throw new DBConnectionOpeningException(connectionStringGestown);

                using (var transaction = connectionGestown.BeginTransaction())
                {
                    try
                    {
                        var query = Queries.ResetMediaBl();
                        using (var cmd = new OracleCommand(query, connectionGestown))
                        {
                            cmd.BindByName = true;
                            cmd.Transaction = transaction;
                            cmd.CommandType = CommandType.Text;
                            cmd.Parameters.Add("sn", OracleDbType.Varchar2).Value = tscSerial;
                            cmd.Parameters.Add("scm", OracleDbType.Int32).Value = shortcardmodel;
                            cmdString = OracleCommandWrapper.ToString(cmd, query);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
                await DBOracleHelper.CloseDBConnection(connectionGestown);
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "ResetMediaBl", cmdString);
                throw;
            }
            finally
            {
                if ((connectionGestown?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connectionGestown);
            }
        }

        public static async Task ResetMediaStatus(string connectionStringGestown, string tscSerial, int shortcardmodel, byte status)
        {
            OracleConnection connectionGestown = null;
            var cmdString = string.Empty;
            try
            {
                connectionGestown = await DBOracleHelper.OpenDBConnection(connectionStringGestown);
                if (connectionGestown == null)
                    throw new DBConnectionOpeningException(connectionStringGestown);

                using (var transaction = connectionGestown.BeginTransaction())
                {
                    try
                    {
                        var query = Queries.ResetMediaStatus();
                        using (var cmd = new OracleCommand(query, connectionGestown))
                        {
                            cmd.BindByName = true;
                            cmd.Transaction = transaction;
                            cmd.CommandType = CommandType.Text;
                            cmd.Parameters.Add("sn", OracleDbType.Varchar2).Value = tscSerial;
                            cmd.Parameters.Add("scm", OracleDbType.Int32).Value = shortcardmodel;
                            cmd.Parameters.Add("status", OracleDbType.Int16).Value = status;
                            cmdString = OracleCommandWrapper.ToString(cmd, query);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
                await DBOracleHelper.CloseDBConnection(connectionGestown);
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "ResetMediaStatus", cmdString);
                throw;
            }
            finally
            {
                if ((connectionGestown?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connectionGestown);
            }
        }

        public static async Task<LogicalMediaInfo?> GetMediaLogicalSerialNumber(string connectionString, uint logicalSerialNumber, decimal manufacturerId, int shortCardModel)
        {
            OracleConnection connection = null;
            LogicalMediaInfo retValue = null;
            var cmdString = string.Empty;
            try
            {
                if (manufacturerId != 0 && shortCardModel == 0)
                    shortCardModel = await GetShortCardModel(connectionString, manufacturerId);

                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.GetMediaLoSn();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("serial", OracleDbType.Varchar2)).Value = logicalSerialNumber.ToString("X").PadLeft(8, '0');
                    cmd.Parameters.Add(new OracleParameter("shortcardmodel", shortCardModel));
                    cmdString = OracleCommandWrapper.ToString(cmd, query);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var info = DBOracleHelper.GetInstanceOfType<LogicalMediaInfo>(reader);
                            if (info == null) continue;
                            retValue = info;
                            break;
                        }
                    }
                }
                return retValue;
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetMediaLogicalSerialNumber", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task<SbmeModels.SBME2.PhysicalMediaInfo?> GetMediaPhysicalSerialNumber(string connectionString, int saleDeviceId, uint phisicalSerialNumber)
        {
            OracleConnection connection = null;
            SbmeModels.SBME2.PhysicalMediaInfo retValue = null;
            var cmdString = string.Empty;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.GetMediaPhSn();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("serial", (long)phisicalSerialNumber));
                    cmd.Parameters.Add(new OracleParameter("saledevice", saleDeviceId));
                    cmdString = OracleCommandWrapper.ToString(cmd, query);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var info = DBOracleHelper.GetInstanceOfType<TemporarilyModels.PhysicalMediaInfo>(reader);
                            retValue = info?.AsSbme2PhMediaInfo;
                            break;
                        }
                    }
                }
                return retValue;
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "GetMediaPhisicalSerialNumber", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        public static async Task CreateMedia(string connectionString, RequestBase<Media> media)
        {
            OracleConnection connection = null;
            var cmdString = string.Empty;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.InsertMedia();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("SHORTCARDMODEL", media.Body.ShortCardModel));
                    cmd.Parameters.Add(new OracleParameter("TSCSERIALNO", media.Body.TscSerialNo));
                    cmd.Parameters.Add(new OracleParameter("SALEOPERATORID", media.Body.SaleOperatorId));
                    cmd.Parameters.Add(new OracleParameter("DOCCLASSID", media.Body.DocClassId));
                    cmd.Parameters.Add(new OracleParameter("DOCTYPEID", media.Body.DocTypeId));
                    cmd.Parameters.Add(new OracleParameter("TSCHOLDERASSOCIATIONTYPE", media.Body.TscHolderAssociationType));
                    cmd.Parameters.Add(new OracleParameter("HOLDERID", (int?)media.Body.HolderId));
                    cmd.Parameters.Add(new OracleParameter("MAINPROFILEID", media.Body.MainProfileId));
                    cmd.Parameters.Add(new OracleParameter("PROFILEID2", media.Body.ProfileId2));
                    cmd.Parameters.Add(new OracleParameter("PROFILEID3", media.Body.ProfileId3));
                    cmd.Parameters.Add(new OracleParameter("ISSUINGDATETIME", OracleDbType.Date)).Value = media.Body.IssuingDateTime;
                    cmd.Parameters.Add(new OracleParameter("PRICE", media.Body.Price));
                    cmd.Parameters.Add(new OracleParameter("PRICEVERSIONID", media.Body.PriceVersionId));
                    cmd.Parameters.Add(new OracleParameter("TSCVALIDITYENDDATE", OracleDbType.Date)).Value = media.Body.TscValidityEndDate;
                    cmd.Parameters.Add(new OracleParameter("PROFILEVALIDITYENDDATE", OracleDbType.Date)).Value = media.Body.ProfileValidityEndDate;
                    cmd.Parameters.Add(new OracleParameter("PROFILE2VALIDITYENDDATE", OracleDbType.Date)).Value = media.Body.Profile2ValidityEndDate;
                    cmd.Parameters.Add(new OracleParameter("PROFILE3VALIDITYENDDATE", OracleDbType.Date)).Value = media.Body.Profile3ValidityEndDate;
                    cmd.Parameters.Add(new OracleParameter("ISSUINGOPERATORID", media.Body.IssuingOperatorId));
                    cmd.Parameters.Add(new OracleParameter("ISSUINGDEVICECLASSID", media.Body.IssuingDeviceClassId));
                    cmd.Parameters.Add(new OracleParameter("ISSUINGDEVICECODE", media.Body.IssuingDeviceCode));
                    cmd.Parameters.Add(new OracleParameter("ISSUINGSALEDEVICEID", media.Body.IssuingSaleDeviceId));
                    cmd.Parameters.Add(new OracleParameter("TSCSTATUS", media.Body.TscStatus));
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    var row = await cmd.ExecuteNonQueryAsync();
                    if (row == 0) throw new Exception("media creation failed");
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "CreateMedia", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return;
        }

        public static async Task ForgetTscDocument(string connectionString, string tscSerial)
        {
            OracleConnection? connection = null;
            OracleTransaction? transaction = null;
            try
            {
                if (tscSerial.Contains(":"))
                {
                    var split = tscSerial.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    switch (split[1])
                    {
                        case "16": tscSerial = split[0]; break;
                        case "10":
                            {
                                var tscSerialDecimal = long.Parse(split[0]);
                                tscSerial = $"{tscSerialDecimal:X}";
                            }
                            break;
                        default: throw new Exception($"Base {split[1]} numbering system not supported");
                    }
                }
                else
                {
                    try
                    {
                        var tscSerialDecimal = long.Parse(tscSerial);
                        tscSerial = $"{tscSerialDecimal:X}";
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error(Logger, ex);
                        throw;
                    }
                }

                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    var query = string.Empty;
                    connection = await DBOracleHelper.OpenDBConnection(connectionString);
                    if (connection == null)
                        throw new DBConnectionOpeningException(connectionString);
                    transaction = connection.BeginTransaction();

                    // REFUNDS
                    var refunds = new List<long>();
                    query = $"SELECT SERIALNO FROM REFUNDS WHERE TSCSERIALNO='{tscSerial}'";
                    using (var cmd = new OracleCommand(query, transaction.Connection))
                    {
                        try
                        {
                            cmd.CommandType = CommandType.Text;
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    refunds.Add(reader.GetInt64(0));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Error(Logger, ex);
                            LogHelper.Error(Logger, query);
                        }
                    }

                    if (refunds.Count > 0)
                    {
                        var strRefunds = string.Join(", ", refunds);

                        // REFUNDDETAILS
                        query = $"DELETE FROM REFUNDDETAILS WHERE SERIALNO IN ({strRefunds})";
                        await ExecuteNonQuery(query, connection);

                        // REFUNDS
                        query = $"DELETE FROM REFUNDS WHERE TSCSERIALNO='{tscSerial}'";
                        await ExecuteNonQuery(query, connection);
                    }

                    // RECHARGES
                    var recharges = new List<long>();
                    query = $"SELECT RECHARGELASTSERIALNO FROM RECHARGES WHERE TSCSERIALNO='{tscSerial}'";
                    using (var cmd = new OracleCommand(query, transaction.Connection))
                    {
                        try
                        {
                            cmd.CommandType = CommandType.Text;
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    recharges.Add(reader.GetInt64(0));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Error(Logger, ex);
                            LogHelper.Error(Logger, query);
                        }
                    }

                    if (recharges.Count > 0)
                    {
                        var strRecharges = string.Join(", ", recharges);

                        // RECHARGESDONEDETAILS
                        query = $"DELETE FROM RECHARGESDONEDETAILS WHERE RECHARGESERIALNO IN ({strRecharges})";
                        await ExecuteNonQuery(query, connection);

                        // RECHARGESDONE
                        query = $"DELETE FROM RECHARGESDONE WHERE RECHARGESERIALNO IN ({strRecharges})";
                        await ExecuteNonQuery(query, connection);

                        // RECHARGES
                        query = $"DELETE FROM RECHARGES WHERE TSCSERIALNO='{tscSerial}'";
                        await ExecuteNonQuery(query, connection);
                    }

                    // TSCBLACKLIST
                    query = $"DELETE FROM TSCBLACKLIST WHERE FIRSTTSCSERIALNO='{tscSerial}' OR LASTTSCSERIALNO='{tscSerial}'";
                    await ExecuteNonQuery(query, connection);

                    // CONTRACTSCONSOLIDATION
                    query = $"DELETE FROM CONTRACTSCONSOLIDATION WHERE TSCSERIALNO='{tscSerial}'";
                    await ExecuteNonQuery(query, connection);

                    // LONGTERMCONTRACTSHISTORY
                    query = $"DELETE FROM LONGTERMCONTRACTSHISTORY WHERE TSCSERIALNO='{tscSerial}'";
                    await ExecuteNonQuery(query, connection);

                    // LONGTERMCONTRACTDETAILS
                    query = $"DELETE FROM LONGTERMCONTRACTDETAILS WHERE TSCSERIALNO='{tscSerial}'";
                    await ExecuteNonQuery(query, connection);

                    // LONGTERMCONTRACTS
                    query = $"DELETE FROM LONGTERMCONTRACTS WHERE TSCSERIALNO='{tscSerial}'";
                    await ExecuteNonQuery(query, connection);

                    // TSCDOCUMENTDETAILSHISTORY
                    query = $"DELETE FROM TSCDOCUMENTDETAILSHISTORY WHERE TSCSERIALNO='{tscSerial}'";
                    await ExecuteNonQuery(query, connection);

                    // TSCDOCUMENTDETAILS
                    query = $"DELETE FROM TSCDOCUMENTDETAILS WHERE TSCSERIALNO='{tscSerial}'";
                    await ExecuteNonQuery(query, connection);

                    // TSCDOCUMENTSHISTORY
                    query = $"DELETE FROM TSCDOCUMENTSHISTORY WHERE TSCSERIALNO='{tscSerial}'";
                    await ExecuteNonQuery(query, connection);

                    // TSCDOCUMENTS
                    query = $"DELETE FROM TSCDOCUMENTS WHERE TSCSERIALNO='{tscSerial}'";
                    await ExecuteNonQuery(query, connection);

                    transaction.Commit();
                }
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                if (connection != null && connection.State != ConnectionState.Closed)
                {
                    await DBOracleHelper.CloseDBConnection(connection);
                }
            }
        }

        private static async Task ExecuteNonQuery(string commandText, OracleConnection connection, bool throwable = true)
        {
            using (var cmd = new OracleCommand(commandText, connection))
            {
                try
                {
                    cmd.CommandType = CommandType.Text;
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    LogHelper.Error(Logger, ex);
                    LogHelper.Error(Logger, commandText);
                    if (throwable) throw;
                }
            }
        }

        // Older Version
        public static async Task<bool> BlackListMediaV2(string connectionString, int reasonCode, BlackList bl, bool createHistory = true, bool commitFlg = true)//[FromBody] RequestBase<BlackListContract>)
        {
            OracleConnection connection = null;
            OracleTransaction transaction = null;
            var cmdString = string.Empty;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                transaction = connection.BeginTransaction();

                if (createHistory)
                {
                    var archiveResult = await ArchiveTSCDocumentV2(connection, transaction, bl.ShortCardModel, bl.LastSerialNo, bl.ReasonCode);

                    // Update document
                    var updateQuery = Queries.UpdateTSCDocument();
                    using (var cmd = new OracleCommand(updateQuery, connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("AgentID", bl.AgentId));
                        cmd.Parameters.Add(new OracleParameter("ReasonCode", bl.ReasonCode));
                        cmd.Parameters.Add(new OracleParameter("Suspended", bl.BlackListSuspended));
                        cmd.Parameters.Add(new OracleParameter("ShortCardModel", bl.ShortCardModel));
                        cmd.Parameters.Add(new OracleParameter("LastSerialNo", bl.LastSerialNo));
                        cmdString = OracleCommandWrapper.ToString(cmd, updateQuery);
                        var res = await cmd.ExecuteNonQueryAsync();
                        if (res == 0) throw new Exception("Update document failed");
                    }

                    // Check if document is in Black list
                    bool isDocumentInBlackList;
                    var results = new List<string>();
                    var checkQuery = Queries.SelectTSCDocument();
                    using (var cmd = new OracleCommand(checkQuery, connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("ShortCardModel", bl.ShortCardModel));
                        cmd.Parameters.Add(new OracleParameter("LastSerialNo", bl.LastSerialNo));

                        cmdString = OracleCommandWrapper.ToString(cmd, checkQuery);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var col1 = reader.GetValue(0).ToString();
                                var col2 = reader.GetValue(1).ToString();
                                var col3 = reader.GetValue(2).ToString();
                                results.Add(col1);
                                results.Add(col2);
                                results.Add(col3);
                            }
                        }

                        // Considering 3 fields
                        if (results.Count == 3) isDocumentInBlackList = true;
                        else isDocumentInBlackList = false;
                    }

                    if (!isDocumentInBlackList)
                    {
                        var insertQuery = Queries.InsertTSCDocumentInBlackList();
                        using (var cmd = new OracleCommand(insertQuery, connection))
                        {
                            cmd.CommandType = CommandType.Text;
                            cmd.Parameters.Add(new OracleParameter("ShortCardModel", bl.ShortCardModel));
                            cmd.Parameters.Add(new OracleParameter("SaleOperatorID", bl.SaleOperatorId));
                            cmd.Parameters.Add(new OracleParameter("LastSerialNo", bl.LastSerialNo));
                            cmd.Parameters.Add(new OracleParameter("FirstSerialNo", bl.FirstSerialNo));
                            if (bl.ReasonCode.HasValue)
                                cmd.Parameters.Add(new OracleParameter("ReasonCode", bl.ReasonCode));
                            else
                                cmd.Parameters.Add(new OracleParameter("ReasonCode", DBNull.Value));
                            cmd.Parameters.Add(new OracleParameter("AgentID", bl.AgentId));
                            cmd.Parameters.Add(new OracleParameter("Suspended", bl.BlackListSuspended));
                            cmd.Parameters.Add(new OracleParameter("InsertDistr", bl.Indistribution));
                            cmdString = OracleCommandWrapper.ToString(cmd, insertQuery);
                            var res = await cmd.ExecuteNonQueryAsync();
                            if (res == 0) throw new Exception("Insert document failed");
                        }
                    }

                    if (commitFlg)
                    {
                        transaction.Commit();
                    }

                    return true;

                }

                // what to do in this case ????
                return false; // row to delete???
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ExHelper.ThrowExceptionContainer(ex, "BlacklistMedia", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }

        // Older Version
        public static async Task<bool> ArchiveTSCDocumentV2(OracleConnection connection, OracleTransaction transaction, int shortCardModel, string tscSerialNo, int? reasonCode)
        {
            var cmdString = string.Empty;
            try
            {
                bool isInsertedInHistory;
                var insertInHistoryQuery = Queries.InsertTSCDocumentInHistory();
                using (var cmd = new OracleCommand(insertInHistoryQuery, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("ReasonCode", reasonCode));
                    cmd.Parameters.Add(new OracleParameter("ShortCardModel", shortCardModel));
                    cmd.Parameters.Add(new OracleParameter("TscSerialNo", tscSerialNo));

                    cmdString = OracleCommandWrapper.ToString(cmd, insertInHistoryQuery);
                    var res = await cmd.ExecuteNonQueryAsync();
                    if (res == 1) isInsertedInHistory = true;
                    else isInsertedInHistory = false;
                }

                if (isInsertedInHistory)
                {
                    var insertInHistoryDetailQuery = Queries.InsertTSCDocumentInHistoryDetail();
                    using (var cmd = new OracleCommand(insertInHistoryDetailQuery, connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("ReasonCode", reasonCode));
                        cmd.Parameters.Add(new OracleParameter("ShortCardModel", shortCardModel));
                        cmd.Parameters.Add(new OracleParameter("TscSerialNo", tscSerialNo));
                        cmdString = OracleCommandWrapper.ToString(cmd, insertInHistoryDetailQuery);
                        var res = await cmd.ExecuteNonQueryAsync();

                        if (res == 1)
                        {
                            transaction.Commit();
                            return true;
                        }
                        else throw new Exception("Insert document in history detail failed");
                    }
                }
                else throw new Exception("Insert document in history failed");
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ExHelper.ThrowExceptionContainer(ex, "ArchiveTSCDocument", cmdString);
                throw;
            }
        }

        // Older Version
        public static async Task<bool> BlackListContractV2(string connectionString, ContractBlackList bcl, bool createHistory = true)//[FromBody] RequestBase<BlackListContract>)
        {
            OracleConnection connection = null;
            OracleTransaction transaction = null;
            var cmdString = string.Empty;
            try
            {
                int flag = 0; // It's LongTermContract

                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                transaction = connection.BeginTransaction();
                var query = Queries.InsertContractInBlackList();

                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("IssuingDate", bcl.IssuingDate));
                    cmd.Parameters.Add(new OracleParameter("SaleOperatorId", bcl.BlackList.SaleOperatorId));
                    cmd.Parameters.Add(new OracleParameter("SaleDeviceId", bcl.SaleDeviceId));
                    cmd.Parameters.Add(new OracleParameter("FirstSerialNo", bcl.BlackList.FirstSerialNo));
                    cmd.Parameters.Add(new OracleParameter("LastSerialNo", bcl.BlackList.LastSerialNo));
                    cmd.Parameters.Add(new OracleParameter("IsShortTermContract", flag)); // flag param or bcl.Blg.Isshorttermcontract ???
                    cmd.Parameters.Add(new OracleParameter("ReasonCode", bcl.BlackList.ReasonCode));
                    cmd.Parameters.Add(new OracleParameter("BlSuspended", bcl.BlackList.BlackListSuspended));
                    cmd.Parameters.Add(new OracleParameter("BlInsertDate", bcl.BlackList.BlackListInsertDate));
                    cmd.Parameters.Add(new OracleParameter("BlAddedBy", bcl.BlackList.AgentId));
                    cmd.Parameters.Add(new OracleParameter("Indistribution", bcl.BlackList.Indistribution));

                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    var row = await cmd.ExecuteNonQueryAsync();
                    if (row == 0) throw new Exception("Insert contract failed");
                }

                if (createHistory)
                {
                    // Try to insert contract in the ShortTermContractHistory  
                    var insertSTCHquery = Queries.InsertShortTermContractInHistory();
                    int rows;

                    using (var cmd = new OracleCommand(insertSTCHquery, connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("IssuingDate", bcl.IssuingDate));
                        cmd.Parameters.Add(new OracleParameter("SaleOperatorId", bcl.BlackList.SaleOperatorId));
                        cmd.Parameters.Add(new OracleParameter("SaleDeviceId", bcl.SaleDeviceId));
                        cmd.Parameters.Add(new OracleParameter("FirstSerialNo", bcl.BlackList.FirstSerialNo));
                        cmd.Parameters.Add(new OracleParameter("LastSerialNo", bcl.BlackList.LastSerialNo));
                        // Paola told me something about the key ... probably there are 3 parameters to add and query to modify
                        cmdString = OracleCommandWrapper.ToString(cmd, insertSTCHquery);
                        rows = await cmd.ExecuteNonQueryAsync();
                    }

                    if (rows == 1) // Insert contract in the ShortTermContractsHistory performed
                    {
                        var updateSTCquery = Queries.UpdateShortTermContracts();
                        using (var cmd = new OracleCommand(updateSTCquery, connection))
                        {
                            cmd.CommandType = CommandType.Text;
                            cmd.Parameters.Add(new OracleParameter("AgentId", bcl.BlackList.AgentId));
                            cmd.Parameters.Add(new OracleParameter("ReasonCode", bcl.BlackList.ReasonCode));
                            cmd.Parameters.Add(new OracleParameter("BlSuspended", bcl.BlackList.BlackListSuspended));
                            cmd.Parameters.Add(new OracleParameter("IssuingDate", bcl.IssuingDate));
                            cmd.Parameters.Add(new OracleParameter("SaleOperatorId", bcl.BlackList.SaleOperatorId));
                            cmd.Parameters.Add(new OracleParameter("SaleDeviceId", bcl.SaleDeviceId));
                            cmd.Parameters.Add(new OracleParameter("LastSerialNo", bcl.BlackList.LastSerialNo));
                            cmd.Parameters.Add(new OracleParameter("FirstSerialNo", bcl.BlackList.FirstSerialNo));

                            cmdString = OracleCommandWrapper.ToString(cmd, updateSTCquery);
                            var row = await cmd.ExecuteNonQueryAsync();
                            if (row == 0) throw new Exception("Update ShortTermContracts failed");
                        }
                    }
                    else // Insert contract in the ShortTermContractsHistory not performed
                    {
                        // So insert contract in the LongTermContractHistory
                        var insertLTCHquery = Queries.InsertLongTermContractInHistory();

                        using (var cmd = new OracleCommand(insertLTCHquery, connection))
                        {
                            cmd.CommandType = CommandType.Text;
                            cmd.Parameters.Add(new OracleParameter("IssuingDate", bcl.IssuingDate));
                            cmd.Parameters.Add(new OracleParameter("SaleOperatorId", bcl.BlackList.SaleOperatorId));
                            cmd.Parameters.Add(new OracleParameter("SaleDeviceId", bcl.SaleDeviceId));
                            cmd.Parameters.Add(new OracleParameter("FirstSerialNo", bcl.BlackList.FirstSerialNo));
                            cmd.Parameters.Add(new OracleParameter("LastSerialNo", bcl.BlackList.LastSerialNo));
                            // Paola told me something about the key ... probably there are 3 parameters to add and query to modify
                            cmdString = OracleCommandWrapper.ToString(cmd, insertLTCHquery);
                            var row = await cmd.ExecuteNonQueryAsync();

                            if (row == 1) // Insert contract in the LongTermContractsHistory performed
                            {
                                var updateLTCquery = Queries.UpdateLongTermContracts();
                                using (var cmd2 = new OracleCommand(updateLTCquery, connection))
                                {
                                    cmd2.CommandType = CommandType.Text;
                                    cmd2.Parameters.Add(new OracleParameter("AgentId", bcl.BlackList.AgentId));
                                    cmd2.Parameters.Add(new OracleParameter("ReasonCode", bcl.BlackList.ReasonCode));
                                    cmd2.Parameters.Add(new OracleParameter("BlSuspended", bcl.BlackList.BlackListSuspended));
                                    cmd2.Parameters.Add(new OracleParameter("IssuingDate", bcl.IssuingDate));
                                    cmd2.Parameters.Add(new OracleParameter("SaleOperatorId", bcl.BlackList.SaleOperatorId));
                                    cmd2.Parameters.Add(new OracleParameter("SaleDeviceId", bcl.SaleDeviceId));
                                    cmd2.Parameters.Add(new OracleParameter("LastSerialNo", bcl.BlackList.LastSerialNo));
                                    cmd2.Parameters.Add(new OracleParameter("FirstSerialNo", bcl.BlackList.FirstSerialNo));

                                    cmdString = OracleCommandWrapper.ToString(cmd2, updateLTCquery);
                                    var updatedrow = await cmd2.ExecuteNonQueryAsync();
                                    if (updatedrow == 0) throw new Exception("Update LongtTermContracts failed");
                                }
                            }
                            else
                            {
                                throw new Exception("Insert Contract in history failed");
                            }
                        }
                    }
                }

                transaction.Commit();
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ExHelper.ThrowExceptionContainer(ex, "BlacklistContract", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return true;
        }

        // Check if card is in BlackList
        // return true if card is found else false
        public static async Task<bool> CheckCardInBlackList(int scm, string sn, string connectionString)
        {
            OracleConnection connection = null;
            OracleTransaction transaction = null;
            var cmdString = string.Empty;
            try
            {
                using (connection = await DBOracleHelper.OpenDBConnection(connectionString))
                {
                    if (connection == null)
                        throw new DBConnectionOpeningException(connectionString);
                    using (transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            var queryToPerform = Queries.SelectReasonCode();
                            using (var cmd = new OracleCommand(queryToPerform, connection))
                            {
                                cmd.CommandType = CommandType.Text;
                                cmd.Parameters.Add(new OracleParameter("SHORTCARDMODEL", scm));
                                cmd.Parameters.Add(new OracleParameter("LASTSERIALNO", sn.ToUpper()));

                                cmdString = OracleCommandWrapper.ToString(cmd, queryToPerform);
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    var results = new List<string>();
                                    while (await reader.ReadAsync())
                                    {
                                        // Bad code: to modify when models will be added
                                        var reasonCode = reader.GetValue(0).ToString();
                                        results.Add(reasonCode);
                                    }
                                    if (results.Count != 1) throw new Exception("Checking card in TSCBLACKLIST failed!");
                                }

                                transaction.Commit();
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "CheckCardInBlacklist", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }

            /*OracleConnection connection = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var results = new List<string>();
                var queryToPerform = Queries.SelectReasonCode();

                using (var cmd = new OracleCommand(queryToPerform, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("SHORTCARDMODEL", scm));
                    cmd.Parameters.Add(new OracleParameter("LASTSERIALNO", sn.ToUpper()));

                    cmdString = OracleCommandWrapper.ToString(cmd, queryToPerform);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Bad code: to modify when models will be added
                            var reasonCode = reader.GetValue(0).ToString();
                            results.Add(reasonCode);
                        }
                    }

                    if (results.Count != 1) return false;
                    else return true;
                }

            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "InsertCustomer", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }*/
        }

        // Delete card from BlackList
        // return true if card is deleted else false
        public static async Task<bool> DeleteCardFromBlackList(BlackListKey blk, string connectionString)
        {
            OracleConnection connection = null;
            OracleTransaction transaction = null;
            var cmdString = string.Empty;
            try
            {
                using (connection = await DBOracleHelper.OpenDBConnection(connectionString))
                {
                    if (connection == null)
                        throw new DBConnectionOpeningException(connectionString);
                    using (transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            var queryToPerform = Queries.DeleteCardFromBlackList();
                            using (var cmd = new OracleCommand(queryToPerform, connection))
                            {
                                cmd.CommandType = CommandType.Text;
                                cmd.Parameters.Add(new OracleParameter("SHORTCARDMODEL", blk.ShortCardModel));
                                cmd.Parameters.Add(new OracleParameter("LASTSERIALNO", blk.Lastserialno.ToUpper()));
                                cmdString = OracleCommandWrapper.ToString(cmd, queryToPerform);
                                var res = await cmd.ExecuteNonQueryAsync();

                                if (res != 1) throw new Exception("Delete card from TSCBLACKLIST failed!");
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "DeleteCardFromBlacklist", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }

            /*OracleConnection connection = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);

                var queryToPerform = Queries.DeleteCardFromBlackList();

                using (var cmd = new OracleCommand(queryToPerform, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("SHORTCARDMODEL", blk.ShortCardModel));
                    cmd.Parameters.Add(new OracleParameter("LASTSERIALNO", blk.Lastserialno.ToUpper()));
                    cmdString = OracleCommandWrapper.ToString(cmd, queryToPerform);
                    var res = await cmd.ExecuteNonQueryAsync();

                    if (res != 1) throw new Exception("Delete card from blacklist failed!");
                    else return true;
                }

            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "InsertCustomer", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }*/
        }

        // Insert contract in BlackList
        // return true if contract is inserted else false
        public static async Task<bool> BlackListContract(string connectionString, ContractBlackList bcl, bool createHistory = true)
        {
            OracleConnection connection = null;
            OracleTransaction transaction = null;
            var cmdString = string.Empty;

            try
            {
                using (connection = await DBOracleHelper.OpenDBConnection(connectionString))
                {
                    if (connection == null)
                        throw new DBConnectionOpeningException(connectionString);
                    using (transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Insert Contract in CONTRACTBLDISTRLIST
                            var query = Queries.InsertContractInBlackList();
                            using (var cmd = new OracleCommand(query, connection))
                            {
                                cmd.CommandType = CommandType.Text;
                                cmd.Parameters.Add(new OracleParameter("IssuingDate", bcl.IssuingDate));
                                cmd.Parameters.Add(new OracleParameter("SaleOperatorId", bcl.BlackList.SaleOperatorId));
                                cmd.Parameters.Add(new OracleParameter("SaleDeviceId", bcl.SaleDeviceId));
                                cmd.Parameters.Add(new OracleParameter("FirstSerialNo", bcl.BlackList.FirstSerialNo));
                                cmd.Parameters.Add(new OracleParameter("LastSerialNo", bcl.BlackList.LastSerialNo));
                                cmd.Parameters.Add(new OracleParameter("IsShortTermContract", bcl.BlackList.IsShortTermContract));
                                cmd.Parameters.Add(new OracleParameter("ReasonCode", bcl.BlackList.ReasonCode));
                                cmd.Parameters.Add(new OracleParameter("BlSuspended", bcl.BlackList.BlackListSuspended));
                                cmd.Parameters.Add(new OracleParameter("BlInsertDate", bcl.BlackList.BlackListInsertDate));
                                cmd.Parameters.Add(new OracleParameter("BlAddedBy", bcl.BlackList.AgentId));
                                cmd.Parameters.Add(new OracleParameter("Indistribution", bcl.BlackList.Indistribution));

                                cmdString = OracleCommandWrapper.ToString(cmd, query);
                                var row = await cmd.ExecuteNonQueryAsync();
                                if (row != 1) throw new Exception("Insert contract in CONTRACTBLDISTRLIST failed");
                            }

                            if (createHistory)
                            {
                                if (bcl.BlackList.IsShortTermContract == 1)
                                {
                                    // Insert contract in the SHORTTERMCONTRACTSHISTORY 
                                    var insertSTCHquery = Queries.InsertShortTermContractInHistory();
                                    using (var cmd = new OracleCommand(insertSTCHquery, connection))
                                    {
                                        cmd.CommandType = CommandType.Text;
                                        cmd.Parameters.Add(new OracleParameter("IssuingDate", bcl.IssuingDate));
                                        cmd.Parameters.Add(new OracleParameter("SaleOperatorId", bcl.BlackList.SaleOperatorId));
                                        cmd.Parameters.Add(new OracleParameter("SaleDeviceId", bcl.SaleDeviceId));
                                        cmd.Parameters.Add(new OracleParameter("FirstSerialNo", bcl.BlackList.FirstSerialNo));
                                        cmd.Parameters.Add(new OracleParameter("LastSerialNo", bcl.BlackList.LastSerialNo));
                                        // New Key, needs values
                                        //cmd.Parameters.Add(new OracleParameter("TSCSaledoperatorid", ));
                                        //cmd.Parameters.Add(new OracleParameter("TSCFirstSerialNo", ));
                                        //cmd.Parameters.Add(new OracleParameter("TSCShortcardmodel", ));                       
                                        cmdString = OracleCommandWrapper.ToString(cmd, insertSTCHquery);
                                        var row = await cmd.ExecuteNonQueryAsync();
                                        if (row != 1) throw new Exception("Insert contract in SHORTTERMCONTRACTSHISTORY failed");
                                    }

                                    // Update SHORTTERMCONTRACTS
                                    var updateSTCquery = Queries.UpdateShortTermContracts();
                                    using (var cmd = new OracleCommand(updateSTCquery, connection))
                                    {
                                        cmd.CommandType = CommandType.Text;
                                        cmd.Parameters.Add(new OracleParameter("AgentId", bcl.BlackList.AgentId));
                                        cmd.Parameters.Add(new OracleParameter("ReasonCode", bcl.BlackList.ReasonCode));
                                        cmd.Parameters.Add(new OracleParameter("BlSuspended", bcl.BlackList.BlackListSuspended));
                                        cmd.Parameters.Add(new OracleParameter("IssuingDate", bcl.IssuingDate));
                                        cmd.Parameters.Add(new OracleParameter("SaleOperatorId", bcl.BlackList.SaleOperatorId));
                                        cmd.Parameters.Add(new OracleParameter("SaleDeviceId", bcl.SaleDeviceId));
                                        cmd.Parameters.Add(new OracleParameter("LastSerialNo", bcl.BlackList.LastSerialNo));
                                        cmd.Parameters.Add(new OracleParameter("FirstSerialNo", bcl.BlackList.FirstSerialNo));

                                        cmdString = OracleCommandWrapper.ToString(cmd, updateSTCquery);
                                        var row = await cmd.ExecuteNonQueryAsync();
                                        if (row != 1) throw new Exception("Update SHORTTERMCONTRACTS failed");
                                    }
                                }
                                if (bcl.BlackList.IsShortTermContract == 0)
                                {
                                    // Insert contract in the LONGTERMCONTRACTHISTORY
                                    var insertLTCHquery = Queries.InsertLongTermContractInHistory();
                                    using (var cmd = new OracleCommand(insertLTCHquery, connection))
                                    {
                                        cmd.CommandType = CommandType.Text;
                                        cmd.Parameters.Add(new OracleParameter("IssuingDate", bcl.IssuingDate));
                                        cmd.Parameters.Add(new OracleParameter("SaleOperatorId", bcl.BlackList.SaleOperatorId));
                                        cmd.Parameters.Add(new OracleParameter("SaleDeviceId", bcl.SaleDeviceId));
                                        cmd.Parameters.Add(new OracleParameter("FirstSerialNo", bcl.BlackList.FirstSerialNo));
                                        cmd.Parameters.Add(new OracleParameter("LastSerialNo", bcl.BlackList.LastSerialNo));
                                        // New Key, needs values
                                        //cmd.Parameters.Add(new OracleParameter("TSCSaledoperatorid", ));
                                        //cmd.Parameters.Add(new OracleParameter("TSCFirstSerialNo", ));
                                        //cmd.Parameters.Add(new OracleParameter("TSCShortcardmodel", )); 
                                        cmdString = OracleCommandWrapper.ToString(cmd, insertLTCHquery);
                                        var row = await cmd.ExecuteNonQueryAsync();
                                        if (row != 1) throw new Exception("Insert contract in LONGTERMCONTRACTSHISTORY failed");
                                    }

                                    // Update LONGTERMCONTRACTS
                                    var updateLTCquery = Queries.UpdateLongTermContracts();
                                    using (var cmd = new OracleCommand(updateLTCquery, connection))
                                    {
                                        cmd.CommandType = CommandType.Text;
                                        cmd.Parameters.Add(new OracleParameter("AgentId", bcl.BlackList.AgentId));
                                        cmd.Parameters.Add(new OracleParameter("ReasonCode", bcl.BlackList.ReasonCode));
                                        cmd.Parameters.Add(new OracleParameter("BlSuspended", bcl.BlackList.BlackListSuspended));
                                        cmd.Parameters.Add(new OracleParameter("IssuingDate", bcl.IssuingDate));
                                        cmd.Parameters.Add(new OracleParameter("SaleOperatorId", bcl.BlackList.SaleOperatorId));
                                        cmd.Parameters.Add(new OracleParameter("SaleDeviceId", bcl.SaleDeviceId));
                                        cmd.Parameters.Add(new OracleParameter("LastSerialNo", bcl.BlackList.LastSerialNo));
                                        cmd.Parameters.Add(new OracleParameter("FirstSerialNo", bcl.BlackList.FirstSerialNo));

                                        cmdString = OracleCommandWrapper.ToString(cmd, updateLTCquery);
                                        var row = await cmd.ExecuteNonQueryAsync();
                                        if (row != 1) throw new Exception("Update LONGTTERMCONTRACTS failed");
                                    }
                                }
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch (Exception ex)
                        {                 
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "BlacklistContract", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            /*OracleConnection connection = null;
            OracleTransaction transaction = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                transaction = connection.BeginTransaction();
                var query = Queries.InsertContractInBlackList();

                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("Issuingdate", bcl.Issuingdate));
                    cmd.Parameters.Add(new OracleParameter("Saleoperatorid", bcl.Blg.Saleoperatorid));
                    cmd.Parameters.Add(new OracleParameter("Saledeviceid", bcl.Saledeviceid));
                    cmd.Parameters.Add(new OracleParameter("Firstserialno", bcl.Blg.Firstserialno));
                    cmd.Parameters.Add(new OracleParameter("Lastserialno", bcl.Blg.Lastserialno));
                    cmd.Parameters.Add(new OracleParameter("Isshorttermcontract", bcl.Blg.Isshorttermcontract));
                    cmd.Parameters.Add(new OracleParameter("Reasoncode", bcl.Blg.Reasoncode));
                    cmd.Parameters.Add(new OracleParameter("Blsuspended", bcl.Blg.Blsuspended));
                    cmd.Parameters.Add(new OracleParameter("Blinsertdate", bcl.Blg.Blinsertdate));
                    cmd.Parameters.Add(new OracleParameter("Bladdedby", bcl.Blg.AgentID));
                    cmd.Parameters.Add(new OracleParameter("Indistribution", bcl.Blg.Indistribution));
                    // Insert Contract in CONTRACTBLDISTRLIST
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    var row = await cmd.ExecuteNonQueryAsync();
                    if (row == 0) throw new Exception("Insert contract failed");
                    else if (row >= 2) throw new Exception("Insert more than one element in contractbldistrlist - Error");
                }

                if (createHistory)
                {
                    // Insert contract in the ShortTermContractHistory  
                    if (bcl.Blg.Isshorttermcontract == 1)
                    {
                        var insertSTCHquery = Queries.InsertShortTermContractInHistory();
                        int rows;

                        using (var cmd = new OracleCommand(insertSTCHquery, connection))
                        {
                            cmd.CommandType = CommandType.Text;
                            cmd.Parameters.Add(new OracleParameter("Issuingdate", bcl.Issuingdate));
                            cmd.Parameters.Add(new OracleParameter("Saleoperatorid", bcl.Blg.Saleoperatorid));
                            cmd.Parameters.Add(new OracleParameter("Saledeviceid", bcl.Saledeviceid));
                            cmd.Parameters.Add(new OracleParameter("FirstSerialNo", bcl.Blg.Firstserialno));
                            cmd.Parameters.Add(new OracleParameter("LastSerialNo", bcl.Blg.Lastserialno));
                            // New Key, needs values
                            //cmd.Parameters.Add(new OracleParameter("TSCSaledoperatorid", ));
                            //cmd.Parameters.Add(new OracleParameter("TSCFirstSerialNo", ));
                            //cmd.Parameters.Add(new OracleParameter("TSCShortcardmodel", ));                       
                            cmdString = OracleCommandWrapper.ToString(cmd, insertSTCHquery);
                            rows = await cmd.ExecuteNonQueryAsync();
                        }

                        if (rows == 1) // Insert contract in the ShortTermContractsHistory performed
                        {
                            var updateSTCquery = Queries.UpdateShortTermContracts();
                            using (var cmd = new OracleCommand(updateSTCquery, connection))
                            {
                                cmd.CommandType = CommandType.Text;
                                cmd.Parameters.Add(new OracleParameter("Agentid", bcl.Blg.AgentID));
                                cmd.Parameters.Add(new OracleParameter("Reasoncode", bcl.Blg.Reasoncode));
                                cmd.Parameters.Add(new OracleParameter("Blsuspended", bcl.Blg.Blsuspended));
                                cmd.Parameters.Add(new OracleParameter("Issuingdate", bcl.Issuingdate));
                                cmd.Parameters.Add(new OracleParameter("Saleoperatorid", bcl.Blg.Saleoperatorid));
                                cmd.Parameters.Add(new OracleParameter("Saledeviceid", bcl.Saledeviceid));
                                cmd.Parameters.Add(new OracleParameter("Lastserialno", bcl.Blg.Lastserialno));
                                cmd.Parameters.Add(new OracleParameter("Firstserialno", bcl.Blg.Firstserialno));

                                cmdString = OracleCommandWrapper.ToString(cmd, updateSTCquery);
                                var row = await cmd.ExecuteNonQueryAsync();
                                if (row == 0) throw new Exception("Update ShortTermContracts failed");
                                else if (row == 1)
                                {
                                    transaction.Commit();
                                    return true;
                                }
                                else throw new Exception("Update more than one element in ShortTermContracts - Error");
                            }
                        }
                        else if (rows == 0) throw new Exception("Insert in ShortTermHistory failed");
                        else throw new Exception("Insert more than one element in ShortTermHistory - Error");
                    }
                    if (bcl.Blg.Isshorttermcontract == 0)
                    {
                        // Insert contract in the LongTermContractHistory
                        var insertLTCHquery = Queries.InsertLongTermContractInHistory();

                        using (var cmd = new OracleCommand(insertLTCHquery, connection))
                        {
                            cmd.CommandType = CommandType.Text;
                            cmd.Parameters.Add(new OracleParameter("Issuingdate", bcl.Issuingdate));
                            cmd.Parameters.Add(new OracleParameter("Saleoperatorid", bcl.Blg.Saleoperatorid));
                            cmd.Parameters.Add(new OracleParameter("Saledeviceid", bcl.Saledeviceid));
                            cmd.Parameters.Add(new OracleParameter("FirstSerialNo", bcl.Blg.Firstserialno));
                            cmd.Parameters.Add(new OracleParameter("LastSerialNo", bcl.Blg.Lastserialno));
                            // New Key, needs values
                            //cmd.Parameters.Add(new OracleParameter("TSCSaledoperatorid", ));
                            //cmd.Parameters.Add(new OracleParameter("TSCFirstSerialNo", ));
                            //cmd.Parameters.Add(new OracleParameter("TSCShortcardmodel", ));
                            cmdString = OracleCommandWrapper.ToString(cmd, insertLTCHquery);
                            var row = await cmd.ExecuteNonQueryAsync();

                            if (row == 1) // Insert contract in the LongTermContractsHistory performed
                            {
                                var updateLTCquery = Queries.UpdateLongTermContracts();
                                using (var cmd2 = new OracleCommand(updateLTCquery, connection))
                                {
                                    cmd2.CommandType = CommandType.Text;
                                    cmd2.Parameters.Add(new OracleParameter("Agentid", bcl.Blg.AgentID));
                                    cmd2.Parameters.Add(new OracleParameter("Reasoncode", bcl.Blg.Reasoncode));
                                    cmd2.Parameters.Add(new OracleParameter("Blsuspended", bcl.Blg.Blsuspended));
                                    cmd2.Parameters.Add(new OracleParameter("Issuingdate", bcl.Issuingdate));
                                    cmd2.Parameters.Add(new OracleParameter("Saleoperatorid", bcl.Blg.Saleoperatorid));
                                    cmd2.Parameters.Add(new OracleParameter("Saledeviceid", bcl.Saledeviceid));
                                    cmd2.Parameters.Add(new OracleParameter("Lastserialno", bcl.Blg.Lastserialno));
                                    cmd2.Parameters.Add(new OracleParameter("Firstserialno", bcl.Blg.Firstserialno));

                                    cmdString = OracleCommandWrapper.ToString(cmd, updateLTCquery);
                                    var updatedrow = await cmd2.ExecuteNonQueryAsync();
                                    if (updatedrow == 0) throw new Exception("Update LongtTermContracts failed");
                                    else if (updatedrow == 1)
                                    {
                                        transaction.Commit();
                                        return true;
                                    }
                                    else throw new Exception("Update more than one element in LongTermContracts - Error");
                                }
                            }
                            else if (row == 0) throw new Exception("Insert in LongTermHistory failed");
                            else throw new Exception("Insert more than one element in LongTermHistory - Error");

                        }
                    }
                }
                else
                {
                    transaction.Commit();
                    return true;
                }
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ExHelper.ThrowExceptionContainer(ex, "InsertCustomer", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
            return false;*/
        }

        // Insert media/card in BlackList
        // return true if card is inserted else false
        public static async Task<bool> BlackListMedia(string connectionString, BlackList bl, bool createHistory = true)
        {
            OracleConnection connection = null;
            OracleTransaction transaction = null;
            var cmdString = string.Empty;

            try
            {
                using (connection = await DBOracleHelper.OpenDBConnection(connectionString))
                {
                    if (connection == null)
                        throw new DBConnectionOpeningException(connectionString);
                    using (transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            if (createHistory)
                            {
                                var archiveResult = await ArchiveTSCDocument(connection, transaction, bl.ShortCardModel, bl.LastSerialNo, bl.ReasonCode);
                                // Update document
                                var updateQuery = Queries.UpdateTSCDocument();
                                using (var cmd = new OracleCommand(updateQuery, connection))
                                {
                                    cmd.CommandType = CommandType.Text;
                                    cmd.Parameters.Add(new OracleParameter("AgentId", bl.AgentId));
                                    cmd.Parameters.Add(new OracleParameter("ReasonCode", bl.ReasonCode));
                                    cmd.Parameters.Add(new OracleParameter("Suspended", bl.BlackListSuspended));
                                    cmd.Parameters.Add(new OracleParameter("ShortCardModel", bl.ShortCardModel));
                                    cmd.Parameters.Add(new OracleParameter("LastSerialNo", bl.LastSerialNo));
                                    cmdString = OracleCommandWrapper.ToString(cmd, updateQuery);
                                    var res = await cmd.ExecuteNonQueryAsync();
                                    if (res != 1) throw new Exception("Update TSCDOCUMENT failed");
                                }
                            }

                            // Check if document is in Black list
                            bool isDocumentInBlackList;
                            var results = new List<string>();
                            var checkQuery = Queries.SelectTSCDocument();
                            using (var cmd = new OracleCommand(checkQuery, connection))
                            {
                                cmd.CommandType = CommandType.Text;
                                cmd.Parameters.Add(new OracleParameter("ShortCardModel", bl.ShortCardModel));
                                cmd.Parameters.Add(new OracleParameter("LastSerialNo", bl.LastSerialNo));

                                cmdString = OracleCommandWrapper.ToString(cmd, checkQuery);
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    while (await reader.ReadAsync())
                                    {
                                        var col1 = reader.GetValue(0).ToString();
                                        var col2 = reader.GetValue(1).ToString();
                                        var col3 = reader.GetValue(2).ToString();
                                        results.Add(col1);
                                        results.Add(col2);
                                        results.Add(col3);
                                    }
                                }

                                // Considering 3 fields
                                if (results.Count == 3) isDocumentInBlackList = true;
                                else isDocumentInBlackList = false;
                            }

                            if (!isDocumentInBlackList)
                            {
                                var insertQuery = Queries.InsertTSCDocumentInBlackList();
                                using (var cmd = new OracleCommand(insertQuery, connection))
                                {
                                    cmd.CommandType = CommandType.Text;
                                    cmd.Parameters.Add(new OracleParameter("ShortCardModel", bl.ShortCardModel));
                                    cmd.Parameters.Add(new OracleParameter("SaleOperatorId", bl.SaleOperatorId));
                                    cmd.Parameters.Add(new OracleParameter("LastSerialNo", bl.LastSerialNo));
                                    cmd.Parameters.Add(new OracleParameter("FirstSerialNo", bl.FirstSerialNo));
                                    if (bl.ReasonCode.HasValue)
                                        cmd.Parameters.Add(new OracleParameter("ReasonCode", bl.ReasonCode));
                                    else
                                        cmd.Parameters.Add(new OracleParameter("ReasonCode", DBNull.Value));
                                    cmd.Parameters.Add(new OracleParameter("AgentID", bl.AgentId));
                                    cmd.Parameters.Add(new OracleParameter("Suspended", bl.BlackListSuspended));
                                    cmd.Parameters.Add(new OracleParameter("InsertDistr", bl.Indistribution));
                                    cmdString = OracleCommandWrapper.ToString(cmd, insertQuery);
                                    var res = await cmd.ExecuteNonQueryAsync();
                                    if (res != 1) throw new Exception("Insert document in TSCBLACKLIST failed");
                                }
                            }

                            transaction.Commit();
                            return true;
                           
                        }
                        catch(Exception ex)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "BlacklistMedia", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }


            /*OracleConnection connection = null;
            OracleTransaction transaction = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                transaction = connection.BeginTransaction();

                if (createHistory)
                {
                    var archiveResult = await ArchiveTSCDocument(connection, transaction, bl.ShortCardModel, bl.Lastserialno, bl.Reasoncode);
                    // Update document
                    var updateQuery = Queries.UpdateTSCDocument();
                    using (var cmd = new OracleCommand(updateQuery, connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("agentID", bl.AgentID));
                        cmd.Parameters.Add(new OracleParameter("reasonCode", bl.Reasoncode));
                        cmd.Parameters.Add(new OracleParameter("suspended", bl.Blsuspended));
                        cmd.Parameters.Add(new OracleParameter("shortCardModel", bl.ShortCardModel));
                        cmd.Parameters.Add(new OracleParameter("lastSerialNo", bl.Lastserialno));
                        cmdString = OracleCommandWrapper.ToString(cmd, updateQuery);
                        var res = await cmd.ExecuteNonQueryAsync();
                        if (res != 1) throw new Exception("Update document failed");
                    }
                }

                // Check if document is in Black list
                bool isDocumentInBlackList;
                var results = new List<string>();
                var checkQuery = Queries.SelectTSCDocument();
                using (var cmd = new OracleCommand(checkQuery, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("shortCardModel", bl.ShortCardModel));
                    cmd.Parameters.Add(new OracleParameter("lastSerialNo", bl.Lastserialno));

                    cmdString = OracleCommandWrapper.ToString(cmd, checkQuery);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var col1 = reader.GetValue(0).ToString();
                            var col2 = reader.GetValue(1).ToString();
                            var col3 = reader.GetValue(2).ToString();
                            results.Add(col1);
                            results.Add(col2);
                            results.Add(col3);
                        }
                    }

                    // Considering 3 fields
                    if (results.Count == 3) isDocumentInBlackList = true;
                    else isDocumentInBlackList = false;
                }

                if (!isDocumentInBlackList)
                {
                    var insertQuery = Queries.InsertTSCDocumentInBlackList();
                    using (var cmd = new OracleCommand(insertQuery, connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("shortCardModel", bl.ShortCardModel));
                        cmd.Parameters.Add(new OracleParameter("saleOperatorID", bl.Saleoperatorid));
                        cmd.Parameters.Add(new OracleParameter("lastSerialNo", bl.Lastserialno));
                        cmd.Parameters.Add(new OracleParameter("firstSerialNo", bl.Firstserialno));
                        if (bl.Reasoncode.HasValue)
                            cmd.Parameters.Add(new OracleParameter("reasonCode", bl.Reasoncode));
                        else
                            cmd.Parameters.Add(new OracleParameter("reasonCode", DBNull.Value));
                        cmd.Parameters.Add(new OracleParameter("agentID", bl.AgentID));
                        cmd.Parameters.Add(new OracleParameter("suspended", bl.Blsuspended));
                        cmd.Parameters.Add(new OracleParameter("insertDistr", bl.Indistribution));
                        cmdString = OracleCommandWrapper.ToString(cmd, insertQuery);
                        var res = await cmd.ExecuteNonQueryAsync();
                        if (res == 0) throw new Exception("Insert document failed");
                    }
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ExHelper.ThrowExceptionContainer(ex, "InsertCustomer", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }*/
        }

        public static async Task<bool> UpdateTSCProofDoc(string connectionString, string proofDocSn, int shortCardModel, string lastSerialNo)
        {
            OracleConnection connection = null;
            var cmdString = string.Empty;

            try
            {
                using (connection = await DBOracleHelper.OpenDBConnection(connectionString))
                {
                    if (connection == null)
                        throw new DBConnectionOpeningException(connectionString);
                    var updateQuery = Queries.UpdateTSCDocumentProofDoc();
                    using (var cmd = new OracleCommand(updateQuery, connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("ProofDocSn", proofDocSn));
                        cmd.Parameters.Add(new OracleParameter("ShortCardModel", shortCardModel));
                        cmd.Parameters.Add(new OracleParameter("LastSerialNo", lastSerialNo));
                        cmdString = OracleCommandWrapper.ToString(cmd, updateQuery);
                        var res = await cmd.ExecuteNonQueryAsync();
                        if (res != 1) throw new Exception("Update TSCDOCUMENT failed");
                    }
                    return true;
                }
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                ExHelper.ThrowExceptionContainer(ex, "BlacklistMedia", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }


            /*OracleConnection connection = null;
            OracleTransaction transaction = null;
            try
            {
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                transaction = connection.BeginTransaction();

                if (createHistory)
                {
                    var archiveResult = await ArchiveTSCDocument(connection, transaction, bl.ShortCardModel, bl.Lastserialno, bl.Reasoncode);
                    // Update document
                    var updateQuery = Queries.UpdateTSCDocument();
                    using (var cmd = new OracleCommand(updateQuery, connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("agentID", bl.AgentID));
                        cmd.Parameters.Add(new OracleParameter("reasonCode", bl.Reasoncode));
                        cmd.Parameters.Add(new OracleParameter("suspended", bl.Blsuspended));
                        cmd.Parameters.Add(new OracleParameter("shortCardModel", bl.ShortCardModel));
                        cmd.Parameters.Add(new OracleParameter("lastSerialNo", bl.Lastserialno));
                        cmdString = OracleCommandWrapper.ToString(cmd, updateQuery);
                        var res = await cmd.ExecuteNonQueryAsync();
                        if (res != 1) throw new Exception("Update document failed");
                    }
                }

                // Check if document is in Black list
                bool isDocumentInBlackList;
                var results = new List<string>();
                var checkQuery = Queries.SelectTSCDocument();
                using (var cmd = new OracleCommand(checkQuery, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("shortCardModel", bl.ShortCardModel));
                    cmd.Parameters.Add(new OracleParameter("lastSerialNo", bl.Lastserialno));

                    cmdString = OracleCommandWrapper.ToString(cmd, checkQuery);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var col1 = reader.GetValue(0).ToString();
                            var col2 = reader.GetValue(1).ToString();
                            var col3 = reader.GetValue(2).ToString();
                            results.Add(col1);
                            results.Add(col2);
                            results.Add(col3);
                        }
                    }

                    // Considering 3 fields
                    if (results.Count == 3) isDocumentInBlackList = true;
                    else isDocumentInBlackList = false;
                }

                if (!isDocumentInBlackList)
                {
                    var insertQuery = Queries.InsertTSCDocumentInBlackList();
                    using (var cmd = new OracleCommand(insertQuery, connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("shortCardModel", bl.ShortCardModel));
                        cmd.Parameters.Add(new OracleParameter("saleOperatorID", bl.Saleoperatorid));
                        cmd.Parameters.Add(new OracleParameter("lastSerialNo", bl.Lastserialno));
                        cmd.Parameters.Add(new OracleParameter("firstSerialNo", bl.Firstserialno));
                        if (bl.Reasoncode.HasValue)
                            cmd.Parameters.Add(new OracleParameter("reasonCode", bl.Reasoncode));
                        else
                            cmd.Parameters.Add(new OracleParameter("reasonCode", DBNull.Value));
                        cmd.Parameters.Add(new OracleParameter("agentID", bl.AgentID));
                        cmd.Parameters.Add(new OracleParameter("suspended", bl.Blsuspended));
                        cmd.Parameters.Add(new OracleParameter("insertDistr", bl.Indistribution));
                        cmdString = OracleCommandWrapper.ToString(cmd, insertQuery);
                        var res = await cmd.ExecuteNonQueryAsync();
                        if (res == 0) throw new Exception("Insert document failed");
                    }
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ExHelper.ThrowExceptionContainer(ex, "InsertCustomer", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }*/
        }

        // Helper Function Called in BlackListMedia()
        public static async Task<bool> ArchiveTSCDocument(OracleConnection connection, OracleTransaction transaction, int shortCardModel, string tscSerialNo, int? reasonCode)
        {
            var cmdString = string.Empty;
            try
            {
                bool isInsertedInHistory;
                var insertInHistoryQuery = Queries.InsertTSCDocumentInHistory();
                using (var cmd = new OracleCommand(insertInHistoryQuery, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new OracleParameter("ReasonCode", reasonCode));
                    cmd.Parameters.Add(new OracleParameter("ShortCardModel", shortCardModel));
                    cmd.Parameters.Add(new OracleParameter("TscSerialNo", tscSerialNo));

                    cmdString = OracleCommandWrapper.ToString(cmd, insertInHistoryQuery);
                    var res = await cmd.ExecuteNonQueryAsync();
                    if (res != 1) isInsertedInHistory = false;
                    else isInsertedInHistory = true;
                }

                if (isInsertedInHistory)
                {
                    var insertInHistoryDetailQuery = Queries.InsertTSCDocumentInHistoryDetail();
                    using (var cmd = new OracleCommand(insertInHistoryDetailQuery, connection))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.Add(new OracleParameter("ReasonCode", reasonCode));
                        cmd.Parameters.Add(new OracleParameter("ShortCardModel", shortCardModel));
                        cmd.Parameters.Add(new OracleParameter("TscSerialNo", tscSerialNo));
                        cmdString = OracleCommandWrapper.ToString(cmd, insertInHistoryDetailQuery);
                        var res = await cmd.ExecuteNonQueryAsync();

                        if (res != 1) throw new Exception();
                    }
                    return true;
                }
                else throw new Exception("Insert document in TSCDOCUMENTSHISTORY failed");
            }
            catch (DBConnectionOpeningException ex)
            {
                throw new ExceptionContainer(ex, "OpenDBConnection");
            }
            catch (ExceptionContainer)
            {
                throw;
            }
            catch (Exception ex)
            {
                //transaction.Rollback(); transaction will be rollbacked in the caller function
                ExHelper.ThrowExceptionContainer(ex, "ArchiveTSCDocuemnt", cmdString);
                throw;
            }
        }
        #endregion
    }
}
