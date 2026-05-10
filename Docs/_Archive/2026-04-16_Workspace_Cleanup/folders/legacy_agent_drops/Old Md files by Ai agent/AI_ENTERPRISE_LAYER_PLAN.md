Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# AI Enterprise Layer Plan

## Zachem eto nuzhno

Nuzhen ne prosto `FSM-skelet`, a polnotsennyy rabochiy sloy povedeniya suschestv dlya finalnoy igry.

Prostymi slovami:

- melkaya mirnaya ryba ne dolzhna dumat kak leviafan
- staynaya ryba ne dolzhna zhit na `GameObject`-logike po odnoy shtuke
- tyazhelyy hischnik ne dolzhen byt prosto “ta zhe ryba, no zlee”
- dron-torgovets ne dolzhen byt pritvoryayuscheysya ryboy

Nuzhna edinaya sistema, gde:

- kazhdyy vid suschestva imeet svoy profil
- raznye tipy suschestv ispolzuyut pravilnyy sposob dvizheniya
- AI slushaet shum, svet, uron, biom, direktora napryazheniya
- vse eto ne ubivaet proizvoditelnost

---

## Glavnyy printsip

Ne vse suschestva ispolzuyut odin i tot zhe mozg.

Pravilnoe razdelenie takoe:

### 1. Obychnaya mirnaya i odinochnaya fauna

Ispolzuet:

- `HectonBaseAI`
- steering
- obhod prepyatstviy
- reaktsii na igroka

Podhodit dlya:

- mirnyh ryb
- ostorozhnyh ryb
- odinochnyh melkih hischnikov
- padalschikov

### 2. Stai ryb

Ispolzuyut:

- `HectonBoidController`
- `BoidSimulation.compute`
- `BoidFishInstanced.shader`

Podhodit dlya:

- melkoy massovoy ryby
- kosyakov
- fonovoy zhivnosti

Vazhno:

- stai ne dolzhny zhit kak sotni otdelnyh `HectonBaseAI`
- eto nuzhno schitat na GPU

### 3. Krupnye osobye suschestva

Ispolzuyut:

- `Candice AI`
- pri neobhodimosti `A* Pathfinding`
- otdelnye rezhimy povedeniya

Podhodit dlya:

- leviafanov
- tyazhelyh ohotnikov
- dronov
- syuzhetnyh ili redkih suschestv

Vazhno:

- `A*` ne nuzhen vsem rybam
- `A*` nuzhen tem, kto realno hodit po dnu, patruliruet marshruty ili zhivet po slozhnym tochkam

---

## Kakie tipy suschestv dolzhny byt v igre

### Mirnye odinochnye

- `Mirnyy 01`
- `Mirnyy 02`
- `Mirnyy 03`
- `Sobiratel 01`
- `Sobiratel 02`

### Mirnye staynye

- `Staynaya 01`
- `Staynaya 02`
- `Staynaya 03`
- `Staynaya 04`

### Ostorozhnye i territorialnye

- `Territorialnyy 01`
- `Territorialnyy 02`
- `Gnezdovoy 01`
- `Gnezdovoy 02`

### Hischniki

- `Hischnik 01`
- `Hischnik 02`
- `Hischnik 03`
- `Zasadnyy 01`
- `Staynyy hischnik 01`

### Krupnye ugrozy

- `Leviafan 01`
- `Leviafan 02`
- `Leviafan 03`

### Tehnicheskie NPC

- `Dron-kurer 01`
- `Dron-remontnik 01`
- `Dron-torgovets 01`

Eto rabochie imena.
Pozzhe mozhno zamenit na nastoyaschie nazvaniya mira, no sistema dolzhna uzhe seychas umet derzhat takie klassy povedeniya.

---

## Iz kakih sloev sostoit polnotsennyy AI

## 1. Profil vida suschestva

Istochnik pravdy:

- `CreatureArchetypeData`

On hranit:

- kto eto
- mirnyy on ili hischnyy
- odinochnyy on ili staynyy
- na chem on dvigaetsya
- kak daleko zamechaet igroka
- kak reagiruet na shum
- kak reagiruet na svet
- skolko takih suschestv mozhno derzhat

Eto uzhe nachalo sdelano.

## 2. Dannye bioma

Istochnik:

- `FaunaBiomeData`

