using Microsoft.Dism;
using MicroWin.functions.Helpers.Loggers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using static System.Collections.Specialized.BitVector32;

namespace MicroWin.functions.dism
{
    public class OsCapabilityRemover : ImageModificationTask
    {
        public override List<string> excludedItems
        {
            get;
            protected set;
        } = [
                "Language.Basic~~~en-US~0.0.1.0",
            ];

        private string exludeFileName = "ExcludesOsCapabilityRemover.txt";
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
            RemoveUnwantedCapabilities(pbReporter, curOpReporter, logWriter);
        }

        private void RemoveUnwantedCapabilities(Action<int> pbReporter, Action<string> curOpReporter, Action<string> logWriter)
        {
            curOpReporter.Invoke("Getting image capabilities...");

            try
            {
                DismApi.Initialize(DismLogLevel.LogErrors);
                using DismSession session = DismApi.OpenOfflineSession(AppState.ScratchPath);

                // Load all Capabilities
                var allCapabilities = DismApi.GetCapabilities(session);

                if (allCapabilities is null) return;

                // Extract Capabilitiy Names
                var capabilityNames = allCapabilities.Select(c => c.Name).ToList();
                // List to String
                string allCapsAsString = string.Join(Environment.NewLine, capabilityNames);
                //logWriter.Invoke($"Capabilities in Image:{Environment.NewLine}{allCapsAsString}"); //should be ok to have it only in log file to keep window messages cleaner
                DynaLog.logMessage($"Capabilities in Image:{Environment.NewLine}{allCapsAsString}");

                logWriter.Invoke($"Amount of capabilities in image: {allCapabilities.Count}");

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

                curOpReporter.Invoke("Filtering image capabilities...");
                IEnumerable<string> capabilitesToRemove = allCapabilities.Select(cap => cap.Name).Where(cap =>
                    !excludedItems.Any(entry => cap.IndexOf(entry, StringComparison.OrdinalIgnoreCase) >= 0));

                string capabilitiesToRemoveString = string.Join(Environment.NewLine, capabilitesToRemove);
                DynaLog.logMessage($"capabilities to remove:{Environment.NewLine}{capabilitiesToRemoveString}");

                logWriter.Invoke($"capabilities to remove:{Environment.NewLine}{capabilitesToRemove.Count()}");

                int idx = 0;
                foreach (string capabilityToRemove in capabilitesToRemove)
                {
                    curOpReporter.Invoke($"Removing capability {capabilityToRemove}...");
                    pbReporter.Invoke((int)(((double)idx / capabilitesToRemove.ToList().Count) * 100));
                    // we have this because the API throws an exception on removal error
                    try
                    {
                        DismApi.RemoveCapability(session, capabilityToRemove);
                        logWriter.Invoke($"OK: capability {capabilityToRemove} could be removed as capability.");
                        DynaLog.logMessage($"OK: capability {capabilityToRemove} removed.");
                    }
                    catch (Exception ex)
                    {
                        logWriter.Invoke($"Capability {capabilityToRemove} could not be removed: {ex.Message}");
                        DynaLog.logMessage($"ERROR: Failed to remove capability: {capabilityToRemove}: {ex.Message}");
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


    }
}
