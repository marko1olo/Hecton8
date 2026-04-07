// ============================================================================
// HECTON-8 — SceneGuard.cs
// Protect non-bootstrap scenes from being loaded directly.
//
// ПРАВИЛО:
// ✗ Запуск 01_MAIN_MENU без 00_BOOTSTRAP = ЗАПРЕЩЕНО
// ✗ Запуск 02_HECTON_WORLD без 00_BOOTSTRAP = ЗАПРЕЩЕНО
// ✓ Запуск 00_BOOTSTRAP = РАЗРЕШЕНО
//
// Если это нарушение обнаружено:
//   1. Логируем ошибку
//   2. Перезагружаем 00_BOOTSTRAP
//   3. Затем загружаем нужную сцену через GameStartContext
//
// ============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using Hecton8.Core;
using Hecton8.Bootstrap;

namespace Hecton8.Guardian
{
    /// <summary>
    /// Guard для сцен. Проверяет что bootstrap был загружен.
    /// При нарушении — перезагружает bootstrap и переходит в нужную сцену.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-29000)] // После BootstrapController, но до остального
    public sealed class SceneGuard : MonoBehaviour
    {
        [SerializeField] private bool _enforceBootstrap = true;

        private void Awake()
        {
            if (!_enforceBootstrap)
                return;

            // ── Проверка что bootstrap был загружен ──
            if (!BootstrapController.AreAllSystemsReady())
            {
                Scene currentScene = gameObject.scene;

                Debug.LogError(
                    $"[SceneGuard] Scene '{currentScene.name}' loaded WITHOUT bootstrap! " +
                    $"This violates the architecture. Reloading 00_BOOTSTRAP...");

                // ── Переход: 00_BOOTSTRAP → нужная сцена ──
                string targetScene = currentScene.name;
                LoadBootstrapThenTarget(targetScene);
            }
        }

        private static void LoadBootstrapThenTarget(string targetSceneName)
        {
            // ── Устанавливаем контекст ──
            // После загрузки bootstrap и menu user выберет сцену вручную
            GameStartContextHolder.Reset();

            // ── Загружаем bootstrap ──
            SceneManager.LoadScene("00_BOOTSTRAP");

            // Примечание: Правильный переход (bootstrap → menu → world) будет
            // когда user нажимает кнопки в UI. Этот guard просто восстанавливает
            // состояние после неправильной загрузки сцены.
        }
    }
}
