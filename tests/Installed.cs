using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Projectile;
using UnityEngine;

// Managed fixtures use the installed types and packed entry points. Only Unity's
// native scene, rendering, and the final health/UI notification are substituted.
public static class Installed
{
    private static Assembly plugin;
    private static int checks;
    private const string Glass = "GKJ_CE_CrackedSpellglass";
    private const string Cup = "GKJ_CE_BitterChalice";
    private const string Mirror = "GKJ_CE_CrackedDuelistsMirror";
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    private static Type Mod(string name) { return plugin.GetType("CursedEquipment." + name, true); }
    private static object Call(string type, string name, params object[] args)
    {
        return Mod(type).GetMethod(name, Static).Invoke(null, args);
    }
    private static void Check(bool passed, string label)
    {
        if (!passed) throw new Exception("FAIL " + label);
        Console.WriteLine("PASS " + label);
        checks++;
    }
    private static void Set(object obj, string field, object value)
    {
        AccessTools.Field(obj.GetType(), field).SetValue(obj, value);
    }
    private static T Fixture<T>() where T : class
    {
        return (T)FormatterServices.GetUninitializedObject(typeof(T));
    }
    public sealed class PackUnit : Unit {}
    private static Unit UnitWith(int id, params string[] items)
    {
        var unit = Fixture<PackUnit>();
        // Unity's managed equality recognizes the fixture as a live native object.
        Set(unit, "m_CachedPtr", new IntPtr(id + 100));
        unit.SetIdentifier(id);
        unit.SetItems(new List<string>(items));
        return unit;
    }
    private static void Remove(Unit unit, string item)
    {
        int index = unit.GetItems().IndexOf(item);
        Call("Chalice", "Remove", unit, index);
        unit.RemoveItem(index);
    }
    private static float Strike(MonsterBaseHealth monster, Unit unit, float amount)
    {
        var args = new object[] { monster, new Damage { origin = unit }, amount };
        return (float)Call("Mirror", "Scale", args);
    }

    private static void Mirrors()
    {
        var a = UnitWith(4, Mirror, Mirror);
        var b = UnitWith(7, "OtherModGear");
        var monster = Fixture<MonsterHealth>();
        Set(monster, "Hitpoints", 100f);
        Check(Strike(monster, a, 0) == 0 && Strike(monster, a, 10) == 20, "zero damage preserves opener and duplicate mirrors double only once");
        Check(Strike(monster, a, 10) == 7.5f, "second hit is discounted");
        Remove(a, Mirror); Remove(a, Mirror); b.GiveItem(Mirror);
        Check(Strike(monster, b, 10) == 20 && Strike(monster, a, 10) == 10, "moving Mirror separates wearer histories");
        Remove(b, Mirror); a.GiveItem(Mirror);
        Check(Strike(monster, a, 10) == 7.5f, "returning the Mirror cannot reset first hit");
        var second = Fixture<MonsterHealth>(); Set(second, "Hitpoints", 100f);
        Check(Strike(second, a, 10) == 20, "fresh monster life gets a new opener");
        Check(Strike(second, b, 10) == 10, "ordinary source damage");
        b.GiveItem(Mirror);
        Check(Strike(second, b, 10) == 7.5f, "pre-equipment hit is remembered");
        var replacement = UnitWith(a.GetIdentifier(), Mirror);
        Check(Strike(second, replacement, 10) == 7.5f, "promotion retains native wearer identity");
    }

