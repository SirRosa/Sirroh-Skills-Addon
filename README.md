# Sirroh Skills Addon

Rozszerzenie **Sirroh Overhaul** do Valheim 1.0.

## Zakres
- **Kolos (dawniej Slayer)**: osobna umiejętność dla broni dwuręcznych, bonus obrażeń, odporność na odrzut i zwiększenie obrażeń ogłuszających.
- **Łowca Głów (dawniej Slayer Skills)**: progres przeciw poszczególnym gatunkom za wypadające trofea, premie do obrażeń, obsługa gwiazdek i współpraca z oswojonymi stworzeniami.
- **Zawadiaka**: niezależna umiejętność broni jednoręcznych, rozwój przez trafienia, premia do obrażeń (+18%), stagger (+35%), odporność na odrzut (+75%) na poziomie 100.
- **Łowca Głów – nowy system zabójstw**: +1 punkt za trofeum, a dodatkowo +1 za każde 10 zabójstw gatunku, nawet bez trofeum. Duże/rzadkie stworzenia i bossowie: co 5. Liczniki są zapisywane na postaci niezależnie od punktów. Lista dużych stworzeń konfigurowalna: FallenValkyrie, Serpent, Gjall, Morgen, Bjorn, Bjorn_sleeping, Unbjorn, Troll, Abomination, StoneGolem, SeekerBrute, GoblinBrute, Lox.
- **Blacksmithing Expanded**: opcjonalny most, bez przejmowania danych wyposażenia, statystyk, jakości lub osobnego skilla kowalstwa.

Źródłowe materiały techniczne przeanalizowano w `SirRosa/Sirroh-Compatibility-Builder`. Kod dodatku jest niezależną implementacją. Nie dołączamy cudzych plików DLL ani zasobów graficznych.

## Zależności
- Sirroh Overhaul (obowiązkowo, ta sama instalacja na serwerze i klientach)
- BepInExPack Valheim i Jotunn
- Blacksmithing Expanded (opcjonalnie)

## Stan
Implementacja i testy kompilacji opisane w workflow `.github/workflows/build.yml`. Pełne testy rozgrywki i wieloosobowe są wymagane przed uznaniem wydania za stabilne.

## Ważne szczegóły

- `Slayer` zachowuje identyfikator skilla `neithar.Slayer`. **Odinstaluj oryginalny Slayer i Slayer Skills**, gdy uruchamiasz addon; jednoczesne uruchomienie jest zablokowane.
- Za trofeum wypadające z pokonanego stworzenia dostaje się punkty przeciw danemu gatunkowi, a zabójstwa liczą się również niezależnie od dropu (10/5). Progres oryginalnego Slayer Skills jest importowany z `SlayerSkills_1847__C`, bez kasowania poprzedniej wartości. Zachowany zapis punktów: `sirroh.skillsaddon.slayerspecies.v1`. Nowy zapis liczników: `sirroh.skillsaddon.headhunter.kills.v1`.
- W multiplayer klient może być właścicielem obiektu stworzenia; dlatego `OnlyHostMayAwardPoints` domyślnie jest wyłączone, podobnie jak w oryginalnej obsłudze punktów. Używaj dodatku w zaufanej grupie. Włączenie tej opcji odrzuca niezweryfikowane klient-klient RPC, ale może pominąć trofea stworzeń należących do klienta.
- Premie Kolosa modyfikują `HitData`, **nie** `ItemData.m_shared`, żeby uniknąć niszczenia danych kowalstwa i ulepszeń.
- Integracja Blacksmithing Expanded pozostawia oryginalny plugin i jego `ItemDataManager` jako jedynego właściciela danych, jakości, infuzji, tierów i XP. Opcjonalny guard wykorzystuje wewnętrzny rejestr `activeItems` i nie pozwala obniżyć poziomu bonusów zapisanego w przedmiocie wskutek chwilowego spadku wartości przekazanej do `ApplyCraftingBonuses`.
- Build CI weryfikuje kompilację i statyczne kontrakty, **nie** symuluje świata Valheim. Przed użyciem w docelowym świecie zrób backup postaci.

## Testy do przeprowadzenia w grze

1. Skopiuj postać z zapisanym Slayer i Slayer Skills, usuń oryginalne DLL i uruchom addon z Overhaul: sprawdź poziom skilla, trofea i zapis po ponownym logowaniu.
2. Wyposaż dwuręczny topór, młot i broń podwójną; sprawdź XP, obrażenia, stagger i knockback bez podwójnego naliczania.
3. Sprawdź drop trofeów, dropy z ragdoll, DOT, 5-gwiazdkowe stworzenia, bossa, chowańca i innych graczy w pobliżu.
4. Na hoście i kliencie sprawdź przyznawanie punktów po śmierci potwora z różnym właścicielem ZDO.
5. Z Blacksmithing Expanded ulepsz przedmiot (bez i z infuzją), przeloguj, porównaj jakość, pancerz, obrażenia, wytrzymałość i dane przedmiotu. Osobno przetestuj poziom skilla kowalstwa po śmierci.

## Zasady nowej wersji 0.2.0

