using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace EEM.HelperClasses
{
    public class EntityByDistanceSorter : IComparer<IMyEntity>, IComparer<IMySlimBlock>, IComparer<Sandbox.ModAPI.Ingame.MyDetectedEntityInfo>
    {
        public Vector3D Position { get; set; }
        public EntityByDistanceSorter(Vector3D Position)
        {
            this.Position = Position;
        }

        public int Compare(IMyEntity x, IMyEntity y)
        {
            double DistanceX = Vector3D.DistanceSquared(Position, x.GetPosition());
            double DistanceY = Vector3D.DistanceSquared(Position, y.GetPosition());

            if (DistanceX < DistanceY) return -1;
            if (DistanceX > DistanceY) return 1;
            return 0;
        }

        public int Compare(Sandbox.ModAPI.Ingame.MyDetectedEntityInfo x, Sandbox.ModAPI.Ingame.MyDetectedEntityInfo y)
        {
            double DistanceX = Vector3D.DistanceSquared(Position, x.Position);
            double DistanceY = Vector3D.DistanceSquared(Position, y.Position);

            if (DistanceX < DistanceY) return -1;
            if (DistanceX > DistanceY) return 1;
            return 0;
        }

        public int Compare(IMySlimBlock x, IMySlimBlock y)
        {
            double DistanceX = Vector3D.DistanceSquared(Position, x.CubeGrid.GridIntegerToWorld(x.Position));
            double DistanceY = Vector3D.DistanceSquared(Position, y.CubeGrid.GridIntegerToWorld(y.Position));

            if (DistanceX < DistanceY) return -1;
            if (DistanceX > DistanceY) return 1;
            return 0;
        }
    }
}