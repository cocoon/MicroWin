using Microsoft.Dism;
using MicroWin.functions.Helpers.Loggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroWin.functions.dism
{
    public class OsFeatureDisabler : ImageModificationTask
    {
        public override List<string> excludedItems { 
            get;
            protected set;
        } = [
                "Defender",
                "Printing",
                "TelnetClient",
                "PowerShell",
                "NetFx",
                "Media",
                "NFS",
                "SearchEngine",
                "RemoteDesktop",
            // SERVER
            "Server-Core",
            "ServerCore-WOW64",
            "WCF-Services45",
            "WCF-TCP-PortSharing45",
            //"SmbDirect",
            //"Tpm-PSH-Cmdlets",
            //"Xps-Foundation-Xps-Viewer",


            
            "Server-Psh-Cmdlets",
            "KeyDistributionService-PSH-Cmdlets",
            "TlsSessionTicketKey-PSH-Cmdlets",

            "WirelessNetworking",
            "Server-Drivers-General",
            //"Server-Drivers-Printers",
            "Server-Shell",
            "Server-Gui-Mgmt",
            //"WindowsServerBackupSnapin",
            //"RSAT",
            //"FileAndStorage-Services",
            //"Storage-Services",
            //"WorkFolders-Client",
            //"SystemDataArchiver",
            //"ServerCoreFonts-NonCritical-Fonts-BitmapFonts",
            //"ServerCoreFonts-NonCritical-Fonts-MinConsoleFonts",
            //"ServerCoreFonts-NonCritical-Fonts-Support",
            //"ServerCoreFonts-NonCritical-Fonts-TrueType",
            //"ServerCoreFonts-NonCritical-Fonts-UAPFonts",
            "ServerCore-Drivers-General",
            "ServerCore-Drivers-General-WOW64",
            //"WindowsAdminCenterSetup",

            ////Nicht default in Server 2025
            //"NetFx3ServerFeatures",
            //"NetFx3",
            //"WCF-HTTP-Activation",
            //"WCF-NonHTTP-Activation",
            //"LightweightServer",

            ];

        private string exludeFileName = "ExcludesOsFeatureDisabler.txt";
        private string exludesFilePath => System.IO.Path.Combine(AppState.AppPath, "excludes", exludeFileName);

        private void LoadExludesFromFile()
        {
            bool replace = true;

            if (File.Exists(exludesFilePath))
            {
                var list = File.ReadAllLines(exludesFilePath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

                if (replace)
                    excludedItems = list;
                else
                    excludedItems.AddRange(list);

                DynaLog.logMessage($"OK: loaded exluces from file:{exludesFilePath}");
            }
        }

        public override void RunTask(Action<int> pbReporter, Action<string> curOpReporter, Action<string> logWriter)
        {
            DisableFeatures(pbReporter, curOpReporter, logWriter);
        }

        private void DisableFeatures(Action<int> pbReporter, Action<string> curOpReporter, Action<string> logWriter)
        {
            curOpReporter.Invoke("Getting image features...");
            DismFeatureCollection? allFeatures = GetFeatureList();

            if (allFeatures is null) return;

            string allFeaturesString = string.Join(Environment.NewLine, allFeatures.Select(f => f.FeatureName));
            DynaLog.logMessage($"All features:{Environment.NewLine}{allFeaturesString}");

            logWriter.Invoke($"Amount of features in image:{Environment.NewLine}{allFeatures.Count}");

            // Load excludes from file
            try
            {
                LoadExludesFromFile();
            }
            catch (Exception ex)
            {
                logWriter.Invoke($"ERROR: failed to load exlucdes from file:{Environment.NewLine}{ex.ToString()}");
                DynaLog.logMessage($"ERROR: failed to load exludes from file:{Environment.NewLine}{ex.ToString()}");
            }

            curOpReporter.Invoke("Filtering image features...");
            IEnumerable<string> featuresToDisable = allFeatures
                .Where(feature => ! new DismPackageFeatureState[3] { DismPackageFeatureState.NotPresent, DismPackageFeatureState.UninstallPending, DismPackageFeatureState.Staged }.Contains(feature.State))
                .Select(feature => feature.FeatureName)
                .Where(feature => !excludedItems.Any(entry => feature.IndexOf(entry, StringComparison.OrdinalIgnoreCase) >= 0));

            string featuresToDisableString = string.Join(Environment.NewLine, featuresToDisable);
            DynaLog.logMessage($"features to disable: {featuresToDisableString}");

            logWriter.Invoke($"Features to disable: {featuresToDisable.Count()}");

            try
            {
                DismApi.Initialize(DismLogLevel.LogErrors);
                using DismSession session = DismApi.OpenOfflineSession(AppState.ScratchPath);
                int idx = 0;
                foreach (string featureToDisable in featuresToDisable)
                {
                    curOpReporter.Invoke($"Disabling feature {featureToDisable}...");
                    pbReporter.Invoke((int)(((double)idx / featuresToDisable.ToList().Count) * 100));
                    try
                    {
#pragma warning disable CS8625
                        DismApi.DisableFeature(session, featureToDisable, null, true);
                        DynaLog.logMessage($"feature disabled: {featureToDisable}");
#pragma warning restore CS8625
                    }
                    catch (Exception ex)
                    {
                        logWriter.Invoke($"Feature {featureToDisable} could not be disabled: {ex.Message}");
                        DynaLog.logMessage($"ERROR: Failed to disable {featureToDisable}: {ex.Message}");
                    }
                    idx++;
                }
            }
            catch (Exception)
            {
                DynaLog.logMessage("ERROR: Failed to Initialize DISM");
            }
            finally
            {
                pbReporter.Invoke(100);
                try
                {
                    DismApi.Shutdown();
                }
                catch { }
            }
        }

        private DismFeatureCollection? GetFeatureList()
        {
            DismFeatureCollection? featureList = null;

            try
            {
                DismApi.Initialize(DismLogLevel.LogErrors);
                using DismSession session = DismApi.OpenOfflineSession(AppState.ScratchPath);
                featureList = DismApi.GetFeatures(session);
            }
            catch (Exception)
            {
                DynaLog.logMessage("ERROR: Failed to Initialize DISM");
            }
            finally
            {
                try
                {
                    DismApi.Shutdown();
                }
                catch { }
            }

            return featureList;
        }


    }
}
