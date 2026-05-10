Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# AI Fauna Chunk Streaming Plan

## Chto est seychas po faktu

- `FaunaDirector` uzhe umeet spavnit i udalyat suschestv vokrug igroka.
- No seychas eto ne nastoyaschiy chankovyy mir.
- Seychas eto koltso vokrug igroka:
  - vnutrenniy radius spavna `50 m`
  - vneshniy radius spavna `150 m`
  - udalenie primerno posle `200 m`
  - globalnyy limit po umolchaniyu `30`
  - do `3` spavnov za tik

Prostymi slovami:
- dlya prototipa eto horosho
- dlya karty `15 x 15 km` etogo ochen malo
- tak nelzya poluchit oschuschenie ogromennogo zhivogo okeana

## Chto uzhe est v proekte, na chto mozhno operetsya

- `ScavengePopulator`
  - uzhe umeet zhit po chankam
  - hranit aktivnye chanki
  - gruzit i vygruzhaet ih bez kashi
- `WorldStreamingDirector`
  - uzhe umeet podstraivat striming pod glubinu i skorost igroka
- `BiomeSamplerCache`
  - uzhe umeet rabotat po kletkam
- `WorldProceduralScatterDirector`
  - uzhe umeet myslit kletkami i oknami

Prostymi slovami:
- chankovoe myshlenie v proekte uzhe est
- imenno fauna poka esche ne perevedena na etot vzroslyy rezhim

## Kakaya arhitektura nuzhna dlya bolshoy karty

### 1. Ne odin spavn-radius, a 4 sloya zhizni mira

#### Sloy 1. Dalnyaya zhivaya ekologiya

Eto samyy dalniy sloy.

Tut net nastoyaschih `GameObject`-suschestv.
Tut hranyatsya tolko dannye:
- skolko zhizni v chanke
- kakie vidy tam zhivut
- spokoynyy eto chank ili opasnyy
- est li tam krupnaya ugroza
- zhiv li redkiy hischnik
- kogda etot chank posledniy raz trevozhili

Prostymi slovami:
- mir zhivet i bez igroka
- no my ne spavnim vsyu kartu chestnymi obektami

#### Sloy 2. Dalnyaya vidimaya zhizn

Eto srednyaya dalnost.

Tut zhivut:
- kosyaki
- oblaka melkoy ryby
- prostye dalnie siluety
- inogda dalniy krupnyy siluet

Tut dolzhny ispolzovatsya:
- GPU boids
- deshevye vizualnye proksi
- ochen prostaya logika bez tyazhelogo mozga

Prostymi slovami:
- igrok vidit, chto okean naselen
- no CPU ne umiraet

#### Sloy 3. Srednyaya zhivaya zona

Eto uzhe blizhe k igroku.

Tut poyavlyayutsya:
- nastoyaschie odinochnye mirnye suschestva
- nastoyaschie territorialnye suschestva
- otdelnye hischniki

No ne ves ih mozg rabotaet kazhdyy kadr.

Zdes nuzhna:
- redkaya logika obnovleniya
- deshevoe dvizhenie
- ogranichennyy radius vospriyatiya
- otklyuchenie tyazhelyh podsistem, poka igrok ne opasno blizko

#### Sloy 4. Blizhnyaya polnaya simulyatsiya

Eto blizhnyaya zona vokrug igroka.

Tut rabotayut uzhe vse sereznye veschi:
- shum
- svet
- rassledovanie
- preduprezhdenie
- ohota
- staya
- zaschita gnezda
- krupnye stsenarii leviafanov

Prostymi slovami:
- polnyy dorogoy mozg dolzhen zhit tolko ryadom s igrokom

## Predlagaemaya geometriya mira dlya fauny

### Chank fauny

Bazovyy chank fauny:
- `192 x 192 m`

Pochemu tak:
- eto uzhe dostatochno krupno dlya bolshogo mira
- no ne slishkom krupno dlya lokalnoy zhizni
- na kartu `15 x 15 km` eto daet upravlyaemuyu setku

### Vnutrennyaya kletka chanka

Vnutri chanka:
- kletka `64 x 64 m`

Ona nuzhna dlya:
- lokalnyh limitov
- bezopasnogo raspredeleniya suschestv
- deshevogo ucheta, gde uzhe zanyato, a gde pusto

### Koltsa vokrug igroka

Dlya fauny nuzhen ne odin krug, a neskolko:

- `0-180 m`
  - polnaya simulyatsiya
- `180-420 m`
  - srednyaya simulyatsiya
- `420-900 m`
  - dalnyaya vidimaya zhizn
- `900-1800 m`
  - tolko dannye ekologii, bez zhivyh obektov

Eto startovye chisla.
Ih potom nado budet krutit po:
- glubine
- skorosti igroka
- plotnosti bioma

## Kak eto dolzhno rabotat v realnosti

### U kazhdogo chanka est pasport

Nuzhna struktura vrode:
- `FaunaChunkState`

Ona hranit:
- koordinatu chanka
- seed chanka
- semeystvo bioma
- tip vody
- uroven mirnoy zhizni
- uroven ugrozy
- spisok dostupnyh vidov
- limit mirnyh suschestv
- limit territorialnyh
- limit hischnikov
- est li slot krupnoy ugrozy
- spisok uzhe aktivnyh obektov
- vremya posledney trevogi

Prostymi slovami:
- chank znaet, kto v nem voobsche mozhet zhit
- i skolko etoy zhizni tam dopustimo

### Chank ne spavnit vse podryad

Pri vhode igroka v radius:
- chank ne sozdaet srazu ves zoopark
- on smotrit na byudzhet
- i dosypaet zhizn postepenno

