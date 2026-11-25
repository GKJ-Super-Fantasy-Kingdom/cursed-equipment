using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Clickable;
using SuperFantasyKingdom.Spawner;
using SuperFantasyKingdom.Status;
using SuperFantasyKingdom.Tavern;
using SuperFantasyKingdom.UI;
using UnityEngine;

public sealed partial class Probe
{
    private const int EquipmentFixtureOrigin = -68249912;
    private const string RedMercy = "gkj_CR_RedMercy";
    private Action observeEquipmentFrame;

    private void LateUpdate() => observeEquipmentFrame?.Invoke();

    private IEnumerator EquipmentNights()
    {
        Check(SettingsManager.Instance.GetUIScaling()==1f,"equipment night observation at 1x UI");
        SettingsManager.Instance.SetTavernSkip(3);
        Write("FIXTURE enabled the native automatic tavern transition in the private player");
        yield return ResumeClock();
        var firstGame=GameManager.Instance;
        int firstDay=DaytimeManager.Instance.CurrentDay();
        Check(firstDay==13,"copied kingdom starts on ordinary day 13");
        var damageGuard=new Harmony("gkj.local.cursedequipment.night-survival");
        damageGuard.Patch(AccessTools.Method(typeof(UnitHealth),nameof(UnitHealth.ReceiveDamage)),
            prefix:new HarmonyMethod(typeof(Probe),nameof(ScaleNativeDefenderDamage)));
        try
        {
            yield return ObserveEquipmentNight(firstDay,false);
            yield return NextEquipmentMorning(firstGame,firstDay+1);
            yield return ObserveEquipmentNight(firstDay+1,true);
        }
        finally { damageGuard.UnpatchSelf(); }
    }

    private static void ScaleNativeDefenderDamage(ref Damage dmg)
    {
        if (dmg.damageAmount>0f) dmg.damageAmount*=.05f;
    }

    private static bool InBossCutscene() => BossSpawner.Instance && BossSpawner.Instance.IsInAnimation();

    private static void HandleNightPrompts()
    {
        if (InBossCutscene()) return;
        if (DialogueManager.Instance.IsInDialogue()) DialogueManager.Instance.Skip();
        if (OverlayManager.Instance.IsOpen()) OverlayManager.Instance.CloseOverlay();
    }

    private IEnumerator NextEquipmentMorning(GameManager prior,int nextDay)
    {
        bool leftTavern=false;
        float deadline=Time.realtimeSinceStartup+90f;
        while (Time.realtimeSinceStartup<deadline && (!GameManager.Instance || GameManager.Instance==prior || !GameManager.Instance.IsGameLoaded()))
        {
            if (!leftTavern && TavernSaveManager.Instance && TavernSaveManager.Instance.GetGameData()!=null && !MainManager.Instance.StartedTransition())
            {
                TavernSaveManager.Instance.Save();
                MainManager.Instance.LoadNewScene("GameScene");
                leftTavern=true;
            }
            yield return null;
        }
        Check(GameManager.Instance && GameManager.Instance!=prior && GameManager.Instance.IsGameLoaded(),"native settlement loaded a new kingdom scene");
        deadline=Time.realtimeSinceStartup+30f;
        while (!GameManager.Instance.IsGameRunning() && Time.realtimeSinceStartup<deadline)
        {
            if (DialogueManager.Instance.IsInDialogue())
            {
                if (DialogueManager.Instance.Skip()) GameManager.Instance.TriggerAfterGameLoaded();
            }
            else if (GameCamera.Instance.Skip()) GameManager.Instance.TriggerAfterGameLoaded();
            yield return new WaitForSecondsRealtime(.15f);
        }
        Check(GameManager.Instance.IsGameRunning() && DaytimeManager.Instance.CurrentDay()==nextDay,"native Continue reached consecutive day "+nextDay);
    }

