# HECTON-8 Geology Production Plan
Date: 2026-05-01
Status: DEPRECATED

## Korotko
Gotovye geology-prefaby ne trogaem i ne udalyaem. Oni ostayutsya v proekte kak fallback.

Osnovnoy production-path:
- kodom generyatsya novye geology meshes
- iz nih sobirayutsya novye final prefabs
- na nih naznachaetsya suschestvuyuschiy triplanar rock shader/material
- family assets nachinayut ssylatsya na novye generated finals
- starye gotovye prefaby ostayutsya zapasnym putem i ne lomayutsya

## Zhestkie pravila
- Ne perepisyvat i ne udalyat `Forest_Rock_Shelf`, `Mossy_Forest_Rock`, `Nordic_Beach_Rock`, `Nordic_Beach_Rock_Formation`, `Rock_Skala`.
- Ne schitat starye gotovye prefaby osnovnym putem.
- Ne pisat validator/report/status markdown.
- Ne pisat editor tool, kotoryy tolko proveryaet.
- Ne delat runtime-first. Tsel: zaranee sgenerennye production assets v proekte.
- Ne delat UV workflow. Triplanar obyazatelen.
- Ne delat placeholder meshes.
- Esli ne hvataet vneshnego resursa, ostanovka tolko po blocked-formatu.

## Owner-sistema
### Osnovnye vladeltsy
- `WorldProceduralGeologyFinalAuthoring`
  Glavnyy authoring owner. Sozdaet realnye mesh assets i prefab assets.
- `WorldProceduralGeologyProfileAuthoring`
  Derzhit profiles dlya vseh geology categories.
- `WorldProceduralFinalVariantAuthoring`
  Privyazyvaet generated finals k family assets.
- `WorldProceduralProxyAuthoring`
  Ostaetsya vladeltsem family/rule/proxy bootstrap. Cherez nego dobavlyaetsya shelf/cliff family.

### Novyy fayl
- `WorldProceduralGeologyMeshBuilder`
  Chistyy production builder, kotoryy stroit mesh data dlya geology finals.

## Chto imenno sozdaetsya
### Kategorii
- `family.rock.small_floor`
- `family.rock.cluster.medium`
- `family.rock.arch.large`
- `family.cave.entrance`
- `family.landmark.spire`
- `family.rock.shelf.large`

### Kolichestvo generated finals
- Small floor rocks: 10
- Medium clusters: 10
- Shelf / cliff large: 8
- Rock arch large: 6
- Cave entrance: 6
- Landmark spire: 6

### Chto est u kazhdogo generated asset
- LOD0 mesh
- LOD1 mesh
- LOD2 mesh
- collider setup
- assigned triplanar rock material
- saved prefab in project
- linked final variant in family asset

## Gde lezhat production assets
### Meshes
- `Assets/_Project/Art/Meshes/WorldProceduralGeology/`

Papki:
- `RockSmallFloor`
- `RockClusterMedium`
- `RockShelfLarge`
- `RockArchLarge`
- `CaveEntrance`
- `LandmarkSpire`