    public sealed class RecordingHealth : UnitHealth
    {
        public RecordingHealth(UnitBase unit) : base(unit) {}
        public List<Heal> Received;
        public override float ReceiveHeal(Heal heal)
        {
            var args = new object[] { this, heal, 0f, 0f };
            if (!(bool)Call("Chalice", "ReceiveAt", args)) throw new Exception("recursive requeue");
            if (Mod("Chalice").GetField("delivering", Static).GetValue(null) != null)
                throw new Exception("nested callback can bypass Chalice");
            Received.Add(heal);
            return heal.healAmount;
        }
    }
    private static RecordingHealth HealthFor(Unit unit)
    {
        var health = Fixture<RecordingHealth>();
        health.Received = new List<Heal>();
        Set(health, "m_Unit", unit); Set(health, "Hitpoints", 50f);
        Set(unit, "m_Health", health);
        return health;
    }
    private static void Queue(RecordingHealth health, Heal heal, float now)
    {
        var args = new object[] { health, heal, now, -1f };
        if ((bool)Call("Chalice", "ReceiveAt", args) || (float)args[3] != 0f)
            throw new Exception("receive eligibility");
    }
    private static void Tick(float time) { Call("Chalice", "Tick", time); }
    private static void Chalices()
    {
        var a = UnitWith(3, Cup, Cup); var b = UnitWith(4);
        var ah = HealthFor(a); var bh = HealthFor(b);
        Heal heal = new Heal { healAmount = 10, healType = HealType.Health, originIdentifier = 9 };
        Queue(ah, heal, 0); Remove(a, Cup); Tick(2.99f);
        Check(ah.Received.Count == 0, "heal waits three scaled seconds");
        Tick(3); Tick(30);
        Check(ah.Received.Count == 1 && ah.Received[0].healAmount == 15 && ah.Received[0].originIdentifier == 9,
            "one amplified delivery, attribution retained, duplicate cup did not stack");
        Queue(ah, heal, 31); Remove(a, Cup); b.GiveItem(Cup); a.GiveItem(Cup); Tick(34);
        Check(ah.Received.Count == 1 && bh.Received.Count == 0, "transfer and same-frame re-equip cancel the pending heal");
        heal.healType = HealType.OverHeal;
        Queue(bh, heal, 35); Call("Chalice", "Death", bh); Tick(40);
        Check(bh.Received.Count == 0, "death invalidates pending heal even if revived before delivery");
        Queue(bh, heal, 40); Set(bh, "m_IsDead", true); Tick(44);
        Check(bh.Received.Count == 0 && !(bool)Call("Chalice","CanDelay",bh,heal), "dead recipient cannot receive or queue delayed overheal");
        Set(bh, "m_IsDead", false);
        Queue(bh, heal, 45); Queue(bh, heal, 46); Tick(48);
        Check(bh.Received.Count == 1 && bh.Received[0].healType == HealType.OverHeal, "each heal keeps its own deadline and type");
        Call("Chalice", "Clear"); Tick(99);
        Check(bh.Received.Count == 1, "Continue clears pending combat heals");
        foreach (var type in new[] { HealType.Shield, HealType.OverShield, HealType.Rage })
        {
            heal.healType = type;
            Check(!(bool)Call("Chalice", "CanDelay", bh, heal), type + " passes through");
        }
    }

    public sealed class RecordingMonster : MonsterHealth
    {
        public RecordingMonster(Monster monster) : base(monster) {}
        public float Amount;
        public override float ReceiveDamage(Damage dmg, bool direct, bool trueDamage)
        {
            if (Mod("CircleDelivery").GetField("Current", Static).GetValue(null) != null)
                throw new Exception("area penalty leaked into damage callbacks");
            Amount = dmg.damageAmount;
            return Amount;
        }
    }
    public sealed class PackDamage : CombatEffectDamage
    {
        public RecordingMonster Receiver;
        public override void Apply(List<Entity> targets, Entity target, AbilityBase origin, int id, Entity self,
            StatContainer stats, float amount, Transform position)
        {
            Call("CircleDelivery","Receive",Receiver,new Damage { origin = self, damageAmount = amount },false,false,this);
        }
    }
    private static void Circles()
    {
        var unit = UnitWith(22, Glass, Glass, Mirror);
        var p = Fixture<ProjectileBase>(); Set(p,"OriginEntity",unit);
        var monster = Fixture<RecordingMonster>(); Set(monster,"Hitpoints",100f);
        var damage = new PackDamage { Receiver = monster };
        var secondary = new CombatEffectHealth();
        p.combatEffects = new List<CombatEffect> { damage, secondary };
        var scoped = (List<CombatEffect>)Call("Spellglass","ForCircle",p,null);
        Check(scoped.Count == 2 && scoped[1] == secondary && p.combatEffects[0] == damage, "circle wraps only damage and preserves shared effect definitions");
        Check((float)Call("Spellglass","Radius",10f,scoped) == 14f, "radius increases once with duplicate Spellglass");
        scoped[0].Apply(null,null,null,22,unit,null,100);
        Check(monster.Amount == 80 && Strike(monster,unit,monster.Amount) == 160 &&
              Strike(monster,unit,monster.Amount) == 60, "damage is reduced once per target and composes with both Mirror multipliers");
        damage.Apply(null,null,null,22,unit,null,100,null);
        Check(monster.Amount == 100, "direct use of the same effect retains full damage");
        Check(Call("Spellglass","ForCircle",p,new List<CombatEffect> { secondary }) == null, "non-damaging override prevents radius boost");
        Check(Call("Spellglass","WrapDamage",new List<CombatEffect> { new CombatEffectRealDamage() }) != null, "real-damage circle qualifies");
        Remove(unit, Glass); Remove(unit, Glass);
        Check(Call("Spellglass","ForCircle",p,null) == null && (float)Call("Spellglass","Radius",10f,null) == 10, "unequipped wearer has base radius");
        var next = UnitWith(23,Glass); Set(p,"OriginEntity",next);
        Check(Call("Spellglass","ForCircle",p,null) != null, "new mod-added wearer qualifies");
    }

