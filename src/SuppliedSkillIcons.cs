using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    internal static class SuppliedSkillIcons
    {
        private static Sprite Load(string resource, Func<Sprite> fallback)
        {
            try
            {
                using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    if (input == null) return fallback();
                    byte[] bytes = new byte[input.Length];
                    int offset = 0, count;
                    while (offset < bytes.Length && (count = input.Read(bytes, offset, bytes.Length - offset)) > 0)
                        offset += count;
                    if (offset != bytes.Length) return fallback();
                    var texture = new Texture2D(96, 96, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(texture, bytes)) return fallback();
                    texture.filterMode = FilterMode.Point;
                    return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(.5f, .5f), 100f);
                }
            }
            catch (Exception e)
            {
                Plugin.Instance?.Warning("Nie udało się wczytać ikony " + resource + ": " + e.GetType().Name);
                return fallback();
            }
        }

        internal static Sprite Kolos() => Load("sirroh.icons.bron_dwureczna.png", SkillArtwork.MakeAxe);
        internal static Sprite Garmazerstwo() => Load("sirroh.icons.garmazerstwo.png", SkillArtwork.MakeDagger);
        internal static Sprite Kuznictwo() => Load("sirroh.icons.kuznictwo.png", SkillArtwork.MakeAxe);
        internal static Sprite LowcaGlow() => Load("sirroh.icons.lowca_glow.png", SkillArtwork.MakeAxe);
    }
}