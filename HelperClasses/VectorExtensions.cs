using VRageMath;

namespace EEM.HelperClasses
{
    public static class VectorExtensions
    {
        public static double DistanceTo(this Vector3D From, Vector3D To)
        {
            return (To - From).Length();
        }

        public static Vector3D LineTowards(this Vector3D From, Vector3D To, double Length)
        {
            return From + (Vector3D.Normalize(To - From) * Length);
        }

        public static Vector3D InverseVectorTo(this Vector3D From, Vector3D To, double Length)
        {
            return From + (Vector3D.Normalize(From - To) * Length);
        }
    }
}