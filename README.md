# Sirroh Skills [Addon]

## 0.4.6 TEST — przyprawianie bez limitu, balans regeneracji i rozdzielanie stosów

Usunięto system gwiazdek i limit pięciu przyprawień. Potrawy można przyprawiać bez końca; ich dane zapisują się teraz w zwięzłym formacie v3 (poprzednie v1 i v2 są nadal odczytywane, a premie zachowane). Nie zmieniamy rzeczywistego `ItemData.m_quality`.

**Regeneracja HP:** jedno przyprawienie zapewnia 20% bazowej regeneracji HP przyprawy, bez minimalnego +1 i bez zaokrągleń. 1/2/10 HP na tick przyprawy daje odpowiednio +0,2/+0,4/+2 HP na tick. Całkowita regeneracja jedzenia nie może przekroczyć dwukrotności jego własnej bazowej regeneracji. Stare posiłki o większym bonusie są ograniczone podczas odczytu, bez utraty zapisanych HP/staminy/Eitr. Również losowy bonus regeneracji nie omija tego limitu.

**Stosy:** gdy A jest mniejszy od B, przyprawiana jest liczba sztuk min(A,B), a pozostała część B zachowuje dotychczasowe właściwości. Podział wykonywany jest natywnym `ItemData.Clone()` i `Inventory.AddItem(item, amount, x, y, false)` z kontrolą, czy cel jest wolny. Na podział potrzeba jednego wolnego slotu; jeśli go nie ma, operacja nie zużywa jedzenia. Potwierdzenie i odświeżenie ekwipunku pozostają takie jak dotychczas.

**Bez zmian:** Kuźnictwo/Blacksmithing, ZenUI 1.9.3, Kolos, Zawadiaka, Łowca Głów, ikony, GUID i stara gałąź main. Kompilacja CI nie jest testem działania w grze; przed uruchomieniem na Valheim 1.0.21 zachowaj kopię zapisów.

### Poprzednie wydania testowe

## Wersja testowa 0.4.5 — opcjonalna kompatybilność z ZenUI

**ZenUI 1.9.3 (ZenDragon.ZenUI):** po wykryciu moda Sirroh Skills automatycznie dołącza izolowany postfix do `ZenUI.Section.TooltipSummary.GetTooltipSummary`. ZenUI domyślnie generuje uproszczone ikony HP/staminy/Eitr z `m_shared`, ignorując premie unikatowego jedzenia. Adapter modyfikuje wyłącznie liczby w gotowych `<nobr>` z ikonami 💖 💪 ✨, bazując na `m_customData` i istniejącej metodzie Garmażerstwa; jeśli przyprawienie wprowadziło nową statystykę, doda brakującą ikonę. Pełne tooltipy, sposób gotowania, przyprawiania, stacki, statystyki rzeczywiste, jakość i globalny `m_shared` pozostają nietknięte. ZenUI jest **opcjonalny** i nie jest wymaganą zależnością. Dla zmienionego ZenUI lub braku wymaganej metody integracja jest wyłączana z ostrzeżeniem, bez zmiany pozostałych skilli. Nie testowano renderowania w uruchomionym kliencie.

### Poprzednia poprawka 0.4.4 — naprawa oryginalnych wartości jedzenia i natywnego licznika jakości


**Naprawa UI Valheim 1.0.21:** silnik generuje najpierw klucze lokalizacji jedzenia (`$item_food_health`, `$item_food_stamina`, `$item_food_eitr`, `$item_food_regen`), a dopiero później wyświetla polskie etykiety. Poprawione wartości wpisywane są bezpośrednio w te cztery oryginalne linie. Jedynym osobnym dopiskiem pozostaje krótki poziom Garmażerstwa i ewentualny buff od poziomu 75. **Wskaźnik 1–5 przyprawień wykorzystuje istniejącą cyfrę jakości przedmiotu na ikonie**, bez dodatkowych warstw UI i bez modyfikacji właściwego poziomu jakości Valheim. Wartości oraz liczba przypraw nie były utracone w 0.4.3; naprawa dotyczy ich prezentacji.


**Identyfikacja w Gale:** nazwa pakietu to `Sirroh_Skills_Addon`, wyświetlana najczęściej jako „Sirroh Skills Addon” (Thunderstore nie akceptuje nawiasów kwadratowych w technicznej nazwie). **Nazwa BepInEx:** `Sirroh Skills [Addon]`. Ikona: oryginalna tarcza SirRo, 256×256 px. **Ważne:** przed instalacją usuń dawny lokalny pakiet `SirrohSkillsAddon` z profilu Gale, inaczej mogą znaleźć się w nim dwie kopie identycznej DLL. Mod zachowuje GUID `sirroh.skills.addon` oraz wszystkie identyfikatory skilli, konfigurację i zapis. Graficzny zasób pozostał bez zmian w porównaniu z poprawnym archiwum 0.4.1.

**Status: kompilacja CI z referencjami serwera Steam; bez potwierdzonych testów rozgrywki na Valheim 1.0.21.** Gałąź `test/garmazerstwo-kuznictwo-2026-10-28` jest odizolowana od `main`. Przed próbą na własnej postaci/świecie zachowaj kopię zapisów. Klient i serwer muszą mieć zgodną DLL.

