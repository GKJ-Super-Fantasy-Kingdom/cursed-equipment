using System.Collections.Generic;
using HarmonyLib;
using SuperFantasyKingdom;
using UnityEngine;

namespace CursedEquipment
{
    [HarmonyPatch]
    internal static class Chalice
    {
        private struct Pending
        {
            internal UnitHealth Health;
            internal Heal Heal;
            internal float Due;
        }

        private static readonly List<Pending> Queue = new List<Pending>();
        private static UnitHealth delivering;

        [HarmonyPrefix, HarmonyPatch(typeof(UnitHealth), nameof(UnitHealth.ReceiveHeal))]
        private static bool Receive(UnitHealth __instance, Heal heal, ref float __result) =>
            ReceiveAt(__instance, heal, Time.time, ref __result);

        internal static bool ReceiveAt(UnitHealth health, Heal heal, float now, ref float result)
        {
            if (ReferenceEquals(delivering, health))
            {
                // Consume before native HealReceived subscribers can request another heal.
                delivering = null;
                return true;
            }
            if (!CanDelay(health, heal)) return true;
            Enqueue(health, heal, now);
            result = 0f;
            return false;
        }

        internal static bool CanDelay(UnitHealth health, Heal heal) =>
            heal.healAmount > 0f && (heal.healType == HealType.Health || heal.healType == HealType.OverHeal)
            && health.IsAlive() && health.GetHitpoints() > 0f && Catalog.Wears(health.GetEntity(), Catalog.Cup);

        internal static void Enqueue(UnitHealth health, Heal heal, float now)
        {
            heal.healAmount *= 1.5f;
            Queue.Add(new Pending { Health = health, Heal = heal, Due = now + 3f });
        }

        internal static void Tick(float now)
        {
            // Remove before delivery. Callbacks may cancel siblings, or queue a later heal.
            while (Queue.Count > 0 && Queue[0].Due <= now)
            {
                var pending = Queue[0];
                Queue.RemoveAt(0);
                var health = pending.Health;
                var owner = health.GetEntity();
                if (owner == null || !ReferenceEquals(owner.GetHealth(), health)
                    || !CanDelay(health, pending.Heal)) continue;
                delivering = health;
                try { health.ReceiveHeal(pending.Heal); }
                finally { delivering = null; }
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(UnitHealth), "Death")]
        private static void Death(UnitHealth __instance) => Cancel(__instance);

        [HarmonyPrefix, HarmonyPatch(typeof(UnitBase), nameof(UnitBase.Unregister))]
        private static void Unregister(UnitBase __instance) => Cancel(__instance.GetHealth());

        [HarmonyPrefix, HarmonyPatch(typeof(Entity), nameof(Entity.RemoveItem))]
        private static void Remove(Entity __instance, int index)
        {
            var items = __instance.GetItems();
            if (items[index] == Catalog.Cup && items.IndexOf(Catalog.Cup) == items.LastIndexOf(Catalog.Cup))
                Cancel(__instance.GetHealth());
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Entity), nameof(Entity.SetItems))]
        private static void Replace(Entity __instance, List<string> items)
        {
            if (Queue.Count > 0 && items?.Contains(Catalog.Cup) != true)
                Cancel(__instance.GetHealth());
        }

        private static void Cancel(IHealth health)
        {
            if (Queue.Count != 0) Queue.RemoveAll(p => ReferenceEquals(p.Health, health));
        }

        [HarmonyPrefix, HarmonyPatch(typeof(GameDataManager), nameof(GameDataManager.Load))]
        internal static void Clear() => Queue.Clear();
    }
}
