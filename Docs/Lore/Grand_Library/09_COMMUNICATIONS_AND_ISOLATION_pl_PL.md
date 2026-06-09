<!-- localization_status: draft_machine_or_llm_pl_PL -->
# KOMUNIKACJA, TELEMETRIA I CISZA ORBITALNA

> **Źródło:** podręcznik wachty łączności Black Keel, notatki szkoleniowe z przekaźników salvage, odzyskane adnotacje Marauderów.  
> **Zakres:** Dlaczego załogi na HECTON-8 czują się same, co naprawdę można przesłać przez ocean i jak cisza staje się jednocześnie fizyką i polityką.  
> **Uwaga dla czytelnika:** Nie ma połączenia FTL do domu, natychmiastowego kanału ratunkowego ani czystej granicy między sygnałem, który zawiódł, a odpowiedzią, którą wstrzymano.

---

## 1. Żadnego cudownego kanału

HECTON-8 uczy każdego nowego nurka tej samej lekcji: od pomocy oddziela cię nie tylko odległość.

Ran jest dość daleko, by zwykły ruch międzygwiezdny przychodził jako rozkład, nie rozmowa. Orbita Aegir jest dość blisko, by widzieć ją na instrumentach, i nadal za daleko, by czuć miłosierdzie. Między nurkiem a Black Keel leży ocean pełen soli, jonów metali, warstw termicznych, zawieszonego pyłu mineralnego, złamanej infrastruktury, żywego filmu, luster solanki i złego nawyku ciśnienia, które zmienia drobne usterki w awarie systemu.

Nie ma ansible. Nie ma awaryjnej wiązki przebijającej księżyc. Nie ma operatora ratunkowego czekającego na bohaterskie ostatnie zdanie. Deep Reach sprzedawało w kontraktach "ciągłą świadomość operacyjną", bo fraza była użyteczna. Załogi dostały łańcuch wąskich, opóźnionych, stratnych kanałów, które działały najlepiej wtedy, gdy nikt rozpaczliwie ich nie potrzebował.

Ta różnica ma znaczenie. Na HECTON-8 izolacja nie jest tylko emocjonalna. Zbudowano ją z fizyki, przepustowości, języka prawnego i kosztu utrzymania człowieka na czuwaniu po drugiej stronie.

*[Notatka na marginesie: Jeśli broszura mówi "połączony", zapytaj z czym. Serwer płacowy nie jest przyjacielem.]*

## 2. Co ocean robi z sygnałem

Ocean nie blokuje każdego sygnału tak samo. Jest gorzej.

Radio szybko umiera, bo przewodząca woda, rozpuszczone sole, metaliczny osad, wraki kadłubów, masa kabli i pył pressure glass zjadają użyteczny zasięg. Łącza laserowe giną w rozproszeniu i chmurach cząstek. Wąskie sygnały optyczne działają tylko na krótkich, czystych liniach widzenia, a HECTON-8 rzadko daje załogom czyste linie na długo. Indukcja magnetyczna może kuśtykać na bardzo krótkich dystansach, dość dla zadokowanego sprzętu, sparowanych narzędzi albo handshake skafandra, ale nie dla rozmowy z orbitą.

Akustyka niesie dalej, ale ma własne problemy. Dźwięk wygina się w gradientach termicznych. Warstwy solanki go odbijają. Poruszające się maszyny go brudzą. Wielkie zwierzęta i stare kadłuby mogą go maskować. Granica gęstości może rzucić pakiet bokiem i sprawić, że odbiornik uzna, iż nadawca się przemieścił. Ocean nie musi być idealną klatką. Wystarczy, że jest dość niespójny, by pewność stała się droga.

Dlatego "blackout" jest mylącym słowem. Blackout brzmi jak brak. HECTON-8 daje załogom coś okrutniejszego: fragmenty. Ostrzeżenie o ciśnieniu przychodzi bez trasy, która je wyjaśnia. Ping alarmowy przychodzi po tym, jak pokój się zmienił. Imię przechodzi czysto, ale checksum współrzędnych zawodzi. Martwy kanał powtarza wczorajszy pakiet, aż zmęczony nurek zaczyna mu odpowiadać.

