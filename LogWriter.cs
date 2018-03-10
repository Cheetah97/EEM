using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using static EEM.Constants;

namespace EEM
{
    public class LogWriter
    {
        public LogWriter(string logName)
        {
            SetupWriter(logName);
        }

        private TextWriter Writer { get; set; }

        private void SetupWriter(string logName)
        {
            if (Writer != null)
            {
                Writer.Flush();
                Writer.Close();
            }
            Writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(logName, typeof(Log));
        }
        
        public void WriteMessage(string message)
        {
            Writer.WriteLine($"{DateTime.Now:HH:mm:ss}{Tab}{message}");
            Writer.Flush();
        }
    }
}
