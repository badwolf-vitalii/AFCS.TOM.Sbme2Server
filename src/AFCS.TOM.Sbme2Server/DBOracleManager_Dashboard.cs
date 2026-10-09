using AFCS.TOM.Sbme2Server.Dashboard;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.DsdeDashboard;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace AFCS.TOM.Sbme2Server
{
    public partial class DBOracleManager2
    {
        public static async Task<IList<DeviceListRecord>> GetDeviceList(string connectionString)
        {
            OracleConnection? connection = null;
            var cmdString = string.Empty;
            try
            {
                var deviceList = new List<DeviceListRecord>();
                connection = await DBOracleHelper.OpenDBConnection(connectionString);
                if (connection == null)
                    throw new DBConnectionOpeningException(connectionString);
                var query = Queries.GetDeviceList();
                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.CommandType = CommandType.Text;
                    cmdString = OracleCommandWrapper.ToString(cmd, query);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var device = DBOracleHelper.GetInstanceOfType<DeviceListRecord>(reader);
                            deviceList.Add(device);
                        }
                    }
                }
                return deviceList.ToList();
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
                ExHelper.ThrowExceptionContainer(ex, "GetDeviceList", cmdString);
                throw;
            }
            finally
            {
                if ((connection?.State ?? ConnectionState.Closed) != ConnectionState.Closed)
                    await DBOracleHelper.CloseDBConnection(connection);
            }
        }
    }
}
