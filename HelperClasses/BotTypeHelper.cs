using System;
using System.Collections.Generic;

namespace EEM.HelperClasses
{
    static class BotTypeHelper
    {
        public static BotTypes ReadBotType(Sandbox.ModAPI.IMyRemoteControl remoteControl)
        {
            try
            {
                string remoteControlCustomData = remoteControl.CustomData.Trim().Replace("\r\n", "\n");
                List<string> customData = new List<string>(remoteControlCustomData.Split('\n'));

                if (remoteControlCustomData.IsNullEmptyOrWhiteSpace()) return BotTypes.None;
                if (customData.Count < 2)
                {
                    return BotTypes.Invalid;
                    //if (Constants.AllowThrowingErrors) throw new Exception("CustomData is invalid", new Exception("CustomData consists of less than two lines"));

                }
                if (customData[0].Trim() != "[EEM_AI]")
                {
                    return BotTypes.Invalid;
                    //if (Constants.AllowThrowingErrors) throw new Exception("CustomData is invalid", new Exception($"AI tag invalid: '{customData[0]}'"));
                }

                string[] bottype = customData[1].Split(':');
                if (bottype[0].Trim() != "Type")
                {
                    return BotTypes.Invalid;
                    //if (Constants.AllowThrowingErrors) throw new Exception("CustomData is invalid", new Exception($"Type tag invalid: '{bottype[0]}'"));
                }

                BotTypes botType = BotTypes.Invalid;
                switch (bottype[1].Trim())
                {
                    case "Fighter":
                        botType = BotTypes.Fighter;
                        break;
                    case "Freighter":
                        botType = BotTypes.Freighter;
                        break;
                    case "Carrier":
                        botType = BotTypes.Carrier;
                        break;
                    case "Station":
                        botType = BotTypes.Station;
                        break;
                }

                return botType;
            }
            catch (Exception scrap)
            {
                remoteControl.CubeGrid.LogError("[STATIC]BotBase.ReadBotType", scrap);
                return BotTypes.Invalid;
            }
        }
    }
}
