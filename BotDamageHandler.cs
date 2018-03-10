using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EEM.HelperClasses;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using Sandbox.ModAPI.Weapons;

namespace EEM
{
    /// <summary>
    /// This class will be a sub-module of BotBase and will be responsible for handling incoming damage and placed blocks.
    /// </summary>
    public class BotDamageHandler
    {
        private static readonly LogWriter DebugLog = new LogWriter(Constants.LogNameDamageHelper);

        public BotBase MyAI { get; private set; }
        protected IMyCubeGrid Grid => MyAI.Grid;

        public bool IgnoreThrusterDamage { get; private set; }
        protected readonly TimeSpan PeaceDelay;
        private Action<IMyPlayer> AdditionalDamageHandler;

        public BotDamageHandler(BotBase AI, Action<IMyPlayer> AdditionalHandler = null, bool IgnoreThrusters = true, TimeSpan? PeaceDelay = null)
        {
            MyAI = AI;
            AiSessionCore.AddDamageHandler(Grid, DamageHandler);
            this.PeaceDelay = PeaceDelay.HasValue ? PeaceDelay.Value : TimeSpan.FromMinutes(15);
            AdditionalDamageHandler = AdditionalHandler;
            IgnoreThrusterDamage = IgnoreThrusters;
        }

        public void Close()
        {
            AiSessionCore.RemoveDamageHandler(Grid);
            AdditionalDamageHandler = null;
        }

        private void DamageHandler(IMySlimBlock block, MyDamageInformation damage)
        {
            try
            {
                IMyPlayer Damager = null;
                if (IgnoreThrusterDamage && (block.CurrentDamage / block.MaxIntegrity < 0.3f) && damage.IsThruster()) return;
                CheckDamage(block, damage, out Damager);
                if (Damager != null)
                {
                    MyAI.DiplomacyModule?.GoHostile(Damager);
                    try
                    {
                        AdditionalDamageHandler?.Invoke(Damager);
                    }
                    catch (Exception Scrap)
                    {
                        Grid.LogError("AdditionalDamageHandler", Scrap);
                    }
                }
            }
            catch (Exception Scrap)
            {
                Grid.LogError("DamageHandler", Scrap);
            }
        }

        private void OnBlockPlaced(IMySlimBlock block)
        {
            if (block == null) return;

            try
            {
                IMyPlayer builder;
                if (!block.IsPlayerBlock(out builder)) return;
                IMyFaction faction = builder.GetFaction();
                if (faction == null) return;
                MyAI.DiplomacyModule?.GoHostile(faction);
                try
                {
                    AdditionalDamageHandler(builder);
                }
                catch (Exception scrap)
                {
                    Grid.LogError("AdditionalDamageHandler[build]", scrap);
                }
            }
            catch (Exception scrap)
            {
                Grid.LogError("BlockPlacedHandler", scrap);
            }
        }

        /// <summary>
        /// Checks incoming damage and determines whether it was done by a player.
        /// <para/>
        /// Note that it does NOT carry out any specific reaction.
        /// </summary>
        protected void CheckDamage(IMySlimBlock block, MyDamageInformation damage, out IMyPlayer damager)
        {
            damager = null;
            try
            {
                if (damage.IsDamagedByDeformation())
                {
                    Grid.DebugWrite("CheckDamage", "Grid was damaged by deformation. Ignoring.");
                    return;
                }
                if (damage.IsMeteor())
                {
                    Grid.DebugWrite("CheckDamage", "Grid was damaged by meteor. Ignoring.");
                    return;
                }

                if (damage.IsThruster())
                {
                    if (block != null && !block.IsDestroyed)
                    {
                        Grid.DebugWrite("CheckDamage", "Grid was slighly damaged by thruster. Ignoring.");
                        return;
                    }
                }

                try
                {
                    if (damage.IsDoneByPlayer(out damager))
                    {
                        if (damager != null)
                            Grid.DebugWrite("CheckDamage", $"Grid is damaged by player {damager.DisplayName}. Trying to activate alert.");
                        else Grid.DebugWrite("CheckDamage", "Couldn't find damager.");
                    }
                    else Grid.DebugWrite("CheckDamage", "Damage.IsDoneByPlayer crashed!");
                }
                catch (Exception scrap)
                {
                    Grid.LogError("CheckDamage.IsDamageDoneByPlayer", scrap);
                }
            }
            catch (Exception scrap)
            {
                Grid.LogError("ReactOnDamage", scrap);
            }
        }
    }
}
