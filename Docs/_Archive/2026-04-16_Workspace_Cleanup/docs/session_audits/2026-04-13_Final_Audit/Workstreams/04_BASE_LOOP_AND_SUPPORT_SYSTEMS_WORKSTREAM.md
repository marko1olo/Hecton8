Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — Base Loop / Support Systems Workstream

Data: 2026-04-13  
Status: PENDING VERIFICATION

## Chto zakryvaet etot front

- Return loop
- Base value
- Crafting / storage / power / oxygen support
- Support systems that make survival loop matter

## Pochemu eto vazhno

Esli baza, fabrikatsiya i support systems suschestvuyut tolko kak nabor otdelnyh mehanik, igra ne skleivaetsya v ustoychivyy tsikl.

## Osnovnye zadachi

### Front A. Return value

- Zafiksirovat, zachem igrok vozvraschaetsya na bazu.
- Sdelat bazu mestom recovery, planning, crafting i progression.

### Front B. Oxygen / refill / safety loop

- Proverit oxygen refill path.
- Proverit safe recovery route.
- Proverit failure feedback.

### Front C. Crafting / storage / power cohesion

- Svyazat kraft, storage, repair, power i upgrades v odin ponyatnyy tsikl.
- Ubrat sostoyaniya, gde sistemy formalno est, no player value ne dayut.

### Front D. Save / world state continuity

- Proverit, chto support loop perezhivaet save/load.
- Proverit reload posle mid-loop progress.

## Candidate owners

- `Assets/_Project/Scripts/SaveManager.cs`
- Player survival / inventory / builder / fabrication owners v `Assets/_Project/Scripts/Gameplay`
- base/support owners v `Assets/_Project/Scripts/Building`, `Crafting`, `Power`, `Inventory`

## Do-Not-Touch Scope

- Ne lezt v shell/UI krome tochek vyzova.
- Ne avtorit narrative content zdes.
- Ne smeshivat s heavy perf work.

## Kak drobit po agentam

Agent 1:
- oxygen / survival / refill path
- Zadacha: survival support loop.

Agent 2:
- crafting / storage / inventory owners
- Zadacha: return value i support cohesion.

Agent 3:
- save continuity po support systems
- Zadacha: proverka sohraneniya i vosstanovleniya tsikla.

## Expected Result

- U igroka poyavlyaetsya yasnaya prichina vozvraschatsya.
- Baza perestaet byt dekorativnoy sistemoy.
- Support loop skleivaetsya s progression.

## Exit Criteria

- Est rabochiy tsikl: explore -> gather -> return -> recover/craft/upgrade -> go deeper.
- Net kriticheskih razryvov posle save/load.
