Date: UNKNOWN_LEGACY
Status: DEPRECATED

🛸 HECTON-8: Plan po vizualnoy i tehnicheskoy polirovke (Visual Juice)
Na baze analiza luchshih resheniy solo-razrabotki (keys Tiny Delivery / Artem Sinica)
1. Tehnicheskaya magiya landshafta (Terrain & Environment)
Tsel: Izbavitsya ot «mylnogo» dna i sdelat relef detalizirovannym pri minimalnyh zatratah resursov.
Height-Based Blending (Obhod limita 4 tekstur):
Problema: Standartnyy terreyn Unity smeshivaet tekstury «gradientom», prevraschaya styk peska i kamnya v gryaz.
Reshenie: Ispolzovat kartu vysot (Height Map) vnutri tekstur. Pri smeshivanii sheyder otrisovyvaet snachala vystupayuschie chasti kamney, a potom pesok v nizinah.
Rezultat: Rezkie, naturalnye perehody. Dno vyglyadit kak v vysokobyudzhetnyh proektah.
Triplanarnyy sheyder dlya skal:
Tehnologiya: Tekstura nakladyvaetsya na obekt ne po UV-razvertke, a po mirovym koordinatam (s treh storon).
Rezultat: Ty mozhesh rastyagivat i krutit ogromnye skaly, i ih tekstura nikogda ne budet rastyanutoy ili «poplyvshey». Eto pozvolyaet sobrat krasivoe dno iz 5-6 bazovyh modeley kamney.
2. Vidimost predmetov (Exploration UX)
Tsel: Sdelat poisk resursov komfortnym, ne prevraschaya igru v «simulyator poiska pikseley».
Stencil Buffer (Prosvechivanie skvoz travu):
Tehnologiya: Na resursah (med, titan, oblomki) visit sheyder, kotoryy pishet v Stencil-bufer. Trava i vodorosli imeyut sheyder, kotoryy ignoriruet perekrytie, esli v bufere est otmetka predmeta.
Rezultat: Esli kusok rudy upal v gustye vodorosli, igrok vidit ego «siluet» skvoz nih. Eto kritichno dlya podvodnogo mira s plotnoy floroy.
Dinamicheskaya reaktsiya rastitelnosti:
Tehnologiya: Sheyder travy/kelpa schityvaet pozitsiyu igroka (cherez Global Vector ili MaterialPropertyBlock).
Rezultat: Trava plavno prigibaetsya ili kolyshetsya, kogda igrok proplyvaet ryadom. Sozdaet oschuschenie fizicheskogo prisutstviya.
3. Fizika «Vozdushnoy podushki» (Movement Feel)
Tsel: Sdelat upravlenie transportom (skuter Manta, drony) plavnym i «dorogim».
Raycast-podveska (Hovering System):
Logika: Obekt ne kasaetsya dna kolayderom. Snizu puskaetsya 4 lucha (Raycasts), kotorye sozdayut «silu vytalkivaniya» (AddForce).
Rezultat: Skuter ne dergaetsya na melkih kamnyah, a plavno obtekaet relef. Eto idealnaya fizika dlya podvodnoy tehniki — ona kazhetsya tyazheloy, no poslushnoy.
4. Arhitektura intellekta NPC (Fauna AI)
Tsel: Zhivoe povedenie ryb, kotoroe ne nagruzhaet protsessor.
Razdelenie Intent i Trajectory:
Globalnoe povedenie (State Machine): Reshaet, chto ryba delaet (Ohotitsya / Patruliruet / Spit).
Strategiya dvizheniya (Steering Behaviors): Reshaet, kak imenno ona plyvet v tekuschuyu sekundu.
Rezultat: Esli hischnik zastryal v uglu skaly, ego «mozg» vse esche hochet tebya sest, no «sistema dvizheniya» zastavlyaet ego plavno uvernutsya ot prepyatstviya, ne preryvaya sostoyanie ohoty. Ryby perestayut vyglyadet kak roboty.
5. Immersivnyy zvuk (Audio Atmosphere)
Tsel: Podcherknut izolyatsiyu i nahozhdenie v skafandre.
Pause-Menu Low-Pass:
Realizatsiya: Pri vyzove menyu pauzy na Master Audio Mixer nakladyvaetsya filtr nizkih chastot (Low-Pass) i snizhaetsya Pitch.
Rezultat: Ves mir stanovitsya «gluhim», kak budto igrok pogruzilsya v svoi mysli ili shlem pereshel v rezhim energosberezheniya. Ochen deshevyy, no moschnyy priem dlya atmosfery.
6. Marketingovye priemy (Promotion)
Tsel: Sobrat Wishlist-y v Steam za schet vizuala.
Format «Ref — Rezultat»:
Delat posty, gde sleva skrinshot iz realnoy zhizni ili kino (naprimer, «Chuzhie» ili «Bezdny»), a sprava — kak eto realizovano u nas v HECTON-8. Lyudi obozhayut smotret, kak tehnologii imitiruyut realnost.
Demonstratsiya instrumentov:
Pokazyvat ne prosto «kak ya plyvu», a «kak rabotaet instrument». Korotkie video (5-10 sek), gde vidno, kak skaner menyaet osveschenie ili kak lazernyy rezak ostavlyaet iskry.