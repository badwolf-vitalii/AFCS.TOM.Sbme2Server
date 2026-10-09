using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.SbmeModels;
using AFCS.TOM.SbmeModels.Enums;
using AFCS.TOM.SbmeModels.SBME2;
using Newtonsoft.Json;
using Oracle.ManagedDataAccess.Client;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Xml.Serialization;

namespace AFCS.TOM.Sbme2Server
{
    internal static class DBOracleHelper
    {
        private static IEnumerable<Type>? _sbme2Enums { get; set; }

        static DBOracleHelper()
        {
            var t = typeof(ConnectionString);
            var assembly = Assembly.GetAssembly(t);
            _sbme2Enums = assembly?.GetTypes()?.Where(t => t.IsEnum && t.IsPublic);
        }

        internal static async Task<OracleConnection> OpenDBConnection(string connectionString)
        {
            try
            {
                var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                return connection;
            }
            catch
            {
                return null;
            }
        }

        internal static async Task<bool> CloseDBConnection(OracleConnection connection)
        {
            try
            {
                if (connection != null && connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                    return true;
                }
                else throw new DBConnectionClosingException();
            }
            catch
            {
                return false;
            }
        }

        internal static OracleParameter GetOracleParameter(PredicateFilter pF)
        {
            if (pF.FieldType.ToString().ToUpper().Equals(typeof(DateTime).ToString().ToUpper()))
            {
                var s = "yyyy-MM-ddTHH:mm:ss";
                if (DateTime.TryParseExact(pF.FieldValue.ToString(), s, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime datet))
                {
                    var datetime = new OracleParameter(pF.FieldName.ToUpper(), OracleDbType.Date);
                    datetime.Value = datet;
                    return datetime;
                }
            }
            if (pF.FieldType.ToString().ToUpper().Equals(typeof(byte[]).ToString().ToUpper()))
            {
                var blob = new OracleParameter(pF.FieldName.ToUpper(), OracleDbType.Blob);
                blob.Direction = ParameterDirection.Input;
                blob.Value = Convert.FromBase64String(pF.FieldValue.ToString());
                return blob;
            }
            return new OracleParameter(pF.FieldName.ToUpper(), pF.FieldValue.ToString());
        }

        internal static OracleParameter GetOracleParameter(SearchFilter pF)
        {
            var fieldName = pF.FieldName?.ToUpper() ?? string.Empty;
            var fieldType = pF.FieldType?.ToUpper() ?? string.Empty;
            var fieldValue = pF.FieldValue ?? string.Empty;

            if (fieldType.Equals("NULL", StringComparison.InvariantCultureIgnoreCase))
            {
                return new OracleParameter(fieldName, DBNull.Value);
            }

            if (NormalizeClassName(fieldType)?.Equals(NormalizeClassName(typeof(DateTime).ToString())?.ToUpper()) ?? false)
            {
                var s = "yyyy-MM-ddTHH:mm:ss";
                if (DateTime.TryParseExact(pF.FieldValue.ToString(), s, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime datet))
                {
                    var datetime = new OracleParameter(fieldName, OracleDbType.Date);
                    datetime.Value = datet;
                    return datetime;
                }
            }
            
            if (NormalizeClassName(fieldType)?.Equals(NormalizeClassName(typeof(byte[]).ToString())?.ToUpper()) ?? false)
            {
                var blob = new OracleParameter(fieldName, OracleDbType.Blob);
                blob.Direction = ParameterDirection.Input;
                blob.Value = string.IsNullOrWhiteSpace(fieldValue) ? null : Convert.FromBase64String(fieldValue);
                return blob;
            }
            
            return new OracleParameter(fieldName, fieldValue);
        }

        internal static async Task<object> GetValueSafe(DbDataReader reader, string fieldName, object defaultValue = null)
        {
            try
            {
                var obj = await reader.GetFieldValueAsync<object>(fieldName);
                if (Convert.IsDBNull(obj)) return defaultValue;
                return obj;
            }
            catch
            {
                return defaultValue;
            }
        }

        private static DebuggingConfiguration _debugConfig { get; set; }
        private static DebuggingConfiguration DebugConfig
        {
            get
            {
                try
                {
                    var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();
                    _debugConfig = config.GetSection("DebuggingConfiguration")?.Get<DebuggingConfiguration>();
                }
                catch
                {
                }
                return _debugConfig ?? DebuggingConfiguration.Default;
            }
        }

        private static void _log(string message)
        {
            if (!DebugConfig.WLogGetInstanceOfType) return;
            LogHelper.Info(null, message);
        }

        private static void _log(Exception ex)
        {
            if (!DebugConfig.WLogGetInstanceOfType) return;
            LogHelper.Info(null, ex);
        }