    private IEnumerator ObserveEquipmentNight(int day,bool bossNight)
    {
        var game=GameManager.Instance;
        var clock=DaytimeManager.Instance;
        var wall=WallManager.Instance;
        var hero=UnitManager.Instance.GetHero();
        var roster=UnitManager.Instance.GetAllUnits().Where(u=>u && u.GetHealth().IsAlive()).ToArray();
        Check(wall.HasWall() && roster.Any(u=>u!=hero),"copied wall and supporting unit available");
        foreach (var unit in roster)
        {
            unit.GetUnitStats().damage.AddModifier(new StatModifier(2f,StatModifierType.PercentMult,EquipmentFixtureOrigin));
            unit.GetHealth().SetHealthPercentage(1f);
            unit.GetHealth().SetCurrentHealth();
        }
        wall.AddToHitpointMultiplier(29f);
        if (!RelicManager.Instance.HasRelic(RedMercy)) RelicManager.Instance.AddRelic(RedMercy);
        Write($"FIXTURE day {day}: raised defender damage and wall capacity, granted Red Mercy, reduced incoming native attacks on defenders to 5%; native timers and AI");
        if (CityManager.Instance.GetCastle())
            GameCamera.Instance.SetPosition(CityManager.Instance.GetCastle().transform.position+Vector3.up);
        game.SetGameSpeed(1f,false);

        bool dusk=false, nightEnded=false, settled=false, measured=false, midnightHeal=false, entrancePause=false, deathPause=false;
        int ordinary=0, midnight=0, kills=0, bossSpawns=0, bossDeaths=0, controlledClears=0;
        float maxSpeed=0;
        observeEquipmentFrame=()=>
        {
            maxSpeed=Mathf.Max(maxSpeed,Time.timeScale);
            if (!InBossCutscene() || Time.timeScale!=0f) return;
            if (BossSpawner.Instance.IsBossDying()) deathPause=true;
            else entrancePause=true;
        };
        Action<int> onDusk=d=>{ if (d==day) dusk=true; };
        Action<int> end=d=>{ if (d==day) nightEnded=true; };
        Action<GameState> state=s=>{ if (s==GameState.Survived && nightEnded) settled=true; };
        Action<MonsterBase> spawn=monster=>
        {
            if (clock.IsEvening() && new StackTrace().GetFrames().Any(f=>f.GetMethod()?.DeclaringType?.Assembly.GetName().Name=="NightsOfTerror")) midnight++;
            else ordinary++;
        };
        Action<MonsterBase,Entity> slain=(monster,origin)=>kills++;
        Action<Boss> bossSpawn=b=>bossSpawns++;
        Action<Boss> bossDeath=b=>bossDeaths++;
        DaytimeManager.OnSunsetStart+=onDusk;
        DaytimeManager.OnNightEnd+=end;
        GameManager.AfterStateChange+=state;
        CombatManager.OnMonsterSpawn+=spawn;
        CombatManager.OnMonsterDeath+=slain;
        CombatManager.OnBossSpawn+=bossSpawn;
        CombatManager.OnBossDeath+=bossDeath;
        try
        {
            var opened=new HashSet<DropBossChest>();
            float deadline=Time.realtimeSinceStartup+320f;
            while (!settled && !game.IsGameOver() && Time.realtimeSinceStartup<deadline)
            {
                if (InBossCutscene())
                {
                    yield return null;
                    continue;
                }
                if (!measured && dusk && CombatManager.Instance.IsCombat())
                {
                    var sampleAlly=roster.FirstOrDefault(u=>u && u!=hero && u.GetHealth().IsAlive());
                    if (!sampleAlly)
                    {
                        sampleAlly=roster.First(u=>u && u!=hero);
                        sampleAlly.GetHealth().Revive(false);
                        Write("FIXTURE revived the copied allied wearer for the Halo and Lantern observations");
                    }
                    Write($"FIXTURE day {day}: sampling ten effects at native combat speed {Time.timeScale:0.0}");
                    CaptureScene("combat-"+day);
                    yield return MeasureEquipmentEffects(hero,sampleAlly,day);
                    measured=true;
                }
                if (!midnightHeal && midnight>0 && clock.IsEvening())
                {
                    CaptureScene("midnight-"+day);
                    yield return CheckNightChalice(hero,3.9f,$"day {day} during native midnight reinforcements");
                    midnightHeal=true;
                }
                if (!bossNight && midnightHeal && midnight==60 && controlledClears==0)
                {
                    var remaining=CombatManager.Instance.GetAllMonsters()
                        .Where(monster=>monster && !monster.IsBoss() && monster.GetHealth().IsAlive()).ToArray();
                    controlledClears=remaining.Length;
                    foreach (var monster in remaining) monster.GetHealth().Kill(false);
                    Write($"FIXTURE cleared {controlledClears} remaining nonboss enemies after the 60 native reinforcement arrivals and the heal sample");
                }
                if (clock.IsEvening() && WorldManager.Instance.HasChestToCollect() && CombatManager.Instance.CurrentMonstersAmount()==0 && !OverlayManager.Instance.IsOpen())
                {
                    var chest=FindObjectsOfType<DropBossChest>().FirstOrDefault(item=>!opened.Contains(item));
                    if (chest)
                    {
                        opened.Add(chest);
                        chest.Open();
                        yield return new WaitForSecondsRealtime(.95f);
                        var ordinaryCard=FindObjectsOfType<UICardRelic>().FirstOrDefault(card=>
                            card.GetCardButton().IsInteractable() && !(Traverse.Create(card).Field("m_RelicIdentifier").GetValue<string>()??"").StartsWith("gkj_CR_"));
                        if (ordinaryCard) ordinaryCard.GetCardButton().onClick.Invoke();
                        Write("FIXTURE opened native boss chest required for settlement");
                    }
                }
                HandleNightPrompts();
                yield return null;
            }
            Write($"NIGHT day={day} ordinary={ordinary} midnight={midnight} slain={kills} bossSpawns={bossSpawns} bossDeaths={bossDeaths} " +
                $"dusk={dusk} ended={nightEnded} settled={settled} gameOver={game.IsGameOver()} controlledClears={controlledClears} " +
                $"maxSpeed={maxSpeed:0.00} bossEntrancePaused={entrancePause} bossDeathPaused={deathPause}");
            Check(dusk && measured && nightEnded && settled && kills>0,"fully settled native night after ten item effects, day "+day);
            NearNight(maxSpeed,1f,"maximum observed game speed on day "+day);
            if (bossNight) Check(bossSpawns==1 && bossDeaths==1 && entrancePause && deathPause,"native boss arrival, defeat and both unforced cutscene pauses");
            else Check(ordinary>0 && midnight>0 && midnightHeal,"ordinary night included midnight reinforcements and measured equipment/relic healing overlap");
        }
        finally
        {
            observeEquipmentFrame=null;
            DaytimeManager.OnSunsetStart-=onDusk;
            DaytimeManager.OnNightEnd-=end;
            GameManager.AfterStateChange-=state;
            CombatManager.OnMonsterSpawn-=spawn;
            CombatManager.OnMonsterDeath-=slain;
            CombatManager.OnBossSpawn-=bossSpawn;
            CombatManager.OnBossDeath-=bossDeath;
        }
    }

