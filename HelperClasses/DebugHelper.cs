using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace EEM.HelperClasses
{
    public static class DebugHelper
    {
        private static readonly List<int> AlreadyPostedMessages = new List<int>();

        public static void Print(string Source, string Message, bool AntiSpam = true)
        {
            string combined = Source + ": " + Message;
            int hash = combined.GetHashCode();

            if (!AlreadyPostedMessages.Contains(hash))
            {
                AlreadyPostedMessages.Add(hash);
                MyAPIGateway.Utilities.ShowMessage(Source, Message);
                VRage.Utils.MyLog.Default.WriteLine(Source + $": Debug message: {Message}");
                VRage.Utils.MyLog.Default.Flush();
            }
        }

        public static void DebugWrite(this IMyCubeGrid Grid, string Source, string Message, bool AntiSpam = true, bool ForceWrite = false)
        {
            if (Constants.GlobalDebugMode || ForceWrite) Print(Grid.DisplayName, $"Debug message from '{Source}': {Message}");
        }

        public static void LogError(this IMyCubeGrid Grid, string Source, Exception Scrap, bool AntiSpam = true, bool ForceWrite = false)
        {
            if (!Constants.GlobalDebugMode && !ForceWrite) return;
            string DisplayName = "Unknown Grid";
            try
            {
                DisplayName = Grid.DisplayName;
            }
            finally
            {
                Print(DisplayName, $"Fatal error in '{Source}': {Scrap.Message}. {(Scrap.InnerException != null ? Scrap.InnerException.Message : "No additional info was given by the game :(")}");
            }
        }
    }
}