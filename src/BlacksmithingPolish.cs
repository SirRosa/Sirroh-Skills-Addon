
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    /// <summary>Polish UI for Blacksmithing Expanded 1.2.4; never renames config keys or skill IDs.</summary>
    internal static class BlacksmithingPolish
    {
        // ConfigurationManager requires this exact class name (not merely matching fields).
        private sealed class ConfigurationManagerAttributes
        {
            public string DispName;
            public string Category;
        }

        private struct Translation
        {
            internal readonly string Name, Help;
            internal Translation(string name, string help) { Name = name; Help = help; }
        }

        private static readonly Dictionary<string, Translation> Text = new Dictionary<string, Translation>(StringComparer.Ordinal)
        {
            {"General|Skill gain factor", new Translation("Mnożnik zdobywania PD", "Mnożnik doświadczenia Kuźnictwa. 1,5 oznacza o 50% szybszy rozwój.")},
            {"General|Skill effect factor", new Translation("Mnożnik skuteczności", "Siła wszystkich premii Kuźnictwa: obrażeń, pancerza i wytrzymałości.")},
            {"General|Workstation infusion milestone interval", new Translation("Próg ulepszeń pieców", "Co tyle poziomów Kuźnictwa zwiększa się poziom premii pieców i mielerzy.")},
            {"General|Enable smelting speed bonus", new Translation("Premia prędkości przetapiania", "Włącz premię szybkości pieca hutniczego i wielkiego pieca. Wyłącz przy konfliktach z innymi modami.")},
            {"General|Enable kiln speed bonus", new Translation("Premia prędkości mielerza", "Włącz premię szybkości produkcji węgla w mielerzu.")},
            {"General|Blacksmithing workstations", new Translation("Stanowiska Kuźnictwa", "Prefaby pieców oraz mielerzy rozdzielone przecinkami. Stanowiska rzemieślnicze dają PD niezależnie od tej listy.")},
            {"General|Smelting speed bonus per tier", new Translation("Szybkość przetapiania na próg", "Przyspieszenie na kolejny próg umiejętności; 0,15 odpowiada 15%.")},
            {"General|Kiln speed bonus per tier", new Translation("Szybkość mielerza na próg", "Przyspieszenie wytwarzania węgla na kolejny próg; 0,15 odpowiada 15%.")},
            {"General|Infusion expire time", new Translation("Czas trwania wzmocnienia", "Czas działania premii pieca lub mielerza po dodaniu paliwa bądź rudy, w sekundach.")},
            {"General|Show infusion visual effect", new Translation("Efekt wizualny wzmocnienia", "Wyświetlaj pomarańczowe światło, gdy urządzenie korzysta z premii prędkości.")},
            {"General|Ore save chance at 100", new Translation("Szansa zachowania rudy na 100", "Szansa niewykorzystania rudy przy przetapianiu na poziomie 100; 0,2 = 20%.")},
            {"General|Extra item chance at 100", new Translation("Szansa dodatkowego przedmiotu na 100", "Szansa dodatkowego przedmiotu po wytworzeniu na poziomie 100; 0,05 = 5%.")},
            {"General|Enable station level bonus", new Translation("Premia poziomu stanowiska", "Kuźnictwo zwiększa wirtualny poziom warsztatu i kuźni.")},
            {"General|Enable extra item bonus", new Translation("Premia dodatkowych przedmiotów", "Włącz możliwość uzyskania dodatkowego przedmiotu przy tworzeniu.")},
            {"General|Enable ore save bonus", new Translation("Oszczędzanie rudy", "Włącz szansę zachowania rudy w trakcie przetapiania.")},
            {"PercentageSystem|Use percentage upgrade bonus", new Translation("Procentowe premie ulepszeń", "Obliczaj premie ulepszeń procentowo zamiast dodawać stałą wartość.")},
            {"PercentageSystem|Stat percentage bonus per upgrade", new Translation("Procent statystyk na ulepszenie", "Premia procentowa do statystyk na każdy poziom jakości przedmiotu.")},
            {"PercentageSystem|Use percentage damage bonus", new Translation("Procentowe premie obrażeń", "Obliczaj premię obrażeń procentowo zamiast płasko.")},
            {"PercentageSystem|Damage percentage bonus per tier", new Translation("Procent obrażeń na próg", "Procentowa premia do obrażeń za kolejny próg Kuźnictwa.")},
            {"PercentageSystem|Use percentage elemental bonus", new Translation("Procentowe premie żywiołów", "Obliczaj premie obrażeń żywiołów procentowo zamiast stałymi punktami.")},
            {"PercentageSystem|Elemental percentage bonus per tier", new Translation("Procent żywiołów na próg", "Premia procentowa obrażeń żywiołowych na kolejny próg.")},
            {"Item Filtering|Use YAML item filtering", new Translation("Filtrowanie przedmiotów YAML", "Używaj list dozwolonych i zablokowanych prefabów z pliku BlacksmithExpItemList.yml.")},
            {"Item Filtering|Log filtered items", new Translation("Logowanie filtrowanych przedmiotów", "Zapisuj w konsoli odrzucone przedmioty.")},
            {"XP|XP per craft", new Translation("PD za wytworzenie", "Bazowe PD Kuźnictwa przy stworzeniu przedmiotu.")},
            {"XP|XP per smelt", new Translation("PD za przetapianie", "Bazowe PD Kuźnictwa za dodanie rudy do urządzenia.")},
            {"XP|XP per repair", new Translation("PD za naprawę", "Bazowe PD Kuźnictwa za naprawienie przedmiotu.")},
            {"XP|XP per upgrade", new Translation("PD za ulepszenie", "Bazowe PD Kuźnictwa za zwiększenie poziomu jakości przedmiotu.")},
            {"XP|First craft bonus XP", new Translation("Premia PD za pierwsze wytworzenie", "Jednorazowe dodatkowe PD za pierwszy egzemplarz każdego rodzaju przedmiotu.")},
            {"Tooltip|Show level in tooltip", new Translation("Poziom Kuźnictwa w opisie", "Pokazuj poziom Kuźnictwa, na którym wykuto przedmiot.")},
            {"Tooltip|Show infusion in tooltip", new Translation("Nasycenie żywiołem w opisie", "Pokazuj rodzaj żywiołowego nasycenia przedmiotu.")},
            {"Durability|Durability tier interval", new Translation("Odstęp progów wytrzymałości", "Co ile poziomów Kuźnictwa odblokować następną premię wytrzymałości.")},
            {"Durability|Durability bonus per tier", new Translation("Wytrzymałość na próg", "Stała liczba punktów wytrzymałości dodawanych za próg.")},
            {"Durability|Durability bonus per upgrade", new Translation("Wytrzymałość na ulepszenie", "Dodatkowa wytrzymałość za poziom jakości przedmiotu.")},
            {"Durability|Respect original durability", new Translation("Zachowaj oryginalną wytrzymałość", "Zwiększaj wytrzymałość tylko przedmiotów, które pierwotnie ją posiadały.")},
            {"Durability|Max durability cap", new Translation("Limit wytrzymałości", "Najwyższa dozwolona wytrzymałość; 0 oznacza brak limitu.")},
            {"Durability|Allow non-repairable items", new Translation("Premie dla przedmiotów nienaprawialnych", "Zezwalaj na premie przedmiotów bez wytrzymałości, w tym pochodni i przedmiotów zużywalnych.")},
            {"Durability|Enable durability bonus", new Translation("Włącz premię wytrzymałości", "Włącz modyfikowanie wytrzymałości. Wyłącz przy konfliktach z innymi modami lub problemach z zapisami.")},
            {"Stats|Enable damage bonus", new Translation("Włącz premię obrażeń", "Kuźnictwo zwiększa obrażenia wykonanej broni.")},
            {"Stats|Enable armor bonus", new Translation("Włącz premię pancerza", "Kuźnictwo zwiększa wartość pancerza wyposażenia.")},
            {"Stats|Boost elemental weapons", new Translation("Wzmacniaj broń żywiołową", "Uwzględniaj broń mającą już wbudowane obrażenia żywiołowe.")},
            {"Stats|Elemental weapon boost chance", new Translation("Szansa premii żywiołowej", "Szansa wzmocnienia żywiołu zamiast obrażeń fizycznych w broni mieszanej.")},
            {"Stats|Stat tier interval", new Translation("Odstęp progów statystyk", "Liczba poziomów Kuźnictwa pomiędzy kolejnymi premiami obrażeń i pancerza.")},
            {"Stats|Armor bonus per tier", new Translation("Pancerz na próg", "Stała premia pancerza za każdy osiągnięty próg.")},
            {"Stats|Armor bonus per upgrade", new Translation("Pancerz na ulepszenie", "Premia pancerza za poziom jakości przedmiotu.")},
            {"Stats|Armor cap", new Translation("Maksymalny pancerz", "Maksymalna wartość pancerza pojedynczego przedmiotu; 0 bez limitu.")},
            {"Stats|Damage bonus per tier", new Translation("Obrażenia na próg", "Płaska premia jednego rodzaju obrażeń na próg, przy wyłączonym systemie procentowym.")},
            {"Stats|Stat bonus per upgrade", new Translation("Statystyki na ulepszenie", "Stała premia obrażeń lub pancerza na poziom jakości, gdy procentowe ulepszanie jest wyłączone.")},
            {"Elemental|Enable elemental bonus", new Translation("Włącz premię żywiołową", "Włącz obrażenia żywiołowe z Kuźnictwa; wyłącz przy konfliktach.")},
            {"Elemental|Add elemental at milestone", new Translation("Dodawaj żywioł po osiągnięciu progu", "Losuj żywiołowe obrażenia po przekroczeniu wymaganego poziomu Kuźnictwa.")},
            {"Elemental|Elemental unlock level", new Translation("Poziom odblokowania żywiołów", "Poziom Kuźnictwa wymagany do nasycania wykonanej broni żywiołem.")},
            {"Elemental|Fire bonus per tier", new Translation("Ogień na próg", "Premia obrażeń od ognia na próg, wywołuje podpalenie.")},
            {"Elemental|Frost bonus per tier", new Translation("Mróz na próg", "Premia obrażeń od mrozu na próg.")},
            {"Elemental|Lightning bonus per tier", new Translation("Błyskawice na próg", "Premia obrażeń od błyskawic na próg, skuteczna przeciw mokrym wrogom.")},
            {"Elemental|Poison bonus per tier", new Translation("Trucizna na próg", "Premia obrażeń od trucizny na próg.")},
            {"Elemental|Spirit bonus per tier", new Translation("Duch na próg", "Premia obrażeń od ducha na próg, szczególnie przeciw nieumarłym.")},
            {"Shields|Enable shield bonus", new Translation("Włącz premię tarcz", "Włącz premie siły bloku i parowania; wyłącz przy konfliktach z innymi modami.")},
            {"Shields|Timed block bonus per tier", new Translation("Premia parowania na próg", "Premia do skuteczności parowania za osiągnięty próg.")},
            {"Shields|Timed block bonus per upgrade", new Translation("Premia parowania na ulepszenie", "Dodatkowa skuteczność parowania za poziom jakości tarczy.")},
            {"Shields|Block power bonus per tier", new Translation("Siła bloku na próg", "Dodatkowa siła bloku za próg Kuźnictwa.")},
            {"Shields|Block power bonus per upgrade", new Translation("Siła bloku na ulepszenie", "Dodatkowa siła bloku za poziom jakości tarczy.")},
        };

        private static readonly Dictionary<string, string> Categories = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            {"General", "Ogólne"},
            {"PercentageSystem", "System procentowy"},
            {"Item Filtering", "Filtrowanie przedmiotów"},
            {"XP", "Doświadczenie"},
            {"Tooltip", "Podpowiedzi"},
            {"Durability", "Wytrzymałość"},
            {"Stats", "Statystyki"},
            {"Elemental", "Żywioły"},
            {"Shields", "Tarcze"},
            {"Blacksmithing", "Kuźnictwo"}
        };

        private static readonly Dictionary<string, string> Elements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            {"Fire", "Ogień"}, {"Frost", "Mróz"}, {"Lightning", "Błyskawice"},
            {"Poison", "Trucizna"}, {"Spirit", "Duch"}
        };

        private static bool warned;
        private static bool tooltipInstalled, messageInstalled;
        private static bool lastPolish, skillTranslated;
        private static float nextCheck;
        private static readonly Dictionary<ConfigEntryBase, ConfigDescription> OriginalDescriptions =
            new Dictionary<ConfigEntryBase, ConfigDescription>();
        private static readonly Regex Forged = new Regex(@"Forged at Blacksmithing (\d+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex Infused = new Regex(@"Elemental Infusion: (Fire|Frost|Lightning|Poison|Spirit)", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        internal static bool PolishEnabled =>
            Localization.instance != null &&
            string.Equals(Localization.instance.GetSelectedLanguage(), "Polish", StringComparison.OrdinalIgnoreCase);

        internal static void Install(Harmony harmony)
        {
            if (harmony == null || !Available()) return;
            Apply();
            if (!tooltipInstalled)
            {
                try
                {
                    var tooltip = AccessTools.Method(typeof(ItemDrop.ItemData), "GetTooltip",
                        new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) });
                    if (tooltip != null)
                    {
                        harmony.Patch(tooltip, postfix: new HarmonyMethod(
                            AccessTools.Method(typeof(BlacksmithingPolish), nameof(TranslateTooltip))) { priority = Priority.Last });
                        tooltipInstalled = true;
                    }
                }
                catch (Exception ex) { WarnOnce("Nie udało się spolszczyć opisów przedmiotów: " + ex.Message); }
            }
            if (!messageInstalled)
            {
                try
                {
                    // Only this exact vanilla signature is patched, and only this mod's message is translated.
                    var message = AccessTools.Method(typeof(Character), "Message", new[]
                        { typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(Sprite), typeof(bool) });
                    if (message != null)
                    {
                        harmony.Patch(message, prefix: new HarmonyMethod(
                            AccessTools.Method(typeof(BlacksmithingPolish), nameof(TranslateMessage))));
                        messageInstalled = true;
                    }
                }
                catch (Exception ex) { WarnOnce("Komunikaty Kuźnictwa: " + ex.Message); }
            }
        }

        private static bool Available() => BepInEx.Bootstrap.Chainloader.PluginInfos != null &&
            BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("org.bepinex.plugins.blacksmithingexpanded");

        // Language is frequently selected after BepInEx Awake/Start.
        // Recheck cheaply until Blacksmithing has registered its SkillManager entry.
        internal static void Tick()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 2f;
            if (!Available()) return;
            if (lastPolish != PolishEnabled || (PolishEnabled && !skillTranslated)) Apply();
        }

        internal static void Apply()
        {
            if (!Available()) return;
            try
            {
                bool polish = PolishEnabled;
                var plugin = BepInEx.Bootstrap.Chainloader.PluginInfos[
                    "org.bepinex.plugins.blacksmithingexpanded"].Instance;
                skillTranslated = TranslateSkill(plugin.GetType(), polish);
                bool changed = TranslateConfiguration(plugin.Config, polish);
                lastPolish = polish;
                if (changed) RefreshConfigurationManager();
            }
            catch (Exception ex) { WarnOnce("Tłumaczenie Blacksmithing: " + ex.Message); }
        }

        private static bool TranslateSkill(Type pluginType, bool polish)
        {
            var f = AccessTools.Field(pluginType, "blacksmithSkill");
            var skill = f?.GetValue(null);
            if (skill == null) return false;
            var t = skill.GetType();
            bool translated = true;
            foreach (var entry in new[] { ("Name", polish ? "Kuźnictwo" : "Blacksmithing"), ("Description",
                polish ? "Udoskonala wytwarzane wyposażenie: zwiększa obrażenia, pancerz i wytrzymałość przedmiotów." :
                "Craft better, last longer. Improves durability, damage, and armor of crafted items.") })
            {
                var localizeKey = AccessTools.Field(t, entry.Item1)?.GetValue(skill);
                if (localizeKey == null) continue;
                var localizer = AccessTools.Method(localizeKey.GetType(), "Polish",
                    new[] { typeof(string) });
                if (localizer == null) translated = false;
                else localizer.Invoke(localizeKey, new object[] { entry.Item2 });
            }
            try
            {
                // Retain the original registered skill ID and all saved item levels.
                var skillDef = AccessTools.Field(t, "skillDef")?.GetValue(skill);
                var icon = SuppliedSkillIcons.Kuznictwo();
                if (skillDef != null && icon != null)
                    AccessTools.Field(skillDef.GetType(), "m_icon")?.SetValue(skillDef, icon);
            }
            catch (Exception e) { WarnOnce("Kuźnictwo — ikona: " + e.Message); }
            return translated;
        }

        private static bool TranslateConfiguration(ConfigFile config, bool polish)
        {
            if (config == null) return false;
            // UI labels are metadata only; serialized Section/Key remain 100% unchanged.
            var descProperty = typeof(ConfigEntryBase).GetProperty("Description",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var backing = AccessTools.Field(typeof(ConfigEntryBase), "<Description>k__BackingField");
            int matched = 0, applied = 0;
            bool changed = false;
            foreach (var pair in config)
            {
                var definition = pair.Key;
                var entry = pair.Value;
                string index = definition.Section + "|" + definition.Key;
                if (!Text.TryGetValue(index, out var translation))
                {
                    if (definition.Key == "Skill gain factor" || definition.Key == "Skill effect factor" || definition.Key == "Skill loss")
                    {
                        // The embedded SkillManager uses its own configurable skill section.
                        translation = new Translation(
                            definition.Key == "Skill gain factor" ? "Mnożnik zdobywania PD" :
                            definition.Key == "Skill effect factor" ? "Mnożnik premii" : "Utrata umiejętności",
                            definition.Key == "Skill loss" ? "Procent umiejętności tracony po śmierci." :
                            "Mnożnik zdobywania doświadczenia lub siły umiejętności.");
                    }
                    else continue;
                }
                matched++;
                if (!OriginalDescriptions.TryGetValue(entry, out var original))
                {
                    original = entry.Description;
                    if (original == null) continue;
                    OriginalDescriptions[entry] = original;
                }
                try
                {
                    ConfigDescription newDescription = original;
                    if (polish)
                    {
                        var tags = original.Tags == null ? new List<object>() : new List<object>(original.Tags);
                        // Override tags from other plugins without mutating their original objects.
                        tags.RemoveAll(tag => tag != null &&
                            tag.GetType().Name == "ConfigurationManagerAttributes");
                        tags.Add(new ConfigurationManagerAttributes
                        {
                            DispName = translation.Name,
                            Category = Categories.TryGetValue(definition.Section, out string translated) ?
                                translated : definition.Section
                        });
                        newDescription = new ConfigDescription(translation.Help,
                            original.AcceptableValues, tags.ToArray());
                    }
                    if (ReferenceEquals(entry.Description, newDescription) ||
                        (polish && entry.Description?.Description == newDescription.Description &&
                         entry.Description.Tags.Any(tag => tag?.GetType() == typeof(ConfigurationManagerAttributes))))
                        continue;
                    var setter = descProperty?.GetSetMethod(true);
                    if (setter != null) setter.Invoke(entry, new object[] { newDescription });
                    else if (backing != null) backing.SetValue(entry, newDescription);
                    else { WarnOnce("Brak możliwości podmiany metadanych konfiguracji bez zmiany kluczy."); break; }
                    applied++;
                    changed = true;
                }
                catch (Exception ex) { WarnOnce("Metadane konfiguracji: " + ex.Message); break; }
            }
            if (changed) Plugin.Instance.LogInfo("Blacksmithing PL: " +
                (polish ? "spolszczono " : "przywrócono ") + applied + "/" + matched +
                " opcji konfiguracji (nazwy pliku i klucze pozostają oryginalne).");
            return changed;
        }

        private static void RefreshConfigurationManager()
        {
            try
            {
                var managerType = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "ConfigurationManager")?
                    .GetType("ConfigurationManager.ConfigurationManager");
                if (managerType == null || ChainloaderManager() == null) return;
                var manager = ChainloaderManager().GetComponent(managerType);
                if (manager == null) return;
                var visible = AccessTools.Property(managerType, "DisplayingWindow")?.GetValue(manager, null);
                if (!(visible is bool open) || !open) return;
                AccessTools.Method(managerType, "BuildSettingList")?.Invoke(manager, null);
            }
            catch (Exception e) { WarnOnce("Odświeżanie konfiguracji: " + e.Message); }
        }

        private static GameObject ChainloaderManager()
        {
            return BepInEx.Bootstrap.Chainloader.ManagerObject;
        }

        private static void TranslateTooltip(ref string __result)
        {
            if (!PolishEnabled || string.IsNullOrEmpty(__result)) return;
            __result = Forged.Replace(__result, "Wykute na poziomie $1 Kuźnictwa");
            __result = Infused.Replace(__result, match => "Nasycenie żywiołem: " + Elements[match.Groups[1].Value]);
        }

        private static void TranslateMessage(ref string __1)
        {
            if (PolishEnabled && __1 == "Masterwork crafting created an extra item!")
                __1 = "Mistrzowskie wykonanie: otrzymano dodatkowy przedmiot!";
        }

        [HarmonyPatch(typeof(Localization), "SetLanguage")]
        private static class RefreshWhenLanguageChanges
        {
            [HarmonyPostfix]
            private static void Postfix() => Apply();
        }

        private static void WarnOnce(string text)
        {
            if (warned) return;
            warned = true;
            Plugin.Instance?.Warning(text);
        }
    }
}
