using System.Collections.Generic;
using System.Text;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    /// <summary>
    /// Batchmode-safe audit of WorldShippingContentFilter against 02_HECTON_WORLD.
    /// Measures how many WorldContentSocket instances are shipping-reachable vs suppressed
    /// (trial/staging hierarchy + trial zone contracts). Does not mutate the scene.
    /// </summary>
    public static class WorldShippingContentFilterValidator
    {
        private const string ProductionWorldScene = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string LogPrefix = "[WorldShippingContentFilterValidator]";

        // COLD ALLOC: List<WorldContentSocket>[256] - editor audit socket scratch - owner: WorldShippingContentFilterValidator
        private static readonly List<WorldContentSocket> _sockets = new List<WorldContentSocket>(256);

        // COLD ALLOC: StringBuilder[4096] - editor audit report builder - owner: WorldShippingContentFilterValidator
        private static readonly StringBuilder _report = new StringBuilder(4096);

        /// <summary>
        /// Public for -executeMethod / CI batchmode. Soft FAIL stays exit 0 under -quit.
        /// Hard exit 1 only when the production world scene cannot be opened.
        /// </summary>
        [MenuItem("Hecton8/Validation/Validate World Shipping Content Filter", priority = 185)]
        public static void ValidateWorldShippingContentFilter()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                string busy = LogPrefix + " RESULT: FAIL — Editor busy (compiling/updating/playing).";
                Debug.LogError(busy);
                if (!batch)
                    EditorUtility.DisplayDialog("World Shipping Content Filter", busy, "OK");
                return;
            }

            if (!TryOpenProductionWorld(out Scene scene, out string openFailure))
            {
                Debug.LogError(LogPrefix + " RESULT: FAIL — " + openFailure);
                if (!batch)
                    EditorUtility.DisplayDialog("World Shipping Content Filter", openFailure, "OK");
                if (batch)
                    EditorApplication.Exit(1);
                return;
            }

            CollectSocketsInScene(scene, _sockets);

            int total = _sockets.Count;
            int suppressed = 0;
            int shipped = 0;
            int inactiveHierarchy = 0;
            int trialZone = 0;
            int trialHierarchy = 0;

            _report.Clear();
            _report.AppendLine("═══════════════════════════════════════════════════════");
            _report.AppendLine("HECTON-8 — World Shipping Content Filter Audit");
            _report.AppendLine("═══════════════════════════════════════════════════════");
            _report.Append("Scene: ").AppendLine(scene.path);
            _report.Append("Socket total (incl. inactive): ").Append(total).AppendLine();
            _report.AppendLine();

            for (int i = 0; i < _sockets.Count; i++)
            {
                WorldContentSocket socket = _sockets[i];
                if (socket == null)
                    continue;

                bool hierarchyInactive = !socket.gameObject.activeInHierarchy;
                bool filterSuppressed = WorldShippingContentFilter.IsSuppressedSocket(socket);

                if (hierarchyInactive)
                    inactiveHierarchy++;

                if (filterSuppressed)
                {
                    suppressed++;
                    ClassifySuppression(socket, ref trialZone, ref trialHierarchy);
                }
                else if (!hierarchyInactive)
                {
                    shipped++;
                }
                else
                {
                    // Active component on inactive parent outside shipping filter names —
                    // still unreachable at runtime until parent is enabled.
                    suppressed++;
                }

                _report.Append("  • ");
                _report.Append(GetTransformPath(socket.transform));
                _report.Append(" | kind=");
                _report.Append(socket.Kind);
                _report.Append(" | id=");
                _report.Append(socket.SocketId);
                _report.Append(" | activeInHierarchy=");
                _report.Append(socket.gameObject.activeInHierarchy ? "1" : "0");
                _report.Append(" | filter=");
                _report.Append(filterSuppressed ? "SUPPRESSED" : "SHIP");
                _report.AppendLine();
            }

            _report.AppendLine();
            _report.Append("shippedReachable=").Append(shipped);
            _report.Append(" suppressed=").Append(suppressed);
            _report.Append(" inactiveHierarchy=").Append(inactiveHierarchy);
            _report.Append(" trialZoneHits=").Append(trialZone);
            _report.Append(" trialHierarchyHits=").Append(trialHierarchy);
            _report.AppendLine();

            // Gate: production world must expose at least one shipping-reachable socket.
            // Counts are measured evidence for the BUILD_PLAYTEST_ISSUES 4-of-14 claim;
            // we do not hard-code expected totals (authoring may grow).
            bool passed = total > 0 && shipped > 0;

            if (total == 0)
            {
                _report.AppendLine("FAIL reason: zero WorldContentSocket components in production world.");
            }
            else if (shipped == 0)
            {
                _report.AppendLine("FAIL reason: every socket is suppressed or inactive — shipping filter leaves no live content.");
            }
            else
            {
                _report.AppendLine("PASS: at least one WorldContentSocket is shipping-reachable under WorldShippingContentFilter.");
            }

            _report.Append("RESULT: ").AppendLine(passed ? "PASS" : "FAIL");
            string reportText = LogPrefix + " " + _report.ToString();

            if (passed)
                Debug.Log(reportText);
            else
                Debug.LogError(reportText);

            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "World Shipping Content Filter",
                    passed
                        ? "PASS\nshipped=" + shipped + " suppressed=" + suppressed + " total=" + total
                        : "FAIL\nshipped=" + shipped + " suppressed=" + suppressed + " total=" + total + "\nSee Console.",
                    "OK");
            }
            // batchmode: soft FAIL under -quit (no EditorApplication.Exit on audit fail).
        }

        private static bool TryOpenProductionWorld(out Scene scene, out string failure)
        {
            scene = default;
            failure = null;

            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isLoaded && active.path == ProductionWorldScene)
            {
                scene = active;
                return true;
            }

            if (!System.IO.File.Exists(ProductionWorldScene))
            {
                failure = "Production world missing on disk: " + ProductionWorldScene;
                return false;
            }

            scene = EditorSceneManager.OpenScene(ProductionWorldScene, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ProductionWorldScene)
            {
                failure = "Failed to open " + ProductionWorldScene;
                return false;
            }

            return true;
        }

        private static void CollectSocketsInScene(Scene scene, List<WorldContentSocket> destination)
        {
            destination.Clear();
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            WorldContentSocket[] found = Object.FindObjectsByType<WorldContentSocket>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < found.Length; i++)
            {
                WorldContentSocket socket = found[i];
                if (socket == null)
                    continue;
                if (socket.gameObject.scene != scene)
                    continue;
                destination.Add(socket);
            }
        }

        private static void ClassifySuppression(
            WorldContentSocket socket,
            ref int trialZone,
            ref int trialHierarchy)
        {
            if (socket == null)
                return;

            WorldZoneAnchor zone = socket.GetZoneAnchor();
            if (zone != null && WorldShippingContentFilter.IsSuppressedZone(zone))
                trialZone++;

            if (WorldShippingContentFilter.IsSuppressedByHierarchy(socket.transform))
                trialHierarchy++;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            // COLD ALLOC: path walk only in editor audit - owner: WorldShippingContentFilterValidator
            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