    private IEnumerator MeasureEquipmentEffects(UnitBase hero,UnitBase ally,int day)
    {
        var heroItems=hero.GetItems().ToList();
        var allyItems=ally.GetItems().ToList();
        var type=AddressablesManager.Instance.GetAllMonster().Values.First(m=>m && m.GetWave()==1);
        var first=CombatManager.Instance.SpawnMonster(type,new Vector3(49,61,0));
        var second=CombatManager.Instance.SpawnMonster(type,new Vector3(52,61,0));
        first.Init(day,false);
        second.Init(day,false);
        var a=first.GetHealth();
        var b=second.GetHealth();
        a.SetMaxHealth(200,true); a.SetHitPoints(200);
        b.SetMaxHealth(200,true); b.SetHitPoints(200);
        try
        {
            hero.SetItems(new List<string>{"Bow",ids[0],ids[2]});
            CheckWaveTarget(hero,first,$"day {day} Spellglass / Mirror controlled target",false);
            // Start secondary samples on a fresh enemy life after the paired hit probe.
            hero.SetItems(new List<string>{expansionIds[2]});
            NearNight(Hit(hero,b),14,"Gravedigger's Lantern isolated damage");
            Hit(ally,a);
            NearNight(Hit(hero,a),8.5f,"Gravedigger's Lantern recent ally penalty");
            yield return WaitNightSeconds(2.2f);
            NearNight(Hit(hero,a),14,"Gravedigger's Lantern ally window cleared");

            hero.SetItems(new List<string>{expansionIds[0]});
            Hit(hero,b);
            NearNight(Hit(hero,b),11,"Duelist's Fang first repeat");
            for (int n=0;n<5;n++) Hit(hero,b);
            NearNight(Hit(hero,b),15,"Duelist's Fang repeat bonus caps at 50 percent");
            NearNight(Hit(hero,a),7,"Duelist's Fang target switch penalty");
            NearNight(Hit(hero,a),11,"Duelist's Fang starts the repeat count again");

            hero.SetItems(new List<string>{expansionIds[3]});
            a.SetHitPoints(100);
            NearNight(Hit(hero,a),8.5f,"Hangman's Quarrel healthy target penalty");
            a.SetHitPoints(38);
            NearNight(Hit(hero,a),16,"Hangman's Quarrel below-20-percent Gift");

            hero.SetItems(new List<string>{expansionIds[4]});
            var hp=(UnitHealth)hero.GetHealth();
            float heroBefore=hp.GetHitpoints();
            a.SetHitPoints(200);
            for (int n=0;n<3;n++) Hit(hero,a);
            Check(!first.GetStatusManager().GetAll().Any(status=>status.GetStatusType()==StatusType.Burn),"Emberwake Blade holds its first three charges");
            Hit(hero,a);
            Check(first.GetStatusManager().GetAll().Any(status=>status.GetStatusType()==StatusType.Burn),"Emberwake Blade applies Burn on hit four");
            NearNight(heroBefore-hp.GetHitpoints(),hp.GetMaxHealth()*.05f,"Emberwake Blade charges 5 percent maximum Health");
            first.GetStatusManager().RemoveAllStatusEffect();

            hero.SetItems(new List<string>{expansionIds[1]});
            hp.SetHitPoints(hp.GetMaxHealth()*.5f);
            NearNight(TakeHealth(hp,10),7,"Martyr's Plate reduces incoming Health damage");
            NearNight(hp.ReceiveHeal(new Heal{healType=HealType.Health,healAmount=10}),6.5f,"Martyr's Plate halves Red Mercy modified healing");

            hero.SetItems(new List<string>{expansionIds[5]});
            hp.SetHitPoints(hp.GetMaxHealth()*.35f);
            NearNight(TakeHealth(hp,10),6.5f,"Leadheart Locket reduces Health damage below half");
            hp.SetHitPoints(hp.GetMaxHealth()*.6f);
            NearNight(TakeHealth(hp,3),3,"Leadheart Locket Health damage above half is unchanged");
            hp.ReceiveHeal(new Heal{healType=HealType.OverHeal,healAmount=hp.GetMaxHealth()});
            NearNight(hp.GetHitpoints(),hp.GetMaxHealth()*.75f,"Leadheart Locket caps amplified overheal");

            var allyHealth=(UnitHealth)ally.GetHealth();
            ally.SetItems(new List<string>{expansionIds[6]});
            allyHealth.SetHitPoints(10);
            TakeHealth(allyHealth,20);
            NearNight(allyHealth.GetHitpoints(),1,"Borrowed Halo first lethal hit leaves one HP on day "+day);
            NearNight(TakeHealth(allyHealth,.5f),0,"Borrowed Halo blocks further Health damage within two game seconds");
            NearNight(Hit(ally,a),5,"Borrowed Halo halves damage during the curse");
            yield return WaitNightSeconds(2.2f);
            NearNight(TakeHealth(allyHealth,.25f),.25f,"Borrowed Halo damage block expired");
            yield return WaitNightSeconds(6.1f);
            NearNight(Hit(ally,a),10,"Borrowed Halo eight-second damage curse expired");
            allyHealth.SetHitPoints(10);
            TakeHealth(allyHealth,20);
            Check(allyHealth.IsDead(),"Borrowed Halo stays spent for the rest of day "+day);
            allyHealth.Revive(false);
            allyHealth.SetHitPoints(allyHealth.GetMaxHealth());
            yield return CheckNightChalice(hero,3.9f,$"day {day} during dusk combat");
        }
        finally
        {
            hero.SetItems(heroItems);
            ally.SetItems(allyItems);
            hero.GetHealth().SetHitPoints(hero.GetHealth().GetMaxHealth());
            CombatManager.Instance.UnregisterMonster(first);
            CombatManager.Instance.UnregisterMonster(second);
            Destroy(first.gameObject);
            Destroy(second.gameObject);
        }
    }

