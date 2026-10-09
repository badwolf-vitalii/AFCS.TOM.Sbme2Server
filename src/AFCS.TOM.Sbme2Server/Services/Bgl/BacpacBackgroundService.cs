using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.Models;
using AFCS.TOM.SbmeDataLayer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.SqlServer.Dac;
using Newtonsoft.Json;
using NLog;
using System.Data;
using System.Diagnostics;
using System.Reflection;

namespace AFCS.TOM.Sbme2Server.Services.Bgl
{
    public class BacpacBackgroundService : BackgroundService
    {
        private static Logger Logger => LogManager.GetLogger("BacpacLogger");
        public static bool IsReady { get; set; }

        private IConfiguration _config { get; }

        private bool _isBacpacGenerationActive;
        private DacServices? _dacServices { get; set; }
        private BglDataLayerConfiguration _bglDataLayerConfiguration { get; }
        private LastBacpacInfo? _lastBacpacInfo { get; set; }

        public BacpacBackgroundService(IConfiguration config)
        {
            _config = config;

            var bglDataLayerConfiguration = new BglDataLayerConfiguration();
            config.GetSection("BglDataLayerConfiguration").Bind(bglDataLayerConfiguration);
            _bglDataLayerConfiguration = bglDataLayerConfiguration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var lastInfFileName = "last.inf";
            var oAssembly = Assembly.GetExecutingAssembly();
            var assemblyLocationDir = Path.GetDirectoryName(oAssembly.Location);
            var lastInfFilePath = Path.Combine(assemblyLocationDir!, lastInfFileName);

            var createDbCalled = false;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (File.Exists(lastInfFilePath))
                    {
                        try
                        {
                            var content = await File.ReadAllTextAsync(lastInfFilePath);
                            _lastBacpacInfo = JsonConvert.DeserializeObject<LastBacpacInfo>(content) ?? _lastBacpacInfo ?? new LastBacpacInfo();
                        }
                        catch (Exception ex)
                        {
                            _lastBacpacInfo = _lastBacpacInfo ?? new LastBacpacInfo();
                            Logger?.Error(ex);
                        }
                    }

                    if (BglDataLayerConfiguration.Instance.AllowArchive && !createDbCalled)
                    {
                        try
                        {
                            var sw = Stopwatch.StartNew();
                            DatabaseCloner.CreateDatabaseWithSameStructure(
                                BglDataLayerConfiguration.Instance.Server,
                                BglDataLayerConfiguration.Instance.Database,
                                BglDataLayerConfiguration.Instance.Database + "_Archive",
                                false,
                                BglDataLayerConfiguration.Instance.User,
                                BglDataLayerConfiguration.Instance.Password);
                            sw.Stop();
                            Logger?.Debug($"CreateDatabaseWithSameStructure() took {sw.ElapsedMilliseconds}ms");
                        }
                        catch (Exception ex)
                        {
                            Logger?.Error($"CreateDatabaseWithSameStructure() failed: {ex}");
                        }
                        finally
                        {
                            createDbCalled = true;
                        }
                    }

                    var ok = false;

                    try
                    {
                        ok = GenerateBacpac();
                    }
                    catch (Exception ex)
                    {
                        Logger?.Error("Bacpac generation failed");
                        Logger?.Error(ex);
                    }

                    if (BglDataLayerConfiguration.Instance.AllowArchive && ok && _lastBacpacInfo != null && !_lastBacpacInfo.CleanedUp)
                    {
                        try
                        {
                            var sw = Stopwatch.StartNew();
                            await CleanUpDb();
                            sw.Stop();
                            Logger?.Debug($"CleanUpDb() took {sw.ElapsedMilliseconds}ms");
                        }
                        catch (Exception ex)
                        {
                            Logger?.Error("DB cleanup failed");
                            Logger?.Error(ex);
                        }
                    }

                    if (ok && (_lastBacpacInfo?.Updated ?? false))
                    {
                        _lastBacpacInfo.Updated = false;
                        var serialized = JsonConvert.SerializeObject(_lastBacpacInfo, Formatting.Indented);
                        await File.WriteAllTextAsync(lastInfFilePath, serialized);
                        IsReady = true;
                        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                        //await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                    else
                    {
                        IsReady = true;
                        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                        //await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                }
                catch (TaskCanceledException ex)
                {
                    Logger?.Error(ex);
                    break;
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex);
                }
                finally
                {
                    IsReady = true;
                }
            }
        }

