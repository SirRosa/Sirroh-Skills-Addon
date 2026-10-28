using System;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sirroh.SkillsAddon
{
    // A self-contained button: no changes to QOL's UI or hooks.
    // QOL supplies its original wooden backplate at runtime, so the exact
    // user-owned 9-slice is shown without duplicating the asset.
    internal static class GarmazerstwoMixUI
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private static readonly FieldInfo ContainerGrid =
            AccessTools.Field(typeof(InventoryGui), "m_containerGrid");
        private static readonly FieldInfo ContainerRoot =
            AccessTools.Field(typeof(InventoryGui), "m_container");
        private static GameObject _woodRoot, _iconRoot;
        private static RectTransform _wood, _hit;
        private static Image _woodImage, _iconImage;
        private static Sprite _symbol, _qolWood;
        private static Texture2D _symbolTexture;
        private static Material _woodMaterial, _iconMaterial;
        private static InventoryGui _gui;
        private static Canvas _canvas;
        private static int _screenW, _screenH;
        private static bool _visible;
        private static bool _warned;

        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(InventoryGui), "Show"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(GarmazerstwoMixUI),
                    nameof(Shown))));
            harmony.Patch(AccessTools.Method(typeof(InventoryGui), "Hide"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(GarmazerstwoMixUI),
                    nameof(Hidden))));
        }

        private static void Shown(InventoryGui __instance)
        {
            try
            {
                _visible = true;
                Ensure(__instance);
                // UI layout is not guaranteed to be ready in Show().
                _screenW = -1;
                _screenH = -1;
            }
            catch (Exception e)
            {
                Plugin.Instance?.Warning("Mieszaj UI initialization failed: " + e);
                Shutdown();
            }
        }

        private static void Hidden()
        {
            _visible = false;
            if (_woodRoot != null) _woodRoot.SetActive(false);
            if (_iconRoot != null) _iconRoot.SetActive(false);
        }

        private static Sprite ResolveQolWood()
        {
            if (_qolWood != null) return _qolWood;
            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                var t = plugin?.Instance?.GetType().Assembly
                    .GetType("Sirroh.Qol.QolFavorites", false);
                if (t == null) continue;
                var prop = t.GetProperty("WoodPanel",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                _qolWood = prop?.GetValue(null, null) as Sprite;
                if (_qolWood != null) return _qolWood;
            }
            return null;
        }

        // ImageConversionModule in the dedicated-server reference set depends
        // on netstandard 2.1, while the BepInEx plugin targets net48. A
        // reflected call uses the game client's actual Unity module instead
        // of introducing a binary compile/runtime dependency on that DLL.
        private static bool DecodePng(Texture2D texture, byte[] png)
        {
            if (texture == null || png == null || png.Length == 0) return false;
            try
            {
                Type t = AccessTools.TypeByName("UnityEngine.ImageConversion")
                    ?? Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                if (t == null)
                    t = Assembly.Load("UnityEngine.ImageConversionModule")
                        .GetType("UnityEngine.ImageConversion", false);
                if (t == null) return false;
                MethodInfo method = t.GetMethod("LoadImage",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }, null);
                if (method != null)
                    return (bool)method.Invoke(null, new object[] { texture, png, true });
                method = t.GetMethod("LoadImage",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Texture2D), typeof(byte[]) }, null);
                return method != null &&
                    (bool)method.Invoke(null, new object[] { texture, png });
            }
            catch (Exception e)
            {
                Plugin.Instance?.Warning("Mieszaj: błąd dekodera PNG: " + e.Message);
                return false;
            }
        }

        private static Sprite LoadKettle()
        {
            if (_symbol != null) return _symbol;
            // The supplied 96x96 icon is embedded unchanged into the Skills DLL.
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream input = assembly.GetManifestResourceStream(
                "sirroh.mix.kociolek.png"))
            {
                if (input == null)
                {
                    // Exact original PNG is optional in older test packages.
                    // Fallback below uses the previously supplied skill art.
                }
                else
                {
                    byte[] bytes = new byte[input.Length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = input.Read(bytes, offset, bytes.Length - offset);
                        if (read <= 0) break;
                        offset += read;
                    }
                    _symbolTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (DecodePng(_symbolTexture, bytes))
                    {
                        _symbol = Sprite.Create(_symbolTexture,
                            new Rect(0f,0f,_symbolTexture.width,_symbolTexture.height),
                            new Vector2(.5f,.5f),100f);
                        return _symbol;
                    }
                }
            }
            // Distribution-safe external override: the exact PNG from the
            // conversation may be shipped next to the DLL even if the GitHub
            // CI runner cannot access conversation attachments.
            try
            {
                string directory = Path.GetDirectoryName(assembly.Location);
                string path = Path.Combine(directory ?? "", "mix_kociolek.png");
                if (File.Exists(path))
                {
                    var bytes = File.ReadAllBytes(path);
                    _symbolTexture = new Texture2D(2,2,TextureFormat.RGBA32,false);
                    if (DecodePng(_symbolTexture,bytes))
                    {
                        _symbol = Sprite.Create(_symbolTexture,
                            new Rect(0,0,_symbolTexture.width,_symbolTexture.height),
                            new Vector2(.5f,.5f),100f);
                        return _symbol;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Instance?.Warning("Mieszaj: nie można załadować ikony: " +
                    e.GetType().Name);
            }
            // Existing original skill art is suitable only as a safe fallback.
            using (Stream input = assembly.GetManifestResourceStream(
                "sirroh.icons.garmazerstwo.png"))
            {
                if (input == null) return null;
                byte[] bytes = new byte[input.Length];
                input.Read(bytes, 0, bytes.Length);
                _symbolTexture = new Texture2D(2,2,TextureFormat.RGBA32,false);
                if (!DecodePng(_symbolTexture,bytes)) return null;
                _symbol = Sprite.Create(_symbolTexture,
                    new Rect(0,0,_symbolTexture.width,_symbolTexture.height),
                    new Vector2(.5f,.5f),100f);
            }
            return _symbol;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Ensure(InventoryGui gui)
        {
            if (gui == null || gui.m_player == null) return;
            if (_gui == gui && _woodRoot != null && _iconRoot != null) return;
            Shutdown();
            _gui = gui;
            _canvas = gui.GetComponentInParent<Canvas>();
            if (_canvas != null) _canvas = _canvas.rootCanvas;
            Transform parent = gui.m_player.parent ?? gui.transform;

            _woodRoot = new GameObject("SirrohSkillsMixWoodBehind", typeof(RectTransform));
            _woodRoot.transform.SetParent(parent, false);
            Stretch(_woodRoot.GetComponent<RectTransform>());
            _woodRoot.transform.SetSiblingIndex(gui.m_player.GetSiblingIndex());

            _iconRoot = new GameObject("SirrohSkillsMixActions", typeof(RectTransform));
            _iconRoot.transform.SetParent(parent, false);
            Stretch(_iconRoot.GetComponent<RectTransform>());
            _iconRoot.transform.SetSiblingIndex(gui.m_player.GetSiblingIndex() + 1);

            var board = new GameObject("MixWoodTab", typeof(RectTransform), typeof(Image));
            board.transform.SetParent(_woodRoot.transform, false);
            _wood = board.GetComponent<RectTransform>();
            _wood.anchorMin = _wood.anchorMax = new Vector2(.5f,.5f);
            _woodImage = board.GetComponent<Image>();
            _woodImage.raycastTarget = false;
            _woodImage.sprite = ResolveQolWood();
            _woodImage.type = Image.Type.Sliced;

            var click = new GameObject("Mieszaj", typeof(RectTransform),
                typeof(Image), typeof(Button));
            click.transform.SetParent(_iconRoot.transform, false);
            _hit = click.GetComponent<RectTransform>();
            _hit.anchorMin = _hit.anchorMax = new Vector2(.5f,.5f);
            Image transparentHit = click.GetComponent<Image>();
            transparentHit.color = Color.clear;
            transparentHit.raycastTarget = true;
            Button button = click.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(GarmazerstwoMixing.Click);

            var graphic = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            graphic.transform.SetParent(click.transform, false);
            var gr = graphic.GetComponent<RectTransform>();
            gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
            gr.offsetMin = new Vector2(9f,9f);
            gr.offsetMax = new Vector2(-9f,-9f);
            _iconImage = graphic.GetComponent<Image>();
            _iconImage.sprite = LoadKettle();
            _iconImage.preserveAspect = true;
            _iconImage.raycastTarget = false;

            var hover = click.AddComponent<MixHover>();
            hover.Initialize(_iconImage);
            SyncLighting();
        }

        private static bool Blocked(InventoryGui gui)
        {
            return gui.m_skillsDialog != null && gui.m_skillsDialog.gameObject.activeInHierarchy
                || gui.m_textsDialog != null && gui.m_textsDialog.gameObject.activeInHierarchy
                || gui.m_trophiesPanel != null && gui.m_trophiesPanel.activeInHierarchy
                || gui.m_achievementsPanel != null && gui.m_achievementsPanel.gameObject.activeInHierarchy
                || gui.m_variantDialog != null && gui.m_variantDialog.gameObject.activeInHierarchy
                || gui.m_splitDialog != null && gui.m_splitDialog.gameObject.activeInHierarchy;
        }

        private static RectTransform ChestPanel(InventoryGui gui)
        {
            object raw = ContainerRoot?.GetValue(gui);
            RectTransform panel = raw as RectTransform;
            if (panel == null && raw is GameObject go) panel = go.GetComponent<RectTransform>();
            if (panel == null && raw is Component component)
                panel = component.transform as RectTransform;
            if (panel != null) return panel;

            raw = ContainerGrid?.GetValue(gui);
            Transform grid = (raw as Component)?.transform;
            if (grid == null && raw is GameObject gridObject) grid = gridObject.transform;
            // ContainerGrid lives inside the chest panel. Its grandparent
            // is preferable to the slot grid for aligning the wooden tab.
            return grid?.parent?.parent as RectTransform ??
                   grid?.parent as RectTransform;
        }

        private static readonly Vector3[] Corners = new Vector3[4];
        private static Camera EventCamera =>
            _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera : null;

        private static Rect ScreenRect(RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(EventCamera,Corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(EventCamera,Corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),
                                   Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
        }

        private static void SetScreenRect(RectTransform rt, float x, float y,
            float width, float height)
        {
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f);
            rt.pivot = new Vector2(.5f,.5f);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,new Vector2(x,y),EventCamera,out Vector2 local))
                rt.anchoredPosition = local;
            float s = _canvas != null ? Mathf.Max(.01f,_canvas.scaleFactor) : 1f;
            rt.sizeDelta = new Vector2(width/s,height/s);
        }

        private static void Position(InventoryGui gui)
        {
            var playerPanel = gui.m_player as RectTransform;
            var weight = gui.m_player.Find("Weight") as RectTransform;
            var chestPanel = ChestPanel(gui);
            if (playerPanel == null || chestPanel == null) return;
            float scale = Mathf.Min(Screen.width/ReferenceWidth,
                                    Screen.height/ReferenceHeight);
            var playerBox = ScreenRect(playerPanel);
            var chestBox = ScreenRect(chestPanel);
            if (chestBox.width < 100f*scale || chestBox.height < 75f*scale) return;

            float edge = playerBox.xMax + 66f*scale;
            float width = 84f*scale;
            if (weight != null)
            {
                var weightBox = ScreenRect(weight);
                if (weightBox.width >= 35f*scale && weightBox.width <= 145f*scale &&
                    weightBox.xMax > playerBox.xMax + 12f*scale &&
                    weightBox.xMax < playerBox.xMax + 160f*scale)
                {
                    edge = weightBox.xMax;
                    width = Mathf.Clamp(edge-playerBox.xMax+16f*scale,
                                        72f*scale,105f*scale);
                }
            }
            float boardX = edge - width*.5f;
            float iconX = boardX + Mathf.Min(6f*scale,width*.18f);
            // Chest height, aligned with the same inventory side-rail width.
            float centerY = chestBox.center.y;
            SetScreenRect(_wood,boardX,centerY,width,55f*scale);
            SetScreenRect(_hit,iconX,centerY,52f*scale,52f*scale);
            _screenW = Screen.width;
            _screenH = Screen.height;
        }

        private static void SyncLighting()
        {
            if (_gui == null || _woodImage == null || _iconImage == null) return;
            var sprite = ResolveQolWood();
            if (sprite == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    Plugin.Instance?.Warning("Mieszaj: oczekiwanie na deskę QOL (ui_wood).");
                }
                return;
            }
            _woodImage.sprite = sprite;
            var images = _gui.m_player.GetComponentsInChildren<Image>(true);
            Material native = null;
            foreach (var img in images)
                if (img?.material?.shader != null &&
                    img.material.shader.name == "Custom/LitGui")
                { native = img.material; break; }
            if (native == null)
            {
                Shader shader = Shader.Find("Custom/LitGui");
                if (shader == null || !shader.isSupported) return;
                native = new Material(shader);
            }
            if (_woodMaterial == null)
            {
                _woodMaterial = new Material(native);
                _woodMaterial.hideFlags = HideFlags.HideAndDontSave;
                Calibrate(_woodMaterial,native,1.25f,.16f);
            }
            if (_iconMaterial == null)
            {
                _iconMaterial = new Material(native);
                _iconMaterial.hideFlags = HideFlags.HideAndDontSave;
                Calibrate(_iconMaterial,native,2.40f,.40f);
            }
            _woodImage.material = _woodMaterial;
            _iconImage.material = _iconMaterial;
        }

        private static void Calibrate(Material target, Material reference,
            float brightness, float blend)
        {
            int id = Shader.PropertyToID("_Brightness");
            if (!target.HasProperty(id) || !reference.HasProperty(id)) return;
            float original = reference.GetFloat(id);
            target.SetFloat(id, Mathf.Clamp(
                original+(Mathf.Max(original,brightness)-original)*blend,.01f,4f));
        }

        internal static void Tick()
        {
            if (_gui == null || _woodRoot == null) return;
            try { TickSafe(); }
            catch (Exception e)
            {
                Plugin.Instance?.Warning("Mieszaj UI update disabled: " + e);
                Shutdown();
            }
        }

        private static void TickSafe()
        {
            bool show = _visible && _gui == InventoryGui.instance &&
                ChestPanel(_gui) != null &&
                InventoryGui.IsVisible() && _gui.m_player != null &&
                _gui.m_player.gameObject.activeInHierarchy &&
                Garmazerstwo.Enabled?.Value == true &&
                GarmazerstwoMixing.CurrentContainer(_gui) != null &&
                !Blocked(_gui) && !Menu.IsVisible() && !UnifiedPopup.IsVisible();
            if (show && (!_woodRoot.activeSelf || _screenW != Screen.width ||
                         _screenH != Screen.height || _woodImage.sprite == null ||
                         _woodMaterial == null))
            {
                Position(_gui);
                SyncLighting();
            }
            if (_woodRoot.activeSelf != show) _woodRoot.SetActive(show);
            if (_iconRoot.activeSelf != show) _iconRoot.SetActive(show);
        }

        internal static void Shutdown()
        {
            if (_woodRoot != null) UnityEngine.Object.Destroy(_woodRoot);
            if (_iconRoot != null) UnityEngine.Object.Destroy(_iconRoot);
            if (_woodMaterial != null) UnityEngine.Object.Destroy(_woodMaterial);
            if (_iconMaterial != null) UnityEngine.Object.Destroy(_iconMaterial);
            _woodRoot = _iconRoot = null;
            _wood = _hit = null;
            _woodImage = _iconImage = null;
            _woodMaterial = _iconMaterial = null;
            _gui = null;
            _canvas = null;
        }

        internal sealed class MixHover : MonoBehaviour,
            IPointerEnterHandler, IPointerExitHandler
        {
            private Image graphic;
            private Outline outline;

            internal void Initialize(Image img)
            {
                graphic = img;
                outline = img.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f,.80f,.43f,.78f);
                outline.effectDistance = new Vector2(1.6f,1.6f);
                outline.enabled = false;
                graphic.color = new Color(.89f,.89f,.89f,1f);
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (graphic != null) graphic.color = Color.white;
                if (outline != null) outline.enabled = true;
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                if (graphic != null) graphic.color = new Color(.89f,.89f,.89f,1f);
                if (outline != null) outline.enabled = false;
            }
        }
    }
}