    private IEnumerator CheckNightChalice(UnitBase unit,float expected,string label)
    {
        var original=unit.GetItems().ToList();
        var hp=(UnitHealth)unit.GetHealth();
        var deliveries=new List<(float amount,float time)>();
        float scheduled=Time.time;
        Action<Entity,float,Heal> healed=(entity,amount,heal)=>
        {
            if (entity==unit && heal.originIdentifier==EquipmentFixtureOrigin) deliveries.Add((amount,Time.time));
        };
        UnitHealth.HealReceived+=healed;
        try
        {
            unit.SetItems(new List<string>{ids[1]});
            hp.SetHitPoints(hp.GetMaxHealth()*.6f);
            float before=hp.GetHitpoints();
            NearNight(hp.ReceiveHeal(new Heal{healType=HealType.Health,healAmount=2,originIdentifier=EquipmentFixtureOrigin}),0,"Bitter Chalice queues rather than healing "+label);
            NearNight(hp.GetHitpoints(),before,"Bitter Chalice still waiting "+label);
            yield return WaitNightSeconds(3.15f);
            Write($"FIXTURE Chalice observation {label}: tagged heals={deliveries.Count}, alive={hp.IsAlive()}, HP={hp.GetHitpoints():0.00}, equipped={unit.GetItems().Contains(ids[1])}, phase={DaytimeManager.Instance.GetDaytimePhase()}");
            Check(deliveries.Count==1 && deliveries[0].time>=scheduled+2.99f,"exactly one tagged Chalice heal arrived after three game seconds "+label);
            NearNight(deliveries[0].amount,expected,"Bitter Chalice and Red Mercy single delivery "+label);
        }
        finally
        {
            UnitHealth.HealReceived-=healed;
            unit.SetItems(original);
            hp.SetHitPoints(hp.GetMaxHealth());
        }
    }

