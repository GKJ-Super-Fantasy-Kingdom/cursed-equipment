using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CursedEquipment
{
    [HarmonyPatch]
    internal static class CardFrame
    {
        private sealed class Trim
        {
            internal Image Clip;
            internal Image IconField;
            internal Image Sigil;
            internal Image Ribbon;
            internal Image Banner;
            internal Color BannerColor;
            internal Vector4 DescriptionMargin;
        }

        private static readonly ConditionalWeakTable<UICardItem, Trim> trims =
            new ConditionalWeakTable<UICardItem, Trim>();
        private static Sprite frame, iconField, sigil, ribbon;
        private static readonly Regex signedPercent = new Regex(@"[+-][0-9]+%");

        [HarmonyPrefix, HarmonyPatch(typeof(UICardItem), nameof(UICardItem.Init))]
        private static void Reset(UICardItem __instance, TextMeshProUGUI ___description)
        {
            if (!trims.TryGetValue(__instance, out var trim) || !trim.Clip.gameObject.activeSelf) return;
            trim.Clip.gameObject.SetActive(false);
            if (trim.IconField) trim.IconField.gameObject.SetActive(false);
            trim.Sigil.gameObject.SetActive(false);
            if (trim.Ribbon)
            {
                trim.Ribbon.gameObject.SetActive(false);
                trim.Banner.color = trim.BannerColor;
            }
            ___description.margin = trim.DescriptionMargin;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UICardItem), nameof(UICardItem.Init))]
        private static void Apply(UICardItem __instance, Item item, Image ___card, TextMeshProUGUI ___title, TextMeshProUGUI ___description)
        {
            if (!Catalog.IsCursed(item) || !___card) return;
            var trim = trims.GetValue(__instance, _ => CreateTrim(___card, ___title));
            if (trim.IconField) trim.IconField.gameObject.SetActive(true);
            trim.Sigil.gameObject.SetActive(true);
            if (trim.Ribbon)
            {
                trim.BannerColor = trim.Banner.color;
                trim.Banner.color *= new Color(.67f, .68f, .92f, 1f);
                trim.Ribbon.gameObject.SetActive(true);
            }
            // Express padding in face units so scaling the native card keeps it consistent.
            trim.DescriptionMargin = ___description.margin;
            var units = ___description.rectTransform.InverseTransformVector(___card.rectTransform.TransformVector(new Vector3(1f, 1f, 0f)));
            ___description.margin += new Vector4(8f * units.x, 2f * units.y, 12f * units.x, 6f * units.y);
            ___description.text = signedPercent.Replace(___description.text, "<nobr>$0</nobr>");
            CopyShape(___card, trim.Clip);
            trim.Clip.gameObject.SetActive(true);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UICardItem), nameof(UICardItem.OnEnter))]
        private static void Hover(UICardItem __instance, Image ___card) => Change(__instance, ___card);

        [HarmonyPostfix, HarmonyPatch(typeof(UICardItem), nameof(UICardItem.OnSelect))]
        private static void Select(UICardItem __instance, Image ___card) => Change(__instance, ___card);

        [HarmonyPostfix, HarmonyPatch(typeof(UICardItem), nameof(UICardItem.OnDeselect))]
        private static void Deselect(UICardItem __instance, Image ___card) => Change(__instance, ___card);

        private static void Change(UICardItem __instance, Image ___card)
        {
            if (trims.TryGetValue(__instance, out var trim) && trim.Clip.gameObject.activeSelf)
                CopyShape(___card, trim.Clip);
        }

        private static void CopyShape(Image surface, Image clip)
        {
            clip.sprite = surface.overrideSprite;
            clip.type = surface.type;
            clip.fillCenter = surface.fillCenter;
            clip.pixelsPerUnitMultiplier = surface.pixelsPerUnitMultiplier;
            clip.preserveAspect = surface.preserveAspect;
            clip.useSpriteMesh = surface.useSpriteMesh;
            clip.fillMethod = surface.fillMethod;
            clip.fillOrigin = surface.fillOrigin;
            clip.fillClockwise = surface.fillClockwise;
            clip.fillAmount = surface.fillAmount;
        }

        private static Trim CreateTrim(Image surface, TextMeshProUGUI title)
        {
            // The transparent artwork is shared with Relics 0.2.8. Native sprites still draw the rim and focus.
            var clip = CreateImage("Cursed Equipment Clip", surface.rectTransform);
            clip.transform.SetAsFirstSibling();
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            CreateImage("Cursed Equipment Art", clip.rectTransform).sprite = frame ??= Catalog.LoadSprite("frames", "shared-trim");
            var layers = new Trim { Clip = clip };
            var field = surface.transform.Find("BG") as RectTransform;
            if (field)
                (layers.IconField = CreateImage("Cursed Equipment Icon Field", field)).sprite = iconField ??= Catalog.LoadSprite("frames", "shared-icon-field");
            layers.Sigil = CreateImage("Cursed Equipment Sigil", surface.rectTransform, new Vector2(.81f, .76f), new Vector2(.90f, .85f));
            layers.Sigil.sprite = sigil ??= Catalog.LoadSprite("frames", "shared-sigil");
            var banner = title ? title.transform.parent as RectTransform : null;
            if (banner && banner.TryGetComponent<Image>(out layers.Banner))
            {
                layers.Ribbon = CreateImage("Cursed Equipment Ribbon", banner, new Vector2(.025f, .355f), new Vector2(.975f, .786f));
                layers.Ribbon.sprite = ribbon ??= Catalog.LoadSprite("frames", "shared-banner");
                layers.Ribbon.transform.SetAsFirstSibling();
            }
            return layers;
        }

        private static Image CreateImage(string name, RectTransform parent) => CreateImage(name, parent, Vector2.zero, Vector2.one);

        private static Image CreateImage(string name, RectTransform parent, Vector2 from, Vector2 to)
        {
            var layer = new GameObject(name, typeof(RectTransform), typeof(Image));
            var image = layer.GetComponent<Image>();
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.pivot = parent.pivot;
            rect.anchorMin = from;
            rect.anchorMax = to;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.raycastTarget = false;
            return image;
        }
    }
}