### Prefabs
- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/`

Starye gotovye prefaby ostayutsya na meste. Novye generated finals lezhat ryadom, otdelnymi prefab assets.

### Materials
- odin obschiy generated geology material na baze `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat`
- libo nabor category materials na tom zhe shader path
- novyy shader ne sozdaetsya

## Kak rabotaet mesh builder
### Obschiy printsip
Builder ne delaet odin bazovyy siluet. On stroit slozhnuyu formu srazu iz neskolkih protsedurnyh mass i detaley.

Na kazhdyy obekt:
- glavnyy obem
- vtorichnye massy
- skoly
- polki
- treschinnye rebra
- vystupy
- asimmetriya
- shumovaya deformatsiya
- layered erosion look

### Po kategoriyam
#### Small floor rocks
- nizkie, tyazhelye, chitaemye sverhu
- ne prevraschayutsya v odinakovye bulyzhniki
- 10 raznyh form
- kontrol po vershinam: ne razduvat sverh nuzhnogo

#### Medium clusters
- 2-5 svyazannyh mass
- cover silhouette
- raznye osi naklona
- chitaemaya massa bez musornoy melochi

#### Shelf / cliff large
- bolshie ustupy
- sloistye svesy
- cliff face
- shelf extension
- tyazhelaya bokovaya massa
- otdelnaya production family, ne surrogat arch/cluster

#### Rock arch large
- dve tyazhelye opory
- verhniy most
- asimmetriya
- fracture detail
- underside detail
- 6 raznyh tipov, ne odna i ta zhe arka

#### Cave entrance
- chitaemyy vhod
- bokovye guby
- verhniy svod
- glubina vhoda
- vneshniy debris/seam
- neskolko raznyh form portala

#### Landmark spire
- silnyy dalniy siluet
- massivnaya baza
- suzhenie vverh
- vtorichnye vystupy
- ne prevraschat v prostoy stolb

## LOD kontrakt
Dlya vseh generated geology finals:
- 3 visible LOD
- thresholds: `0.6 / 0.15 / 0.04`
- `CrossFade` vklyuchen
- LOD1 rezhet srednyuyu meloch
- LOD2 derzhit tolko glavnyy siluet

Otdelnye pravila:
- arka na LOD2 ostaetsya arkoy
- cave entrance na LOD2 ostaetsya vhodom
- shelf/cliff na LOD2 ostaetsya ustupom
- spire na LOD2 ostaetsya shpilem

## Kollizii
### Small floor
- 1-3 primitive colliders maksimum

### Cluster medium
- 2-3 primitive colliders

### Shelf / cliff
- uproschennye kollizii pod poverhnost i kray

### Arch
- kolliziya ne dolzhna ubivat prohod pod arkoy

### Cave entrance
- kolliziya ne dolzhna zakryvat vhod

### Spire
- grubaya prostaya kolliziya

Obschee pravilo:
- ne stavit `MeshCollider` na LOD0

## Material i sheyder
Ispolzuetsya suschestvuyuschiy:
- `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat`

Chto delaetsya:
- sozdaetsya managed generated geology material stack na etom shader path
- instancing vklyuchaetsya tam, gde eto nuzhno
- novogo parallel shader pipeline net
- UV unwrap net

## Family / rule linkage
### Chto menyaetsya
- `family.rock.small_floor` perevoditsya na generated finals
- `family.rock.cluster.medium` perevoditsya na generated finals
- `family.rock.arch.large` poluchaet neskolko generated finals
- `family.cave.entrance` poluchaet neskolko generated finals
- `family.landmark.spire` poluchaet neskolko generated finals
- dobavlyaetsya `family.rock.shelf.large`

### Chto ne menyaetsya
- starye gotovye prefabs ostayutsya fallback
- proxy path ostaetsya
- suschestvuyuschie ruchnye bolshie prefabs ne lomayutsya

## Poryadok raboty bez raspyleniya
### Etap 1
Sdelat `WorldProceduralGeologyMeshBuilder`
- odin builder
- vse 6 categories
- generatsiya LOD0/1/2

### Etap 2
Peredelat `WorldProceduralGeologyFinalAuthoring`
- ubrat sborku geology iz gotovyh kamney kak osnovnoy put
- sozdavat mesh assets + prefab assets
- srazu naznachat material + colliders + LODGroup

### Etap 3
Rasshirit `WorldProceduralGeologyProfileAuthoring`
- small
- cluster
- shelf
- arch
- cave
- spire

### Etap 4
Rasshirit `WorldProceduralFinalVariantAuthoring`
- vse generated finals privyazat k family assets
- starye ruchnye assets ne udalyat

### Etap 5
Rasshirit `WorldProceduralProxyAuthoring`
- dobavit `family.rock.shelf.large`
- dobavit rule dlya shelf/cliff geology
- ostavit staroe kak fallback path

### Etap 6
Sgenerirovat realnye project assets
Rezultat fizicheski lezhit v proekte:
- meshes
- prefabs
- materials if needed
- updated family links

## Chto schitaetsya itogom raboty
Sdelano tolko esli est:
- realnye mesh assets v proekte
- realnye generated prefabs v proekte
- material naznachen
- family assets ukazyvayut na generated finals
- starye gotovye prefabs ostalis kak fallback
- shelf/cliff geology poyavilsya kak production asset line

## Chto ostaetsya PENDING VERIFICATION
- shader compile inside Unity
- fakticheskiy vizual v stsene
- collider behavior dlya arch/cave
- GPUI registration where needed
- MapMagic scatter activation where needed
- profiler/log evidence

## Zafiksirovannoe reshenie
- suschestvuyuschie gotovye geology prefabs ne lomaem
- ne zamenyaem ih udaleniem
- oni ostayutsya fallback
- osnovnoy production put: novye generated geology finals kodom
- scope ne raspylyaetsya: odin builder, odin authoring path, shest geology categories, zaranee sohranennye assets
