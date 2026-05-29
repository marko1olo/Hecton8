using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CandiceAIforGames.AI
{
    public class BasicSaveSystem
    {
        string storagePath;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string LegacyFormatterDisabledMessage = "Candice BasicSaveSystem legacy formatter path is disabled. Use the first-party save authority.";
        private static bool s_loggedLegacyFormatterDisabled;
#endif
        public BasicSaveSystem(string filename)
        {
            storagePath = Application.dataPath + "//Candice Behavior Designer/Resources/Datastore/" + filename + ".bin";
        }
        public bool SaveToFile(object data)
        {

            bool isSaved = false;
            LogLegacyFormatterDisabledOnce();
            return isSaved;

        }

        public object LoadFromFile()
        {
            object obj = null;
            LogLegacyFormatterDisabledOnce();
            return obj;
        }

        private static void LogLegacyFormatterDisabledOnce()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (s_loggedLegacyFormatterDisabled)
            {
                return;
            }

            s_loggedLegacyFormatterDisabled = true;
            Debug.LogWarning(LegacyFormatterDisabledMessage);
#endif
        }

    }
}