    private IEnumerator WaitNightSeconds(float seconds)
    {
        float gameTime=Time.time+seconds;
        float deadline=Time.realtimeSinceStartup+40f;
        while (Time.time<gameTime && Time.realtimeSinceStartup<deadline)
        {
            HandleNightPrompts();
            yield return null;
        }
        Check(Time.time>=gameTime,"native clock advanced "+seconds+" game seconds");
    }

    private static float Hit(UnitBase owner,IHealth health)
    {
        float before=health.GetHitpoints();
        health.ReceiveDamage(new Damage{damageAmount=10,origin=owner,originIdentifier=owner.GetIdentifier(),damageElement=DamageElement.None},true,true);
        return before-health.GetHitpoints();
    }

    private static float TakeHealth(UnitHealth unit,float amount)
    {
        float before=unit.GetHitpoints();
        var method=AccessTools.Method(typeof(Health),"TakeHealthDamage");
        // A standing native wall absorbs one half before Health receives the sample.
        float incoming=amount*2f;
        method.Invoke(unit,new object[]{new Damage{damageAmount=incoming,originIdentifier=-1,damageElement=DamageElement.None},incoming});
        return before-unit.GetHitpoints();
    }

    private void NearNight(float actual,float expected,string label) => Check(Mathf.Abs(actual-expected)<.04f,$"{label} = {actual:0.###}, expected {expected:0.###}");
}
