using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace EEM.HelperClasses
{
    public static class TerminalExtensions
    {
        public static IMyGridTerminalSystem GetTerminalSystem(this IMyCubeGrid Grid)
        {
            return MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(Grid);
        }

        /// <summary>
        /// Allows GetBlocksOfType to work like a chainable function.
        /// <para />
        /// Enjoy allocating.
        /// </summary>
        public static List<T> GetBlocksOfType<T>(this IMyGridTerminalSystem Term, Func<T, bool> collect = null) where T : class, Sandbox.ModAPI.Ingame.IMyTerminalBlock
        {
            List<T> TermBlocks = new List<T>();
            Term.GetBlocksOfType<T>(TermBlocks, collect);
            return TermBlocks;
        }

        public static void Trigger(this IMyTimerBlock Timer)
        {
            Timer.GetActionWithName("TriggerNow").Apply(Timer);
        }

        public static List<IMyInventory> GetInventories(this IMyEntity Entity)
        {
            if (!Entity.HasInventory) return new List<IMyInventory>();

            List<IMyInventory> Inventories = new List<IMyInventory>();
            for (int i=0; i<Entity.InventoryCount; i++)
            {
                Inventories.Add(Entity.GetInventory(i));
            }
            return Inventories;
        }
    }
}