- **Kolos** zachowuje ID umiejętności starego Slayer (`neithar.Slayer`) — poziomy nie zerują się przy zmianie nazwy.
- **Zawadiaka** ma nowe ID `sirroh.skills.addon.zawadiaka`, liczy tylko wyposażone bronie typu OneHandedWeapon (nie pięści, tarcze ani narzędzia). Nie może jednocześnie otrzymać XP za to samo trafienie co Kolos. Premie kumulują się z odpowiednim standardowym skillem broni w Sirroh Overhaul.
- **Łowca Głów** to progres przeciw gatunkowi, widoczny w menu trofeów, a nie jeden wspólny skill. Gatunki bez trofeów zapisują postęp pod nazwą prefabu; wyświetla się on w komunikatach progresu, nawet bez pozycji w menu trofeów.
- Przy każdych 10 zabójstwach jednego zwykłego gatunku albo 5 dużego/rzadkiego gatunku dostajesz **+1 punkt**, osobno od szansy wypadającego trofeum. Liczy się ostatnie zabójstwo gracza lub przypisanego chowańca. Bossowie mają próg 5.
- Konfiguracja pozwala zmienić progi i listę dużych stworzeń. Liczniki pozostają zachowane po relogu i śmierci; redukcja poziomów po śmierci może obniżyć same punkty, nie liczbę zabójstw.
- Nowe ustawienia Kolosa są w kategorii `Kolos`, Łowcy Głów w `Łowca Głów`. Poprzednie wartości konfiguracji nie są migrowane automatycznie, natomiast progres postaci jest zachowany.

Przed wdrożeniem na docelowym świecie sprawdź host/klient: 1) zabójstwa bez dropu trofeum, 2) 10 i 5 killi z odpowiednim licznikiem, 3) zabójstwa chowańca, 4) jednoręczne vs dwuręczne bronie, 5) migracja zapisanych punktów, 6) statystyki ekwipunku Blacksmithing po ulepszeniu.

## v0.3.0: postęp Łowcy Głów — poprawka

Oryginalny Slayer Skills dopisywał tylko poziom gatunku do wpisów w zakładce trofeów. W przypadku braku zdobytego trofeum nie było żadnej pozycji UI. Wprowadzono dodatkowo:

- Komunikat po **każdym** zabójstwie (np. „Łowca Głów: Jeleń — 3/10 zabójstw | poziom 2/100”), konfigurowalny przez `PokazPostepZabojstw`.
- Panel **ŁOWCA GŁÓW • POSTĘP** przy otwartym ekwipunku. Pokazuje wszystkie gatunki z zapisanym postępem, w tym stworzenia bez trofeum; stronicowanie przy większej liczbie gatunków. Można wyłączyć przez `PanelPostepuWekwipunku`.
- Dla wpisu w zakładce trofeów pokazuj także `0/100` oraz postęp zabójstw do progu 10/5, zamiast czekać na pierwszy punkt.
- Przebudowywanie mapy nazw trofeów po zmianie języka gry na polski.

## v0.3.0: polska lokalizacja Blacksmithing Expanded 1.2.4

Dodatek tłumaczy **wszystkie 60 zidentyfikowanych opcji konfiguracyjnych** (nazwy, kategorie, objaśnienia), **Kowalstwo** w zakładce umiejętności, polski opis skilla, linie opisów przedmiotów („Wykuto przy Kowalstwie”, „Nasycenie żywiołem”) oraz wiadomość o bonusowym przedmiocie.

Spolszczenie działa opcjonalnie, gdy Blacksmithing Expanded jest obecny, a językiem gry jest **Polish**. Zastosowano oryginalną metodę `SkillManager.LocalizeKey.Polish` i metadane konfiguracyjne: **nie zmieniono nazw kluczy BepInEx, wartości ustawień, kodu Blacksmithing Expanded ani danych wyposażenia**. Teksty diagnostyczne w konsoli pozostają w języku oryginału.

## Etapy projektu

1. **Ukończone w kodzie:** Kolos (Slayer), Zawadiaka, Łowca Głów, migracja starych punktów, liczniki zabójstw, panel, opcjonalny most Blacksmithing i polska lokalizacja.
2. **Przeanalizowane:** brak wspólnego skilla XP z `HuntingSkill` z Sirroh Overhaul. Łowca Głów zapisuje niezależną tabelę punktów gatunkowych. Chowańce są wiązane z kluczem właściciela `sirroh.tresura.owner.v1`; poziomy stworzeń CLLC odczytywane przez `GetLevel()`.
3. **Do przetestowania w Valheim (nie w CI):** rzeczywiste trafienia, trofea i komunikaty 1/10 oraz 1/5; zdalny właściciel zwierzęcia; właściwości zbroi i broni Blacksmithing po ulepszeniu/zapisie/odczycie; zachowanie UI z ZenUI; zmiana języka podczas gry.
4. **Następna większa integracja:** BetterTames, LetMeTameYou, Husbandry po zatwierdzeniu powyższych testów. Tych modów addon nie wyłącza ani nie przejmuje, aby nie naruszyć AI czy synchronizacji zwierząt.

Wersja `0.3.0` jest **testową**, a pomyślna kompilacja nie oznacza potwierdzonej zgodności w rozgrywce.
