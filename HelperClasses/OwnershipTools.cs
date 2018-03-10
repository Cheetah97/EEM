using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace EEM.HelperClasses
{
    public static class OwnershipTools
    {
        public static long PirateID
        {
            get
            {
                return MyVisualScriptLogicProvider.GetPirateId();
            }
        }

        public static bool IsOwnedByPirates(this IMyTerminalBlock Block)
        {
            return Block.OwnerId == PirateID;
        }

        public static bool IsOwnedByNPC(this IMyTerminalBlock Block, bool AllowNobody = true, bool CheckBuilder = false)
        {
            if (!CheckBuilder)
            {
                if (Block.IsOwnedByPirates()) return true;
                if (!AllowNobody && Block.IsOwnedByNobody()) return false;
                IMyPlayer Owner = MyAPIGateway.Players.GetPlayerByID(Block.OwnerId);
                return Owner != null ? Owner.IsBot : true;
            }
            else
            {
                if (!Block.IsOwnedByNPC(AllowNobody)) return false;
                long BuilderID = Block.GetBuiltBy();
                if (!AllowNobody && BuilderID == 0) return false;
                IMyPlayer Owner = MyAPIGateway.Players.GetPlayerByID(BuilderID);
                return Owner != null ? Owner.IsBot : true;
            }
        }

        public static bool IsPirate(this IMyCubeGrid Grid, bool StrictCheck = false)
        {
            if (Grid.BigOwners.Count == 0 || Grid.BigOwners[0] == 0) return false;
            if (!StrictCheck) return Grid.BigOwners.Contains(PirateID);
            else
            {
                return Grid.BigOwners.Count == 1 && Grid.BigOwners[0] == PirateID;
            }
        }

        public static bool IsNPC(this IMyCubeGrid Grid)
        {
            if (Grid.IsPirate()) return true;
            if (Grid.BigOwners.Count == 0) return false;
            IMyPlayer Owner = MyAPIGateway.Players.GetPlayerByID(Grid.BigOwners[0]);
            return Owner != null ? Owner.IsBot : true;
        }

        public static bool IsOwnedByNobody(this IMyCubeGrid Grid)
        {
            return Grid.BigOwners.Count == 0 || Grid.BigOwners[0] == 0;
        }

        public static bool IsOwnedByNobody(this IMyCubeBlock Block)
        {
            return Block.OwnerId == 0;
        }

        public static bool IsBuiltByNobody(this IMyCubeBlock Block)
        {
            return Block.GetBuiltBy() == 0;
        }

        public static bool IsPlayerBlock(this IMySlimBlock Block, out IMyPlayer Builder)
        {
            Builder = null;
            long BuiltBy = Block.GetBuiltBy();
            if (BuiltBy == 0) return false;
            Builder = MyAPIGateway.Players.GetPlayerByID(BuiltBy);
            return Builder != null && !Builder.IsBot;
        }

        public static bool IsPlayerBlock(this IMyCubeBlock Block, out IMyPlayer Owner)
        {
            Owner = null;
            if (Block.OwnerId != 0)
            {
                return MyAPIGateway.Players.IsValidPlayer(Block.OwnerId, out Owner);
            }
            else
            {
                long BuiltBy = Block.GetBuiltBy();
                if (BuiltBy == 0) return false;
                Owner = MyAPIGateway.Players.GetPlayerByID(BuiltBy);
                return Owner != null && !Owner.IsBot;
            }
        }
    }
}