        internal static T GetInstanceOfType<T>(DbDataReader reader) where T : new()
        {
            var debugCounters = new[] { 0, 0, 0, 0, 0, 0 };
            var instance = new T();
            var props = typeof(T)?.GetProperties();
            if (props != null)
                foreach (var prop in props)
                {
                    try
                    {
                        if (prop.HasAttribute<XmlIgnoreAttribute>()) continue;
                        if (prop.HasAttribute<JsonIgnoreAttribute>()) continue;
                        object? defaultValue = null;
                        _log("155: prop.PropertyType.Name.ToLower().Contains(\"nullable\")");
                        var containsNullable = prop.PropertyType.Name.ToLower().Contains("nullable");
                        if (containsNullable)
                        {
                            _log("159: prop.PropertyType.GenericTypeArguments[0].Name.ToLower()");
                            var propertyTypeName = prop.PropertyType.GenericTypeArguments[0].Name.ToLower();
                            defaultValue = (propertyTypeName) switch {
                                "decimal" => (Decimal?)null,
                                "char" => (Char?)null,
                                "schar" => (SByte?)null,
                                "byte" => (Byte?)null,
                                "int16" => (Int16?)null,
                                "uint16" => (UInt16?)null,
                                "int32" => (Int32?)null,
                                "uint32" => (UInt32?)null,
                                "int64" => (Int64?)null,
                                "uint64" => (UInt64?)null,
                                "single" => (Single?)null,
                                "double" => (Double?)null,
                                "string" => (String?)null,
                                _ => null
                            };
                        }
                        else
                        {
                            _log("180: Type.GetTypeCode(prop.PropertyType)");
                            var propertyTypeName = Type.GetTypeCode(prop.PropertyType);
                            defaultValue = (propertyTypeName) switch {
                                TypeCode.Char => 0,
                                TypeCode.SByte => 0,
                                TypeCode.Byte => 0,
                                TypeCode.Int16 => 0,
                                TypeCode.UInt16 => 0,
                                TypeCode.Int32 => 0,
                                TypeCode.UInt32 => 0,
                                TypeCode.Int64 => 0,
                                TypeCode.UInt64 => 0,
                                TypeCode.Single => 0,
                                TypeCode.Double => 0,
                                TypeCode.Decimal => 0,
                                TypeCode.Boolean => false,
                                TypeCode.String => string.Empty,
                                TypeCode.DateTime => new DateTime(),
                                TypeCode.Object => (prop.PropertyType.Name.ToLower()) switch {
                                    "char[]" => new Char[0],
                                    "schar[]" => new SByte[0],
                                    "byte[]" => new Byte[0],
                                    "int16[]" => new Int16[0],
                                    "uint16[]" => new UInt16[0],
                                    "int32[]" => new Int32[0],
                                    "uint32[]" => new UInt32[0],
                                    "int64[]" => new Int64[0],
                                    "uint64[]" => new UInt64[0],
                                    "single[]" => new Single[0],
                                    "double[]" => new Double[0],
                                    "string[]" => new String[0],
                                    _ => null
                                },
                                _ => null
                            };
                        }
                        if (defaultValue == null)
                            _log("217: defaultValue is NULL");
                        _log("218: GetValueSafe(reader, prop.Name, defaultValue).Result");
                        var value = GetValueSafe(reader, prop.Name, defaultValue).Result;
                        _log("220: if (value is string && (value?.ToString()?.Length ?? 0) == 1)");
                        if (value is string && (value?.ToString()?.Length ?? 0) == 1)
                        {
                            try
                            {
                                _log("225: prop.SetValue(instance, value.ToString()[0]);");
                                prop.SetValue(instance, value.ToString()[0]);
                                ++debugCounters[0];
                            }
                            catch
                            {
                                _log("231: prop.SetValue(instance, value.ToString());");
                                prop.SetValue(instance, value.ToString());
                                ++debugCounters[1];
                            }
                        }
                        else
                        {
                            _log("238: if (!(value?.GetType()?.Name?.Equals(prop.PropertyType.Name) ?? true))");
                            if (!(value?.GetType()?.Name?.Equals(prop.PropertyType.Name) ?? true))
                            {
                                _log("241: var nullable = prop.PropertyType.Name.ToLower().Contains(\"nullable\");");
                                var nullable = prop.PropertyType.Name.ToLower().Contains("nullable");
                                if (nullable)
                                    _log("244: prop.PropertyType.GenericTypeArguments[0].Name.ToLower()");
                                else
                                    _log("246: prop.PropertyType.Name.ToLower();");
                                var propTypeName = nullable
                                    ? prop.PropertyType.GenericTypeArguments[0].Name.ToLower()
                                    : prop.PropertyType.Name.ToLower();
                                _log($"250: propTypeName is {propTypeName}");
                                _log("251: value.GetType().Name.ToLower();");
                                var valueTypeName = value.GetType().Name.ToLower();
                                _log($"253: valueTypeName = {valueTypeName}");
                                switch (valueTypeName)
                                {
                                    case "char":
                                        value = ConvertType<char>(propTypeName, nullable, value);
                                        break;
                                    case "string":
                                        var tmp = (string)ConvertType<string>(propTypeName, nullable, value);
                                        if ((tmp?.Length ?? 0) == 1)
                                            value = tmp[0];
                                        else
                                            value = tmp;
                                        break;
                                    case "byte":
                                        value = ConvertType<byte>(propTypeName, nullable, value);
                                        break;
                                    case "short":
                                    case "int16":
                                        value = ConvertType<short>(propTypeName, nullable, value);
                                        break;
                                    case "int":
                                    case "int32":
                                        value = ConvertType<int>(propTypeName, nullable, value);
                                        break;
                                    case "long":
                                    case "int64":
                                        value = ConvertType<long>(propTypeName, nullable, value);
                                        break;
                                    case "ushort":
                                    case "uint16":
                                        value = ConvertType<ushort>(propTypeName, nullable, value);
                                        break;
                                    case "uint":
                                    case "uint32":
                                        value = ConvertType<uint>(propTypeName, nullable, value);
                                        break;
                                    case "ulong":
                                    case "uint64":
                                        value = ConvertType<ulong>(propTypeName, nullable, value);
                                        break;
                                    case "float":
                                        value = ConvertType<float>(propTypeName, nullable, value);
                                        break;
                                    case "double":
                                        value = ConvertType<double>(propTypeName, nullable, value);
                                        break;
                                    case "decimal":
                                        value = ConvertType<decimal>(propTypeName, nullable, value);
                                        break;
                                }
                                if (value != null)
                                    try
                                    {
                                        _log("306: var @enum = _sbme2Enums?.FirstOrDefault(p => p.Name.ToLower().Equals(propTypeName));");
                                        var @enum = _sbme2Enums?.FirstOrDefault(p => p.Name.ToLower().Equals(propTypeName));
                                        if (@enum != null) value = Enum.ToObject(@enum, value);
                                        else _log("@309: enum is NULL");
                                    }
                                    catch (Exception ex)
                                    {
                                        _log(ex);
                                    }
                                else
                                    _log("316: value is NULL");
                            }

                            _log("319: prop.SetValue(instance, value);");
                            prop.SetValue(instance, value);
                            ++debugCounters[4];

                            //try
                            //{
                            //    if (value.ToString().Length == 1)
                            //    {
                            //        prop.SetValue(instance, value.ToString()[0]);
                            //        ++debugCounters[2];
                            //    }
                            //    else
                            //    {
                            //        prop.SetValue(instance, value.ToString());
                            //        ++debugCounters[3];
                            //    }
                            //}
                            //catch
                            //{
                            //}
                        }
                    }
                    catch (Exception ex)
                    {
                        //catch
                        {
                            string msg = prop.Name + " : " + ex.Message;
                            ++debugCounters[5];
                            _log("347: " + msg);
                            Console.WriteLine(msg);
                        }
                    }
                }
            if (debugCounters[5] > 0)
            {
                var msg = $"Errors: {debugCounters[5]}";
                _log("355: " + msg);
                Console.WriteLine(msg);
            }
            return instance;
        }

