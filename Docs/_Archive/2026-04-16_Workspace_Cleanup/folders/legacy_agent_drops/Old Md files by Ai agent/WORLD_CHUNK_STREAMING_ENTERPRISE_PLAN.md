Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# World Chunk Streaming Enterprise Plan

## Glavnaya tsel

Sdelat **odnu vzrosluyu chankovuyu arhitekturu dlya vsego mira**, a ne otdelnye kostyli:

- LOD relefa i bolshih kuskov mira
- flora
- oblomki
- resursy
- fauna
- postroyki igroka
- krupnye ugrozy

Prostymi slovami:
- mir `15 x 15 km` nelzya derzhat odnoy i toy zhe logikoy “vse ryadom s igrokom”
- u kazhdogo sloya mira dolzhen byt svoy rezhim zhizni
- no vse oni dolzhny zhit **po odnoy obschey setke i odnim obschim pravilam**

## Chto est seychas po faktu

U nas uzhe est horoshie kuski:

- `WorldStreamingDirector`
  - uzhe umeet podstraivat striming po glubine i skorosti
- `WorldSliceDirector`
  - uzhe umeet vklyuchat i oslablyat interesnye tochki mira
- `ScatterBudgetController`
  - uzhe upravlyaet byudzhetami resursov i kollayderov
- `ScavengePopulator`
  - uzhe realno zhivet po chankam
- `HectonRockManager`
  - uzhe derzhit kamni po chankam
- `FaunaDirector`
  - poka esche tolko koltso vokrug igroka, ne polnotsennye chanki

Prostymi slovami:
- fundament uzhe est
- no seychas eto esche ne odna vzroslaya sistema
- eto nabor silnyh, no chastichno razroznennyh kuskov

## Kakaya arhitektura nuzhna

### 1. Odna obschaya setka mira

Startovaya obschaya setka:

- razmer chanka: `192 x 192 m`
- vnutrennyaya kletka chanka: `64 x 64 m`
- razmer bolshoy zony dlya krupnyh ugroz: `768 m`

Zachem:
- resursy, flora, oblomki, postroyki i suschestva dolzhny govorit na odnom prostranstvennom yazyke
- inache u nas kazhdyy sloy budet zhit v svoem mire

### 2. Ne odin rezhim striminga, a 4 koltsa mira

#### Blizhnyaya polnaya simulyatsiya
- okolo `0-180 m`
- tut zhivut:
  - polnyy AI
  - kollaydery
  - interaktivnye resursy
  - blizhayshie postroyki
  - plotnaya flora

#### Srednyaya simulyatsiya
- okolo `180-420 m`
- tut zhivut:
  - uproschennye suschestva
  - uproschennye interaktivnye obekty
  - oblegchennye obnovleniya postroek

#### Dalnyaya vidimaya zona
- okolo `420-900 m`
- tut zhivut:
  - LOD
  - vizualnye proksi
  - dalnie kosyaki
  - siluety
  - dalnie krupnye orientiry

#### Dalnyaya zona dannyh
- okolo `900-1800 m`
- tut mir zhivet tolko kak dannye:
  - chto v chanke voobsche est
  - naskolko on zhivoy
  - naskolko on opasnyy
  - byl li on izmenen igrokom

### 3. U kazhdogo sloya mira svoya stoimost

#### Relef i bolshie kuski mira
- samyy vazhnyy vizualnyy sloy
- dolzhen gruzitsya ranshe vsego

#### Flora
- mozhet byt massovoy
- dolzhna imet dalnie i blizhnie rezhimy

#### Oblomki
- chast mozhet zhit kak instansy
- chast kak interaktivnye tochki

#### Resursy
- obyazany byt chankovymi
- obyazany uvazhat sohraneniya

#### Fauna
- massovaya zhizn ne dolzhna byt vsya na polnom AI

#### Postroyki
- dolzhny zhit po chankam tozhe
- no ne teryat sohranennoe sostoyanie

#### Krupnye ugrozy
- ne po malenkim chankam
- a po bolshim zonam

## Chto uzhe zalozheno kodom v etom bloke

Ya dobavil obschuyu osnovu:

- [WorldStreamingLayer.cs](C:/hades/Hecton8/Assets/_Project/Scripts/WorldStreamingLayer.cs)
- [WorldChunkCoordinate.cs](C:/hades/Hecton8/Assets/_Project/Scripts/WorldChunkCoordinate.cs)
- [WorldChunkStreamingProfile.cs](C:/hades/Hecton8/Assets/_Project/Scripts/WorldChunkStreamingProfile.cs)
- [WorldChunkStreamingAuthoring.cs](C:/hades/Hecton8/Assets/_Project/Scripts/Editor/WorldChunkStreamingAuthoring.cs)

Chto eto daet:
- edinyy spisok sloev striminga mira
- edinyy perevod mirovyh koordinat v chank
- edinyy asset-profil razmerov i kolets striminga
- editor-menyu dlya sborki profilya

## Blizhayshiy pravilnyy poryadok rabot

### Etap 1. Zafiksirovat obschiy profil mira

Sobrat asset:
- `WorldChunkStreamingProfile`

I sdelat ego istochnikom pravdy dlya:
- `WorldStreamingDirector`
- `ScatterBudgetController`
- sleduyuschih chankovyh sistem

### Etap 2. Perevesti faunu na realnye chanki

Ne “koltso vokrug igroka”, a:
- aktivnye chanki
- urovni simulyatsii
- bolshie zony krupnyh ugroz

### Etap 3. Perevesti resursy i interaktivnye tochki v obschuyu koordinatnuyu shemu

Ne lomaya tekuschiy `ScavengePopulator`, a vyrovnyat ego s obschey setkoy mira.

### Etap 4. Perevesti floru i oblomki na tu zhe shemu

Chtoby:
- dalnyaya vizualnaya massa zhila deshevo
- blizhniy sloy byl interaktivnee

### Etap 5. Podtyanut postroyki igroka

Postroyki dolzhny:
- normalno sohranyatsya
- podgruzhatsya chankami
- ne pytatsya zhit kak “vsegda aktivnyy ves mir”

## Chestnyy vyvod

### Chego nelzya delat

Nelzya delat:
- otdelnye chanki tolko dlya fauny
- otdelnye chanki tolko dlya resursov
- otdelnye radiusy tolko dlya postroek

Eto dast:
- kashu
- rassinhron
- tyazheluyu podderzhku

### Chto nado delat

Nado delat:
- odin obschiy yazyk mira
- odna setka
- odin profil
- raznye rezhimy dlya raznyh sloev

Prostymi slovami:
- mir dolzhen byt ne prosto bolshim
- a bolshim **sistemno**