On dolzhen govorit:

- kakie vidy suschestv voobsche mogut zhit v etom meste
- s kakim vesom oni spavnyatsya
- skolko ih mozhno derzhat

Teper eto dolzhno opiratsya ne tolko na prefab, no i na profil vida.

## 3. Bazovoe vospriyatie

Obyazatelno:

- rasstoyanie do igroka
- shum igroka
- svet igroka
- poluchenie urona

Sledom nuzhno dobavit:

- pamyat o poslednem shume
- pamyat o poslednem svete
- territoriyu
- gnezdo
- interes k dobyche ili padali

## 4. Rezhimy povedeniya

Nuzhny otdelnye igrovye rezhimy:

- spokoynoe plavanie
- bluzhdanie
- interes
- ispug
- begstvo
- ohota
- presledovanie
- ataka
- vozvrat v territoriyu
- patrul
- obsluzhivanie tochki
- torgovoe ozhidanie

Vazhno:

- eto ne prosto novye nazvaniya sostoyaniy
- eto raznye igrovye pravila perehoda

## 5. Direktor mira

Istochnik:

- `FaunaDirector`
- `HectonDirectorAI`

On dolzhen upravlyat:

- skolko seychas zhivnosti v mire
- gde mir spokoynyy
- gde mir napryazhennyy
- gde usilit hischnikov
- gde usilit mirnuyu zhizn

## 6. Osobye sistemy

Otdelno nuzhno svyazat:

- `GPU Boids` dlya stay
- `Candice AI` dlya slozhnyh aktorov
- `A*` dlya dronov i nekotoryh krupnyh suschestv

---

## Pravilnaya model povedeniya po klassam

### Mirnaya odinochnaya ryba

Chuvstvuetsya tak:

- plavaet spokoyno
- inogda menyaet kurs
- pugaetsya shuma i sveta
- ubegaet
- vozvraschaetsya v spokoynyy rezhim

### Staynaya ryba

Chuvstvuetsya tak:

- zhivet kosyakom
- derzhitsya plotnostyu
- rassypaetsya pri ugroze
- sobiraetsya nazad

### Territorialnoe suschestvo

Chuvstvuetsya tak:

- ne obyazatelno ohotitsya pervym
- no ne lyubit vtorzhenie
- zaschischaet mesto
- mozhet progonyat igroka

### Hischnik

Chuvstvuetsya tak:

- slushaet shum
- zamechaet svet
- mozhet interesovatsya igrokom
- ne vsegda napadaet mgnovenno
- snachala mozhet soprovozhdat, otsenivat, zahodit sboku

### Leviafan

Chuvstvuetsya tak:

- ego slyshno zaranee
- ego vidno izdaleka
- u nego est territoriya
- on ne melteshit kak obychnaya ryba
- ego deystviya redkie, tyazhelye i zapominayuschiesya

### Dron

Chuvstvuetsya tak:

- ne zhivoe suschestvo, a sistemnyy aktor mira
- imeet marshrut, zadachu, servisnuyu tochku ili torgovuyu rol
- mozhet byt neytralnym, poleznym ili opasnym

---

## Chto uzhe est

Gotovo:

- `HectonBaseAI` kak bazovyy mozg
- `FaunaDirector` kak spavn i kontrol kolichestva
- `HectonDirectorAI` kak obschiy direktor napryazheniya
- `NoiseSystem`
- `LightDetectionSystem`
- `CreatureArchetypeData` kak profil vida

---

## Chto delaem dalshe po ocheredi

## Etap 1. Dovesti profil vida do zhivogo ispolzovaniya

Sdelat:

- `FaunaBiomeData` rabotaet cherez profil vida
- `FaunaDirector` pri spavne primenyaet profil k suschestvu
- `HectonBaseAI` prinimaet profil i perestraivaet svoi znacheniya

Tsel:

- raznye suschestva perestayut byt kopiyami s raznymi prefabami

Chto uzhe sdelano seychas:

