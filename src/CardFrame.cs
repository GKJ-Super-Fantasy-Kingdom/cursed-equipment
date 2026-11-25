using System.Runtime.CompilerServices;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CursedEquipment
{
    [HarmonyPatch(typeof(UICardItem),nameof(UICardItem.Init))]
    internal static class CardFrame
    {
        private sealed class NativeFrame
        {
            internal Sprite Default;
            internal Sprite Highlight;
            internal Sprite Selected;
            internal Sprite Current;
            internal Image.Type ImageType;
            internal GameObject Backdrop;
            internal bool BackdropActive;
        }

        private static readonly ConditionalWeakTable<UICardItem,NativeFrame> native =
            new ConditionalWeakTable<UICardItem,NativeFrame>();
        private static Sprite resting, hover, selected;

        private static void Prefix(UICardItem __instance, Image ___card, ref Sprite ___spriteCardDefault,
            ref Sprite ___spriteCardHighlight, ref Sprite ___spriteCardSelected)
        {
            if (!native.TryGetValue(__instance,out var original)) return;
            ___spriteCardDefault=original.Default;
            ___spriteCardHighlight=original.Highlight;
            ___spriteCardSelected=original.Selected;
            ___card.type=original.ImageType;
            ___card.sprite=original.Current;
            if (original.Backdrop) original.Backdrop.SetActive(original.BackdropActive);
            native.Remove(__instance);
        }

        private static void Postfix(UICardItem __instance, Item item, Image ___card,
            ref Sprite ___spriteCardDefault, ref Sprite ___spriteCardHighlight, ref Sprite ___spriteCardSelected)
        {
            if (!Catalog.IsCursed(item) || !___card) return;
            if (!resting)
            {
                hover=Catalog.LoadSprite("frames","equipment-hover");
                selected=Catalog.LoadSprite("frames","equipment-selected");
                resting=Catalog.LoadSprite("frames","equipment");
            }
            // The native wood and its trim sit above the card sprite. The banner,
            // icon and badges are independent siblings and keep their native layers.
            var backdrop=___card.transform.Find("BG")?.gameObject;
            native.Add(__instance,new NativeFrame {
                Default=___spriteCardDefault, Highlight=___spriteCardHighlight, Selected=___spriteCardSelected,
                Current=___card.sprite, ImageType=___card.type, Backdrop=backdrop,
                BackdropActive=backdrop && backdrop.activeSelf
            });
            if (backdrop) backdrop.SetActive(false);
            ___card.sprite=___card.sprite==___spriteCardSelected ? selected
                : ___card.sprite==___spriteCardHighlight ? hover : resting;
            ___spriteCardDefault=resting;
            ___spriteCardHighlight=hover;
            ___spriteCardSelected=selected;
            ___card.type=Image.Type.Simple;
        }
    }
}
