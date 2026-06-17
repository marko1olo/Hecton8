using System;
using UnityEngine;

namespace Hecton8.Editor
{
    internal static class WorldProceduralFloraProxyShapeBuilder
    {
        private enum KelpTallVariant
        {
            Stalk,
            Lean,
            Ribbon
        }

        private enum KelpPatchVariant
        {
            Patch,
            Grove,
            Ring
        }

        private enum KelpCanopyVariant
        {
            Crown,
            Frond,
            Fan
        }

        private enum CoralLowVariant
        {
            Bed,
            Plate,
            Knoll
        }

        private enum CoralBranchingVariant
        {
            Branch,
            Mass,
            Fan
        }

        private enum CoralMassiveVariant
        {
            Head,
            Porous,
            Boulder
        }

        private enum CoralPlateVariant
        {
            Ledge,
            Shelf,
            Stack
        }

        public static bool TryBuild(string rootName, Vector3 scale, Material material, out GameObject root)
        {
            root = null;
            if (string.IsNullOrWhiteSpace(rootName))
                return false;

            if (rootName.StartsWith("family_kelp_tall__", StringComparison.Ordinal))
            {
                root = new GameObject($"PFB_{rootName}");
                BuildKelpTallProxy(root.transform, scale, material, ResolveKelpTallVariant(rootName));
                return true;
            }

            if (rootName.StartsWith("family_kelp_patch_dense__", StringComparison.Ordinal))
            {
                root = new GameObject($"PFB_{rootName}");
                BuildKelpPatchProxy(root.transform, scale, material, ResolveKelpPatchVariant(rootName));
                return true;
            }

            if (rootName.StartsWith("family_kelp_canopy__", StringComparison.Ordinal))
            {
                root = new GameObject($"PFB_{rootName}");
                BuildKelpCanopyProxy(root.transform, scale, material, ResolveKelpCanopyVariant(rootName));
                return true;
            }

            if (rootName.StartsWith("family_coral_low__", StringComparison.Ordinal))
            {
                root = new GameObject($"PFB_{rootName}");
                BuildCoralLowProxy(root.transform, scale, material, ResolveCoralLowVariant(rootName));
                return true;
            }

            if (rootName.StartsWith("family_coral_branching__", StringComparison.Ordinal))
            {
                root = new GameObject($"PFB_{rootName}");
                BuildCoralBranchingProxy(root.transform, scale, material, ResolveCoralBranchingVariant(rootName));
                return true;
            }

            if (rootName.StartsWith("family_coral_massive__", StringComparison.Ordinal))
            {
                root = new GameObject($"PFB_{rootName}");
                BuildCoralMassiveProxy(root.transform, scale, material, ResolveCoralMassiveVariant(rootName));
                return true;
            }

            if (rootName.StartsWith("family_coral_plate__", StringComparison.Ordinal))
            {
                root = new GameObject($"PFB_{rootName}");
                BuildCoralPlateProxy(root.transform, scale, material, ResolveCoralPlateVariant(rootName));
                return true;
            }

            return false;
        }

