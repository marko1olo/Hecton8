# Rationale 1897

Static-only decision rationale.

`Data_TitaniumScrap` is canonical because the item asset owns the stable ID, is cataloged, is used by first-hour quest triggers/completion, is consumed by structural recipes, and is yielded by titanium resource nodes/harvestables.

`PFB_Resource_TitaniumScrap` is the canonical pickup route holder because `Data_TitaniumScrap.worldPrefab` points to its GUID. It is not visual-complete because static YAML shows a built-in primitive cube and `Mat_Resource_Scrap`, which is flat and lacks texture maps.

`Item_Titanium` is not canonical item identity because it also references `Data_TitaniumScrap`. It cannot be blindly deleted because bootstrap/scanner references still target it and it owns `resource.titanium_fragment` scan presentation.

`Data_Titanium` is rejected because no active asset/stable ID was found in scoped static search. The remaining exact load path in `FieldToolRuntimeSmokeTester.cs` is stale compatibility/test debt.

DataMonolith impact is unresolved by static text alone. Existing binary presence is not proof. Future data owner must bake/validate canonical `Data_TitaniumScrap` rows and reject `Data_Titanium` aliasing unless migration is explicit.
