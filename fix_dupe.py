with open("Assets/_Project/Scripts/Editor/BiomeMatrixBootstrapAuthoring.cs", "r") as f:
    content = f.read()

# Find the first definition of BiomeFamilyData
start_dupe = content.find("        private class BiomeFamilyData")
# Find the end of FamilyDataMap (which is right before the second ApplyFamilyTemplate maybe?)
end_dupe = content.find("        private static void ApplyFamilyTemplate", start_dupe)

# Wait, wait, let's just restore the file and patch it properly.