- poyavilsya `CreatureArchetypeData` kak pasport vida
- `FaunaBiomeData` uzhe umeet brat prefab, ves i limity iz pasporta vida
- `FaunaDirector` uzhe primenyaet pasport vida pri spavne
- `HectonBaseAI` uzhe umeet perestraivat sebya pod mirnogo, territorialnogo, hischnika, leviafana ili drona
- v `HectonBaseAI` uzhe poyavilsya promezhutochnyy rezhim `Investigate`
- v pasporte vida uzhe poyavilis nastroyki domashney zony
- territorialnoe suschestvo uzhe mozhet vozvraschatsya k svoey tochke spavna, a ne prosto teryatsya v mire
- territorialnoe suschestvo uzhe zaschischaet imenno svoyu zonu, a ne ves biom tselikom

Prostymi slovami:

- suschestvo teper mozhet ne tolko srazu napast ili ubezhat
- ono mozhet snachala proverit, chto imenno uslyshalo ili uvidelo
- eto pervyy shag k bolee zhivomu povedeniyu bez rezkogo pryzhka srazu v slozhnye derevya povedeniya

## Etap 2. Razvesti klassy suschestv po realnomu povedeniyu

Sdelat:

- mirnyy
- territorialnyy
- ohotnik
- leviafan
- dron

Tsel:

- odin bazovyy AI poluchaet raznye igrovye rezhimy, a ne prosto drugie tsifry

## Etap 3. Vynesti stai na GPU

Sdelat:

- zhivaya stsena dlya `HectonBoidController`
- privyazka stay k biomam
- pereklyuchenie mezhdu odinochnoy i staynoy faunoy

Tsel:

- mnogo ryby bez ubiystva CPU

## Etap 4. Podklyuchit Candice i A* pravilno

Sdelat:

- Candice ne dlya vseh podryad, a tolko dlya slozhnyh suschestv
- `A*` tolko dlya:
  - dronov
  - nekotoryh tyazhelyh nazemno-donnyh aktorov
  - vozmozhno otdelnyh krupnyh suschestv

Tsel:

- slozhnyy AI tam, gde on nuzhen
- bez bessmyslennogo pathfinding u obychnoy ryby

## Etap 5. Sdelat nastoyaschie igrovye reaktsii

Sdelat:

- reaktsiya na shum
- reaktsiya na svet
- reaktsiya na uron
- reaktsiya na territoriyu
- reaktsiya na chislennost poblizosti
- reaktsiya na fazu direktora

Tsel:

- povedenie stanovitsya zhivym, a ne prosto “uvidel igroka po radiusu”

## Etap 6. Dovesti unikalnye roli

Sdelat:

- leviafany
- drony barter
- servisnye drony
- krupnye ugrozy biomov

---

## Pravila optimizatsii

Nelzya:

- delat vseh ryb otdelnymi dorogimi AI
- taschit pathfinding na vsyu faunu
- derzhat kuchu tyazhelyh proverok kazhdyy kadr na vseh suschestvah

Nuzhno:

- stai schitat na GPU
- odinochnyh schitat cherez tekuschiy `FSM`
- vospriyatie delat deshevo
- spyaschih suschestv derzhat spyaschimi
- tyazhelyh suschestv odnovremenno derzhat malo

---

## Chto schitaem uspehom

AI sloy schitaetsya po-nastoyaschemu rabochim, kogda:

- mirnaya ryba vedet sebya kak mirnaya ryba
- hischnik pugaet ne tolko uronom, no i povedeniem
- stai realno zhivut kosyakami
- leviafan oschuschaetsya kak sobytie mira
- dron oschuschaetsya kak chast ekosistemy i infrastruktury
- vse eto ne ubivaet proizvoditelnost

---

## Tekuschiy status

Seychas realno v rabote:

- etap 1
- nachalo etapa 2

To est:

- uzhe poyavilsya profil vida suschestva
- uzhe est shum i svet
- seychas dovodim svyazku:
  - `profil vida`
  - `spavn`
  - `bazovyy AI`

Posle etogo mozhno chestno perehodit k otdelnym rezhimam:

- mirnyy
- territorialnyy
- ohotnik
- leviafan
- dron

---

## Progress 2026-03-31 — Preduprezhdenie i podkradyvanie

### Chto sdelano

- V bazovyy AI dobavleny dva novyh zhivyh rezhima:
  - `Threaten`
  - `Stalk`
- `Threaten` nuzhen dlya territorialnyh suschestv.
- `Stalk` nuzhen dlya ohotnikov i buduschih leviafanov.
- V profil vida dobavleny otdelnye nastroyki:
  - skolko dlitsya preduprezhdenie
  - na kakoy distantsii suschestvo davit na igroka
  - skolko dlitsya skrytnoe vedenie tseli
  - na kakoy distantsii hischnik derzhit igroka pered atakoy

