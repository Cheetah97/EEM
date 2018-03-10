using System;
using System.Collections.Generic;
using VRageMath;

public class Constants
{
    // used in both CleanUp.cs and BuyShip.cs
    public static readonly HashSet<string> NPC_FACTIONS = new HashSet<string>()
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
    public const bool CLEANUP_DEBUG = false;

    // text required in the RC's CustomData to be even considrered for removal
    public const string CLEANUP_RC_TAG = "[EEM_AI]";

    // any of these is required to be in RC's CustomData for the grid to be removed.
    public static readonly string[] CLEANUP_RC_EXTRA_TAGS = { "Type:Fighter", "Type:Freighter" };

    // clamp the world's view range to this minimum value, which is used for removing distant ships
    public const int CLEANUP_MIN_RANGE = 12000;

    // remove connector-connected ships too?
    public const bool CLEANUP_CONNECTOR_CONNECTED = false;

    // world setting of max drones
    public const int FORCE_MAX_DRONES = 20;




    // BuyShip.cs's constants:

    // time in seconds to wait after spawning before spawning again
    public const int TRADE_DELAY_SECONDS = 15;

    // prefix and suffix words that encapsulate the prefab name
    public const string TRADE_ECHO_PREFIX = "Ship bought:";
    public const string TRADE_ECHO_SUFFIX = "\n";

    // relative spawn position to the PB, use negative for the opposite direction
    public const double SPAWN_RELATIVE_OFFSET_UP = 10.0;
    public const double SPAWN_RELATIVE_OFFSET_LEFT = 0.0;
    public const double SPAWN_RELATIVE_OFFSET_FORWARD = 0.0;

    // after the ship is spawned, all blocks with inventory in the PB's grid with this word in their name and owned by the trader faction will have their contents purged
    public const string POSTSPAWN_EMPTYINVENTORY_TAG = "(purge)";

    // the particle effect name (particles.sbc) to spawn after the ship is spawned
    public const string SPAWN_EFFECT_NAME = "EEMWarpIn";

    // the ship's bounding sphere radius is used for the particle effect scale, this value scales that radius.
    public const float SPAWN_EFFECT_SCALE = 0.2f;

    // radius of the area around the spawn zone to check for foreign objects
    public const float SPAWN_AREA_RADIUS = 15f;

    // the argument that gets used by the mod to call the PB when the buy ship fails due to the position being blocked
    public const string PBARG_FAIL_POSITIONBLOCKED = "fail-positionblocked";

    // in case the PB doesn't work, notify the players within this radius of the failed spawn ship
    public const float SPAWN_FAIL_NOTIFY_DISTANCE = 50;
}