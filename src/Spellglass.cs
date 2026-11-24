using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Projectile;
using UnityEngine;

namespace CursedEquipment
{
    // Keep the native circle query, hit requirements, ordering, crits and callbacks.
    // Only the radius operand and the damage effects sent by this query change.
    [HarmonyPatch]
    internal static class Spellglass
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ProjectileActionFindTargets), "Trigger");
            yield return AccessTools.Method(typeof(ProjectileSActionFindTargets), "Trigger");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator, MethodBase original)
        {
            var effects = AccessTools.Field(original.DeclaringType, "combatEffects");
            var radius = AccessTools.Field(original.DeclaringType, "radius");
            var hit = AccessTools.Method(typeof(ProjectileBase), nameof(ProjectileBase.Hit));
            var scoped = generator.DeclareLocal(typeof(List<CombatEffect>));
            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, effects);
            yield return CodeInstruction.Call(typeof(Spellglass), nameof(ForCircle));
            yield return new CodeInstruction(OpCodes.Stloc, scoped);

            int radii = 0, hits = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(hit))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc, scoped).MoveLabelsFrom(instruction);
                    yield return CodeInstruction.Call(typeof(Spellglass), nameof(Hit));
                    hits++;
                }
                else
                {
                    yield return instruction;
                    if (!instruction.LoadsField(radius)) continue;
                    yield return new CodeInstruction(OpCodes.Ldloc, scoped);
                    yield return CodeInstruction.Call(typeof(Spellglass), nameof(Radius));
                    radii++;
                }
            }
            if (radii == 0 || hits != 1)
                throw new InvalidOperationException("Unexpected circle query in " + original);
        }

        internal static List<CombatEffect> ForCircle(ProjectileBase projectile, List<CombatEffect> overrides)
        {
            if (!Catalog.Wears(projectile.GetOriginEntity(), Catalog.Glass)) return null;
            return WrapDamage(overrides != null && overrides.Count > 0 ? overrides : projectile.combatEffects);
        }

        internal static List<CombatEffect> WrapDamage(List<CombatEffect> effects)
        {
            List<CombatEffect> scoped = null;
            if (effects == null) return null;
            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (!(effect is CombatEffectDamage || effect is CombatEffectRealDamage)) continue;
                if (scoped == null) scoped = new List<CombatEffect>(effects);
                scoped[i] = new CircleDamage(effect);
            }
            return scoped;
        }

        internal static float Radius(float radius, List<CombatEffect> scoped) =>
            scoped == null ? radius : radius * 1.4f;

        private static void Hit(ProjectileBase projectile, Entity target, List<CombatEffect> effects,
            bool setTarget, List<CombatEffect> scoped) => projectile.Hit(target, scoped ?? effects, setTarget);

        // Wrap the individual effect, so an OnHit proc or another projectile never
        // inherits a damage penalty merely because it ran inside the wider search.
        private sealed class CircleDamage : CombatEffect
        {
            private readonly CombatEffect source;
            internal CircleDamage(CombatEffect source)
            {
                this.source = source;
                originRequirements = source.originRequirements;
                targetRequirements = source.targetRequirements;
            }

            public override void Apply(List<Entity> targets, Entity target, AbilityBase origin, int originIdentifier,
                Entity self, StatContainer stats, float amount, Transform position = null)
            {
                var previous = CircleDelivery.Current;
                CircleDelivery.Current = source;
                try { source.Apply(targets, target, origin, originIdentifier, self, stats, amount, position); }
                finally { CircleDelivery.Current = previous; }
            }
        }
    }

    [HarmonyPatch]
    internal static class CircleDelivery
    {
        [ThreadStatic] internal static CombatEffect Current;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CombatEffectDamage), "Trigger");
            yield return AccessTools.Method(typeof(CombatEffectRealDamage), "Trigger");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var receive = AccessTools.Method(typeof(IHealth), nameof(IHealth.ReceiveDamage));
            int calls = 0;
            foreach (var instruction in instructions)
            {
                if (!instruction.Calls(receive)) { yield return instruction; continue; }
                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                yield return CodeInstruction.Call(typeof(CircleDelivery), nameof(Receive));
                calls++;
            }
            if (calls != 1) throw new InvalidOperationException("Unexpected damage delivery signature.");
        }

        internal static float Receive(IHealth health, Damage damage, bool direct, bool trueDamage, CombatEffect source)
        {
            var previous = Current;
            if (ReferenceEquals(previous, source)) damage.damageAmount *= .8f;
            Current = null;
            try { return health.ReceiveDamage(damage, direct, trueDamage); }
            finally { Current = previous; }
        }
    }
}
