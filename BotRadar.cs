using System;
using System.Collections.Generic;
using EEM.HelperClasses;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using Ingame = Sandbox.ModAPI.Ingame;

namespace EEM
{
    /// <summary>
    /// This class will provide simple look-around capabilities for a bot.
    /// <para/>
    /// TODO: add LoS checks.
    /// </summary>
    public class BotRadar
    {
        public BotBase MyAI { get; private set; }
        protected IMyCubeGrid Grid => MyAI.Grid;
        protected Vector3D GridPosition => Grid.GetPosition();
        protected IMyFaction OwnerFaction => MyAI.OwnerFaction;

        public BotRadar(BotBase AI)
        {
            MyAI = AI;
        }

        public List<Ingame.MyDetectedEntityInfo> LookAround(float radius, Func<Ingame.MyDetectedEntityInfo, bool> filter = null)
        {
            List<Ingame.MyDetectedEntityInfo> radarData = new List<Ingame.MyDetectedEntityInfo>();
            BoundingSphereD lookaroundSphere = new BoundingSphereD(GridPosition, radius);

            List<IMyEntity> entitiesAround = MyAPIGateway.Entities.GetTopMostEntitiesInSphere(ref lookaroundSphere);
            entitiesAround.RemoveAll(x => x == Grid || GridPosition.DistanceTo(x.GetPosition()) < Grid.WorldVolume.Radius * 1.5);

            long ownerId;
            if (OwnerFaction != null)
            {
                ownerId = OwnerFaction.FounderId;
                Grid.DebugWrite("LookAround", "Found owner via faction owner");
            }
            else
            {
                ownerId = MyAI.RemoteControl.OwnerId;
                Grid.DebugWrite("LookAround", "OWNER FACTION NOT FOUND, found owner via RC owner");
            }

            foreach (IMyEntity detectedEntity in entitiesAround)
            {
                Ingame.MyDetectedEntityInfo radarDetectedEntity = MyDetectedEntityInfoHelper.Create(detectedEntity as MyEntity, ownerId);
                if (radarDetectedEntity.Type == Ingame.MyDetectedEntityType.None || radarDetectedEntity.Type == Ingame.MyDetectedEntityType.Unknown) continue;
                if (filter?.Invoke(radarDetectedEntity) ?? true) radarData.Add(radarDetectedEntity);
            }

            //DebugWrite("LookAround", $"Radar entities detected: {String.Join(" | ", RadarData.Select(x => $"{x.Name}"))}");
            return radarData;
        }

        public List<Ingame.MyDetectedEntityInfo> LookForEnemies(float radius, bool considerNeutralsAsHostiles = false, Func<Ingame.MyDetectedEntityInfo, bool> filter = null) =>
            !considerNeutralsAsHostiles ? LookAround(radius, x => x.IsHostile() && (filter?.Invoke(x) ?? true)) : LookAround(radius, x => x.IsNonFriendly() && (filter?.Invoke(x) ?? true));

        /// <summary>
        /// Returns distance from the grid to an object.
        /// </summary>
        public float Distance(Ingame.MyDetectedEntityInfo target)
        {
            return (float)Vector3D.Distance(GridPosition, target.Position);
        }
    }
}
