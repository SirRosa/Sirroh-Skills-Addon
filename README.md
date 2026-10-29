**Blacksmithing Expanded — 0.4.10:** Łatka tooltipu obejmuje warianty 5 i 6 argumentów `GetTooltip`. Nowa opcja `ShowInfusionsInTooltip` pokazuje odczytaną z oryginalnych danych infuzję bez edytowania zapisu i ustawień BE. Mechanizm wyliczania bonusów, poziomu przedmiotu i oryginalna łatka chroniąca poziom zostały zachowane.

## 0.4.10 TEST — balans Garmażerstwa i kompatybilność

- Gotowanie: maksymalnie +50% do bazowego HP, staminy i Eitr, niezależnie od serii i starszych zapisów. Zachowany klucz FoodBonusAt100_v2 z limitem 50.
- Przyprawianie: maksymalnie +50% bazowych statystyk, bez ograniczania liczby prób. Stare v1/v2/v3 są czytane z nowymi limitami bez zmiany danych przy odczycie. Nie zmieniono transakcji mieszania, reguł stacków ani sekundarnych efektów regeneracji.

# Sirroh Skills [Addon]

## 0.4.9 TEST — Mistrza Kuchni i limit +200% przyprawiania

- **Premia za poziom Garmażerstwa:** na poziomie 100 dokładnie +100% podstawowych HP, staminy i Eitru (na poziomie 40 bez dodatkowej serii +40%). Zachowana premia za serię gotowania nie może przebić +100%. Nowa opcja konfiguracyjna `FoodBonusAt100_v2` zastępuje historyczną wartość 20% bez wymuszania usuwania istniejącego pliku konfiguracyjnego.
- **Premia za przyprawianie:** osobna pula, do **+200% bazowych HP/staminy/Eitru każdego dania**; wartość bazowa równa zero nie może otrzymać dodatniego bonusu tej statystyki. Przykład: danie 25 HP na poziomie 100 daje 50 HP po gotowaniu, dodatkowo maksymalnie 50 HP ze wszystkich przypraw: razem **100 HP** (bazowe 25 HP plus 300% bonusu).
- **Mistrza Kuchni:** po osiągnięciu limitu przypraw we wszystkich bazowo występujących statystykach pojawia się suffix w tooltipie i dalsze ręczne przyprawianie zostaje zablokowane, bez otwierania potwierdzenia i bez zużycia jedzenia.
- **Mieszaj:** symuluje premie dla każdej potencjalnej sztuki przyprawy *przed* otwarciem potwierdzenia; nie zużywa składników bez faktycznego wzrostu HP/staminy/Eitru. Po doprowadzeniu cennych potraw do limitu awansuje kolejne najlepsze pozostałe danie do roli bazy. Ukończone potrawy nie są używane jako składniki. Pozostaje nadzór właściciela ZDO, wardów, zmiany zawartości skrzyni oraz zapis transakcyjny.
- **Stackowanie gotowych potraw:** mistrzowskie warianty tej samej receptury mają kanoniczny profil **premii przyprawiania** (w tym niewielki, stały profil regeneracji), niezależnie od użytej mieszanki składników. Łączą się, jeżeli nadal identyczne są osobne premie od poziomu gotowania, losowy buff i dane innych modów. Mod nie scala potraw mających różne aktywne efekty kosztem utraty danych.
- **Starsze przedmioty:** zapisane przed aktualizacją nadmierne bonusy są ograniczane także podczas odczytu i spożycia; dotychczasowy format `v3` i odczyt historycznych `v1/v2` pozostają zgodne. Aktualizacja nie przepisuje masowo wszystkich skrzyń.

**Status:** kompilacja i testy kontraktowe w CI; działanie w Valheim 1.0.21, multiplayer, QOL/ZenUI i zapisy skrzyń wymaga testu z kopią świata i postaci. `main` i gałąź 0.4.8 pozostają bez zmian.

## 0.4.8 TEST — Mieszaj: hurtowe przyprawianie w otwartej skrzyni

**Nowy przycisk Mieszaj** na bocznej drewnianej desce, pozycjonowany na wysokości panelu otwartej skrzyni. Deska korzysta z istniejącego zasobu `QolFavorites.WoodPanel` w Sirroh QOL (refleksja, bez zmian w QOL). Ikona kociołka `mix_kociolek.png` jest wczytywana z katalogu DLL; kompatybilny zasób osadzony w DLL także jest obsługiwany. Bez QOL pozostałe umiejętności Skills Addon działają niezależnie.

**Selekcja:** każda potrawa otrzymuje ocenę aktualnych HP, staminy, Eitr, regeneracji zdrowia i niewielkiego współczynnika czasu działania. Posiłki uzyskujące co najmniej 85% oceny najlepszego kandydata zostają bazami; słabsze potrawy służą jako przyprawy. W obrębie jednej receptury słabszy wariant może być przyprawą dla innej receptury, lecz nigdy dla samego siebie.

**Stacki:** jedna zużyta sztuka przyprawy wzmacnia cały wybrany stack bazowego dania. Algorytm rozdziela jednostki przypraw pomiędzy uprawnione grupy baz, zachowuje liczbę sztuk w bazowych stackach oraz obce metadane. Nie tworzy nowych stosów. Przydział i efekt pokazuje najpierw natywne potwierdzenie Tak/Nie, a PD naliczane są wyłącznie za faktycznie zużyte przyprawy.

**Bezpieczeństwo:** przed zapisem ponowna weryfikacja wszystkich pozycji i `m_customData` w otwartej skrzyni, praw dostępu i wardów oraz własności ZDO. Wyniki przeliczane są *przed* mutacją ekwipunku. W razie błędu podejmowana jest próba rollbacku i osobne zgłoszenie jego ewentualnej niekompletności. Nie dotykamy skrzyń na statkach.

**Nienaruszone:** ręczne przyprawianie drag/drop, 20% bazowej regeneracji HP przyprawy, limit 2× bazowej regeneracji dania, mechanika stacków 0.4.7, Kuźnictwo, ZenUI, Kolos, Zawadiaka i Łowca Głów, GUID-y oraz `main`.

**Status:** kompilacja i testy CI potwierdzone na gałęzi testowej, **bez testów w uruchomionej grze i w multiplayerze**. Zrób kopię świata i postaci przed testowaniem. Przetestuj skrzynie normalne i PlanBuild, zmiany zawartości w trakcie okna potwierdzenia, przerwanie dostępu, dwa klienty jednocześnie, pojedynczą przyprawę + 50 posiłków, różne premie tej samej potrawy, QuickStack i Sort.

## 0.4.7 TEST — te same dania służą do stackowania, nie do przyprawiania

Przeciągnięcie posiłku na **ten sam rodzaj posiłku** omija system przyprawiania (bez okna i bez zużywania dania). Wtedy standardowy ekwipunek Valheim obsługuje przenoszenie i łączenie stacków. Posiłki o **różnych zapisanych premiach Garmażerstwa** nadal nie są scalane: natywne łączenie takich stacków spowodowałoby utratę odrębnych statystyk. Można łączyć tylko zgodne stacki (ten sam rodzaj i te same zapisane bonusy); niezgodne pozostają osobno. Przyprawianie dwoma *różnymi* daniami działa jak w 0.4.6.

Nie zmieniono wzorów regeneracji, automatycznego dzielenia stacków, Kuźnictwa ani ZenUI. BepInEx GUID, identyfikatory skilli i `main` pozostają bez zmian.


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
