using System;
using EEM.HelperClasses;
using VRage.Game.ModAPI;
using IMyRemoteControl = Sandbox.ModAPI.IMyRemoteControl;

namespace EEM
{
    public static class BotFabrication
    {
        public static BotBase FabricateBot(IMyCubeGrid grid, IMyRemoteControl remoteCcontrol)
        {
            try
            {
                BotTypes botType = BotTypeHelper.ReadBotType(remoteCcontrol);

                BotBase bot = null;
                switch (botType)
                {
                    case BotTypes.Fighter:
                        bot = new BotFighter(grid);
                        break;
                    case BotTypes.Freighter:
                        bot = new BotFreighter(grid);
                        break;
                    case BotTypes.Station:
                        bot = new BotStation(grid);
                        break;
                    //default:
                        //if (Constants.AllowThrowingErrors) throw new Exception("Invalid bot type");
                        //break;
                }

                return bot;
            }
            catch (Exception scrap)
            {
                grid.LogError("BotFabric.FabricateBot", scrap);
                return null;
            }
        }
    }

    /*public sealed class InvalidBot : BotBase
    {
        static public readonly BotTypes BotType = BotTypes.None;

        public override bool Operable
        {
            get
            {
                return false;
            }
        }

        public InvalidBot(IMyCubeGrid Grid = null) : base(Grid)
        {
        }

        public override bool Init(IMyRemoteControl RC = null)
        {
            return false;
        }

        public override void Main()
        {
            // Empty
        }

        protected override bool ParseSetup()
        {
            return false;
        }
    }*/
}