- **Kuźnictwo**: polska nazwa i opisy Blacksmithing Expanded 1.2.4, 60 spolszczonych ustawień F1, nazwy żywiołów oraz dopisek „Wykute na poziomie X Kuźnictwa”. Nie zmieniamy zapisanych identyfikatorów ani statystyk Blacksmithing. Zewnętrzny Blacksmithing Expanded nadal jest opcjonalny.
- **Kolos**: oryginalny identyfikator `neithar.Slayer`, użytkownika ikona Broń Dwuręczna.
- **Łowca Głów**: zachowany istniejący system punktów, bez dodatkowej sztucznej umiejętności; plik ikony trofeów jest osadzony, lecz nie zastępuje zwykłych ikon trofeów.
- **Garmażerstwo**: nowy skill `sirroh.skills.addon.garmazerstwo`, +0–20% statystyk gotowanego jedzenia i do +10% z serii Gotowania, od poziomu 75 jeden losowy dodatkowy efekt.
- **Przyprawianie**: upuszczenie posiłku A na B w ekwipunku, natywne potwierdzenie Tak/Nie, zużycie 1:1. Każde z maksymalnie pięciu przyprawień zwiększa sumaryczne statystyki posiłku B o 5–10% **rzeczywistych statystyk przygotowanego posiłku A** (minimum +1 dla obecnej statystyki). Posiłek A może być wcześniej przyprawiony; jego 5 gwiazdek nie przenosi się na B, które otrzymuje jedną nową gwiazdkę. Po przyprawieniu poziom gotowania i procent bazowej premii B aktualizują się do obecnego poziomu Garmażerstwa. Dotychczasowe posiłki z zapisem v1 są odczytywane i migrowane przy następnym przyprawieniu.
- **Stosy**: pełen stos B może być przyprawiony stosami A, gdy A jest co najmniej tyle samo. Częściowe przyprawianie B wymaga najpierw ręcznego rozdzielenia stosu. Jedzenie o różnym `m_customData` nie powinno być łączone.
- **Zapis**: łączna premia HP/staminy/Eitr i regeneracji oraz liczba gwiazdek są zapisane w danych egzemplarza jedzenia, nie w `SharedData`. Nie zapisujemy już nazw ani historii pojedynczych przypraw. Podstawowe linie tooltipu („Życie”, „Kondycja”, „Eitr”, „Leczenie”) pokazują ulepszone wartości; na ikonie jest natywna liczba jakości 1–5. Dla spożytych posiłków działa oddzielny zapis postaci.

**Przed potwierdzeniem stabilności wymagane testy w grze:** czysty profil, uruchomienie wraz z Overhaul, gotowanie/seryjne gotowanie, losowania 75/100, przyprawianie 1:1 i 10:10, pięć gwiazdek, zapis/relog, aktywne jedzenie, QuickStack/Sort/Restock z QOL, ExtraSlots, Blacksmithing 1.2.4, multiplayer/dedicated server. Kompilacja i statyczne testy API nie dowodzą pełnej zgodności rozgrywki.


Rozszerzenie **Sirroh Overhaul** do Valheim 1.0.21 (docelowa wersja; wymagane testy rozgrywki).

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

## v0.3.1: minimalny interfejs Łowcy Głów

- **Tylko zakładka trofeów.** Jedna dodatkowa linia pod trofeum zawiera np. `2/100 • 3/10` (pierwsza liczba: punkty gatunku; druga: zabójstwa do kolejnego punktu).
- **Brak dodatkowego okna, panelu i wiadomości po każdym zabójstwie.** Usunięto IMGUI i odpowiednie opcje. Nadal obowiązują punkty za trofeum oraz +1 za każde 10/5 zabójstw.
- Zachowana migracja starych wartości i zapis naliczonych zabójstw. Bieżące dane są ponownie wyświetlane po otwarciu zakładki trofeów.
- Nazwy gatunków i lista trofeów odświeżają się po zmianie języka.

## v0.3.1: poprawione spolszczenie Blacksmithing Expanded

**Zidentyfikowana przyczyna braku polskich nazw w F1:** ConfigurationManager odczytuje tylko tagi klasy nazwanej dokładnie `ConfigurationManagerAttributes`. Poprzednio używaliśmy klasy `LabelTag`, którą ignorował. Teraz spolszczone są wszystkie 60 oryginalnych ustawień, kategorie oraz opisy konfiguracyjne widoczne w ConfigurationManager.

Dodatkowo:
- rejestracja polskiej nazwy i opisu skilla **Kowalstwo** przez `SkillManager.LocalizeKey.Polish`;
- tłumaczenie opisów przedmiotów (poziom wykucia i rodzaj infuzji) i komunikatu o dodatkowym wykonanym przedmiocie;
- późna inicjalizacja po uruchomieniu obu pluginów, obsługa zmiany języka w trakcie gry, odświeżanie aktywnego okna ConfigurationManager;
- przywrócenie angielskich nazw i opisów ustawień po zmianie języka z polskiego na inny.

**Ważne:** angielskie wewnętrzne identyfikatory kluczy w pliku `org.bepinex.plugins.blacksmithingexpanded.cfg` pozostają bez zmian. Ich tłumaczenie bez migracji zrywałoby powiązanie z zapisanymi ustawieniami; tłumaczymy ich widoczne nazwy i opisy w interfejsie. Identyfikator skilla `Blacksmithing` również pozostaje oryginalny, aby nie zerować progresu.

## Stan i pozostałe testy

- Kompilacja i testy kontraktowe Valheim 1.0 przez GitHub Actions.
- Konieczne testy w grze: w polskim F1 nazwy i opisy 60 opcji, zmiana języka, nazwa skilla Kowalstwo, tooltipy i aktualizacja licznika 1/10 w menu trofeów, multiplayer oraz brak utraty ulepszeń Blacksmithing.
- Nie zmieniono oryginalnego DLL Blacksmithing Expanded ani Sirroh Overhaul; integracja jest opcjonalnym modułem dodatku.