        private static void BuildKelpTallProxy(Transform root, Vector3 scale, Material material, KelpTallVariant variant)
        {
            bool leaning = variant != KelpTallVariant.Stalk;
            Quaternion stipeRotation = leaning
                ? Quaternion.Euler(0f, 0f, variant == KelpTallVariant.Ribbon ? 24f : 16f)
                : Quaternion.identity;
            Vector3 stipePosition = leaning
                ? new Vector3(variant == KelpTallVariant.Ribbon ? 0.2f : 0.14f, 1.9f, 0f)
                : new Vector3(0f, 1.9f, 0f);

            Vector3 stipeScale = variant == KelpTallVariant.Ribbon
                ? new Vector3(scale.x * 0.86f, scale.y * 1.12f, scale.z * 0.82f)
                : scale;

            AddPrimitiveChild(root, PrimitiveType.Cylinder, stipePosition, stipeScale, material, stipeRotation);
            AddKelpBaseRibs(root, material, leaning ? 0.06f : 0f, 0f, scale);
            AddKelpBasalBlades(root, material, new Vector3(0f, 0.65f, 0f), 1.05f);
            AddKelpMidFronds(root, material, new Vector3(leaning ? 0.12f : 0f, 2.3f, 0f), 1f, leaning ? 22f : 0f);

            if (variant == KelpTallVariant.Ribbon)
            {
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cube,
                    new Vector3(-0.18f, 3.48f, 0.04f),
                    new Vector3(scale.x * 1.62f, scale.y * 0.08f, scale.z * 0.18f),
                    material,
                    Quaternion.Euler(0f, 14f, 88f));
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cube,
                    new Vector3(0.24f, 4.02f, -0.02f),
                    new Vector3(scale.x * 1.82f, scale.y * 0.08f, scale.z * 0.16f),
                    material,
                    Quaternion.Euler(0f, -12f, 102f));
            }
        }

        private static void BuildKelpPatchProxy(Transform root, Vector3 scale, Material material, KelpPatchVariant variant)
        {
            bool grove = variant == KelpPatchVariant.Grove;
            float clusterScale = grove ? 1.08f : 0.92f;
            AddKelpStalkWithFronds(root, material, new Vector3(0f, 0f, 0f), scale * clusterScale, 0f, 1.05f, true);
            AddKelpStalkWithFronds(root, material, new Vector3(-0.92f, 0f, 0.48f), scale * 0.82f, -10f, 0.86f, false);
            AddKelpStalkWithFronds(root, material, new Vector3(0.86f, 0f, -0.44f), scale * 0.76f, 14f, 0.78f, false);

            if (grove)
            {
                AddKelpStalkWithFronds(root, material, new Vector3(-0.28f, 0f, -0.94f), scale * 0.7f, -18f, 0.72f, false);
                AddKelpStalkWithFronds(root, material, new Vector3(0.54f, 0f, 0.88f), scale * 0.68f, 21f, 0.7f, false);
            }

            if (variant == KelpPatchVariant.Ring)
            {
                AddKelpStalkWithFronds(root, material, new Vector3(-0.64f, 0f, -0.76f), scale * 0.64f, -24f, 0.66f, false);
                AddKelpStalkWithFronds(root, material, new Vector3(0.74f, 0f, 0.72f), scale * 0.6f, 26f, 0.64f, false);
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cylinder,
                    new Vector3(0f, 0.08f, 0f),
                    new Vector3(scale.x * 1.58f, scale.y * 0.05f, scale.z * 1.34f),
                    material,
                    Quaternion.Euler(90f, 0f, 0f));
            }
        }

        private static void BuildKelpCanopyProxy(Transform root, Vector3 scale, Material material, KelpCanopyVariant variant)
        {
            bool frondOnly = variant == KelpCanopyVariant.Frond;
            if (!frondOnly)
            {
                AddPrimitiveChild(root, PrimitiveType.Cylinder, new Vector3(0f, 2.2f, 0f), scale, material, Quaternion.identity);
                AddKelpBaseRibs(root, material, 0f, 0f, scale);
            }

            AddPrimitiveChild(
                root,
                PrimitiveType.Cube,
                new Vector3(0f, 4.5f, 0f),
                new Vector3(scale.x * 2.8f, scale.y * 0.08f, scale.z * 0.44f),
                material,
                Quaternion.Euler(0f, 0f, 8f));
            AddPrimitiveChild(
                root,
                PrimitiveType.Cube,
                new Vector3(-0.78f, 4.25f, 0.12f),
                new Vector3(scale.x * 2f, scale.y * 0.08f, scale.z * 0.36f),
                material,
                Quaternion.Euler(0f, 24f, -24f));
            AddPrimitiveChild(
                root,
                PrimitiveType.Cube,
                new Vector3(0.92f, 4.1f, -0.1f),
                new Vector3(scale.x * 1.9f, scale.y * 0.08f, scale.z * 0.34f),
                material,
                Quaternion.Euler(0f, -18f, 22f));
            AddPrimitiveChild(
                root,
                PrimitiveType.Sphere,
                new Vector3(0.22f, 4.2f, 0f),
                new Vector3(scale.x * 0.34f, scale.y * 0.14f, scale.z * 0.34f),
                material,
                Quaternion.identity);

            if (frondOnly)
            {
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cube,
                    new Vector3(0f, 2.8f, 0f),
                    new Vector3(scale.x * 1.45f, scale.y * 0.08f, scale.z * 0.24f),
                    material,
                    Quaternion.Euler(0f, 12f, 54f));
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cube,
                    new Vector3(0.34f, 3.2f, 0f),
                    new Vector3(scale.x * 1.2f, scale.y * 0.08f, scale.z * 0.22f),
                    material,
                    Quaternion.Euler(0f, -8f, 34f));
            }

            if (variant == KelpCanopyVariant.Fan)
            {
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cube,
                    new Vector3(-1.18f, 4.56f, 0.14f),
                    new Vector3(scale.x * 2.26f, scale.y * 0.08f, scale.z * 0.3f),
                    material,
                    Quaternion.Euler(0f, 34f, -28f));
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cube,
                    new Vector3(1.12f, 4.48f, -0.18f),
                    new Vector3(scale.x * 2.18f, scale.y * 0.08f, scale.z * 0.28f),
                    material,
                    Quaternion.Euler(0f, -30f, 26f));
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cube,
                    new Vector3(0.12f, 3.46f, 0.08f),
                    new Vector3(scale.x * 1.72f, scale.y * 0.08f, scale.z * 0.2f),
                    material,
                    Quaternion.Euler(0f, 8f, 58f));
            }
        }

        private static void BuildCoralLowProxy(Transform root, Vector3 scale, Material material, CoralLowVariant variant)
        {
            AddPrimitiveChild(
                root,
                PrimitiveType.Sphere,
                new Vector3(0f, scale.y * 0.28f, 0f),
                new Vector3(scale.x * 1.18f, scale.y * 0.62f, scale.z * 1.12f),
                material,
                Quaternion.identity);
            AddPrimitiveChild(
                root,
                PrimitiveType.Sphere,
                new Vector3(-0.42f * scale.x, scale.y * 0.18f, 0.24f * scale.z),
                new Vector3(scale.x * 0.74f, scale.y * 0.4f, scale.z * 0.68f),
                material,
                Quaternion.Euler(0f, 18f, 0f));
            AddPrimitiveChild(
                root,
                PrimitiveType.Sphere,
                new Vector3(0.38f * scale.x, scale.y * 0.16f, -0.28f * scale.z),
                new Vector3(scale.x * 0.66f, scale.y * 0.34f, scale.z * 0.62f),
                material,
                Quaternion.Euler(0f, -12f, 0f));

            if (variant == CoralLowVariant.Plate)
            {
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cylinder,
                    new Vector3(0.08f, scale.y * 0.22f, 0.04f),
                    new Vector3(scale.x * 0.92f, scale.y * 0.08f, scale.z * 0.92f),
                    material,
                    Quaternion.Euler(0f, 0f, 10f));
            }

            if (variant == CoralLowVariant.Knoll)
            {
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Sphere,
                    new Vector3(0f, scale.y * 0.42f, 0.12f * scale.z),
                    new Vector3(scale.x * 0.58f, scale.y * 0.38f, scale.z * 0.56f),
                    material,
                    Quaternion.identity);
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Sphere,
                    new Vector3(0.18f * scale.x, scale.y * 0.34f, -0.18f * scale.z),
                    new Vector3(scale.x * 0.44f, scale.y * 0.28f, scale.z * 0.42f),
                    material,
                    Quaternion.identity);
            }
        }

        private static void BuildCoralBranchingProxy(Transform root, Vector3 scale, Material material, CoralBranchingVariant variant)
        {
            AddPrimitiveChild(
                root,
                PrimitiveType.Cylinder,
                new Vector3(0f, scale.y * 0.44f, 0f),
                new Vector3(scale.x * 0.46f, scale.y * 0.78f, scale.z * 0.46f),
                material,
                Quaternion.identity);
            AddCoralBranch(root, material, new Vector3(-0.22f * scale.x, scale.y * 0.98f, 0.08f), new Vector3(scale.x * 0.24f, scale.y * 0.54f, scale.z * 0.24f), -32f, -18f);
            AddCoralBranch(root, material, new Vector3(0.18f * scale.x, scale.y * 1.04f, -0.06f), new Vector3(scale.x * 0.22f, scale.y * 0.58f, scale.z * 0.22f), 28f, 14f);
            AddCoralBranch(root, material, new Vector3(0.02f, scale.y * 1.18f, 0.18f * scale.z), new Vector3(scale.x * 0.18f, scale.y * 0.44f, scale.z * 0.18f), 12f, -26f);

            if (variant == CoralBranchingVariant.Mass)
            {
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Sphere,
                    new Vector3(0f, scale.y * 0.52f, 0f),
                    new Vector3(scale.x * 0.9f, scale.y * 0.42f, scale.z * 0.9f),
                    material,
                    Quaternion.identity);
            }

            if (variant == CoralBranchingVariant.Fan)
            {
                AddCoralBranch(root, material, new Vector3(-0.4f * scale.x, scale.y * 0.82f, -0.1f), new Vector3(scale.x * 0.16f, scale.y * 0.46f, scale.z * 0.16f), -8f, -46f);
                AddCoralBranch(root, material, new Vector3(0.42f * scale.x, scale.y * 0.86f, 0.1f), new Vector3(scale.x * 0.16f, scale.y * 0.48f, scale.z * 0.16f), 6f, 44f);
            }
        }

        private static void BuildCoralMassiveProxy(Transform root, Vector3 scale, Material material, CoralMassiveVariant variant)
        {
            AddPrimitiveChild(
                root,
                PrimitiveType.Sphere,
                new Vector3(0f, scale.y * 0.34f, 0f),
                new Vector3(scale.x * 1.28f, scale.y * 0.9f, scale.z * 1.22f),
                material,
                Quaternion.identity);
            AddPrimitiveChild(
                root,
                PrimitiveType.Sphere,
                new Vector3(-0.34f * scale.x, scale.y * 0.22f, 0.26f * scale.z),
                new Vector3(scale.x * 0.72f, scale.y * 0.46f, scale.z * 0.7f),
                material,
                Quaternion.Euler(0f, 18f, 0f));
            AddPrimitiveChild(
                root,
                PrimitiveType.Sphere,
                new Vector3(0.42f * scale.x, scale.y * 0.2f, -0.18f * scale.z),
                new Vector3(scale.x * 0.62f, scale.y * 0.4f, scale.z * 0.6f),
                material,
                Quaternion.Euler(0f, -12f, 0f));

            if (variant == CoralMassiveVariant.Porous)
            {
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cube,
                    new Vector3(0.18f * scale.x, scale.y * 0.44f, 0.06f),
                    new Vector3(scale.x * 0.34f, scale.y * 0.18f, scale.z * 0.24f),
                    material,
                    Quaternion.Euler(0f, 22f, 18f));
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cube,
                    new Vector3(-0.22f * scale.x, scale.y * 0.3f, -0.16f),
                    new Vector3(scale.x * 0.28f, scale.y * 0.16f, scale.z * 0.22f),
                    material,
                    Quaternion.Euler(12f, -16f, -12f));
            }

            if (variant == CoralMassiveVariant.Boulder)
            {
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Sphere,
                    new Vector3(0f, scale.y * 0.62f, -0.04f),
                    new Vector3(scale.x * 0.82f, scale.y * 0.42f, scale.z * 0.78f),
                    material,
                    Quaternion.identity);
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Sphere,
                    new Vector3(-0.18f * scale.x, scale.y * 0.48f, 0.24f * scale.z),
                    new Vector3(scale.x * 0.48f, scale.y * 0.24f, scale.z * 0.46f),
                    material,
                    Quaternion.identity);
            }
        }

        private static void BuildCoralPlateProxy(Transform root, Vector3 scale, Material material, CoralPlateVariant variant)
        {
            AddPrimitiveChild(
                root,
                PrimitiveType.Cylinder,
                new Vector3(0f, scale.y * 0.2f, 0f),
                new Vector3(scale.x * 0.34f, scale.y * 0.36f, scale.z * 0.34f),
                material,
                Quaternion.identity);
            AddPrimitiveChild(
                root,
                PrimitiveType.Cylinder,
                new Vector3(0f, scale.y * 0.58f, 0f),
                new Vector3(scale.x * 1.34f, scale.y * 0.08f, scale.z * 1.22f),
                material,
                Quaternion.Euler(0f, 0f, 6f));
            AddPrimitiveChild(
                root,
                PrimitiveType.Cylinder,
                new Vector3(-0.24f * scale.x, scale.y * 0.9f, 0.18f * scale.z),
                new Vector3(scale.x * 0.98f, scale.y * 0.07f, scale.z * 0.9f),
                material,
                Quaternion.Euler(8f, 0f, -18f));

            if (variant == CoralPlateVariant.Shelf)
            {
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cylinder,
                    new Vector3(0.34f * scale.x, scale.y * 1.14f, -0.12f * scale.z),
                    new Vector3(scale.x * 0.86f, scale.y * 0.06f, scale.z * 0.74f),
                    material,
                    Quaternion.Euler(-10f, 0f, 22f));
            }

            if (variant == CoralPlateVariant.Stack)
            {
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cylinder,
                    new Vector3(0.08f * scale.x, scale.y * 1.22f, 0.08f * scale.z),
                    new Vector3(scale.x * 0.76f, scale.y * 0.05f, scale.z * 0.7f),
                    material,
                    Quaternion.Euler(-14f, 0f, 12f));
                AddPrimitiveChild(
                    root,
                    PrimitiveType.Cylinder,
                    new Vector3(-0.12f * scale.x, scale.y * 1.42f, -0.14f * scale.z),
                    new Vector3(scale.x * 0.58f, scale.y * 0.04f, scale.z * 0.52f),
                    material,
                    Quaternion.Euler(10f, 0f, -20f));
            }
        }

        private static KelpTallVariant ResolveKelpTallVariant(string rootName)
        {
            if (rootName.EndsWith("__lean", StringComparison.Ordinal))
                return KelpTallVariant.Lean;

            if (rootName.EndsWith("__ribbon", StringComparison.Ordinal))
                return KelpTallVariant.Ribbon;

            return KelpTallVariant.Stalk;
        }

        private static KelpPatchVariant ResolveKelpPatchVariant(string rootName)
        {
            if (rootName.EndsWith("__patch_tall", StringComparison.Ordinal) || rootName.EndsWith("__grove", StringComparison.Ordinal))
                return KelpPatchVariant.Grove;

            if (rootName.EndsWith("__ring", StringComparison.Ordinal))
                return KelpPatchVariant.Ring;

            return KelpPatchVariant.Patch;
        }

        private static KelpCanopyVariant ResolveKelpCanopyVariant(string rootName)
        {
            if (rootName.EndsWith("__frond", StringComparison.Ordinal))
                return KelpCanopyVariant.Frond;

            if (rootName.EndsWith("__fan", StringComparison.Ordinal))
                return KelpCanopyVariant.Fan;

            return KelpCanopyVariant.Crown;
        }

        private static CoralLowVariant ResolveCoralLowVariant(string rootName)
        {
            if (rootName.EndsWith("__plate", StringComparison.Ordinal))
                return CoralLowVariant.Plate;

            if (rootName.EndsWith("__knoll", StringComparison.Ordinal))
                return CoralLowVariant.Knoll;

            return CoralLowVariant.Bed;
        }

        private static CoralBranchingVariant ResolveCoralBranchingVariant(string rootName)
        {
            if (rootName.EndsWith("__mass", StringComparison.Ordinal))
                return CoralBranchingVariant.Mass;

            if (rootName.EndsWith("__fan", StringComparison.Ordinal))
                return CoralBranchingVariant.Fan;

            return CoralBranchingVariant.Branch;
        }

        private static CoralMassiveVariant ResolveCoralMassiveVariant(string rootName)
        {
            if (rootName.EndsWith("__porous", StringComparison.Ordinal))
                return CoralMassiveVariant.Porous;

            if (rootName.EndsWith("__boulder", StringComparison.Ordinal))
                return CoralMassiveVariant.Boulder;

            return CoralMassiveVariant.Head;
        }

        private static CoralPlateVariant ResolveCoralPlateVariant(string rootName)
        {
            if (rootName.EndsWith("__shelf", StringComparison.Ordinal))
                return CoralPlateVariant.Shelf;

            if (rootName.EndsWith("__stack", StringComparison.Ordinal))
                return CoralPlateVariant.Stack;

            return CoralPlateVariant.Ledge;
        }

        private static void AddCoralBranch(Transform root, Material material, Vector3 localPosition, Vector3 localScale, float tiltX, float tiltZ)
        {
            AddPrimitiveChild(
                root,
                PrimitiveType.Cylinder,
                localPosition,
                localScale,
                material,
                Quaternion.Euler(tiltX, 0f, tiltZ));
            AddPrimitiveChild(
                root,
                PrimitiveType.Sphere,
                localPosition + new Vector3(0f, localScale.y * 0.44f, 0f),
                new Vector3(localScale.x * 1.2f, localScale.y * 0.24f, localScale.z * 1.2f),
                material,
                Quaternion.identity);
        }

        private static void AddKelpStalkWithFronds(
            Transform root,
            Material material,
            Vector3 baseOffset,
            Vector3 stipeScale,
            float zRotation,
            float frondScale,
            bool addBasalBlades)
        {
            Quaternion stipeRotation = Mathf.Abs(zRotation) > 0.01f
                ? Quaternion.Euler(0f, 0f, zRotation)
                : Quaternion.identity;
            AddPrimitiveChild(root, PrimitiveType.Cylinder, baseOffset + new Vector3(0f, stipeScale.y * 0.5f, 0f), stipeScale, material, stipeRotation);
            AddKelpBaseRibs(root, material, baseOffset.x, baseOffset.z, stipeScale);

            if (addBasalBlades)
                AddKelpBasalBlades(root, material, baseOffset + new Vector3(0f, 0.45f, 0f), frondScale);

            AddKelpMidFronds(root, material, baseOffset + new Vector3(0f, stipeScale.y * 0.64f, 0f), frondScale, zRotation);
        }

        private static void AddKelpBaseRibs(Transform root, Material material, float centerX, float centerZ, Vector3 stipeScale)
        {
            Vector3 ribScale = new Vector3(stipeScale.x * 0.38f, Mathf.Max(0.22f, stipeScale.y * 0.16f), stipeScale.z * 0.38f);
            AddPrimitiveChild(root, PrimitiveType.Cylinder, new Vector3(centerX - stipeScale.x * 0.36f, ribScale.y * 0.55f, centerZ), ribScale, material, Quaternion.identity);
            AddPrimitiveChild(root, PrimitiveType.Cylinder, new Vector3(centerX + stipeScale.x * 0.36f, ribScale.y * 0.5f, centerZ - stipeScale.z * 0.08f), ribScale, material, Quaternion.Euler(0f, 18f, 0f));
            AddPrimitiveChild(root, PrimitiveType.Cylinder, new Vector3(centerX, ribScale.y * 0.45f, centerZ + stipeScale.z * 0.34f), ribScale * 0.9f, material, Quaternion.Euler(0f, -24f, 0f));
        }

        private static void AddKelpBasalBlades(Transform root, Material material, Vector3 center, float scale)
        {
            Vector3 bladeScale = new Vector3(1.1f * scale, 0.08f, 0.22f * scale);
            AddPrimitiveChild(root, PrimitiveType.Cube, center + new Vector3(-0.42f * scale, 0.12f, 0f), bladeScale, material, Quaternion.Euler(0f, 16f, 42f));
            AddPrimitiveChild(root, PrimitiveType.Cube, center + new Vector3(0.45f * scale, 0.18f, 0.08f), bladeScale * 0.94f, material, Quaternion.Euler(0f, -18f, -36f));
            AddPrimitiveChild(root, PrimitiveType.Cube, center + new Vector3(0.04f, 0.16f, -0.32f * scale), bladeScale * 0.88f, material, Quaternion.Euler(18f, 0f, 18f));
        }

        private static void AddKelpMidFronds(Transform root, Material material, Vector3 center, float scale, float tiltZ)
        {
            AddPrimitiveChild(root, PrimitiveType.Cube, center + new Vector3(-0.54f * scale, 0.58f * scale, 0f), new Vector3(1.5f * scale, 0.08f, 0.2f * scale), material, Quaternion.Euler(0f, 8f, -58f + tiltZ * 0.4f));
            AddPrimitiveChild(root, PrimitiveType.Cube, center + new Vector3(0.62f * scale, 0.88f * scale, -0.06f), new Vector3(1.7f * scale, 0.08f, 0.2f * scale), material, Quaternion.Euler(0f, -10f, 52f + tiltZ * 0.35f));
            AddPrimitiveChild(root, PrimitiveType.Cube, center + new Vector3(0.1f, 1.34f * scale, 0.08f), new Vector3(1.38f * scale, 0.08f, 0.18f * scale), material, Quaternion.Euler(0f, 4f, 74f + tiltZ * 0.25f));
            AddPrimitiveChild(root, PrimitiveType.Sphere, center + new Vector3(-0.08f, 0.92f * scale, 0f), new Vector3(0.16f * scale, 0.16f * scale, 0.16f * scale), material, Quaternion.identity);
            AddPrimitiveChild(root, PrimitiveType.Sphere, center + new Vector3(0.2f * scale, 1.26f * scale, 0f), new Vector3(0.14f * scale, 0.14f * scale, 0.14f * scale), material, Quaternion.identity);
        }

        private static void AddPrimitiveChild(Transform parent, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Material material, Quaternion localRotation)
        {
            GameObject child = GameObject.CreatePrimitive(primitive);
            child.name = primitive.ToString();
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = localRotation;
            child.transform.localScale = localScale;

            if (child.TryGetComponent(out Collider collider))
                UnityEngine.Object.DestroyImmediate(collider);

            if (child.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;
        }
    }
}
