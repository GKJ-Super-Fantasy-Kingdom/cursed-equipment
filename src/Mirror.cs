using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SuperFantasyKingdom;

namespace CursedEquipment
{
    [HarmonyPatch(typeof(Health), nameof(Health.ReceiveDamage))]
    internal static class Mirror
    {
        // Monster.OnEnable builds a fresh Health on every pooled spawn. A unit keeps
        // its native identifier when moved, ranked up, or evolved.
        private static readonly ConditionalWeakTable<MonsterBaseHealth, HashSet<int>> History =
            new ConditionalWeakTable<MonsterBaseHealth, HashSet<int>>();

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source)
        {
            var code = source.ToList();
            var identifier = AccessTools.Method(typeof(Entity), nameof(Entity.GetIdentifier));
            int i = code.FindIndex(op => op.Calls(identifier)) - 1;
            // The native positive-health-damage branch starts here after fully shielded
            // and zero-damage returns. The same local feeds damage numbers and HP loss.
            if (i < 5 || code[i - 5].opcode != OpCodes.Ldloc_2 || code[i - 1].opcode != OpCodes.Ret
                || code[i - 3].opcode != OpCodes.Bgt_Un || code.Count(op => op.Calls(identifier)) != 1)
                throw new InvalidOperationException("Unexpected health damage branch.");
            code.InsertRange(i, new[] {
                new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(code[i]),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldloc_2),
                CodeInstruction.Call(typeof(Mirror), nameof(Scale)),
                new CodeInstruction(OpCodes.Stloc_2)
            });
            return code;
        }

        internal static float Scale(Health health, Damage dmg, float takeDamage)
        {
            if (!(health is MonsterBaseHealth monster) || !(dmg.origin is UnitBase wearer)
                || takeDamage <= 0f || monster.GetHitpoints() == 0f) return takeDamage;
            bool first = History.GetOrCreateValue(monster).Add(wearer.GetIdentifier());
            float scaled = Catalog.Wears(wearer, Catalog.Mirror) ? takeDamage * (first ? 2f : .75f) : takeDamage;
            return Arsenal.ScaleHit(monster, wearer, dmg.isStatus, scaled);
        }
    }
}
