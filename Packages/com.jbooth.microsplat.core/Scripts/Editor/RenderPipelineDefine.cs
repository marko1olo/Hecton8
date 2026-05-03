//////////////////////////////////////////////////////
// MicroSplat
// Copyright (c) Jason Booth
//////////////////////////////////////////////////////

using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_2019_3_OR_NEWER

// installs defines for render pipelines, so we can #if USING_HDRP and do stuff. Can't believe Unity doesn't provide this crap, they
// really go out of their way to make it hard to work across pipelines.

namespace JBooth.MicroSplat
{
   public static class RenderPipelineDefine
   {
      private const string HDRP_PACKAGE = "HDRenderPipelineAsset";
      private const string URP_PACKAGE = "UniversalRenderPipelineAsset";

      public static bool IsHDRP { get; private set; }
      public static bool IsURP { get; private set; }
      public static bool IsStandardRP { get; private set; }

      [UnityEditor.Callbacks.DidReloadScripts]
      private static void OnScriptsReloaded()
      {
         if (Application.isBatchMode)
         {
            return;
         }

         IsHDRP = DoesTypeExist(HDRP_PACKAGE);
         IsURP = DoesTypeExist(URP_PACKAGE);

         if (!(IsHDRP || IsURP))
         {
            IsStandardRP = true;
         }

      }

      public static bool DoesTypeExist(string className)
      {
         Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
         for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
         {
            Type[] types = GetTypesSafe(assemblies[assemblyIndex]);
            for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
               Type type = types[typeIndex];
               if (type != null && type.Name == className)
               {
                  return true;
               }
            }
         }

         return false;
      }

      public static Type[] GetTypesSafe(System.Reflection.Assembly assembly)
      {
         try
         {
            return assembly.GetTypes();
         }
         catch (ReflectionTypeLoadException e)
         {
            return e.Types;
         }
      }
   }
}

#endif