### Chto eto znachit prostymi slovami

- Territorialnoe suschestvo teper ne obyazano srazu kusat.
- Ono mozhet snachala pokazat:
  - eto moya zona
  - otoydi
- Hischnik teper ne obyazan srazu letet v lob.
- On mozhet snachala:
  - vesti igroka
  - derzhatsya sboku
  - nakaplivat davlenie

### Chto eto daet igre

- AI perestaet byt kartonnym.
- U suschestv poyavlyaetsya chitaemoe povedenie do ataki.
- Igrok nachinaet chuvstvovat:
  - preduprezhdenie
  - davlenie
  - ohotu
- Eto gorazdo blizhe k horoshey podvodnoy pesochnitse, chem mgnovennyy perehod iz spokoystviya v ukus.

---

## Progress 2026-03-31 — Pomosch sosedey i zaschita gnezda

### Chto sdelano

- Dobavlena zaschita gnezda vokrug tochki spavna.
- Dobavlen zov pomoschi sosedey.
- Eto nastraivaetsya pryamo v profile vida:
  - zaschischaet li vid gnezdo
  - radius zaschity gnezda
  - zovet li sosedey
  - radius zova
  - zaderzhka mezhdu zovami
  - skolko sosedey maksimum prihodit
  - zovutsya li tolko suschestva togo zhe vida

### Chto eto znachit prostymi slovami

- Esli igrok lezet v kladku ili blizko k gnezdu, suschestvo teper mozhet zaschischat ne tolko sebya, no i mesto.
- Esli suschestvo realno vstrevozheno, ono mozhet podnyat ryadom svoih.
- Mir nachinaet rabotat kak lokalnaya ekosistema, a ne kak nabor odinochnyh bolvanok.

### Chto eto daet igre

- Poyavlyayutsya uchastki, kuda nepriyatno prosto tak vlezat.
- Igrok mozhet sluchayno razozlit ne odnogo zaschitnika, a malenkuyu lokalnuyu gruppu.
- Eto delaet rify, prohody i kladki bolee zapominayuschimisya i zhivymi.

---

## Progress 2026-03-31 — Sovmestnaya ohota hischnikov

### Chto sdelano

- Dobavleny nastroyki gruppovoy ohoty v profil vida.
- Hischniki teper mogut podklyuchatsya k ohote ne kak kopii lidera, a kak raznye uchastniki gruppy.
- Vo vremya skrytnogo presledovaniya gruppa mozhet rashoditsya vokrug igroka po raznym pozitsiyam.

### Chto eto znachit prostymi slovami

- odin hischnik derzhit igroka speredi
- vtoroy zahodit sleva
- tretiy derzhitsya chut dalshe i zhdet udobnyy moment dlya vhoda

### Chto eto daet igre

- vstrecha s gruppoy hischnikov perestaet byt tupym navalom v odnu tochku
- igroku slozhnee prosto kaytit vseh po pryamoy
- ohota stanovitsya hitree i blizhe k horoshemu podvodnomu survival-oschuscheniyu

---

## Progress 2026-03-31 — Bolshoe davlenie leviafanov

### Chto sdelano

- Dobavleno otdelnoe sostoyanie bolshogo davleniya dlya krupnyh ugroz.
- Leviafan teper mozhet snachala derzhat krug vokrug igroka, a ne srazu sryvatsya v obychnuyu ataku.
- Instrumenty igroka umeyut eto raspoznavat.

### Chto eto znachit prostymi slovami

- krupnoe suschestvo pokazyvaet sebya
- lomaet chuvstvo bezopasnosti
- derzhit distantsiyu davleniya
- i tolko potom vhodit v zhestkiy kontakt

### Chto eto daet igre

- leviafan oschuschaetsya sobytiem mira
- vstrecha s nim stanovitsya dramatichnee i ponyatnee
- eto uzhe ne prosto bolshaya agressivnaya ryba

---

## Progress 2026-03-31 — Raznye stsenarii vstrechi u krupnyh ugroz

### Chto sdelano

