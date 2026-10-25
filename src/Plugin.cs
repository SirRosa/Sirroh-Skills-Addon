using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Bootstrap;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    [BepInPlugin(Guid, "Sirroh Skills Addon", Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(OverhaulGuid)]
    [BepInIncompatibility("neithar.Slayer")]
    [BepInIncompatibility("neobotics.valheim_mod.slayerskills")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string Guid = "sirroh.skills.addon";
        internal const string OverhaulGuid = "sirro.valheim.pracafizyczna";
        internal const string Version = "0.1.0";
        internal static Plugin Instance;
        internal static Skills.SkillType SlayerType;
        private Harmony harmony;

        internal static bool HasOverhaul =>
            Chainloader.PluginInfos != null && Chainloader.PluginInfos.ContainsKey(OverhaulGuid);

        internal void LogInfo(string message) => Logger.LogInfo(message);
        internal void Warning(string message) => Logger.LogWarning(message);

        private void Awake()
        {
            Instance = this;
            SlayerCombat.Configure(Config);
            SlayerTrophies.Configure(Config);
            BlacksmithingBridge.Initialize(Config);
            SlayerType = SkillManager.Instance.AddSkill(new SkillConfig
            {
                // Reuse the original ID: existing Slayer level survives the migration.
                Identifier = "neithar.Slayer",
                Name = "Pogromca",
                Description = "Broń dwuręczna, opór na odrzut i ogłuszenia",
                Icon = SkillArtwork.MakeAxe(),
                IncreaseStep = 1f
            });
            harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            BlacksmithingBridge.Install(harmony);
            BlacksmithingBridge.Report();
            LogInfo("Slayer/Slayer Skills active; unified XP belongs to Sirroh Overhaul.");
        }

        private void Start()
        {
            // Soft-load support when BE initializes after this BepInEx plugin.
            BlacksmithingBridge.Initialize(Config);
            BlacksmithingBridge.Install(harmony);
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            SlayerTrophies.Clear();
            BlacksmithingBridge.Clear();
            if (ReferenceEquals(Instance, this)) Instance = null;
        }
    }

    internal static class SkillArtwork
    {
        // Self-owned flat monochrome 32px texture; no third-party icons embedded.
        internal static Sprite MakeAxe()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool shaft = Math.Abs(x - (15 - (y - 6) / 4)) <= 2 && y > 4 && y < 28;
                bool blade = y >= 17 && y <= 27 &&
                             x >= 12 && x < (y > 21 ? 28 - (y - 21) : 28 - (21 - y));
                if (shaft || blade)
                    pixels[y * size + x] = new Color32(shaft ? (byte)164 : (byte)200,
                                                         shaft ? (byte)163 : (byte)202,
                                                         shaft ? (byte)158 : (byte)203, 255);
            }
            texture.SetPixels32(pixels);
            texture.filterMode = FilterMode.Point;
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }
    }
}