        private bool GenerateBacpac(bool force = false)
        {
            var autoExit = false;
            try
            {
                if (_isBacpacGenerationActive)
                {
                    autoExit = true;
                    return false;
                }
                _isBacpacGenerationActive = true;

                if (_lastBacpacInfo == null)
                {
                    _lastBacpacInfo = new LastBacpacInfo();
                }

                var today = DateTime.Today;
                var months = BglDataLayerConfiguration.Instance?.BacpacLaunchMonths ?? 0;
                var dt = DateTime.MaxValue;
                if (!force)
                {
                    var requested = _lastBacpacInfo.DateTimeRequested ?? DateTime.MinValue;
                    var auto = _lastBacpacInfo.DateTimeAuto ?? DateTime.MinValue;
                    dt = requested > auto ? requested : auto;
                    if (dt.Year == today.Year && dt.Month == today.Month && dt.Day == today.Day)
                    {
                        return true;
                    }
                }

                _lastBacpacInfo.Updated = false;

                if (_dacServices == null)
                {
                    var connectionString = _bglDataLayerConfiguration.TrustedConnection
                        ? $"Server={_bglDataLayerConfiguration.Server};Trusted_Connection=true;TrustServerCertificate=true;Encrypt=false;"
                        : $"Server={_bglDataLayerConfiguration.Server};Trusted_Connection=false;user={_bglDataLayerConfiguration.User};password=#PASSWORD#;TrustServerCertificate=true;Encrypt=false;";
                    LogHelper.Info(Logger, $"Bacpac with connection string: {connectionString}");
                    connectionString = connectionString.Replace("#PASSWORD#", _bglDataLayerConfiguration.Password);

                    _dacServices = new DacServices(connectionString);
                }

                var outputFileName = _bglDataLayerConfiguration.Database + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss.bacpac");
                var monthlyOutputFileName = "Monthly_" + _bglDataLayerConfiguration.Database + "_" + DateTime.Now.ToString("yyyy-MM-dd.bacpac");
                var oAssembly = Assembly.GetExecutingAssembly();
                var assemblyLocationDir = Path.GetDirectoryName(oAssembly.Location);
                var dir = Path.Combine(assemblyLocationDir!, "Bacpacs");
                outputFileName = Path.Combine(dir, outputFileName);
                monthlyOutputFileName = Path.Combine(dir, monthlyOutputFileName);
                string[]? oldFiles = null;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                else
                {
                    oldFiles = Directory.GetFiles(dir);
                }

                try
                {
                    Logger?.Info($"Exporting bacpac to '{outputFileName}'");
                    _dacServices.ExportBacpac(outputFileName, _bglDataLayerConfiguration.Database);
                }
                catch
                {
                    _dacServices = null;
                    throw;
                }

                if (months > 0 && dt.AddMonths(months) <= today)
                {
                    var oldMonthly = string.Empty;
                    if (oldFiles?.Any() ?? false)
                    {
                        foreach (var file in oldFiles)
                        {
                            try
                            {
                                if (!file.StartsWith("Monthly_", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    File.Delete(file);
                                }
                                else
                                {
                                    oldMonthly = file;
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.Error(Logger, ex);
                            }
                        }
                    }

                    if (!_lastBacpacInfo.DateTimeMonthly.HasValue || _lastBacpacInfo.DateTimeMonthly < new DateTime(today.Year, today.Month, _lastBacpacInfo.DateTimeMonthly.Value.Day))
                    {
                        if (!string.IsNullOrWhiteSpace(oldMonthly))
                        {
                            try
                            {
                                File.Delete(oldMonthly);
                            }
                            catch (Exception ex)
                            {
                                LogHelper.Error(Logger, ex);
                            }
                        }
                        Logger?.Info($"Saving the monthly bacpac to '{monthlyOutputFileName}'");
                        File.Copy(outputFileName, monthlyOutputFileName, true);
                        _lastBacpacInfo.DateTimeMonthly = new DateTime(today.Year, today.Month, today.Day);
                        _lastBacpacInfo.CleanedUp = false;
                    }
                }

                if (force)
                {
                    _lastBacpacInfo.DateTimeRequested = DateTime.Now;
                }
                else
                {
                    _lastBacpacInfo.DateTimeAuto = DateTime.Now;
                }
                _lastBacpacInfo.Updated = true;

                return true;
            }
            catch
            {
                throw;
            }
            finally
            {
                if (!autoExit)
                {
                    _isBacpacGenerationActive = false;
                }
            }
        }

        private async Task CleanUpDb()
        {
            if (_lastBacpacInfo == null)
            {
                _lastBacpacInfo = new LastBacpacInfo();
            }

            if (_lastBacpacInfo.CleanedUp)
            {
                return;
            }

            try
            {
                var connectionString = _bglDataLayerConfiguration.TrustedConnection
                        ? $"Server={_bglDataLayerConfiguration.Server};Database={_bglDataLayerConfiguration.Database};Trusted_Connection=true;TrustServerCertificate=true;Encrypt=false;"
                        : $"Server={_bglDataLayerConfiguration.Server};Database={_bglDataLayerConfiguration.Database};Trusted_Connection=false;user={_bglDataLayerConfiguration.User};password=#PASSWORD#;TrustServerCertificate=true;Encrypt=false;";
                LogHelper.Info(Logger, $"Cleanup with connection string: {connectionString}");
                connectionString = connectionString.Replace("#PASSWORD#", _bglDataLayerConfiguration.Password);

                var archiveConnectionString = _bglDataLayerConfiguration.TrustedConnection
                        ? $"Server={_bglDataLayerConfiguration.Server};Database={_bglDataLayerConfiguration.Database}_Archive;Trusted_Connection=true;TrustServerCertificate=true;Encrypt=false;"
                        : $"Server={_bglDataLayerConfiguration.Server};Database={_bglDataLayerConfiguration.Database}_Archive;Trusted_Connection=false;user={_bglDataLayerConfiguration.User};password=#PASSWORD#;TrustServerCertificate=true;Encrypt=false;";
                LogHelper.Info(Logger, $"Archive with connection string: {archiveConnectionString}");
                archiveConnectionString = archiveConnectionString.Replace("#PASSWORD#", _bglDataLayerConfiguration.Password);

                await using var strategyContext = new DataLayerContext(connectionString);
                var strategy = strategyContext.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    using var dc = new DataLayerContext(connectionString);
                    using var archiveDc = new DataLayerContext(archiveConnectionString);

                    await using var cpyTransaction = await archiveDc.Database.BeginTransactionAsync();
                    await using var delTransaction = await dc.Database.BeginTransactionAsync();

                    var cpyTranClosed = false;
                    var delTranClosed = false;

                    try
                    {
                        var cpyDone = false;
                        var delDone = false;
                        var cpyRows = 0;
                        var delRows = 0;

                        var today = DateTime.Today;
                        var date = today.AddMonths(-(BglDataLayerConfiguration.Instance?.BacpacDataMonths ?? 3));

                        #region Preparing IDs
                        var ap = await dc.AccountingPeriods.Where(p => p.StartDate < date).Select(p => p.Id).ToListAsync();
                        var ash = await dc.AgentShifts.Where(p => ap.Contains(p.AccountingPeriodId)).Select(p => p.Id).ToListAsync();
                        var dsh = await dc.DeviceShifts.Where(p => ash.Contains(p.AgentShiftId)).Select(p => p.Id).ToListAsync();
                        var st = await dc.SaleTransactions.Where(p => dsh.Contains(p.DeviceShiftId)).Select(p => p.Id).ToListAsync();
                        var pd = await dc.PaymentDetails.Where(p => p.SaleTransactionId.HasValue && st.Contains(p.SaleTransactionId.Value)).Select(p => p.Id).ToListAsync();
                        var pos = await dc.PosDetails.Where(p => st.Contains(p.SaleTransactionId)).Select(p => p.Id).ToListAsync();
                        var ar = await dc.Articles.Where(p => p.SaleTransactionId.HasValue && st.Contains(p.SaleTransactionId.Value)).Select(p => p.Id).ToListAsync();
                        var aiCsc = await dc.ContactlessCardArticleInfos.Where(p => ar.Contains(p.ArticleId)).Select(p => p.Id).ToListAsync();
                        var aiCtr = await dc.CscContractArticleInfos.Where(p => ar.Contains(p.ArticleId)).Select(p => p.Id).ToListAsync();
                        var aiTic = await dc.MagneticArticleInfos.Where(p => ar.Contains(p.ArticleId)).Select(p => p.Id).ToListAsync();
                        var aiProf = await dc.ProfileRenewalArticleInfos.Where(p => ar.Contains(p.ArticleId)).Select(p => p.Id).ToListAsync();
                        var aiExp = await dc.ContactlessCardExpirationExtensionArticleInfos.Where(p => ar.Contains(p.ArticleId)).Select(p => p.Id).ToListAsync();
                        var aiPt = await dc.PtItemArticleInfos.Where(p => ar.Contains(p.ArticleId)).Select(p => p.ArticleId).ToListAsync();
                        var aiCtr2 = await dc.CscContractRefundArticleInfos.Where(p => st.Contains(p.SaleTransactionId)).Select(p => p.ArticleId).ToListAsync();
                        var aiTic2 = await dc.MagneticRefundArticleInfos.Where(p => st.Contains(p.SaleTransactionId)).Select(p => p.ArticleId).ToListAsync();
                        var aiPt2 = await dc.PtItemRefundArticleInfos.Where(p => st.Contains(p.SaleTransactionId)).Select(p => p.ArticleId).ToListAsync();
                        var bl = await dc.CanceledCscContracts.Where(p => dsh.Contains(p.DeviceShiftId)).Select(p => p.Id).ToListAsync();
                        var appsh = await dc.ApplicationShutdowns.Where(p => p.DeviceShiftId.HasValue && dsh.Contains(p.DeviceShiftId.Value)).Select(p => p.Id).ToListAsync();
                        var anom = await dc.CardAnomalies.Where(p => dsh.Contains(p.DeviceShiftId)).Select(p => p.Id).ToListAsync();
                        var cashd = await dc.CashDrawers.Where(p => ash.Contains(p.AgentShiftId)).Select(p => p.Id).ToListAsync();
                        var cmd = await dc.Commands.Where(p => p.RegistrationTime < date).Select(p => p.Id).ToListAsync();
                        var attach = await dc.CommandAttachments.Where(p => cmd.Contains(p.CommandId)).Select(p => p.Id).ToListAsync();
                        var vtssct = await dc.VtSellContractInfos.Where(p => p.InsertDate < date).Select(p => p.Id).ToListAsync();
                        var vtsscm = await dc.VtSellCommitInfos.Where(p => p.InsertDate < date).Select(p => p.Id).ToListAsync();
                        var vtsscmCtrUid = await dc.VtSellCommitInfos.Where(p => p.InsertDate < date).Select(p => p.ContractUid).ToListAsync();
                        var vtsscmpti = await dc.VtSellCommitPaymentTypeInfos.Where(p => vtsscmCtrUid.Contains(p.ContractUid)).Select(p => p.Id).ToListAsync();
                        var vtsst = await dc.VtSellTransactionPaymentTypeInfos.Where(p => p.InsertDate < date).Select(p => p.Id).ToListAsync();
                        var ptct = await dc.PtConfirmTransactions.Where(p => p.RequestSendTime < date).Select(p => p.Id).ToListAsync();
                        var ptctpm = await dc.PtConfirmTransactionPayments.Where(p => ptct.Contains(p.PtConfirmTransactionId)).Select(p => p.Id).ToListAsync();
                        var ptctpr = await dc.PtConfirmTransactionProducts.Where(p => ptct.Contains(p.PtConfirmTransactionId)).Select(p => p.Id).ToListAsync();
                        var ptbti = await dc.PtBankTransferInfos.Where(p => st.Contains(p.SaleTransactionId)).Select(p => p.Id).ToListAsync();
                        #endregion

                        #region Check
                        if (!ap.Any() &&
                            !ash.Any() &&
                            !dsh.Any() &&
                            !st.Any() &&
                            !ar.Any() &&
                            !pd.Any() &&
                            !pos.Any() &&
                            !aiCsc.Any() &&
                            !aiCtr.Any() &&
                            !aiTic.Any() &&
                            !aiProf.Any() &&
                            !aiExp.Any() &&
                            !aiPt.Any() &&
                            !aiCtr2.Any() &&
                            !aiTic2.Any() &&
                            !aiPt2.Any() &&
                            !bl.Any() &&
                            !appsh.Any() &&
                            !anom.Any() &&
                            !cashd.Any() &&
                            !cmd.Any() &&
                            !attach.Any() &&
                            !vtssct.Any() &&
                            !vtsscm.Any() &&
                            !vtsscmCtrUid.Any() &&
                            !vtsscmpti.Any() &&
                            !vtsst.Any() &&
                            !ptct.Any() &&
                            !ptctpm.Any() &&
                            !ptctpr.Any() &&
                            !ptbti.Any())
                        {
                            Logger?.Info("No records to archive.");
                            await cpyTransaction.RollbackAsync();
                            await delTransaction.RollbackAsync();
                            return;
                        }
                        #endregion

                        #region Copy
                        await CopyFast(
                            dc,
                            archiveDc,
                            ap,
                            ash,
                            dsh,
                            st,
                            ar,
                            ptbti,
                            ptct,
                            ptctpm,
                            ptctpr,
                            pos,
                            pd,
                            bl,
                            aiPt2,
                            aiTic2,
                            aiCtr2,
                            aiPt,
                            aiExp,
                            aiProf,
                            aiTic,
                            aiCtr,
                            aiCsc,
                            vtsst,
                            vtsscmpti,
                            vtsscm,
                            vtssct,
                            attach,
                            cmd,
                            cashd,
                            anom,
                            appsh);

                        try
                        {
                            cpyRows = await archiveDc.SaveChangesAsync();
                            cpyDone = true;
                        }
                        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
                        {
                            archiveDc.ChangeTracker.Clear();

                            await CopySafe(
                                dc,
                                archiveDc,
                                ap,
                                ash,
                                dsh,
                                st,
                                ar,
                                ptbti,
                                ptct,
                                ptctpm,
                                ptctpr,
                                pos,
                                pd,
                                bl,
                                aiPt2,
                                aiTic2,
                                aiCtr2,
                                aiPt,
                                aiExp,
                                aiProf,
                                aiTic,
                                aiCtr,
                                aiCsc,
                                vtsst,
                                vtsscmpti,
                                vtsscm,
                                vtssct,
                                attach,
                                cmd,
                                cashd,
                                anom,
                                appsh);

                            try
                            {
                                cpyRows = await archiveDc.SaveChangesAsync();
                                cpyDone = true;
                            }
                            catch (Exception ex0)
                            {
                                // abort copying
                                Logger?.Info($"Copying failed: {ex0}");
                                throw;
                            }
                        }
                        catch (Exception ex)
                        {
                            // abort copying
                            Logger?.Info($"Copying failed: {ex}");
                            throw;
                        }
                        #endregion

                        #region Delete
                        if (appsh.Any())
                        {
                            var records = dc.ApplicationShutdowns.Where(p => appsh.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (anom.Any())
                        {
                            var records = dc.CardAnomalies.Where(p => anom.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (cashd.Any())
                        {
                            var records = dc.CashDrawers.Where(p => cashd.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (cmd.Any())
                        {
                            var records = dc.Commands.Where(p => cmd.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (attach.Any())
                        {
                            var records = dc.CommandAttachments.Where(p => attach.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (vtssct.Any())
                        {
                            var records = dc.VtSellContractInfos.Where(p => vtssct.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (vtsscm.Any())
                        {
                            var records = dc.VtSellCommitInfos.Where(p => vtsscm.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (vtsscmpti.Any())
                        {
                            var records = dc.VtSellCommitPaymentTypeInfos.Where(p => vtsscmpti.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (vtsst.Any())
                        {
                            var records = dc.VtSellTransactionPaymentTypeInfos.Where(p => vtsst.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (aiCsc.Any())
                        {
                            var records = dc.ContactlessCardArticleInfos.Where(p => aiCsc.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (aiCtr.Any())
                        {
                            var records = dc.CscContractArticleInfos.Where(p => aiCtr.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (aiTic.Any())
                        {
                            var records = dc.MagneticArticleInfos.Where(p => aiTic.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (aiProf.Any())
                        {
                            var records = dc.ProfileRenewalArticleInfos.Where(p => aiProf.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (aiExp.Any())
                        {
                            var records = dc.ContactlessCardExpirationExtensionArticleInfos.Where(p => aiExp.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (aiPt.Any())
                        {
                            var records = dc.PtItemArticleInfos.Where(p => aiPt.Contains(p.ArticleId));
                            dc.RemoveRange(records);
                        }
                        if (aiCtr2.Any())
                        {
                            var records = dc.CscContractRefundArticleInfos.Where(p => aiCtr2.Contains(p.ArticleId));
                            dc.RemoveRange(records);
                        }
                        if (aiTic2.Any())
                        {
                            var records = dc.MagneticRefundArticleInfos.Where(p => aiTic2.Contains(p.ArticleId));
                            dc.RemoveRange(records);
                        }
                        if (aiPt2.Any())
                        {
                            var records = dc.PtItemRefundArticleInfos.Where(p => aiPt2.Contains(p.ArticleId));
                            dc.RemoveRange(records);
                        }
                        if (bl.Any())
                        {
                            var records = dc.CanceledCscContracts.Where(p => bl.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (pd.Any())
                        {
                            var records = dc.PaymentDetails.Where(p => pd.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (pos.Any())
                        {
                            var records = dc.PosDetails.Where(p => pos.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (ptctpr.Any())
                        {
                            var records = dc.PtConfirmTransactionProducts.Where(p => ptctpr.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (ptctpm.Any())
                        {
                            var records = dc.PtConfirmTransactionPayments.Where(p => ptctpm.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (ptct.Any())
                        {
                            var records = dc.PtConfirmTransactions.Where(p => ptct.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (ptbti.Any())
                        {
                            var records = dc.PtBankTransferInfos.Where(p => ptbti.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (ar.Any())
                        {
                            var records = dc.Articles.Where(p => ar.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (st.Any())
                        {
                            var records = dc.SaleTransactions.Where(p => st.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (dsh.Any())
                        {
                            var records = dc.DeviceShifts.Where(p => dsh.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (ash.Any())
                        {
                            var records = dc.AgentShifts.Where(p => ash.Contains(p.Id));
                            dc.RemoveRange(records);
                        }
                        if (ap.Any())
                        {
                            var records = dc.AccountingPeriods.Where(p => ap.Contains(p.Id));
                            dc.RemoveRange(records);
                        }

                        try
                        {
                            delRows = await dc.SaveChangesAsync();
                            delDone = true;
                        }
                        catch (Exception ex)
                        {
                            // abort deletion
                            Logger?.Info($"Deletion failed: {ex}");
                            throw;
                        }
                        #endregion

                        #region Close Transactions
                        if (cpyDone && delDone)
                        {
                            try
                            {
                                await delTransaction.CommitAsync();
                                delTranClosed = true;
                            }
                            catch
                            {
                                try
                                {
                                    await delTransaction.RollbackAsync();
                                    delTranClosed = true;
                                }
                                catch
                                {
                                    // transaction may already be committed or disposed
                                }

                                throw;
                            }

                            try
                            {
                                await cpyTransaction.CommitAsync();
                                cpyTranClosed = true;
                            }
                            catch
                            {
                                try
                                {
                                    await cpyTransaction.RollbackAsync();
                                    cpyTranClosed = true;
                                }
                                catch
                                {
                                    // transaction may already be committed or disposed
                                }

                                throw;
                            }

                            if (cpyRows == 1)
                            {
                                Logger?.Info($"1 row has been archived");
                            }
                            else
                            {
                                Logger?.Info($"{cpyRows} rows have been archived");
                            }

                            if (delRows == 1)
                            {
                                Logger?.Info($"1 row has been deleted");
                            }
                            else
                            {
                                Logger?.Info($"{delRows} rows have been deleted");
                            }
                        }
                        else
                        {
                            if (!cpyTranClosed) try { await cpyTransaction.RollbackAsync(); } catch { }
                            if (!delTranClosed) try { await delTransaction.RollbackAsync(); } catch { }
                        }
                        #endregion
                    }
                    catch
                    {
                        if (!cpyTranClosed) try { await cpyTransaction.RollbackAsync(); } catch { }
                        if (!delTranClosed) try { await delTransaction.RollbackAsync(); } catch { }
                        throw;
                    }
                });

                _lastBacpacInfo.CleanedUp = true;
                var serialized = JsonConvert.SerializeObject(_lastBacpacInfo, Formatting.Indented);

                var lastInfFileName = "last.inf";
                var oAssembly = Assembly.GetExecutingAssembly();
                var assemblyLocationDir = Path.GetDirectoryName(oAssembly.Location);
                var lastInfFilePath = Path.Combine(assemblyLocationDir!, lastInfFileName);
                await File.WriteAllTextAsync(lastInfFilePath, serialized);
            }
            catch (Exception ex)
            {
                Logger?.Error(ex);
                if (ex.InnerException != null)
                {
                    Logger?.Error(ex.InnerException);
                }
            }
        }

        private static bool IsDuplicateKeyException(DbUpdateException ex) => ex.InnerException is SqlException sqlEx && sqlEx.Number is 2601 or 2627;

        private static async Task CopyFast(
            DataLayerContext dc,
            DataLayerContext archiveDc,
            List<Guid> ap,
            List<Guid> ash,
            List<Guid> dsh,
            List<Guid> st,
            List<Guid> ar,
            List<Guid> ptbti,
            List<Guid> ptct,
            List<Guid> ptctpm,
            List<Guid> ptctpr,
            List<Guid> pos,
            List<Guid> pd,
            List<string> bl,
            List<Guid> aiPt2,
            List<Guid> aiTic2,
            List<Guid> aiCtr2,
            List<Guid> aiPt,
            List<Guid> aiExp,
            List<Guid> aiProf,
            List<Guid> aiTic,
            List<Guid> aiCtr,
            List<Guid> aiCsc,
            List<Guid> vtsst,
            List<Guid> vtsscmpti,
            List<Guid> vtsscm,
            List<Guid> vtssct,
            List<Guid> attach,
            List<Guid> cmd,
            List<Guid> cashd,
            List<Guid> anom,
            List<Guid> appsh)
        {
            if (ap.Any())
            {
                var records = await dc.AccountingPeriods
                    .Where(p => ap.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.AccountingPeriods.AddRangeAsync(records);
            }
            if (ash.Any())
            {
                var records = await dc.AgentShifts
                    .Where(p => ash.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.AgentShifts.AddRangeAsync(records);
            }
            if (dsh.Any())
            {
                var records = await dc.DeviceShifts
                    .Where(p => dsh.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.DeviceShifts.AddRangeAsync(records);
            }
            if (st.Any())
            {
                var records = await dc.SaleTransactions
                    .Where(p => st.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.SaleTransactions.AddRangeAsync(records);
            }
            if (ar.Any())
            {
                var records = await dc.Articles
                    .Where(p => ar.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.Articles.AddRangeAsync(records);
            }
            if (ptbti.Any())
            {
                var records = await dc.PtBankTransferInfos
                    .Where(p => ptbti.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PtBankTransferInfos.AddRangeAsync(records);
            }
            if (ptct.Any())
            {
                var records = await dc.PtConfirmTransactions
                    .Where(p => ptct.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PtConfirmTransactions.AddRangeAsync(records);
            }
            if (ptctpm.Any())
            {
                var records = await dc.PtConfirmTransactionPayments
                    .Where(p => ptctpm.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PtConfirmTransactionPayments.AddRangeAsync(records);
            }
            if (ptctpr.Any())
            {
                var records = await dc.PtConfirmTransactionProducts
                    .Where(p => ptctpr.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PtConfirmTransactionProducts.AddRangeAsync(records);
            }
            if (pos.Any())
            {
                var records = await dc.PosDetails
                    .Where(p => pos.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PosDetails.AddRangeAsync(records);
            }
            if (pd.Any())
            {
                var records = await dc.PaymentDetails
                    .Where(p => pd.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PaymentDetails.AddRangeAsync(records);
            }
            if (bl.Any())
            {
                var records = await dc.CanceledCscContracts
                    .Where(p => bl.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.CanceledCscContracts.AddRangeAsync(records);
            }
            if (aiPt2.Any())
            {
                var records = await dc.PtItemRefundArticleInfos
                    .Where(p => aiPt2.Contains(p.ArticleId))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PtItemRefundArticleInfos.AddRangeAsync(records);
            }
            if (aiTic2.Any())
            {
                var records = await dc.MagneticRefundArticleInfos
                    .Where(p => aiTic2.Contains(p.ArticleId))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.MagneticRefundArticleInfos.AddRangeAsync(records);
            }
            if (aiCtr2.Any())
            {
                var records = await dc.CscContractRefundArticleInfos
                    .Where(p => aiCtr2.Contains(p.ArticleId))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.CscContractRefundArticleInfos.AddRangeAsync(records);
            }
            if (aiPt.Any())
            {
                var records = await dc.PtItemArticleInfos
                    .Where(p => aiPt.Contains(p.ArticleId))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PtItemArticleInfos.AddRangeAsync(records);
            }
            if (aiExp.Any())
            {
                var records = await dc.ContactlessCardExpirationExtensionArticleInfos
                    .Where(p => aiExp.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.ContactlessCardExpirationExtensionArticleInfos.AddRangeAsync(records);
            }
            if (aiProf.Any())
            {
                var records = await dc.ProfileRenewalArticleInfos
                    .Where(p => aiProf.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.ProfileRenewalArticleInfos.AddRangeAsync(records);
            }
            if (aiTic.Any())
            {
                var records = await dc.MagneticArticleInfos
                    .Where(p => aiTic.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.MagneticArticleInfos.AddRangeAsync(records);
            }
            if (aiCtr.Any())
            {
                var records = await dc.CscContractArticleInfos
                    .Where(p => aiCtr.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.CscContractArticleInfos.AddRangeAsync(records);
            }
            if (aiCsc.Any())
            {
                var records = await dc.ContactlessCardArticleInfos
                    .Where(p => aiCsc.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.ContactlessCardArticleInfos.AddRangeAsync(records);
            }
            if (vtsst.Any())
            {
                var records = await dc.VtSellTransactionPaymentTypeInfos
                    .Where(p => vtsst.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.VtSellTransactionPaymentTypeInfos.AddRangeAsync(records);
            }
            if (vtsscmpti.Any())
            {
                var records = await dc.VtSellCommitPaymentTypeInfos
                    .Where(p => vtsscmpti.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.VtSellCommitPaymentTypeInfos.AddRangeAsync(records);
            }
            if (vtsscm.Any())
            {
                var records = await dc.VtSellCommitInfos
                    .Where(p => vtsscm.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.VtSellCommitInfos.AddRangeAsync(records);
            }
            if (vtssct.Any())
            {
                var records = await dc.VtSellContractInfos
                    .Where(p => vtssct.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.VtSellContractInfos.AddRangeAsync(records);
            }
            if (attach.Any())
            {
                var records = await dc.CommandAttachments
                    .Where(p => attach.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.CommandAttachments.AddRangeAsync(records);
            }
            if (cmd.Any())
            {
                var records = await dc.Commands
                    .Where(p => cmd.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.Commands.AddRangeAsync(records);
            }
            if (cashd.Any())
            {
                var records = await dc.CashDrawers
                    .Where(p => cashd.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.CashDrawers.AddRangeAsync(records);
            }
            if (anom.Any())
            {
                var records = await dc.CardAnomalies
                    .Where(p => anom.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.CardAnomalies.AddRangeAsync(records);
            }
            if (appsh.Any())
            {
                var records = await dc.ApplicationShutdowns
                    .Where(p => appsh.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.ApplicationShutdowns.AddRangeAsync(records);
            }
        }

        private static async Task CopySafe(
            DataLayerContext dc,
            DataLayerContext archiveDc,
            List<Guid> ap,
            List<Guid> ash,
            List<Guid> dsh,
            List<Guid> st,
            List<Guid> ar,
            List<Guid> ptbti,
            List<Guid> ptct,
            List<Guid> ptctpm,
            List<Guid> ptctpr,
            List<Guid> pos,
            List<Guid> pd,
            List<string> bl,
            List<Guid> aiPt2,
            List<Guid> aiTic2,
            List<Guid> aiCtr2,
            List<Guid> aiPt,
            List<Guid> aiExp,
            List<Guid> aiProf,
            List<Guid> aiTic,
            List<Guid> aiCtr,
            List<Guid> aiCsc,
            List<Guid> vtsst,
            List<Guid> vtsscmpti,
            List<Guid> vtsscm,
            List<Guid> vtssct,
            List<Guid> attach,
            List<Guid> cmd,
            List<Guid> cashd,
            List<Guid> anom,
            List<Guid> appsh)
        {
            if (ap.Any())
            {
                var existingIds = await archiveDc.AccountingPeriods
                    .Where(p => ap.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = ap.Except(existingIds).ToList();

                var records = await dc.AccountingPeriods
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.AccountingPeriods.AddRangeAsync(records);
            }
            if (ash.Any())
            {
                var existingIds = await archiveDc.AgentShifts
                    .Where(p => ash.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = ash.Except(existingIds).ToList();

                var records = await dc.AgentShifts
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.AgentShifts.AddRangeAsync(records);
            }
            if (dsh.Any())
            {
                var existingIds = await archiveDc.DeviceShifts
                    .Where(p => dsh.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = dsh.Except(existingIds).ToList();

                var records = await dc.DeviceShifts
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.DeviceShifts.AddRangeAsync(records);
            }
            if (st.Any())
            {
                var existingIds = await archiveDc.SaleTransactions
                    .Where(p => st.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = st.Except(existingIds).ToList();

                var records = await dc.SaleTransactions
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.SaleTransactions.AddRangeAsync(records);
            }
            if (ar.Any())
            {
                var existingIds = await archiveDc.Articles
                    .Where(p => ar.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = ar.Except(existingIds).ToList();

                var records = await dc.Articles
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.Articles.AddRangeAsync(records);
            }
            if (ptbti.Any())
            {
                var existingIds = await archiveDc.PtBankTransferInfos
                    .Where(p => ptbti.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = ptbti.Except(existingIds).ToList();

                var records = await dc.PtBankTransferInfos
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PtBankTransferInfos.AddRangeAsync(records);
            }
            if (ptct.Any())
            {
                var existingIds = await archiveDc.PtConfirmTransactions
                    .Where(p => ptct.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = ptct.Except(existingIds).ToList();

                var records = await dc.PtConfirmTransactions
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PtConfirmTransactions.AddRangeAsync(records);
            }
            if (ptctpm.Any())
            {
                var existingIds = await archiveDc.PtConfirmTransactionPayments
                    .Where(p => ptctpm.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = ptctpm.Except(existingIds).ToList();

                var records = await dc.PtConfirmTransactionPayments
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PtConfirmTransactionPayments.AddRangeAsync(records);
            }
            if (ptctpr.Any())
            {
                var existingIds = await archiveDc.PtConfirmTransactionProducts
                    .Where(p => ptctpr.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = ptctpr.Except(existingIds).ToList();

                var records = await dc.PtConfirmTransactionProducts
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PtConfirmTransactionProducts.AddRangeAsync(records);
            }
            if (pos.Any())
            {
                var existingIds = await archiveDc.PosDetails
                    .Where(p => pos.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = pos.Except(existingIds).ToList();

                var records = await dc.PosDetails
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PosDetails.AddRangeAsync(records);
            }
            if (pd.Any())
            {
                var existingIds = await archiveDc.PaymentDetails
                    .Where(p => pd.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = pd.Except(existingIds).ToList();

                var records = await dc.PaymentDetails
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PaymentDetails.AddRangeAsync(records);
            }
            if (bl.Any())
            {
                var existingIds = await archiveDc.CanceledCscContracts
                    .Where(p => bl.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = bl.Except(existingIds).ToList();

                var records = await dc.CanceledCscContracts
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.CanceledCscContracts.AddRangeAsync(records);
            }
            if (aiPt2.Any())
            {
                var existingIds = await archiveDc.PtItemRefundArticleInfos
                    .Where(p => aiPt2.Contains(p.ArticleId))
                    .Select(p => p.ArticleId)
                    .ToListAsync();

                var newIds = aiPt2.Except(existingIds).ToList();

                var records = await dc.PtItemRefundArticleInfos
                    .Where(p => newIds.Contains(p.ArticleId))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PtItemRefundArticleInfos.AddRangeAsync(records);
            }
            if (aiTic2.Any())
            {
                var existingIds = await archiveDc.MagneticRefundArticleInfos
                    .Where(p => aiTic2.Contains(p.ArticleId))
                    .Select(p => p.ArticleId)
                    .ToListAsync();

                var newIds = aiTic2.Except(existingIds).ToList();

                var records = await dc.MagneticRefundArticleInfos
                    .Where(p => newIds.Contains(p.ArticleId))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.MagneticRefundArticleInfos.AddRangeAsync(records);
            }
            if (aiCtr2.Any())
            {
                var existingIds = await archiveDc.CscContractRefundArticleInfos
                    .Where(p => aiCtr2.Contains(p.ArticleId))
                    .Select(p => p.ArticleId)
                    .ToListAsync();

                var newIds = aiCtr2.Except(existingIds).ToList();

                var records = await dc.CscContractRefundArticleInfos
                    .Where(p => newIds.Contains(p.ArticleId))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.CscContractRefundArticleInfos.AddRangeAsync(records);
            }
            if (aiPt.Any())
            {
                var existingIds = await archiveDc.PtItemArticleInfos
                    .Where(p => aiPt.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = aiPt.Except(existingIds).ToList();

                var records = await dc.PtItemArticleInfos
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.PtItemArticleInfos.AddRangeAsync(records);
            }
            if (aiExp.Any())
            {
                var existingIds = await archiveDc.ContactlessCardExpirationExtensionArticleInfos
                    .Where(p => aiExp.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = aiExp.Except(existingIds).ToList();

                var records = await dc.ContactlessCardExpirationExtensionArticleInfos
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.ContactlessCardExpirationExtensionArticleInfos.AddRangeAsync(records);
            }
            if (aiProf.Any())
            {
                var existingIds = await archiveDc.ProfileRenewalArticleInfos
                    .Where(p => aiProf.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = aiProf.Except(existingIds).ToList();

                var records = await dc.ProfileRenewalArticleInfos
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.ProfileRenewalArticleInfos.AddRangeAsync(records);
            }
            if (aiTic.Any())
            {
                var existingIds = await archiveDc.MagneticArticleInfos
                    .Where(p => aiTic.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = aiTic.Except(existingIds).ToList();

                var records = await dc.MagneticArticleInfos
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.MagneticArticleInfos.AddRangeAsync(records);
            }
            if (aiCtr.Any())
            {
                var existingIds = await archiveDc.CscContractArticleInfos
                    .Where(p => aiCtr.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = aiCtr.Except(existingIds).ToList();

                var records = await dc.CscContractArticleInfos
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.CscContractArticleInfos.AddRangeAsync(records);
            }
            if (aiCsc.Any())
            {
                var existingIds = await archiveDc.ContactlessCardArticleInfos
                    .Where(p => aiCsc.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = aiCsc.Except(existingIds).ToList();

                var records = await dc.ContactlessCardArticleInfos
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.ContactlessCardArticleInfos.AddRangeAsync(records);
            }
            if (vtsst.Any())
            {
                var existingIds = await archiveDc.VtSellTransactionPaymentTypeInfos
                    .Where(p => vtsst.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = vtsst.Except(existingIds).ToList();

                var records = await dc.VtSellTransactionPaymentTypeInfos
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.VtSellTransactionPaymentTypeInfos.AddRangeAsync(records);
            }
            if (vtsscmpti.Any())
            {
                var existingIds = await archiveDc.VtSellCommitPaymentTypeInfos
                    .Where(p => vtsscmpti.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = vtsscmpti.Except(existingIds).ToList();

                var records = await dc.VtSellCommitPaymentTypeInfos
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.VtSellCommitPaymentTypeInfos.AddRangeAsync(records);
            }
            if (vtsscm.Any())
            {
                var existingIds = await archiveDc.VtSellCommitInfos
                    .Where(p => vtsscm.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = vtsscm.Except(existingIds).ToList();

                var records = await dc.VtSellCommitInfos
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.VtSellCommitInfos.AddRangeAsync(records);
            }
            if (vtssct.Any())
            {
                var existingIds = await archiveDc.VtSellContractInfos
                    .Where(p => vtssct.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = vtssct.Except(existingIds).ToList();

                var records = await dc.VtSellContractInfos
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.VtSellContractInfos.AddRangeAsync(records);
            }
            if (attach.Any())
            {
                var existingIds = await archiveDc.CommandAttachments
                    .Where(p => attach.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = attach.Except(existingIds).ToList();

                var records = await dc.CommandAttachments
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.CommandAttachments.AddRangeAsync(records);
            }
            if (cmd.Any())
            {
                var existingIds = await archiveDc.Commands
                    .Where(p => cmd.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = cmd.Except(existingIds).ToList();

                var records = await dc.Commands
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.Commands.AddRangeAsync(records);
            }
            if (cashd.Any())
            {
                var existingIds = await archiveDc.CashDrawers
                    .Where(p => cashd.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = cashd.Except(existingIds).ToList();

                var records = await dc.CashDrawers
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.CashDrawers.AddRangeAsync(records);
            }
            if (anom.Any())
            {
                var existingIds = await archiveDc.CardAnomalies
                    .Where(p => anom.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = anom.Except(existingIds).ToList();

                var records = await dc.CardAnomalies
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.CardAnomalies.AddRangeAsync(records);
            }
            if (appsh.Any())
            {
                var existingIds = await archiveDc.ApplicationShutdowns
                    .Where(p => appsh.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync();

                var newIds = appsh.Except(existingIds).ToList();

                var records = await dc.ApplicationShutdowns
                    .Where(p => newIds.Contains(p.Id))
                    .AsNoTracking()
                    .ToListAsync();

                await archiveDc.ApplicationShutdowns.AddRangeAsync(records);
            }
        }

        private static async Task CopyBeforeDeleteAsync<TId>(
            string sourceConnectionString,
            string archiveConnectionString,
            string schema,
            string table,
            IReadOnlyCollection<TId> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return;
            }

            var idTable = new DataTable();
            idTable.Columns.Add("Id", typeof(TId));

            foreach (var id in ids)
            {
                idTable.Rows.Add(id);
            }

            await using var source = new SqlConnection(sourceConnectionString);
            await using var archive = new SqlConnection(archiveConnectionString);

            await source.OpenAsync();
            await archive.OpenAsync();

            await using var createTemp = new SqlCommand("CREATE TABLE #Ids (Id uniqueidentifier NOT NULL PRIMARY KEY);", source);

            await createTemp.ExecuteNonQueryAsync();

            using (var idBulk = new SqlBulkCopy(source))
            {
                idBulk.DestinationTableName = "#Ids";
                await idBulk.WriteToServerAsync(idTable);
            }

            var tableName = $"[{schema}].[{table}]";

            await using var readCommand = new SqlCommand($@"SELECT src.* FROM {tableName} src JOIN #Ids ids ON ids.Id = src.Id;", source);

            await using var reader = await readCommand.ExecuteReaderAsync();

            using var bulkCopy = new SqlBulkCopy(
                archive,
                SqlBulkCopyOptions.KeepIdentity,
                null) {
                DestinationTableName = tableName,
                BatchSize = 5000,
                BulkCopyTimeout = 0
            };

            await bulkCopy.WriteToServerAsync(reader);
        }
    }
}