To est:
- snachala mirnaya dalnyaya zhizn
- potom lokalnye odinochnye
- potom ugrozy
- potom, esli mesto realno etogo trebuet, krupnaya vstrecha

### Krupnye ugrozy ne zhivut po malenkim chankam

Leviafan ne dolzhen byt “svoy na kazhdyy chank”.

Dlya nego nuzhen otdelnyy uroven:
- `FaunaMacroZone`

Razmer:
- primerno `600-900 m`

Imenno v etoy makrozone reshaetsya:
- est li tut bolshoy hozyain
- kakoy imenno
- gde ego osnovnye marshruty
- gde igrok mozhet s nim peresechsya

Prostymi slovami:
- leviafan dolzhen byt hozyainom bolshogo kuska vody
- a ne prosto zhirnym suschestvom iz kletki `192 m`

## Kak sdelat mnogo suschestv i ne ubit igru

### 1. Ne vse suschestva dolzhny byt chestnymi GameObject

Eto glavnoe pravilo.

Dlya ogromnoy karty:
- nastoyaschie `GameObject` nuzhny tolko ryadom
- vse ostalnoe dolzhno zhit kak:
  - dannye
  - boids
  - proksi
  - redkie dalnie siluety

### 2. Mirnaya ryba dolzhna byt massovoy, no deshevoy

Pravilnyy balans takoy:
- massovaya melkaya zhizn = GPU / proksi
- odinochnye interesnye suschestva = obychnyy AI
- opasnye umnye suschestva = polnyy AI

Inache:
- libo mir pustoy
- libo CPU umiraet

### 3. Chanki dolzhny rabotat po byudzhetu za tik

Nuzhno zhestko ogranichit:
- skolko chankov mozhno obnovit za tik
- skolko suschestv mozhno dosozdat za tik
- skolko dorogih perehodov AI mozhno vklyuchit za tik

Prostymi slovami:
- nikakoy “igrok rezko poplyl i my sozdali 200 suschestv za kadr”

### 4. Sohranenie tozhe dolzhno byt chankovym

Sohranyat nado ne vsyu faunu karty kak zhivye obekty.

Nuzhno sohranyat tolko:
- sostoyanie chankov
- ubityh redkih suschestv
- potrevozhennye gnezda
- istoschennye osobye tochki
- sostoyanie krupnyh ugroz

Prostymi slovami:
- save dolzhen pomnit vazhnye posledstviya
- no ne obyazan hranit traektoriyu kazhdoy melkoy rybki

## Kakoy balans po vidam nuzhen dlya takoy karty

### Mirnoy zhizni dolzhno byt na poryadki bolshe, no ne chestnymi AI-obektami

Dlya bolshoy karty normalnaya ideya takaya:

- mirnaya massovaya zhizn:
  - ochen mnogo
  - v osnovnom boids i proksi
- mirnaya interesnaya zhizn:
  - zametno menshe
  - uzhe chestnye obekty
- territorialnye:
  - redkie tochki haraktera
- hischniki:
  - esche rezhe
- krupnye hischniki:
  - lokalnye sobytiya
- leviafany:
  - redkie hozyaeva bolshih uchastkov

Prostymi slovami:
- igrok dolzhen pochti vsegda videt zhizn
- no ne pochti vsegda videt boy

### Dlya melkovodya

Raz karta ogromnaya, melkovode ne dolzhno byt “startovoy luzhey”.

Tam dolzhno byt:
- mnogo mirnoy zhizni
- redkie territorialnye hozyaeva rifov
- redkie hischnye karmany
- 1-2 ochen redkie krupnye poverhnostnye setpiece-vstrechi

### Dlya sredney glubiny

Tam dolzhno byt:
- menshe massovoy krasoty
- bolshe lokalnoy opasnosti
- bolshe interesnyh prohodov i zasad

### Dlya pozdney glubiny

Tam dolzhno byt:
- menshe obschego shuma
- bolshe davleniya
- bolshe umnyh hischnikov
- krupnye ugrozy v imenovannyh mestah

## Chto imenno nado delat sleduyuschim kodom

### Shag 1

Sdelat novyy sloy dannyh:
- `FaunaChunkState`
- `FaunaMacroZoneState`

### Shag 2

Perevesti `FaunaDirector` iz rezhima:
- “koltso vokrug igroka”

v rezhim:
- “aktivnye chanki vokrug igroka”

### Shag 3

Dobavit urovni simulyatsii:
- dalnyaya ekologiya
- dalnyaya vidimaya zhizn
- srednyaya simulyatsiya
- polnaya blizhnyaya simulyatsiya

### Shag 4

Vyvesti mirnuyu massovuyu zhizn v otdelnyy deshevyy sloy:
- boids
- libo ochen deshevye gruppovye proksi

### Shag 5

Sdelat dlya krupnyh ugroz otdelnye makrozony, a ne vydavat ih po obychnoy logike malenkih chankov

## Chestnyy vyvod

### Na vopros “est li u nas chanki?”

Otvet:
- v proekte chankovoe myshlenie uzhe est
- no imenno fauna poka esche ne perevedena na nastoyaschiy chankovyy rezhim

### Na vopros “kak eto dolzhno byt realizovano?”

Otvet:
- ne odnim radiusom vokrug igroka
- a chankami + sloyami simulyatsii + makrozonami krupnyh ugroz

### Na vopros “kak sdelat ochen mnogo suschestv i ne ubit igru?”

Otvet:
- ne pytatsya delat vseh suschestv polnymi AI-obektami
- massovuyu zhizn derzhat v deshevom sloe
- polnyy AI vklyuchat tolko ryadom
- krupnye sobytiya derzhat otdelno na urovne bolshih zon
