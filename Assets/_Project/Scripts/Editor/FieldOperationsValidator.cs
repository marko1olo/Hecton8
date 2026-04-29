using System.Collections.Generic;
using Hecton8.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    internal static class FieldOperationsValidator
    {
        [MenuItem("Hecton/Validation/Validate Field Operations Stack")]
        private static void Validate()
        {
            List<string> issues = new List<string>();

            if (Object.FindAnyObjectByType<FieldOperationLogSystem>() == null)
                issues.Add("FieldOperationLogSystem is missing from the active scene.");

            if (Object.FindAnyObjectByType<ScanLogSystem>() == null)
                issues.Add("ScanLogSystem is missing from the active scene.");

            if (Object.FindAnyObjectByType<ScannerTool>() == null)
                issues.Add("No ScannerTool instance is reachable in the active scene.");

            if (Object.FindAnyObjectByType<SalvageSamplerTool>() == null)
                issues.Add("No SalvageSamplerTool instance is reachable in the active scene.");

            if (Object.FindAnyObjectByType<LaserCutter>() == null)
                issues.Add("No LaserCutter instance is reachable in the active scene.");

            if (Object.FindAnyObjectByType<EnvironmentalAnalyzerTool>() == null)
                issues.Add("No EnvironmentalAnalyzerTool instance is reachable in the active scene.");

            if (Object.FindAnyObjectByType<StunPistolTool>() == null)
                issues.Add("No StunPistolTool instance is reachable in the active scene.");

            if (Object.FindAnyObjectByType<KnifeTool>() == null)
                issues.Add("No KnifeTool instance is reachable in the active scene.");

            if (Object.FindAnyObjectByType<HarpoonLauncherTool>() == null)
                issues.Add("No HarpoonLauncherTool instance is reachable in the active scene.");

            ValidateDescriptorCoverage(issues);

            if (issues.Count == 0)
            {
                Debug.Log("[FieldOpsValidation] PASS no issues found.");
                return;
            }

            for (int i = 0; i < issues.Count; i++)
                Debug.LogWarning($"[FieldOpsValidation] {issues[i]}");

            Debug.LogWarning($"[FieldOpsValidation] COMPLETE issues={issues.Count}");
        }

        private static void ValidateDescriptorCoverage(List<string> issues)
        {
            FieldTargetDescriptor[] descriptors = Object.FindObjectsByType<FieldTargetDescriptor>(FindObjectsSortMode.None);
            if (descriptors == null || descriptors.Length == 0)
            {
                issues.Add("No FieldTargetDescriptor instances are present in the active scene.");
                return;
            }

            bool hasRoute = false;
            bool hasCargo = false;
            bool hasResource = false;
            bool hasCombat = false;
            bool hasService = false;

            for (int i = 0; i < descriptors.Length; i++)
            {
                FieldTargetDescriptor descriptor = descriptors[i];
                if (descriptor == null)
                    continue;

                switch (descriptor.Role)
                {
                    case FieldTargetRole.RouteAnchor:
                    case FieldTargetRole.RouteRelay:
                    case FieldTargetRole.RouteFrontier:
                        hasRoute = true;
                        break;
                    case FieldTargetRole.CargoLight:
                    case FieldTargetRole.CargoWork:
                    case FieldTargetRole.CargoHeavy:
                    case FieldTargetRole.CargoOverweight:
                        hasCargo = true;
                        break;
                    case FieldTargetRole.ResourceCache:
                    case FieldTargetRole.ResourceNodeActive:
                    case FieldTargetRole.ResourceNodeDepleted:
                        hasResource = true;
                        break;
                    case FieldTargetRole.ServiceDamaged:
                    case FieldTargetRole.ServiceFlooded:
                    case FieldTargetRole.ServiceControl:
                        hasService = true;
                        break;
                    case FieldTargetRole.BioformDormant:
                    case FieldTargetRole.BioformAggressive:
                    case FieldTargetRole.BioformFractured:
                    case FieldTargetRole.BioformDown:
                        hasCombat = true;
                        break;
                }
            }

            if (!hasRoute)
                issues.Add("Authored route markers are missing FieldTargetDescriptor coverage.");
            if (!hasCargo)
                issues.Add("Authored cargo targets are missing FieldTargetDescriptor coverage.");
            if (!hasResource)
                issues.Add("Authored resource targets are missing FieldTargetDescriptor coverage.");
            if (!hasService)
                issues.Add("Authored service targets are missing FieldTargetDescriptor coverage.");
            if (!hasCombat)
                issues.Add("Authored combat targets are missing FieldTargetDescriptor coverage.");
        }
    }
}
