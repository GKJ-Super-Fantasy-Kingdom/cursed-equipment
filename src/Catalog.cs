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

        private static readonly Dictionary<string, (string title, string text, string art)> Cards =
            new Dictionary<string, (string, string, string)>
            {
                [Glass] = ("Cracked Spellglass", "Gift: Damaging circles gain 40% radius.\nCurse: Each target takes 20% less damage.", "glass"),
                [Cup] = ("Bitter Chalice", "Gift: Receive 50% more healing.\nCurse: Heals arrive 3 seconds late.", "chalice"),
                [Mirror] = ("Cracked Duelist’s Mirror", "Gift: First hit to each monster’s health deals double.\nCurse: Later hits deal 25% less.", "mirror")
            };

        // All equipment tests read the native unit inventory at the point of use.
        internal static bool Wears(Entity entity, string id) =>
            entity is UnitBase && entity.GetItems()?.Contains(id) == true;

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
                fields.Field("icon").SetValue(LoadIcon(card.Value.art));
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

        private static Sprite LoadIcon(string name)
        {
            using (var input = typeof(Plugin).Assembly.GetManifestResourceStream("items." + name + ".png"))
            using (var bytes = new MemoryStream())
            {
                input.CopyTo(bytes);
                var texture = new Texture2D(2, 2) { filterMode = FilterMode.Point };
                if (!texture.LoadImage(bytes.ToArray())) throw new InvalidDataException(name);
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(.5f, .5f), 32);
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
            __result = card.text;
            return false;
        }
    }
}
