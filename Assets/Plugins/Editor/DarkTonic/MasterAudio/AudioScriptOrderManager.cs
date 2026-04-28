/*! \cond PRIVATE */
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DarkTonic.MasterAudio.EditorScripts
{
    // ReSharper disable once CheckNamespace
    public class AudioScriptOrderManager
    {
        private const string SessionOrderSyncKey = "MasterAudio.AudioScriptOrderHandled";

        static AudioScriptOrderManager()
        {
            if (SessionState.GetBool(SessionOrderSyncKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionOrderSyncKey, true);
            EditorApplication.delayCall -= ApplyScriptOrder;
            EditorApplication.delayCall += ApplyScriptOrder;
        }

        private static bool ShouldDefer()
        {
            if (InternalEditorUtility.inBatchMode)
            {
                return true;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return true;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return true;
            }

            return false;
        }

        private static void ApplyScriptOrder()
        {
            EditorApplication.delayCall -= ApplyScriptOrder;

            if (ShouldDefer())
            {
                EditorApplication.delayCall += ApplyScriptOrder;
                return;
            }

            foreach (var monoScript in MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (monoScript.GetClass() == null)
                {
                    continue;
                }

                foreach (var a in Attribute.GetCustomAttributes(monoScript.GetClass(), typeof(AudioScriptOrder)))
                {
                    var currentOrder = MonoImporter.GetExecutionOrder(monoScript);
                    var newOrder = ((AudioScriptOrder) a).Order;
                    if (currentOrder != newOrder)
                    {
                        MonoImporter.SetExecutionOrder(monoScript, newOrder);
                    }
                }
            }
        }
    }
}
/*! \endcond */
