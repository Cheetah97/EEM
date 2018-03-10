using System.Collections.Generic;

namespace EEM
{
    public static class Constants
    {
        // General Constants

        /// <summary>
        /// This permits certain operations to throw custom exceptions in order to
        /// provide detailed descriptions of what gone wrong, over call stack.<para />
        /// BEWARE, every exception thrown must be explicitly provided with a catcher, or it will crash the entire game!
        /// </summary>
        public const bool AllowThrowingErrors = true;

        public const string SecurityOnTimerPrefix = "[Alert_On]";

        public const string SecurityOffTimerPrefix = "[Alert_Off]";

        public const string WarpinEffect = "EEMWarpIn";

        public const string LogNameAiSessionCore = "AiSessionCore.csv";

        public const string LogNameDamageHelper = "DamageHelper.csv";

        /// <summary>
        /// This toggles the Debug mode. Without Debug, only critical messages are shown in chat.
        /// </summary>
        public const bool GlobalDebugMode = false;

        /// <summary>
        /// This toggles the Debug Anti-Spam mode. With this, a single combination of ship name + message will be displayed only once per session.
        /// </summary>
        public const bool DebugAntiSpam = false;

        // used in both CleanUp.cs and BuyShip.cs
        public static readonly HashSet<string> NpcFactions = new HashSet<string>()
        {
            "SPRT",
            "CIVL",
            "UCMF",
            "SEPD",
            "ISTG",
            "AMPH",
            "KUSS",
            "HS",
            "MA-I",
            "EXMC",
            "IMDC"
        };

        // CleanUp.cs's constants:

        // verbose debug log output
        public const bool CleanupDebug = false;

        // text required in the RC's CustomData to be even considrered for removal
        public const string CleanupRcTag = "[EEM_AI]";

        // any of these is required to be in RC's CustomData for the grid to be removed.
        public static readonly string[] CleanupRcExtraTags = { "Type:Fighter", "Type:Freighter" };

        // clamp the world's view range to this minimum value, which is used for removing distant ships
        public const int CleanupMinRange = 12000;

        // remove connector-connected ships too?
        //public const bool CleanupConnectorConnected = false;

        // world setting of max drones
        public const int ForceMaxDrones = 20;

        
        // BuyShip.cs's constants:

        // time in seconds to wait after spawning before spawning again
        public const int TradeDelaySeconds = 15;

        // prefix and suffix words that encapsulate the prefab name
        public const string TradeEchoPrefix = "Ship bought:";
        public const string TradeEchoSuffix = "\n";

        // relative spawn position to the PB, use negative for the opposite direction
        public const double SpawnRelativeOffsetUp = 10.0;
        public const double SpawnRelativeOffsetLeft = 0.0;
        public const double SpawnRelativeOffsetForward = 0.0;

        // after the ship is spawned, all blocks with inventory in the PB's grid with this word in their name and owned by the trader faction will have their contents purged
        public const string PostspawnEmptyinventoryTag = "(purge)";

        // the particle effect name (particles.sbc) to spawn after the ship is spawned
        public const string SpawnEffectName = "EEMWarpIn";

        // the ship's bounding sphere radius is used for the particle effect scale, this value scales that radius.
        public const float SpawnEffectScale = 0.2f;

        // radius of the area around the spawn zone to check for foreign objects
        public const float SpawnAreaRadius = 15f;

        // the argument that gets used by the mod to call the PB when the buy ship fails due to the position being blocked
        public const string PbargFailPositionblocked = "fail-positionblocked";

        // in case the PB doesn't work, notify the players within this radius of the failed spawn ship
        public const float SpawnFailNotifyDistance = 50;


        // text formatting
        public const string Tab = ",";
        public const string NewLine = "\n";

    }
}