        private static object ConvertType<T>(string prop, bool nullable, dynamic val) =>
            nullable
            ? prop switch {
                "char" => (char?)val,
                "string" => (string)val,
                "byte" => (byte?)val,
                "int16" => (short?)val,
                "short" => (short?)val,
                "int" => (int?)val,
                "int32" => (int?)val,
                "long" => (long?)val,
                "int64" => (long?)val,
                "ushort" => (ushort?)val,
                "uint16" => (ushort?)val,
                "uint" => (uint?)val,
                "uint32" => (uint?)val,
                "ulong" => (ulong?)val,
                "uint64" => (ulong?)val,
                "decimal" => (decimal?)val,
                _ => val
            }
            : prop switch {
                "char" => (char)val,
                "string" => (string)val,
                "byte" => (byte)val,
                "int16" => (short)val,
                "short" => (short)val,
                "int" => (int)val,
                "int32" => (int)val,
                "long" => (long)val,
                "int64" => (long)val,
                "ushort" => (ushort)val,
                "uint16" => (ushort)val,
                "uint" => (uint)val,
                "uint32" => (uint)val,
                "ulong" => (ulong)val,
                "uint64" => (ulong)val,
                "decimal" => (decimal)val,
                _ => val
            };

        internal static string? NormalizeClassName(string? fullName) =>
            string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : (Path.HasExtension(fullName)
                ? Path.GetExtension(fullName)
                : Path.GetFileNameWithoutExtension(fullName))
              .Replace("0", ".")
              .Replace("1", ".")
              .Replace("2", ".")
              .Replace("3", ".")
              .Replace("4", ".")
              .Replace("5", ".")
              .Replace("6", ".")
              .Replace("7", ".")
              .Replace("8", ".")
              .Replace("9", ".")
              .Replace(".", string.Empty);
    }
}
