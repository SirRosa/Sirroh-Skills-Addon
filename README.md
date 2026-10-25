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
