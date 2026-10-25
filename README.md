# Sirroh Skills Addon

Rozszerzenie **Sirroh Overhaul** do Valheim 1.0.

## Zakres
- **Slayer (Pogromca)**: osobna umiejętność dla broni dwuręcznych, bonus obrażeń, odporność na odrzut i zwiększenie obrażeń ogłuszających.
- **Slayer Skills (Pogromca Stworzeń)**: progres przeciw poszczególnym gatunkom za wypadające trofea, premie do obrażeń, obsługa gwiazdek i współpraca z oswojonymi stworzeniami.
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
- Za trofeum wypadające z pokonanego stworzenia dostaje się punkty przeciw danemu gatunkowi. Progres oryginalnego Slayer Skills jest importowany z `SlayerSkills_1847__C`, bez kasowania poprzedniej wartości. Nowy zapis: `sirroh.skillsaddon.slayerspecies.v1`.
- W multiplayer klient może być właścicielem obiektu stworzenia; dlatego `OnlyHostMayAwardPoints` domyślnie jest wyłączone, podobnie jak w oryginalnej obsłudze punktów. Używaj dodatku w zaufanej grupie. Włączenie tej opcji odrzuca niezweryfikowane klient-klient RPC, ale może pominąć trofea stworzeń należących do klienta.
- Premie Pogromcy modyfikują `HitData`, **nie** `ItemData.m_shared`, żeby uniknąć niszczenia danych kowalstwa i ulepszeń.
- Integracja Blacksmithing Expanded pozostawia oryginalny plugin i jego `ItemDataManager` jako jedynego właściciela danych, jakości, infuzji, tierów i XP. Opcjonalny guard wykorzystuje wewnętrzny rejestr `activeItems` i nie pozwala obniżyć poziomu bonusów zapisanego w przedmiocie wskutek chwilowego spadku wartości przekazanej do `ApplyCraftingBonuses`.
- Build CI weryfikuje kompilację i statyczne kontrakty, **nie** symuluje świata Valheim. Przed użyciem w docelowym świecie zrób backup postaci.

## Testy do przeprowadzenia w grze

1. Skopiuj postać z zapisanym Slayer i Slayer Skills, usuń oryginalne DLL i uruchom addon z Overhaul: sprawdź poziom skilla, trofea i zapis po ponownym logowaniu.
2. Wyposaż dwuręczny topór, młot i broń podwójną; sprawdź XP, obrażenia, stagger i knockback bez podwójnego naliczania.
3. Sprawdź drop trofeów, dropy z ragdoll, DOT, 5-gwiazdkowe stworzenia, bossa, chowańca i innych graczy w pobliżu.
4. Na hoście i kliencie sprawdź przyznawanie punktów po śmierci potwora z różnym właścicielem ZDO.
5. Z Blacksmithing Expanded ulepsz przedmiot (bez i z infuzją), przeloguj, porównaj jakość, pancerz, obrażenia, wytrzymałość i dane przedmiotu. Osobno przetestuj poziom skilla kowalstwa po śmierci.