    private static void NativeHooks()
    {
        var healthDamage = typeof(Health).GetMethod("ReceiveDamage");
        var modified = (IEnumerable<CodeInstruction>)Call("Mirror", "Transpiler", PatchProcessor.GetOriginalInstructions(healthDamage));
        Check(modified.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).Name == "Scale"),
            "Mirror transforms installed positive HP damage branch");
        foreach (string name in new[] { "Spellglass", "CircleDelivery" })
        {
            foreach (var method in (IEnumerable<MethodBase>)Call(name,"TargetMethods"))
            {
                var generator = new DynamicMethod("probe", typeof(void), Type.EmptyTypes).GetILGenerator();
                var il = PatchProcessor.GetOriginalInstructions(method, generator);
                var result = name == "Spellglass" ? Call(name,"Transpiler",il,generator,method) : Call(name,"Transpiler",il);
                var transformed = ((IEnumerable<CodeInstruction>)result).ToList();
                Check(transformed.Any(i => i.operand is MethodInfo && ((MethodInfo)i.operand).DeclaringType == Mod(name)),
                    method.DeclaringType.Name + "." + method.Name + " transforms installed IL");
            }
        }
        var ids = new[] { Glass, Cup, Mirror };
        var foreign = Fixture<Item>(); Set(foreign, "identifier", "ForeignGear");
        Check((bool)Call("Catalog","Title",foreign,null), "other packs retain native labels");
        foreach (var id in ids)
        {
            var item = Fixture<Item>(); Set(item,"identifier",id);
            var args = new object[] {item, null};
            Check(!(bool)Call("Catalog","Description",args) && ((string)args[1]).Contains("Gift:") &&
                  ((string)args[1]).Contains("\nCurse:"), id + " puts Gift and Curse in the same native description");
        }
        foreach (var art in new[] {"glass","chalice","mirror"})
            using (var s = plugin.GetManifestResourceStream("items." + art + ".png"))
                Check(s != null && s.Length > 100, art + " sprite embedded");
    }

    private static void CopiedSave(string path)
    {
        var json = JObject.Parse(File.ReadAllText(path));
        var units = (JArray)json["units"];
        var saved = (JObject)units.First(u => u["items"] is JArray && u["items"].Count() < 3);
        var items = (JArray)saved["items"];
        var original = items.Values<string>().ToList();
        while (items.Count < 3) items.Add(new[] {Mirror, Cup, Glass}[items.Count]);
        // Round-trip the native item-list representation in memory; keep the file intact.
        var loaded = JObject.Parse(json.ToString());
        var row = loaded["units"].Single(u => (int)u["identifier"] == (int)saved["identifier"]);
        var unit = UnitWith((int)row["identifier"]); unit.SetItems(row["items"].Values<string>().ToList());
        Check(unit.GetItems().SequenceEqual(items.Values<string>()) && unit.GetItems().Take(original.Count).SequenceEqual(original),
            "copied checkpoint inventory round-trips ordinary and cursed IDs through Entity.SetItems");
    }
    public static void Run(string dll, string copiedSave)
    {
        plugin = Assembly.LoadFrom(dll);
        NativeHooks(); Circles(); Mirrors(); Chalices(); CopiedSave(copiedSave);
        Console.WriteLine(checks + " checks passed.");
    }
}