- krupnym ugrozam dobavleny raznye stsenarii vstrechi
- seychas est:
  - krug davleniya
  - rezkaya zasada
  - storozh prohoda

### Chto eto znachit prostymi slovami

- odin leviafan pugaet krugom i siloy prisutstviya
- drugoy staraetsya poymat na rezkom sblizhenii
- tretiy derzhit vazhnyy prohod i davit imenno kak hozyain marshruta

### Chto eto daet igre

- krupnye suschestva perestayut byt odinakovymi
- igrok nachinaet zapominat ne prosto model, a tip vstrechi
- raznye glubiny i marshruty poluchayut raznyy harakter ugrozy

---

## Progress 2026-03-31 — Lozhnye zahody u krupnyh hischnikov

### Chto sdelano

- dobavlen otdelnyy lozhnyy zahod pered nastoyaschim kontaktom
- bolshoy hischnik teper mozhet:
  - rezko sokratit distantsiyu
  - sorvat igroku ritm
  - ne udarit srazu
  - uyti v storonu i povtorit davlenie
- eto podklyucheno:
  - k pasportu vida
  - k bazovomu AI
  - k analizatoru
  - k nozhu
  - k stan-pistoletu

### Chto eto znachit prostymi slovami

- igrok bolshe ne mozhet chitat krupnuyu ugrozu po sheme "ili nichego, ili ukus"
- teper est promezhutochnyy strashnyy moment
- suschestvo mozhet pugat ne tolko uronom, no i tem, chto delaet vid, budto uzhe poshlo v ataku

### Chto eto daet igre

- vstrechi stanovyatsya nervnee i zhivee
- krupnye hischniki chuvstvuyut sebya umnee i hitree
- u igroka poyavlyaetsya vazhnyy navyk:
  - ne tratit zaschitnyy instrument slishkom rano
  - ne vletat v otvet na lozhnyy zahod

---

## Progress 2026-03-31 — Realnyy reestr hischnikov i leviafanov

### Chto sdelano

- dobavlen otdelnyy authoring dlya nabora vidov
- teper mozhno odnoy komandoy peresozdat realnye profili:
  - territorialnyh zaschitnikov
  - raznyh hischnikov
  - raznyh leviafanov
- u kazhdogo profilya teper est:
  - rol
  - skorost
  - uron
  - lozhnyy zahod / staya / storozh prohoda tam, gde eto nuzhno
  - podskazka, k kakim semeystvam fauny i biomam ego sazhat
- dobavleny dokumenty:
  - `AI_CREATURE_ROSTER_ENTERPRISE.md`
  - `AI_CREATURE_ROSTER_REPORT.md`

### Chto eto znachit prostymi slovami

- u nas teper ne odin obschiy "hischnik" i ne odin obschiy "leviafan"
- u nas est park konkretnyh vidov s raznymi stsenariyami vstrechi
- eto uzhe mozhno tseplyat k prefabam i k buduschim naboram fauny bez ruchnoy kashi

### Chto eto daet igre

- raznye vody i raznye biomy smogut poluchat raznye tipy ugroz
- igrok nachnet zapominat ne tolko model suschestva, no i ego stil vstrechi

---

## Progress 2026-03-31 — AI fauna posazhena v mir kak sistema

### Chto sdelano

- dobavlena mirnaya zhizn, chtoby biomy byli ne tolko pro ugrozy
- sdelan authoring vremennyh proksi-prefabov dlya vseh klassov fauny
- vidy teper avtomaticheski poluchayut vremennoe telo, esli finalnogo prefaba esche net
- sdelan avtosborschik naborov fauny po biomam
- dobavlen otchet `AI_FAUNA_WORLD_INTEGRATION_REPORT.md`

### Chto eto znachit prostymi slovami

- AI uzhe mozhno chestno sazhat v mir, a ne derzhat tolko v kode
- dazhe bez finalnyh modeley mozhno proveryat plotnost zhizni, ugrozy i redkost leviafanov
- kazhdyy biom teper mozhno avtomaticheski napolnit zhiznyu po svoemu harakteru

### Chto eto daet igre

- mir perestaet byt pustym tam, gde AI uzhe napisan
- spokoynye vody ne stanovyatsya arenoy
- tyazhelye vody ne ostayutsya mertvymi
- leviafany ostayutsya redkoy bolshoy vstrechey, a ne obychnym fonom