## 3. Telemetria akustyczna

Większość dalekiego przekazu przez wodę używa niskoczęstotliwościowej telemetrii akustycznej.

W idealnych diagramach szkoleniowych nurek wysyła pakiet do lokalnego przekaźnika. Przekaźnik wypycha go przez kanał niskiej częstotliwości. Wyższa boja, cable spine albo odbiornik skierowany na orbitę odbiera pakiet, waliduje go i przekazuje zdarzenie do systemów Black Keel. W terenie każdy krok może zostać wygięty przez geologię, ruch, utratę zasilania, korozję albo przekaźnik, który nadal ma numer seryjny, ale nie ma już użytecznej lojalności wobec sieci wokół siebie.

Przepustowość nie jest filmowa. Jest ciasna, powolna i racjonowana. Załoga może wysłać kody statusu, ostrzeżenia ciśnienia skafandra, route tags, hashes manifestu, krótkie wybuchy tekstu, podpisy roszczeń i skompresowane evidence flags. Nie może streamować obrazu z hełmu z dna basin. Nie może prowadzić normalnej rozmowy z orbitą. Nie może szybko wyjaśnić skomplikowanego pomieszczenia, jeśli nie przygotowała właściwych tagów, zanim pomieszczenie stało się skomplikowane.

Opóźnienie też nie jest jedną liczbą. Dobra płytka trasa może wydawać się prawie responsywna. Głęboka trasa przez bałagan kanionu solanki może zmienić odpowiedź w rytuał. Osiem minut zdarza się dość często, by stać się żartem; piętnaście dość często, by przestało być zabawne. Pod ciśnieniem nawet dziewięćdziesiąt sekund może trwać dłużej niż ludzka decyzja.

*[Notatka na marginesie: Podręcznik mówi "wyślij kod alarmowy". Nie mówi, co robić, gdy ocean decyduje, czy kod nadal jest twój.]*

## 4. Przekaźniki, kości i martwa infrastruktura

Deep Reach nie polegało na jednym czystym nadajniku. Zbudowało warstwy.

Górne trasy używały masztów boi, pylonów serwisowych, węzłów tether i repeaterów platformowych. Cable Reef stał się gęstym, brzydkim szkieletem komunikacyjnym: magistrale zasilania, data umbilicals, obejmy naprawcze, obudowy przekaźników i sprzęt pokryty biofilmem, który nadal budzi się przy właściwym napięciu. Głębsze systemy używały acoustic pingers, cache'y konserwacyjnych, pressure-rated memory spools i route beacons zdolnych przechować wiadomość, aż odbiornik przejdzie w zasięgu.

Po Great Tide te warstwy nie umarły po prostu. Niektóre umarły. Niektóre zapętliły się. Niektóre stały się lokalne. Niektóre przyjmowały pakiety i nigdy ich nie przekazywały. Niektóre przekazywały stare pakiety ze świeżymi timestamps. Niektóre nadal odpowiadają logice ciągłości Atlas, a nie procedurze Black Keel. Niektóre są użyteczne właśnie dlatego, że żadne biuro nie pamięta, że istnieją.

Dobrzy Marauderzy uczą się różnicy między przekaźnikiem a duchem. Przekaźnik dowodzi ścieżki. Duch dowodzi tylko, że coś kiedyś miało zasilanie i powód, by mówić.

Ta różnica staje się gameplayem. Gracz może odtworzyć route beacon i otworzyć bezpieczniejszą nawigację. Może znaleźć memory spool i odzyskać wiadomość, której nikt wyżej nie chciał indeksować. Może użyć martwego przekaźnika jako przynęty, decoy albo listening post. Sprzęt komunikacyjny nie jest dekoracją. To stara władza, stara custody i stary strach wciąż próbujące się poruszać.

## 5. Reżim nasłuchu Black Keel

Black Keel słucha. To nie to samo, co odpowiadać.

Jako claim tender Keel priorytetyzuje custody events: upload manifestu, dowód materiału, tożsamość kontraktora, stan trasy, wypłacalność skafandra, recoverable evidence i sygnały wpływające na odpowiedzialność. Potwierdza to, co system może wycenić. Eskaluje to, co może uszkodzić strukturę roszczenia. Rejestruje więcej, niż pociesza.

