using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BepInEx.Bootstrap;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Clickable;
using SuperFantasyKingdom.Projectile;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public sealed partial class Probe
{
    private static readonly string[] combinedPackages =
        { "gkj.holdtheline", "gkj.nightsofterror", "gkj.cursedrelics", "gkj.sfk.cursedequipment" };

    private void CheckCombinedPackages()
    {
        Check(Application.version == "0.8.068", "installed game 0.8.068");
        using (var sha = SHA256.Create())
        {
            foreach (var plugin in Chainloader.PluginInfos.Values.OrderBy(p => p.Metadata.GUID))
            {
                using (var stream = File.OpenRead(plugin.Location))
                    Write($"LOADED {plugin.Metadata.GUID} {plugin.Metadata.Version} {BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant()} assembly={plugin.Instance?.GetType().Assembly.GetName().Version}");
            }
        }
        Check(combinedPackages.All(id => Chainloader.PluginInfos.TryGetValue(id, out var p) && p.Instance)
            && Chainloader.PluginInfos[combinedPackages[0]].Metadata.Version.ToString() == "0.2.3"
            && Chainloader.PluginInfos[combinedPackages[1]].Metadata.Version.ToString() == "0.1.3",
            "all four gameplay mods instantiated with Hold the Line 0.2.3 and Nights of Terror 0.1.3");
    }

    private IEnumerator Combined(AddressablesManager catalog, UnitBase hero, float scale)
    {
        var overlay = OverlayManager.Instance;
        for (int step=0; step<15 && (DialogueManager.Instance.IsInDialogue() || overlay.IsOpen()); step++)
        {
            if (DialogueManager.Instance.IsInDialogue()) DialogueManager.Instance.Skip();
            else overlay.CloseOverlay();
            yield return new WaitForSecondsRealtime(.15f);
        }
        var recipient = UnitManager.Instance.GetAllUnits().First(u => u && u != hero && !u.IsDead()
            && u.GetHealth().GetMaxHealth()>0 && u.GetItems().Count<3);
        if (phase == "combined-reload")
        {
            Check(recipient.GetItems().Count(id=>id==ids[1])==1 && !hero.GetItems().Contains(ids[1]),
                "Continue restored one purchased Chalice on the recipient, former wearer has none");
            yield return ResumeClock();
            yield return CheckChalice(recipient, "Continued wearer");
            overlay.OpenUnitDetails(recipient,true);
            yield return new WaitForSecondsRealtime(.5f);
            CaptureScene("continued-loadout");
            yield return new WaitForSecondsRealtime(2.5f);
            yield break;
        }
        bool padMode = phase.Contains("-pad");
        bool demo = phase == "combined-pad-large" && Environment.GetEnvironmentVariable("SFK_EQUIP_VISIBLE")=="1";
        var input = GamepadManager.Instance.GetPlayerInput();
        input.neverAutoSwitchControlSchemes = true;
        var pad = padMode ? InputSystem.AddDevice<Gamepad>() : null;
        var mouse = padMode ? null : InputSystem.AddDevice<Mouse>();
        var keyboard = padMode ? null : InputSystem.AddDevice<Keyboard>();
        if (padMode) input.SwitchCurrentControlScheme("Gamepad", pad);
        else input.SwitchCurrentControlScheme("KeyboardMouse", keyboard, mouse);
        yield return new WaitForSecondsRealtime(.4f);
        Check(SettingsManager.Instance.GetUIScaling()==scale && GamepadManager.Instance.IsGamepadInUse()==padMode,
            "active input scheme "+input.currentControlScheme+" and UI scale "+scale);

        var forge = FindObjectOfType<BuildingForge>();
        Check(forge, "copied kingdom has live Blacksmith");
        var fields = Traverse.Create(forge);
        var pool = fields.Field("m_Options").GetValue<List<string>>();
        Check(ids.All(pool.Contains), "Blacksmith stock pool includes all cursed equipment");
        var open = AccessTools.Method(typeof(BuildingObjectShop),"AfterDialogue");
        open.Invoke(forge,new object[]{false});
        yield return new WaitForSecondsRealtime(1.5f);
        var shop = overlay.GetMerchant();
        Write("UNALTERED FORGE OFFER "+string.Join(", ", Cards(shop).Select(CardId)));
        yield return Back(shop,pad,mouse,keyboard);
        Check(!shop.gameObject.activeInHierarchy, "Back closed the unaltered Blacksmith");
        if (overlay.IsOpen()) overlay.CloseOverlay();

        // Keep normal shop generation, purchase prices and callbacks. Pin the three
        // item IDs for equivalent layout and selection checks across all four runs.
        fields.Field("m_Options").SetValue(new List<string>(ids));
        foreach (var key in new[]{"m_SoldOne","m_SoldTwo","m_SoldThree"}) fields.Field(key).SetValue(false);
        var price = ((ResourceAmount[])AccessTools.Method(typeof(BuildingForge),"GetPrice")
            .Invoke(forge,new object[]{catalog.GetItem(ids[1])}))[0];
        var wallet = ResourceManager.Instance;
        if (!wallet.CanBeBuilt(price)) wallet.AddResource(price);
        int stock = wallet.GetResourceAvailableAmount(price.type);
        open.Invoke(forge,new object[]{false});
        yield return new WaitForSecondsRealtime(1.6f);
        Check(Cards(shop).Length==3 && ids.All(id=>Cards(shop).Any(c=>CardId(c)==id)), "controlled Blacksmith issued the three native offers");
        foreach (var card in Cards(shop))
        {
            var desc = Traverse.Create(card).Field("description").GetValue<TMP_Text>();
            var title = Traverse.Create(card).Field("title").GetValue<TMP_Text>();
            desc.ForceMeshUpdate(); title.ForceMeshUpdate();
            Check(!desc.isTextOverflowing && !title.isTextOverflowing && desc.text.Contains("Gift:") && desc.text.Contains("Curse:"),
                title.text+" and both terms fit");
        }
        yield return Back(shop,pad,mouse,keyboard);
        Check(!shop.gameObject.activeInHierarchy && wallet.GetResourceAvailableAmount(price.type)==stock,
            "Back canceled the cursed offer without charging");
        if (overlay.IsOpen()) overlay.CloseOverlay();
        yield return new WaitForSecondsRealtime(.5f);
        open.Invoke(forge,new object[]{false});
        yield return new WaitForSecondsRealtime(1.55f);

        var purchases = shop.transform.Find("Panel/BuyButtons");
        var buys = purchases.GetComponentsInChildren<Button>();
        var target = Array.FindIndex(Cards(shop),c=>CardId(c)==ids[1]);
        Check(target>=0 && buys.Length==3 && buys[target].IsInteractable(),"native Chalice Buy control available after reopening");
        if (padMode)
        {
            for (int n=0;n<8 && EventSystem.current.currentSelectedGameObject!=buys[target].gameObject;n++)
            {
                int current=Array.FindIndex(buys,b=>b.gameObject==EventSystem.current.currentSelectedGameObject);
                yield return Press(pad,current<0 ? DirectionTo(buys[target], GamepadButton.DpadUp)
                    : current<target ? GamepadButton.DpadRight : GamepadButton.DpadLeft);
            }
            Check(EventSystem.current.currentSelectedGameObject==buys[target].gameObject,"D-pad focused paid Chalice");
            CaptureScene("forge");
            if (demo) yield return new WaitForSecondsRealtime(7f);
            yield return Press(pad,GamepadButton.South);
        }
        else
        {
            CaptureScene("forge");
            yield return Click(mouse,buys[target]);
        }
        yield return new WaitForSecondsRealtime(1.6f);
        var items = ItemManager.Instance;
        Check(items.GetDragItems().Count(d=>d && d.GetItemIdentifier()==ids[1])==1
            && wallet.GetResourceAvailableAmount(price.type)==stock-price.amount,
            "paid native Blacksmith produced one Chalice and charged exactly "+price.amount+" "+price.type);
        Equip(hero,ids[1]);
        yield return ResumeClock();
        yield return CheckChalice(hero,"First wearer");
        var old = hero.GetHealth();
        float hp = old.GetHitpoints();
        old.ReceiveHeal(new Heal{healType=HealType.Health,healAmount=2});
        items.SpawnDragItem("Magnet",hero.transform.position,true);
        Equip(hero,"Magnet");
        if (demo) yield return new WaitForSecondsRealtime(1.1f);
        Equip(recipient,ids[1]);
        yield return GameSeconds(3.2f);
        Check(Mathf.Abs(old.GetHitpoints()-hp)<.05f && recipient.GetItems().Count(id=>id==ids[1])==1,
            "Magnet transfer canceled the old queue and equipped one Chalice on the recipient");
        Check(Mathf.Abs(old.ReceiveHeal(new Heal{healType=HealType.Health,healAmount=2})-2)<.05f,
            "former wearer regained immediate base healing");
        yield return CheckChalice(recipient,"New wearer");
        GameDataManager.Instance.Save();
        Check(File.ReadAllText(Path.Combine(Application.persistentDataPath,"1_human_game.data")).Contains(ids[1]),"native save wrote the purchased Chalice");
        if (demo)
        {
            overlay.OpenUnitDetails(recipient,true);
            yield return new WaitForSecondsRealtime(6f);
            CaptureScene("transferred");
            yield return new WaitForSecondsRealtime(.4f);
            overlay.CloseOverlay();
            yield return ResumeClock();
        }
        var health=recipient.GetHealth();
        health.ReceiveHeal(new Heal{healType=HealType.Health,healAmount=2});
        health.Kill(false);
        yield return GameSeconds(3.2f);
        Check(health.IsDead() && health.GetHitpoints()==0,"recipient death canceled the queue without revival");
        if (phase == "combined-pad-large")
        {
            health.Revive(false);
            health.SetHitPoints(health.GetMaxHealth());
            old.SetHitPoints(old.GetMaxHealth());
            yield return NativeWaves(hero);
        }
    }

    private IEnumerator Back(UIOverlayMerchant shop, Gamepad pad, Mouse mouse, Keyboard keyboard)
    {
        if (pad != null)
        {
            yield return Press(pad,GamepadButton.East);
            if (!shop.gameObject.activeInHierarchy) { Write("INPUT East closed the merchant"); yield break; }
            var leave = shop.transform.Find("Panel/Buttons/Cancel/Button").gameObject;
            for (int n=0;n<6 && EventSystem.current.currentSelectedGameObject!=leave;n++)
                yield return Press(pad,DirectionTo(leave.GetComponent<Button>(),GamepadButton.DpadDown));
            Check(EventSystem.current.currentSelectedGameObject==leave,"D-pad focused native Blacksmith Back control");
            yield return Press(pad,GamepadButton.South);
            Write("INPUT South on native Back, merchant active="+shop.gameObject.activeInHierarchy+
                " overlay open="+OverlayManager.Instance.IsOpen());
        }
        else
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));
            yield return new WaitForSecondsRealtime(.2f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            if (shop.gameObject.activeInHierarchy)
            {
                if (PauseMenuManger.Instance.IsOpen())
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));
                    yield return new WaitForSecondsRealtime(.15f);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                }
                yield return Click(mouse,shop.transform.Find("Panel/Buttons/Cancel/Button").GetComponent<Button>());
            }
            else Write("INPUT Escape closed overlay");
        }
        yield return new WaitForSecondsRealtime(.35f);
    }

    private static UICardItem[] Cards(UIOverlayMerchant shop) => shop.GetComponentsInChildren<UICardItem>();
    private static string CardId(UICardItem card) => Traverse.Create(card).Field("m_ItemIdentifier").GetValue<string>();

    private static GamepadButton DirectionTo(Selectable target,GamepadButton fallback)
    {
        var current=EventSystem.current.currentSelectedGameObject?.GetComponent<Selectable>();
        if (!current) return fallback;
        var candidates=new[]{
            (GamepadButton.DpadUp,current.FindSelectableOnUp()),
            (GamepadButton.DpadDown,current.FindSelectableOnDown()),
            (GamepadButton.DpadLeft,current.FindSelectableOnLeft()),
            (GamepadButton.DpadRight,current.FindSelectableOnRight())
        };
        var best=candidates.Where(c=>c.Item2)
            .OrderBy(c=>Vector3.SqrMagnitude(c.Item2.transform.position-target.transform.position)).FirstOrDefault();
        return best.Item2 ? best.Item1 : fallback;
    }
    private static IEnumerator Press(Gamepad pad, GamepadButton button)
    {
        InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(button));
        yield return new WaitForSecondsRealtime(.16f);
        InputSystem.QueueStateEvent(pad,new GamepadState());
        yield return new WaitForSecondsRealtime(.2f);
    }

    private static IEnumerator Click(Mouse mouse, Button button)
    {
        var rect=(RectTransform)button.transform;
        var canvas=button.GetComponentInParent<Canvas>().rootCanvas;
        var point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point}); yield return new WaitForSecondsRealtime(.2f);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1}); yield return new WaitForSecondsRealtime(.15f);
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
    }

    private void Equip(UnitBase unit,string id)
    {
        var items=ItemManager.Instance;
        var drop=items.GetDragItems().Last(i=>i && i.GetItemIdentifier()==id);
        Check(items.CanHoldItem(unit,AddressablesManager.Instance.GetItem(id)),"native gear restrictions allowed "+id);
        items.SetDragItem(drop); items.GiveDragItem(unit);
    }

    private IEnumerator CheckChalice(UnitBase unit,string label)
    {
        var hp=unit.GetHealth();
        hp.SetHitPoints(hp.GetMaxHealth()*.35f);
        float before=hp.GetHitpoints(), at=Time.time;
        float amount=Mathf.Min(2,hp.GetMaxHealth()*.1f);
        var heal=new Heal{healType=HealType.Health,healAmount=amount};
        Check(hp.ReceiveHeal(heal)==0 && hp.GetHitpoints()==before,label+" delayed healing");
        yield return GameSeconds(2.7f);
        Check(Time.time>=at+3 || hp.GetHitpoints()==before,label+" held healing before its deadline");
        yield return GameSeconds(.6f);
        Check(Mathf.Abs(hp.GetHitpoints()-before-amount*1.5f)<.05f,label+" got exactly one +50% heal");
    }

    private IEnumerator ResumeClock()
    {
        var game=GameManager.Instance;
        var overlay=OverlayManager.Instance;
        for (int i=0;i<10 && (game.IsForcedPause() || overlay.IsOpen() || DialogueManager.Instance.IsInDialogue());i++)
        {
            if (DialogueManager.Instance.IsInDialogue()) DialogueManager.Instance.Skip();
            else if (overlay.IsOpen()) overlay.CloseOverlay();
            else GameCamera.Instance.Skip();
            yield return new WaitForSecondsRealtime(.25f);
        }
        if (game.IsForcedPause()) game.EndForcedPause();
        game.ResumeGame(true);
        game.SetGameSpeed(1,false);
        Check(Mathf.Approximately(Time.timeScale,1),"copied kingdom running at normal speed");
    }

    private IEnumerator GameSeconds(float duration)
    {
        float stop=Time.time+duration;
        float deadline=Time.realtimeSinceStartup+18;
        while (Time.time<stop && Time.realtimeSinceStartup<deadline)
        {
            var overlay=OverlayManager.Instance;
            if (Time.timeScale==0 && overlay.IsOpen())
            {
                var popup=Traverse.Create(overlay).Field("m_CurrentlyOpen").GetValue<UnityEngine.Object>();
                Write("Fixture resumed after a native popup paused the clock: "+(popup ? popup.name : "unknown"));
                yield return ResumeClock();
            }
            else yield return null;
        }
        if (Time.time<stop)
            throw new InvalidOperationException($"Game clock stalled during {duration}s check. Scale={Time.timeScale}, overlay={OverlayManager.Instance.IsOpen()}, forced pause={GameManager.Instance.IsForcedPause()}.");
    }

    private IEnumerator NativeWaves(UnitBase hero)
    {
        var ordinary=new List<MonsterBase>();
        var midnight=new List<MonsterBase>();
        void Observe(MonsterBase monster)
        {
            bool reinforcement = DaytimeManager.Instance.IsEvening() && new System.Diagnostics.StackTrace().GetFrames()
                .Any(frame=>frame.GetMethod()?.DeclaringType?.Assembly.GetName().Name=="NightsOfTerror");
            (reinforcement ? midnight : ordinary).Add(monster);
        }
        CombatManager.OnMonsterSpawn += Observe;
        try
        {
            hero.SetItems(new List<string>{"Bow",ids[0],ids[2]});
            GridManager.Instance.GridChanged(hero.GetGridPosition());
            var game=GameManager.Instance;
            var clock=DaytimeManager.Instance;
            var nights=Chainloader.PluginInfos[combinedPackages[1]].Instance;
            if (CityManager.Instance.GetCastle()) GameCamera.Instance.SetPosition(CityManager.Instance.GetCastle().transform.position+Vector3.up);
            game.ResumeGame(true); game.SetGameSpeed(1,false);
            float limit=Time.realtimeSinceStartup+145;
            bool testedOrdinary=false, testedMidnight=false;
            int plannedCount=0;
            float nextLog=0;
            while (Time.realtimeSinceStartup<limit && !game.IsGameOver() && (!testedMidnight || midnight.Count<plannedCount))
            {
                if (Time.realtimeSinceStartup>=nextLog)
                {
                    Write($"WAVE clock={clock.CurrentDayTime()} phase={clock.GetDaytimePhase()} ordinary={ordinary.Count} midnight={midnight.Count} speed={Time.timeScale}");
                    nextLog=Time.realtimeSinceStartup+10;
                }
                if (DialogueManager.Instance.IsInDialogue()) DialogueManager.Instance.Skip();
                else if (OverlayManager.Instance.IsOpen())
                {
                    var card=FindObjectsOfType<UICardBoss>().FirstOrDefault(c=>c.GetCardButton().IsInteractable());
                    if(card) card.GetCardButton().onClick.Invoke();
                    else OverlayManager.Instance.Cancel(false);
                }
                if (!game.IsForcedPause() && !OverlayManager.Instance.IsOpen() && Time.timeScale!=1)
                    game.SetGameSpeed(1,false);
                var normal=ordinary.FirstOrDefault(m=>m && m.GetHealth().IsAlive());
                if (!testedOrdinary && normal)
                {
                    Write("NATIVE ordinary spawn "+normal.GetEntityIdentifier()+" clock="+clock.CurrentDayTime());
                    CheckWaveTarget(hero,normal,"ordinary");
                    testedOrdinary=true;
                }
                var extra=midnight.FirstOrDefault(m=>m && m.GetHealth().IsAlive());
                if (!testedMidnight && extra)
                {
                    int ordinaryAliveAtMidnight=ordinary.Count(m=>m && m.GetHealth().IsAlive());
                    var group=Traverse.Create(nights).Field("group").GetValue();
                    plannedCount=Traverse.Create(group).Property("Count").GetValue<int>();
                    Write("NATIVE midnight spawn "+extra.GetEntityIdentifier()+" clock="+clock.CurrentDayTime()+" ordinary still alive="+ordinaryAliveAtMidnight);
                    CheckWaveTarget(hero,extra,"midnight reinforcement");
                    CaptureScene("midnight");
                    testedMidnight=true;
                }
                yield return null;
            }
            Check(testedOrdinary && testedMidnight && plannedCount>0 && midnight.Count==plannedCount,
                $"natural wave and scheduled midnight group advanced after controlled hit probes, {midnight.Count}/{plannedCount} reinforcements");
            yield return new WaitForSecondsRealtime(10f);
        }
        finally { CombatManager.OnMonsterSpawn -= Observe; }
    }

    private void CheckWaveTarget(UnitBase wearer,MonsterBase target,string wave)
    {
        var hp=target.GetHealth();
        var oldPosition=target.transform.position;
        float oldHp=hp.GetHitpoints(), oldMax=hp.GetMaxHealth();
        var projectile = new GameObject("Combined Spellglass fixture").AddComponent<ProjectileS>();
        try
        {
            projectile.enabled=false;
            var isolated=new Vector3(62,60);
            target.transform.position=isolated;
            projectile.transform.position=isolated-Vector3.right*12;
            hp.SetMaxHealth(200,true); hp.SetHitPoints(200);
            var stats=new DamageLibrary(wearer.GetStats(),wearer);
            stats.critChance.SetBaseValue(0);
            Traverse.Create(projectile).Field("OriginEntity").SetValue(wearer);
            Traverse.Create(projectile).Field("Damage").SetValue(stats);
            projectile.combatEffects=new List<CombatEffect>{new CombatEffectDamage {value=-10, directDamage=true,trueDamage=true,
                calculations=Array.Empty<Calculation>(),targetRequirements=Array.Empty<SuperFantasyKingdom.Requirements.EntityRequirement>()}};
            var circle=new ProjectileSActionFindTargets();
            Traverse.Create(circle).Field("radius").SetValue(10f);
            Traverse.Create(circle).Field("combatEffects").SetValue(new List<CombatEffect>());
            circle.Init(projectile); Physics2D.SyncTransforms();
            var trigger=AccessTools.Method(circle.GetType(),"Trigger");
            wearer.RemoveItem(wearer.GetItems().IndexOf(ids[0]));
            trigger.Invoke(circle,new object[]{projectile});
            Check(hp.GetHitpoints()==200,wave+" outside base radius");
            wearer.GiveItem(ids[0]);
            trigger.Invoke(circle,new object[]{projectile});
            Check(Mathf.Abs(hp.GetHitpoints()-184)<.02f,wave+" took 16 opener HP, observed "+hp.GetHitpoints());
            wearer.RemoveItem(wearer.GetItems().IndexOf(ids[2])); wearer.GiveItem(ids[2]);
            trigger.Invoke(circle,new object[]{projectile});
            Check(Mathf.Abs(hp.GetHitpoints()-178)<.02f,wave+" took 6 follow-up HP after re-equipping Mirror");
        }
        finally
        {
            hp.SetMaxHealth(oldMax,true); hp.SetHitPoints(oldHp);
            target.transform.position=oldPosition;
            Physics2D.SyncTransforms();
            Destroy(projectile.gameObject);
        }
    }
}
