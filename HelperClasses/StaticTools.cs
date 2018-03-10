using VRage.Game.ModAPI;

namespace EEM.HelperClasses
{
    public static class StaticTools
    {
        public static bool IsDamagedByDeformation(this MyDamageInformation damage)
        {
            return damage.AttackerId == 0 || damage.IsDeformation || damage.Type.ToString() == "Deformation";
        }

    }
}
