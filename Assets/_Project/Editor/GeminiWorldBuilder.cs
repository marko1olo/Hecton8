using UnityEngine;
using UnityEditor;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using Den.Tools;
using System.Collections.Generic;

public class GeminiWorldBuilder
{
    [MenuItem("Hecton/Gemini World Builder/Build Master-Grade World")]
    public static void BuildMasterGradeWorld()
    {
        Debug.Log("Gemini: Building Optimized Master-Grade World v3...");
        
        string path = "Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN_GEMINI.asset";
        Graph targetGraph = AssetDatabase.LoadAssetAtPath<Graph>(path);

        if (targetGraph == null)
        {
            targetGraph = ScriptableObject.CreateInstance<Graph>();
            AssetDatabase.CreateAsset(targetGraph, path);
        }

        targetGraph.generators = new Generator[0];

        // --- 1. REGIONAL NOISE (Patchy Selection) ---
        Noise200 regionNoise = Generator.Create(typeof(Noise200)) as Noise200;
        regionNoise.size = 1500; // Very large organic patches
        regionNoise.guiPosition = new Vector2(-400, 500);
        targetGraph.Add(regionNoise);

        // --- 2. BLUEPRINT ---
        Import200 blueprint = Generator.Create(typeof(Import200)) as Import200;
        blueprint.matrixAsset = AssetDatabase.LoadAssetAtPath<MatrixAsset>("Assets/MapMagic/Map_Graph/New Gen/USE IT.asset");
        blueprint.guiPosition = new Vector2(3000, -200);
        targetGraph.Add(blueprint);

        Levels200 blueprintLevels = Generator.Create(typeof(Levels200)) as Levels200;
        blueprintLevels.inMax = 0.8f; 
        blueprintLevels.guiPosition = new Vector2(3200, -200);
        targetGraph.Add(blueprintLevels);
        targetGraph.Link((IOutlet<object>)blueprint, (IInlet<object>)blueprintLevels);

        // --- 3. THE BIOME MATRIX (27 Tiers x 4 Regions) ---
        // We will collect each tier's final output node here
        List<Generator> tierOutputs = new List<Generator>(27);

        for (int t = 0; t < 27; t++)
        {
            float depthMin = t / 27f;
            float depthMax = (t + 1) / 27f;

            Levels200 tierMask = Generator.Create(typeof(Levels200)) as Levels200;
            tierMask.inMin = depthMin; tierMask.inMax = depthMax;
            tierMask.outMin = 0; tierMask.outMax = 1;
            tierMask.guiPosition = new Vector2(200, t * 150);
            targetGraph.Add(tierMask);
            targetGraph.Link((IOutlet<object>)blueprintLevels, (IInlet<object>)tierMask);

            // One Blend per Tier for its 4 regions
            Blend200 tierMixer = Generator.Create(typeof(Blend200)) as Blend200;
            tierMixer.layers = new Blend200.Layer[4];
            tierMixer.guiPosition = new Vector2(2000, t * 150);
            targetGraph.Add(tierMixer);

            for (int r = 0; r < 4; r++)
            {
                tierMixer.layers[r] = new Blend200.Layer();
                tierMixer.layers[r].inlet.SetGen(tierMixer);
                tierMixer.layers[r].algorithm = Blend200.BlendAlgorithm.max;

                // Region selector
                Levels200 regMast = Generator.Create(typeof(Levels200)) as Levels200;
                regMast.inMin = r * 0.25f; regMast.inMax = (r + 1) * 0.25f;
                regMast.guiPosition = new Vector2(600, t * 150 + r * 30);
                targetGraph.Add(regMast);
                targetGraph.Link((IOutlet<object>)regionNoise, (IInlet<object>)regMast);

                // Combined Mask (Tier * Region)
                Blend200 combMask = Generator.Create(typeof(Blend200)) as Blend200;
                combMask.layers[1].algorithm = Blend200.BlendAlgorithm.multiply;
                combMask.guiPosition = new Vector2(900, t * 150 + r * 30);
                targetGraph.Add(combMask);
                targetGraph.Link((IOutlet<object>)tierMask, (IInlet<object>)combMask.layers[0].inlet);
                targetGraph.Link((IOutlet<object>)regMast, (IInlet<object>)combMask.layers[1].inlet);

                // Biome Detail
                Noise200 detail = Generator.Create(typeof(Noise200)) as Noise200;
                detail.seed = 1234 + t * 4 + r;
                detail.size = 30 + r * 10;
                detail.guiPosition = new Vector2(1200, t * 150 + r * 30);
                targetGraph.Add(detail);

                // Unique Modifiers
                Generator mod = detail;
                if (r == 0) { // Ridges
                    Curve200 cur = Generator.Create(typeof(Curve200)) as Curve200;
                    cur.guiPosition = new Vector2(1500, t * 150 + r * 30);
                    targetGraph.Add(cur);
                    targetGraph.Link((IOutlet<object>)detail, (IInlet<object>)cur);
                    mod = cur;
                } else if (r == 1) { // Slabs
                    Terrace200 ter = Generator.Create(typeof(Terrace200)) as Terrace200;
                    ter.num = 6;
                    ter.guiPosition = new Vector2(1500, t * 150 + r * 30);
                    targetGraph.Add(ter);
                    targetGraph.Link((IOutlet<object>)detail, (IInlet<object>)ter);
                    mod = ter;
                }

                // Masked Biome
                Blend200 bioMasked = Generator.Create(typeof(Blend200)) as Blend200;
                bioMasked.layers[1].algorithm = Blend200.BlendAlgorithm.multiply;
                bioMasked.guiPosition = new Vector2(1750, t * 150 + r * 30);
                targetGraph.Add(bioMasked);
                targetGraph.Link((IOutlet<object>)mod, (IInlet<object>)bioMasked.layers[0].inlet);
                targetGraph.Link((IOutlet<object>)combMask, (IInlet<object>)bioMasked.layers[1].inlet);

                // Link to Tier Mixer (Layer r)
                targetGraph.Link((IOutlet<object>)bioMasked, (IInlet<object>)tierMixer.layers[r].inlet);
            }
            tierOutputs.Add(tierMixer);
        }

        // --- 4. GLOBAL COMBINATION (THE CROWN BLEND) ---
        Blend200 finalMatrix = Generator.Create(typeof(Blend200)) as Blend200;
        finalMatrix.layers = new Blend200.Layer[27];
        finalMatrix.guiPosition = new Vector2(2500, 1500);
        targetGraph.Add(finalMatrix);

        for (int i = 0; i < 27; i++)
        {
            finalMatrix.layers[i] = new Blend200.Layer();
            finalMatrix.layers[i].inlet.SetGen(finalMatrix);
            finalMatrix.layers[i].algorithm = Blend200.BlendAlgorithm.max;
            targetGraph.Link((IOutlet<object>)tierOutputs[i], (IInlet<object>)finalMatrix.layers[i].inlet);
        }

        // --- 5. GROWTH INTEGRATION ---
        // FinalHeight = Blueprint + (Detail * 0.15)
        Levels200 scaleDown = Generator.Create(typeof(Levels200)) as Levels200;
        scaleDown.outMax = 0.15f; 
        scaleDown.guiPosition = new Vector2(2800, 1500);
        targetGraph.Add(scaleDown);
        targetGraph.Link((IOutlet<object>)finalMatrix, (IInlet<object>)scaleDown);

        Blend200 growth = Generator.Create(typeof(Blend200)) as Blend200;
        growth.layers[1].algorithm = Blend200.BlendAlgorithm.add;
        growth.guiPosition = new Vector2(3500, 500);
        targetGraph.Add(growth);
        targetGraph.Link((IOutlet<object>)blueprintLevels, (IInlet<object>)growth.layers[0].inlet);
        targetGraph.Link((IOutlet<object>)scaleDown, (IInlet<object>)growth.layers[1].inlet);

        // --- 6. OUTPUTS ---
        HeightOutput200 hOut = Generator.Create(typeof(HeightOutput200)) as HeightOutput200;
        hOut.guiPosition = new Vector2(4000, 500);
        targetGraph.Add(hOut);
        targetGraph.Link((IOutlet<object>)growth, (IInlet<object>)hOut);

        EditorUtility.SetDirty(targetGraph);
        AssetDatabase.SaveAssets();
        Debug.Log("Master-Grade v3 Build Complete! Organic, Spikeless 108-Biome Matrix deployed.");
    }
}
