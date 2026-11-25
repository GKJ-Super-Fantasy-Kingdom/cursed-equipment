using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Status;
using UnityEngine;

namespace CursedEquipment
{
    [HarmonyPatch]
    internal static class Arsenal
    {
        private sealed class Wearer
        {
            internal MonsterBaseHealth PreviousTarget;
            internal int Followups;
            internal int EmberHits;
            internal int HaloDay = -1;
            internal float HaloBlockUntil;
            internal float HaloPenaltyUntil;
        }

        private static ConditionalWeakTable<UnitBase, Wearer> wearers = new ConditionalWeakTable<UnitBase, Wearer>();
        private static ConditionalWeakTable<MonsterBaseHealth, Dictionary<int, float>> recentHits =
            new ConditionalWeakTable<MonsterBaseHealth, Dictionary<int, float>>();
        private static BurnStatusScriptableObject burn;
        internal static float Now { get; set; }

        internal static float ScaleHit(MonsterBaseHealth target, UnitBase source, bool status, float amount)
        {
            var state = wearers.GetOrCreateValue(source);
            if (Catalog.Wears(source, Catalog.Halo) && Now < state.HaloPenaltyUntil)
                amount *= .5f;
            if (status) return amount;

            var hits = recentHits.GetOrCreateValue(target);
            int id = source.GetIdentifier();
            if (Catalog.Wears(source, Catalog.Lantern))
                amount *= hits.Any(hit => hit.Key != id && Now - hit.Value < 2f) ? .85f : 1.4f;
            hits[id] = Now;

            float fang = 1f;
            if (ReferenceEquals(state.PreviousTarget, target))
            {
                state.Followups = Math.Min(5, state.Followups + 1);
                fang += state.Followups * .1f;
            }
            else
            {
                if (state.PreviousTarget != null) fang = .7f;
                state.PreviousTarget = target;
                state.Followups = 0;
            }
            if (Catalog.Wears(source, Catalog.Fang)) amount *= fang;
            if (Catalog.Wears(source, Catalog.Quarrel))
                amount *= target.GetHealthPercentageAbs() < .2f ? 1.6f : .85f;
            return amount;
        }

        // This base call receives the native wall-adjusted amount from UnitHealth.
        [HarmonyPrefix, HarmonyPatch(typeof(Health), "TakeHealthDamage")]
        private static bool ProtectWearer(Health __instance, ref float takeDamage, ref float __result)
        {
            if (!(__instance is UnitHealth unit) || !(unit.GetEntity() is UnitBase wearer)
                || unit.GetHitpoints() <= 0f || takeDamage <= 0f) return true;

            float hp = unit.GetHitpoints();
            takeDamage = ReduceHealthDamage(wearer, unit, hp, takeDamage);
            if (Catalog.Wears(wearer, Catalog.Halo))
            {
                int day = DaytimeManager.Instance == null ? -1 : DaytimeManager.Instance.CurrentDay();
                bool inCombat = CombatManager.Instance != null && CombatManager.Instance.IsCombat();
                takeDamage = ApplyHalo(wearer, hp, takeDamage, day, inCombat);
            }
            if (takeDamage > 0f) return true;
            __result = 0f;
            return false;
        }

        private static float ReduceHealthDamage(UnitBase wearer, UnitHealth health, float hp, float amount)
        {
            if (Catalog.Wears(wearer, Catalog.Plate)) amount *= .7f;
            if (Catalog.Wears(wearer, Catalog.Locket) && hp < health.GetMaxHealth() * .5f)
                amount *= .65f;
            return amount;
        }

        internal static float ApplyHalo(UnitBase wearer, float hp, float amount, int day, bool inCombat)
        {
            var state = wearers.GetOrCreateValue(wearer);
            if (Now < state.HaloBlockUntil) return 0f;
            if (day < 0 || !inCombat || day == state.HaloDay || amount < hp) return amount;
            state.HaloDay = day;
            state.HaloBlockUntil = Now + 2f;
            state.HaloPenaltyUntil = Now + 8f;
            return Math.Max(0f, hp - 1f);
        }

        // UnitHealth forwards ordinary and delayed Chalice delivery through this call.
        [HarmonyPrefix, HarmonyPatch(typeof(Health), nameof(Health.ReceiveHeal))]
        private static bool LimitHeal(Health __instance, ref Heal heal, ref float __result)
        {
            if (!(__instance is UnitHealth unit) || heal.healAmount <= 0f ||
                (heal.healType != HealType.Health && heal.healType != HealType.OverHeal)) return true;
            var recipient = unit.GetEntity();
            if (Catalog.Wears(recipient, Catalog.Plate)) heal.healAmount *= .5f;
            if (!Catalog.Wears(recipient, Catalog.Locket)) return true;
            heal.healAmount = CapHeal(unit.GetHitpoints(), unit.GetMaxHealth(), heal.healAmount);
            if (heal.healAmount > 0f) return true;
            __result = 0f;
            return false;
        }

        internal static float CapHeal(float current, float maximum, float requested)
            => maximum <= 0f ? requested : Math.Min(requested, Math.Max(0f, maximum * .75f - current));

        [HarmonyPostfix, HarmonyPatch(typeof(Health), "TakeHealthDamage")]
        private static void Emberwake(Health __instance, Damage dmg, float __result)
        {
            if (!(__instance is MonsterBaseHealth target) || __result <= 0f || !target.IsAlive() ||
                dmg.isStatus || !(dmg.origin is UnitBase wearer) || !Catalog.Wears(wearer, Catalog.Blade)) return;
            var health = wearer.GetHealth();
            if (health == null || !health.IsAlive() || health.GetHitpoints() <= 0f) return;
            var state = wearers.GetOrCreateValue(wearer);
            if (++state.EmberHits != 4) return;
            state.EmberHits = 0;

            float current = health.GetHitpoints();
            if (current > 1f)
            {
                health.SetHitPoints(Math.Max(1f, current - health.GetMaxHealth() * .05f));
                health.SetCurrentHealth();
                UnitManager.Instance?.UnitChange(wearer);
            }
            target.GetEntity().GetStatusManager().AddStatus(BurnAsset(), 1f, 1f, wearer.GetIdentifier(), wearer);
        }

        private static BurnStatusScriptableObject BurnAsset()
        {
            if (burn != null) return burn;
            burn = ScriptableObject.CreateInstance<BurnStatusScriptableObject>();
            Traverse.Create(burn).Field("amount").SetValue(1f);
            Traverse.Create(burn).Field("duration").SetValue(4f);
            return burn;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(GameDataManager), nameof(GameDataManager.Load))]
        private static void Clear()
        {
            wearers = new ConditionalWeakTable<UnitBase, Wearer>();
            recentHits = new ConditionalWeakTable<MonsterBaseHealth, Dictionary<int, float>>();
        }
    }
}
