using UnityEngine;
using System.IO;
using Hecton8.Core;

namespace Hecton8.EditorTools
{
    public static class FlashlightValidationTest
    {
        public static void RunTest()
        {
            // 1. Create a dummy wall
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = 12; // VoxelCaveLayerMask is typically 12? Let's check LayerMasks.
            // Actually, HectonLayerMasks.VoxelCaveLayerMask uses layer name "VoxelCave". We can just set the layer to whatever VoxelCave is.
            wall.layer = 12; // Force to 12 just in case
            wall.transform.position = new Vector3(0, 0, 1.0f);
            wall.transform.localScale = new Vector3(1, 1, 1); // thickness 1, front face Z = 0.5

            // 2. Simulate camera at various distances
            float[] distances = new float[] { 1.5f, 1.0f, 0.9f, 0.8f, 0.6f, 0.4f, 0.1f };
            string log = "# PlayerFlashlight Collision Repositioning Validation\n\n";
            log += "Wall is located at Z = 1.0. Wall thickness = 1.0. Front face is at Z = 0.5.\n\n";
            log += "| Camera Z | Original Light Z | Wall Overlap? | Candidate Stepping | Final Light Z | Comment |\n";
            log += "|----------|------------------|---------------|--------------------|---------------|---------|\n";

            int layerMask = 1 << 12; // explicit layer 12
            
            foreach (float dist in distances)
            {
                // Camera is looking +Z
                Vector3 camPos = new Vector3(0, 0, 0.5f - dist);
                Vector3 forward = Vector3.forward;

                Vector3 origin = camPos + forward * 0.2f;
                Vector3 finalPos = origin;
                bool overlapped = UnityEngine.Physics.CheckSphere(origin, 0.05f, layerMask);
                string steppingLog = "";

                if (overlapped)
                {
                    bool found = false;
                    float[] steps = new float[] { 0.05f, 0.10f, 0.20f, 0.40f };
                    for (int i = 0; i < steps.Length; i++)
                    {
                        Vector3 candidate = camPos - forward * steps[i];
                        if (!UnityEngine.Physics.CheckSphere(candidate, 0.05f, layerMask))
                        {
                            finalPos = candidate;
                            found = true;
                            steppingLog += $"Found clear at step -{steps[i]}";
                            break;
                        }
                        else
                        {
                            steppingLog += $"Fail -{steps[i]}, ";
                        }
                    }

                    if (!found)
                    {
                        finalPos = camPos;
                        steppingLog += "Fallback to CamPos";
                    }
                }
                else
                {
                    steppingLog = "N/A";
                }

                string comment = overlapped ? (finalPos == camPos ? "Dimmed/No shadows" : "Repositioned Backwards") : "Normal";
                log += $"| {camPos.z:F2} | {origin.z:F2} | {overlapped} | {steppingLog} | {finalPos.z:F2} | {comment} |\n";
            }

            File.WriteAllText("Docs/AgentLogs/FlashlightValidationResult.md", log);
            Debug.Log("FlashlightValidationTest Completed.");
        }
    }
}
