#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MapMagic.Nodes.GUI;
using MapMagic.Nodes;
using MapMagic.Core;
using MapMagic.Generators.Matrix;
using Den.Tools.GUI;
using Den.Tools;

namespace Hecton8.Tests.Editor
{
    public sealed class MapMagicGraphWindowEditTests
    {
        [Test]
        public void GraphWindow_CatchesExceptionsDuringMiniDraw()
        {
            var window = ScriptableObject.CreateInstance<GraphWindow>();
            var graph = ScriptableObject.CreateInstance<Graph>();
            var gen = new Noise200();

            window.graph = graph;
            window.selected.Add(gen);

            var oldUi = UI.current;
            try
            {
                var ui = new UI();
                ui.textures = null; // This will cause NullReferenceException inside GeneratorDraw.DrawGenerator
                UI.current = ui;

                System.Reflection.MethodInfo drawMiniSelectedMethod = typeof(GraphWindow).GetMethod("DrawMiniSelected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                // Expect the error BEFORE it is logged
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Draw Graph Window failed: System.NullReferenceException"));

                drawMiniSelectedMethod.Invoke(window, null);
            }
            finally
            {
                UI.current = oldUi;
                Object.DestroyImmediate(window);
                Object.DestroyImmediate(graph);
            }
        }
    }
}
#endif