Na pokładzie są ludzcy watch officers, ale nie siedzą w dramatycznym kanale, czekając, by uratować jednego nurka. Obsługują okna, kolejki, przegląd uszkodzonych pakietów, arbitration holds, security flags i stałą pracę udowadniania, że Keel odpowiedziała zgodnie z polityką. Oficer wachtowy może się przejmować. Kolejka nie. Polityka to miejsce, do którego troska trafia, by stać się dopuszczalna albo bezużyteczna.

Deep Reach nazywało tę dyscyplinę "orbital silence" w aktywnych okresach roszczeń. Termin brzmiał jak bezpieczeństwo operacyjne. W praktyce oznaczał, że tender unika inicjowania niepotrzebnego kontaktu, woli receipts od rozmowy i traktuje mowę nieustrukturyzowaną jako źródło odpowiedzialności.

Dlatego Marauder może krzyczeć do kanału i otrzymać tylko czysty numer potwierdzenia.

*[Notatka na marginesie: Keel cię usłyszała. To nigdy nie było pytanie.]*

## 6. Ścieżki awarii

Awarie komunikacji na HECTON-8 rzadko przychodzą jako jedna czerwona lampka.

Kolejka pakietów może się zapełnić, gdy załoga myśli, że przekaźnik nadaje. Skafander może ponawiać to samo ostrzeżenie ciśnienia, aż odbiornik stłumi je jako zdublowany szum. Przekaźnik może fizycznie istnieć, ale nadal być przypisany do starego custody owner. Route beacon może obudzić się po przepięciu i nadpisać nowszą mapę ścieżką pre-Tide. Watch system może poddać wiadomość quarantine, bo evidence flag, debt flag i distress flag dotarły w złej kolejności.

Złe dane nie zawsze są ciszą. Czasem złe dane są pewnością.

Najgroźniejsze awarie to stale handles: stare ID kontaktów, stare zaufanie do przekaźników, stare nazwy tras, stare pieczęcie autoryzacji. Nurek myśli, że rozmawia z Black Keel. Pakiet w rzeczywistości odbija się przez lokalny cache, który nie widział orbity od dwudziestu lat. Załoga podąża za odpowiedzią, która była ważna, zanim przesunęła się krawędź uskoku. Salvage manifest dociera do custody, ale dołączona prośba o pomoc odpada, bo nie jest częścią zaakceptowanego schema.

Dlatego załogi znakują własne trasy i trzymają fizyczne dowody. Farba na włazie może przeżyć konto przekaźnika. Przywiązana lina może przebić czystą współrzędną. Znacznik na ciele może nieść prawdę, której telemetria odmówiła klasyfikacji.

## 7. Izolacja jako presja na gracza

Izolacja nie powinna brzmieć jak wymówka lore. Powinna brzmieć jak system ciśnienia.

Gracz może otrzymywać pings, fragmenty, receipts, opóźnione ostrzeżenia, uszkodzone wiadomości, stare duchy tras, potwierdzenia Black Keel, lokalne odpowiedzi Atlas i znaki zostawione przez załogi. Żadne z nich nie powinno działać jak doskonały narrator. Każdy sygnał prosi o osąd. Kto wysłał? Kiedy? Przez jaki przekaźnik? Co pomija? Kto zyskuje, jeśli gracz mu zaufa?

To daje settingowi konkretną samotność. Gracz nie jest sam dlatego, że wszechświat o nim zapomniał. Jest sam dlatego, że dostępne systemy mogą widzieć jego części i wciąż nie stać się pomocą.

Działające łącze komunikacyjne może być bardziej przerażające niż martwe. Martwe mówi prawdę jasno. Działające może powiedzieć, że ostrzeżenie tlenowe odebrano, roszczenie pozostaje aktywne, upload czeka, a prawo do ratunku nie jest implikowane.

To jest cisza HECTON-8. Nie brak dźwięku. Obecność systemów, które usłyszały dość, by rozliczyć chwilę, ale nie dość, by ją uratować.
