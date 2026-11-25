using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using SuperFantasyKingdom;
using UnityEngine;

namespace CursedEquipment
{
    [HarmonyPatch]
    internal static class Catalog
    {
        internal const string Glass = "GKJ_CE_CrackedSpellglass";
        internal const string Cup = "GKJ_CE_BitterChalice";
        internal const string Mirror = "GKJ_CE_CrackedDuelistsMirror";
        internal const string Fang = "GKJ_CE_DuelistsFang";
        internal const string Plate = "GKJ_CE_MartyrsPlate";
        internal const string Lantern = "GKJ_CE_GravediggersLantern";
        internal const string Quarrel = "GKJ_CE_HangmansQuarrel";
        internal const string Blade = "GKJ_CE_EmberwakeBlade";
        internal const string Locket = "GKJ_CE_LeadheartLocket";
        internal const string Halo = "GKJ_CE_BorrowedHalo";

        private static readonly Dictionary<string, (string title, string gift, string curse, string art)> Cards =
            new Dictionary<string, (string, string, string, string)>
            {
                [Glass] = ("Cracked Spellglass", "Damaging circles gain 40% radius.", "Each target takes 20% less damage.", "glass"),
                [Cup] = ("Bitter Chalice", "Receive 50% more healing.", "Heals arrive 3 seconds late.", "chalice"),
                [Mirror] = ("Cracked Duelist's Mirror", "Double the first hit to each monster's health.", "Later hits deal 25% less.", "mirror"),
                [Fang] = ("Duelist's Fang", "+10% damage per repeat hit, up to +50%.", "Switching targets costs 30% damage on that hit.", "fang"),
                [Plate] = ("Martyr's Plate", "Take 30% less Health damage.", "Receive 50% less healing.", "plate"),
                [Lantern] = ("Gravedigger's Lantern", "+40% damage to lone targets.", "-15% if an ally hit the target in the last 2s.", "lantern"),
                [Quarrel] = ("Hangman's Quarrel", "+60% damage to foes below 20% Health.", "-15% against healthier foes.", "quarrel"),
                [Blade] = ("Emberwake Blade", "Every 4th hit on a surviving foe applies Burn.", "Each Burn costs 5% max Health, down to 1 HP.", "emberwake"),
                [Locket] = ("Leadheart Locket", "Below half Health, take 35% less Health damage.", "Heals stop at 75% Health.", "locket"),
                [Halo] = ("The Borrowed Halo", "Survive 1 lethal hit per battle. Block Health hits for 2s.", "Deal half damage for 8s.", "halo")
            };

        // All equipment tests read the native unit inventory at the point of use.
        internal static bool Wears(Entity entity, string id) =>
            entity is UnitBase && entity.GetItems()?.Contains(id) == true;

        internal static bool IsCursed(Item item) => Cards.ContainsKey(item.GetIdentifier() ?? "");

        [HarmonyPatch(typeof(AddressablesManager), "Awake"), HarmonyPostfix]
        internal static void Register(AddressablesManager __instance)
        {
            var items = __instance.GetAllItems();
            var ids = __instance.GetAllItemIdentifiers();
            foreach (var card in Cards)
            {
                if (items.ContainsKey(card.Key)) continue;
                var prefab = new GameObject(card.Key);
                prefab.SetActive(false);
                prefab.transform.SetParent(__instance.transform, false);
                var item = prefab.AddComponent<Item>();
                var fields = Traverse.Create(item);
                fields.Field("identifier").SetValue(card.Key);
                fields.Field("icon").SetValue(LoadSprite("items",card.Value.art,16));
                fields.Field("soldAtBlacksmith").SetValue(true);
                fields.Field("rarity").SetValue(Rarity.Rare);
                fields.Field("kingdom").SetValue(Races.None);
                fields.Field("keywords").SetValue(Array.Empty<EffectKeywordType>());
                // A passive, inert native attack satisfies CanHoldItem and ItemManager.Apply.
                fields.Field("attack").SetValue(new Attack { combatEffects = new List<CombatEffect>() });
                fields.Field("cooldown").SetValue(9999999f);
                items.Add(card.Key, item);
                if (!ids.Contains(card.Key)) ids.Add(card.Key);
            }
        }

        internal static Sprite LoadSprite(string group,string name,float pixelsPerUnit=32f)
        {
            using (var input = typeof(Plugin).Assembly.GetManifestResourceStream(group+"."+name+".png"))
            using (var bytes = new MemoryStream())
            {
                input.CopyTo(bytes);
                var texture = new Texture2D(2,2) { filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Clamp };
                if (!texture.LoadImage(bytes.ToArray())) throw new InvalidDataException(name);
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(.5f, .5f),pixelsPerUnit,0,SpriteMeshType.FullRect);
                sprite.name = name;
                return sprite;
            }
        }

        [HarmonyPatch(typeof(Item), nameof(Item.GetTitle)), HarmonyPrefix]
        private static bool Title(Item __instance, ref string __result)
        {
            if (!Cards.TryGetValue(__instance.GetIdentifier() ?? "", out var card)) return true;
            __result = card.title;
            return false;
        }

        [HarmonyPatch(typeof(Item), nameof(Item.GetDescription)), HarmonyPrefix]
        private static bool Description(Item __instance, ref string __result)
        {
            if (!Cards.TryGetValue(__instance.GetIdentifier() ?? "", out var card)) return true;
            __result = $"Gift: {card.gift}\n<color=#991F29>Curse: {card.curse}</color>";
            return false;
        }
    }
}
