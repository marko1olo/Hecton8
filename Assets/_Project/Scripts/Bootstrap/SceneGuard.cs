// ============================================================================
// HECTON-8 — SceneGuard.cs
// Protect non-bootstrap scenes from being loaded directly.
//
// PRAVILO:
// ✗ Zapusk 01_MAIN_MENU bez 00_BOOTSTRAP = ZAPRESchENO
// ✗ Zapusk 02_HECTON_WORLD bez 00_BOOTSTRAP = ZAPRESchENO
// ✓ Zapusk 00_BOOTSTRAP = RAZREShENO
//
// Esli eto narushenie obnaruzheno:
//   1. Logiruem oshibku
//   2. Perezagruzhaem 00_BOOTSTRAP
//   3. Zatem zagruzhaem nuzhnuyu stsenu cherez GameStartContext
//
// ============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using Hecton8.Core;
using Hecton8.Bootstrap;
using Hecton8.World;

namespace Hecton8.Guardian
{
    /// <summary>
    /// Guard dlya stsen. Proveryaet chto bootstrap byl zagruzhen.
    /// Pri narushenii — perezagruzhaet bootstrap i perehodit v nuzhnuyu stsenu.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-29000)] // Posle BootstrapController, no do ostalnogo
    public sealed class SceneGuard : MonoBehaviour
    {
        private const string BootstrapSceneName = "00_BOOTSTRAP";

        [SerializeField] private bool _enforceBootstrap = true;

        private void Awake()
        {
            WorldShippingSceneRuntimeGuard.CleanupLoadedScene(gameObject.scene);

            if (!_enforceBootstrap)
                return;

            // ── Proverka chto bootstrap byl zagruzhen ──
            if (!GameBootstrapper.AreAllSystemsReady())
            {
                Scene currentScene = gameObject.scene;

                Hecton8.Core.H8Debug.LogError(
                    $"[SceneGuard] Scene '{currentScene.name}' loaded WITHOUT bootstrap! " +
                    $"This violates the architecture. Reloading {BootstrapSceneName}...");

                // ── Perehod: 00_BOOTSTRAP → nuzhnaya stsena ──
                string targetScene = currentScene.name;
                LoadBootstrapThenTarget(targetScene);
            }
        }

        private static void LoadBootstrapThenTarget(string targetSceneName)
        {
            // ── Ustanavlivaem kontekst ──
            // Posle zagruzki bootstrap i menu user vyberet stsenu vruchnuyu
            GameStartContextHolder.Reset();

            // ── Zagruzhaem bootstrap ──
            SceneManager.LoadScene(BootstrapSceneName);

            // Primechanie: Pravilnyy perehod (bootstrap → menu → world) budet
            // kogda user nazhimaet knopki v UI. Etot guard prosto vosstanavlivaet
            // sostoyanie posle nepravilnoy zagruzki stseny.
        }
    }
}
