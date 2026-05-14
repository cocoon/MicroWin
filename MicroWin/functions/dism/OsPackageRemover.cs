using Microsoft.Dism;
using MicroWin.functions.Helpers.Loggers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;

namespace MicroWin.functions.dism
{
    public class OsPackageRemover : ImageModificationTask
    {
        public override List<string> excludedItems
        {
            get;
            protected set;
        } = [
                "ApplicationModel",
                "Windows-Client-LanguagePack",
                "LanguageFeatures-Basic",
                "Package_for_ServicingStack",
                "DotNet",
                "Notepad",
                "WMIC",
                "Ethernet",
                "Wifi",
                "FodMetadata",
                "Foundation",
                "LanguageFeatures",
                "VBSCRIPT",
                "License",
                "Hello-Face",
                "ISE",
                "OpenSSH",
                "PMCPPC"
            ];

        private string exludeFileName = "ExcludesOsPackageRemover.txt";
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
            RemoveUnwantedPackages(pbReporter, curOpReporter, logWriter);
        }

        private void RemoveUnwantedPackages(Action<int> pbReporter, Action<string> curOpReporter, Action<string> logWriter)
        {
            curOpReporter.Invoke("Getting image packages...");
            DismPackageCollection? allPackages = GetPackageList();

            if (allPackages is null) return;

            string allPackagesString = string.Join(Environment.NewLine, allPackages.Select(f => f.PackageName));
            DynaLog.logMessage($"All packages:{Environment.NewLine}{allPackagesString}");

            logWriter.Invoke($"Amount of packages in image: {allPackages.Count}");

            // Load excludes from file
            try
            {
                LoadExludesFromFile();
            }
            catch (Exception ex)
            {
                logWriter.Invoke($"ERROR: failed to load exluces from file:{Environment.NewLine}{ex.ToString()}");
                DynaLog.logMessage($"ERROR: failed to load exluces from file:{Environment.NewLine}{ex.ToString()}");
            }

            curOpReporter.Invoke("Filtering image packages...");
            IEnumerable<string> packagesToRemove = allPackages.Select(pkg => pkg.PackageName).Where(pkg =>
                !excludedItems.Any(entry => pkg.IndexOf(entry, StringComparison.OrdinalIgnoreCase) >= 0));

            string packagesToRemoveString = string.Join(Environment.NewLine, packagesToRemove);
            DynaLog.logMessage($"packages to remove:{Environment.NewLine}{packagesToRemoveString}");

            logWriter.Invoke($"Packages to remove:{Environment.NewLine}{packagesToRemove.Count()}");

            try
            {
                DismApi.Initialize(DismLogLevel.LogErrors);
                using DismSession session = DismApi.OpenOfflineSession(AppState.ScratchPath);

                int idx = 0;
                foreach (string packageToRemove in packagesToRemove)
                {
                    curOpReporter.Invoke($"Removing package {packageToRemove}...");
                    pbReporter.Invoke((int)(((double)idx / packagesToRemove.ToList().Count) * 100));
                    // we have this because the API throws an exception on removal error
                    try
                    {
                        DismApi.RemovePackageByName(session, packageToRemove);
                        logWriter.Invoke($"OK: Package {packageToRemove} could be removed.");
                        DynaLog.logMessage($"OK: Package {packageToRemove} removed.");
                    }
                    catch (Exception ex)
                    {
                        logWriter.Invoke($"Package {packageToRemove} could not be removed: {ex.Message}");
                        DynaLog.logMessage($"ERROR: Failed to remove {packageToRemove}: {ex.Message}");
                    }
                    idx++;
                }
            }
            catch (Exception some)
            {
                logWriter.Invoke($"ERROR: {some.Message}");
                DynaLog.logMessage($"ERROR: {some.Message}");
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

        private DismPackageCollection? GetPackageList()
        {
            DismPackageCollection? packages = null;

            try
            {
                DismApi.Initialize(DismLogLevel.LogErrors);
                using DismSession session = DismApi.OpenOfflineSession(AppState.ScratchPath);
                packages = DismApi.GetPackages(session);
            }
            catch (Exception)
            {
                // TODO implement the logging
            }
            finally
            {
                try
                {
                    DismApi.Shutdown();
                }
                catch { }
            }

            return packages;
        }

    }
}
