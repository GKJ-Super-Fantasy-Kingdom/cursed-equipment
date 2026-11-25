using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.TitleScreen;
using SuperFantasyKingdom.Tavern;
using SuperFantasyKingdom.UI;
using SuperFantasyKingdom.Projectile;
using SuperFantasyKingdom.Clickable;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[BepInPlugin("gkj.local.cursedequipment.qa", "Cursed Equipment private playtest", "0.1.0")]
[BepInDependency("gkj.sfk.cursedequipment")]
public sealed partial class Probe : BaseUnityPlugin
{
    private string evidence, phase;
    private readonly string[] ids = { "GKJ_CE_CrackedSpellglass", "GKJ_CE_BitterChalice", "GKJ_CE_CrackedDuelistsMirror" };
    private static bool Skip() => false;
    private void Awake()
    {
        evidence = Environment.GetEnvironmentVariable("SFK_EQUIP_EVIDENCE");
        if (string.IsNullOrEmpty(evidence) || (Application.productName != "Cursed Equip QA 00001"
            && Application.productName != "Cursed Combo QA 00001")
            || Application.companyName != "GKJ QA Isolated Lab") return;
        phase = Environment.GetEnvironmentVariable("SFK_EQUIP_PHASE");
        var h = new Harmony("gkj.local.cursedequipment.qa");
        foreach (var method in new[] { AccessTools.Method(typeof(SteamManager),"Awake"), AccessTools.Method(typeof(AchievementManager),"CheckAchievements") })
            h.Patch(method, prefix:new HarmonyMethod(typeof(Probe),nameof(Skip)));
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        StartCoroutine(Run());
    }
    private void Write(string line) => File.AppendAllText(Path.Combine(evidence,phase+".txt"),line+"\n");
    private void Check(bool ok, string name)
    {
        Write((ok ? "PASS " : "FAIL ") + name);
        if (!ok) throw new InvalidOperationException(name);
    }
    private IEnumerator Run()
    {
        var stack = new System.Collections.Generic.Stack<IEnumerator>();
        stack.Push(Scenario());
        while (stack.Count > 0)
        {
            object current;
            try
            {
                var step = stack.Peek();
                if (!step.MoveNext()) { stack.Pop(); continue; }
                current = step.Current;
            }
            catch (Exception e) { Write(e.ToString()); Application.Quit(1); yield break; }
            if (current is IEnumerator nested) stack.Push(nested);
            else yield return current;
        }
        Write("FINISHED"); Application.Quit();
    }

    private IEnumerator Scenario()
    {
        Write(Application.version + " " + Application.persistentDataPath);
        while (!FindObjectOfType<TitleScreenManager>() || !SettingsManager.Instance || !SaveManager.Instance) yield return null;
        yield return new WaitForSecondsRealtime(1);
        bool mouseMode = phase.StartsWith("mouse");
        float scale = phase == "mouse" || phase.EndsWith("-large") ? 1.5f : 1f;
        if (phase.StartsWith("combined-"))
        {
            if (Environment.GetEnvironmentVariable("SFK_EQUIP_VISIBLE")=="1")
                while (!File.Exists(Path.Combine(evidence,"foreground-ready"))) yield return null;
            CheckCombinedPackages();
            if (phase != "combined-reload" && PlayerPrefs.HasKey("PreventCheating3Human0"))
            {
                PlayerPrefs.DeleteKey("PreventCheating3Human0");
                Write("Reset the retry marker for the newly copied private day-13 fixture");
            }
        }
        if (Environment.GetEnvironmentVariable("SFK_EQUIP_VISIBLE")=="1") SettingsManager.Instance.SetFullscreen(2);
        SettingsManager.Instance.SetUIScaling(scale);
        SettingsManager.Instance.SetPause(0);
        MainManager.Instance.ContinueGame();
        bool tavern = false;
        while (!GameManager.Instance || !GameManager.Instance.IsGameLoaded() || GamepadManager.Instance?.GetPlayerInput() == null)
        {
            if (!tavern && TavernSaveManager.Instance && TavernSaveManager.Instance.GetGameData()!=null)
            {
                var saved = TavernSaveManager.Instance.GetGameData();
                if (phase=="reload") Check(ids.All(id=>saved.units.Any(u=>u.items?.Contains(id)==true)), "Continue tavern restored equipped IDs");
                tavern = true;
                TavernSaveManager.Instance.Save();
                MainManager.Instance.LoadNewScene("GameScene");
            }
            yield return null;
        }
        for (int i = 0; i < 100 && !GameManager.Instance.IsGameRunning(); i++)
        {
            if (DialogueManager.Instance.IsInDialogue()) { if (DialogueManager.Instance.Skip()) GameManager.Instance.TriggerAfterGameLoaded(); }
            else if (GameCamera.Instance.Skip()) GameManager.Instance.TriggerAfterGameLoaded();
            yield return new WaitForSecondsRealtime(.1f);
        }
        var catalog = AddressablesManager.Instance;
        Check(ids.All(id => catalog.GetItem(id) && catalog.GetItem(id).GetIcon() && catalog.GetItem(id).SoldAtBlacksmith()
            && catalog.GetAllItemIdentifiers().Count(x => x == id) == 1), "registered each item and Forge flag once");
        var unit = UnitManager.Instance.GetHero();
        if (phase.StartsWith("combined-"))
        {
            yield return Combined(catalog, unit, scale);
            yield break;
        }
        if (phase == "content")
        {
            CompatibleContent(catalog);
            yield break;
        }
        if (phase == "reload")
        {
            Check(unit.GetItems().Count==3 && ids.All(id => unit.GetItems().Contains(id)), "fresh Continue restored each equipped ID exactly once");
            if (OverlayManager.Instance.IsOpen()) OverlayManager.Instance.CloseOverlay();
            yield return new WaitForSecondsRealtime(.5f);
            CaptureScene("continued-kingdom");
            var details = OverlayManager.Instance.OpenUnitDetails(unit,true);
            yield return new WaitForSecondsRealtime(.6f);
            details.GetComponentInParent<UIUnitDetails>().OnMouseEnterItem(unit.GetItems().IndexOf(ids[1]));
            yield return new WaitForSecondsRealtime(.3f);
            CaptureScene("continued-loadout");
            yield break;
        }
        yield return new WaitForSecondsRealtime(.5f);
        if (OverlayManager.Instance.IsOpen()) OverlayManager.Instance.CloseOverlay();
        var input = GamepadManager.Instance.GetPlayerInput();
        input.neverAutoSwitchControlSchemes = true;
        Gamepad pad = null; Mouse mouse = null;
        if (mouseMode)
        {
            mouse = InputSystem.AddDevice<Mouse>();
            input.SwitchCurrentControlScheme("KeyboardMouse",InputSystem.AddDevice<Keyboard>(),mouse);
        }
        else
        {
            pad = InputSystem.AddDevice<Gamepad>();
            input.SwitchCurrentControlScheme("Gamepad",pad);
        }
        yield return new WaitForSecondsRealtime(.5f);
        int next = 0;
        var shop = OverlayManager.Instance.GetMerchantScreen();
        shop.Init("Blacksmith");
        shop.SetActor(DialogueManager.Instance.GetActor(Actors.Blacksmith));
        shop.SetText("Choose a piece of equipment.");
        shop.SetUseCardSelectCallback(true);
        var forge = Resources.FindObjectsOfTypeAll<BuildingForge>().FirstOrDefault();
        if (forge)
        {
            var eligible = AccessTools.Method(typeof(BuildingForge),"CanAddItem");
            Check(ids.All(id=>(bool)eligible.Invoke(forge,new object[]{catalog.GetItem(id)}))
                && catalog.GetAllItems().Any(i=>!ids.Contains(i.Key) && (bool)eligible.Invoke(forge,new object[]{i.Value})),
                "native Forge admits the trio alongside ordinary gear");
        }
        ResourceAmount[] costs = forge ? (ResourceAmount[])AccessTools.Method(typeof(BuildingForge),"GetPrice")
            .Invoke(forge,new object[]{catalog.GetItem(ids[0])}) : new[]{new ResourceAmount(ResourceType.Coins,1)};
        Write((forge ? "Native Forge price " : "Fixture merchant price ")+costs[0].amount+" "+costs[0].type);
        if (!ResourceManager.Instance.CanBeBuilt(costs)) ResourceManager.Instance.AddResources(costs);
        shop.Items(() => catalog.GetItem(ids[next++ % 3]), item => costs);
        shop.SetCancel("Leave");
        shop.Display();
        yield return new WaitForSecondsRealtime(1.2f);
        var cards = shop.GetComponentsInChildren<UICardItem>();
        var buttons = shop.transform.Find("Panel/BuyButtons").GetComponentsInChildren<UnityEngine.UI.Button>();
        Check(buttons.Length==3 && buttons.All(b => b.isActiveAndEnabled && b.interactable), "three native purchase buttons available");
        Check(cards.Length == 3 && SettingsManager.Instance.GetUIScaling()==scale, "native merchant rendered three cards at "+scale);
        foreach (var card in cards)
        {
            var desc = Traverse.Create(card).Field("description").GetValue<TMP_Text>();
            var title = Traverse.Create(card).Field("title").GetValue<TMP_Text>();
            desc.ForceMeshUpdate(); title.ForceMeshUpdate();
            Check(desc.text.Contains("Gift:") && desc.text.Contains("Curse:") && !desc.isTextOverflowing && !title.isTextOverflowing,
                title.text+" title and both terms fit at "+scale);
        }
        UICardItem chosen;
        if (mouseMode)
        {
            CaptureScene(phase);
            chosen = cards[0];
            var t = (RectTransform)buttons[0].transform;
            var canvas = chosen.GetComponentInParent<Canvas>().rootCanvas;
            var pos = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,t.TransformPoint(t.rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState {position = pos}); yield return new WaitForSecondsRealtime(.3f);
            InputSystem.QueueStateEvent(mouse,new MouseState {position = pos,buttons = 1}); yield return new WaitForSecondsRealtime(.15f);
            InputSystem.QueueStateEvent(mouse,new MouseState {position = pos});
        }
        else
        {
            var old = EventSystem.current.currentSelectedGameObject;
            if (!buttons.Any(b=>b.gameObject==old))
            {
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadUp));
                yield return new WaitForSecondsRealtime(.18f);
                InputSystem.QueueStateEvent(pad,new GamepadState()); yield return new WaitForSecondsRealtime(.25f);
                old = EventSystem.current.currentSelectedGameObject;
            }
            var preferred = GamepadManager.Instance.GetGameObjectToSelect();
            Write("Pad focus before horizontal move: "+(old ? old.name : "none")+
                ", preferred="+(preferred ? preferred.name : "none")+", active="+GamepadManager.Instance.IsGamepadInUse());
            var direction = old == buttons[0].gameObject ? GamepadButton.DpadRight : GamepadButton.DpadLeft;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(direction)); yield return new WaitForSecondsRealtime(.18f);
            InputSystem.QueueStateEvent(pad,new GamepadState()); yield return new WaitForSecondsRealtime(.2f);
            int index = Array.FindIndex(buttons,b => b.gameObject == EventSystem.current.currentSelectedGameObject);
            Check(index>=0 && old != buttons[index].gameObject, "D-pad changed focused native purchase button");
            chosen = cards[index];
            CaptureScene(phase);
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South)); yield return new WaitForSecondsRealtime(.18f);
            InputSystem.QueueStateEvent(pad,new GamepadState());
        }
        string chosenId = Traverse.Create(chosen).Field("m_ItemIdentifier").GetValue<string>();
        yield return new WaitForSecondsRealtime(1.6f);
        var drop = ItemManager.Instance.GetDragItems().LastOrDefault(d => d && d.GetItemIdentifier() == chosenId);
        Check(drop, phase+" selected exact card and spawned native equipment");
        // Give the clicked offer to the hero through the same native endpoint as a grid drop.
        unit.SetItems(new System.Collections.Generic.List<string>());
        Check(ItemManager.Instance.CanHoldItem(unit, catalog.GetItem(chosenId)), "ordinary equipment rules admit offer");
        unit.SetPinned(true);
        try { Check(!ItemManager.Instance.CanHoldItem(unit,catalog.GetItem(chosenId)), "pinned wearer retains native restriction"); }
        finally { unit.SetPinned(false); }
        ItemManager.Instance.SetDragItem(drop); ItemManager.Instance.GiveDragItem(unit);
        Check(unit.GetItems().Contains(chosenId), "selected native drop equipped");
        foreach (var id in ids.Where(id => id != chosenId)) unit.GiveItem(id);
        GridManager.Instance.GridChanged(unit.GetGridPosition());
        Check(!ItemManager.Instance.CanHoldItem(unit, catalog.GetItem(ids[0])), "native three-item limit enforced");
        if (phase == "mouse")
        {
            var health = unit.GetHealth();
            float max = health.GetMaxHealth(); health.SetHitPoints(max*.4f);
            float start = health.GetHitpoints();
            float heal = Mathf.Min(4,max*.1f);
            Check(health.ReceiveHeal(new Heal {healAmount=heal, healType=HealType.Health}) == 0 && health.GetHitpoints()==start,"Chalice suppresses immediate native heal");
            GameManager.Instance.ResumeGame(true);
            GameManager.Instance.SetGameSpeed(1,false);
            yield return new WaitForSeconds(3.2f);
            Check(Mathf.Abs(health.GetHitpoints()-start-heal*1.5f)<.05f,"native health receives one amplified heal");
            GameDataManager.Instance.Save();
            Check(File.ReadAllText(Path.Combine(Application.persistentDataPath,"1_human_game.data")).Contains(ids[2]),"native checkpoint saved equipped gear");
            CombatChecks(unit);
            var second = UnitManager.Instance.GetAllUnits().FirstOrDefault(u => u && u != unit && !u.IsDead()
                && u.GetHealth().GetMaxHealth()>0 && ItemManager.Instance.CanHoldItem(u,catalog.GetItem(ids[1])));
            Check(second, "second living wearer on copied save");
            unit.GetHealth().SetHitPoints(start);
            health.ReceiveHeal(new Heal {healAmount=heal,healType=HealType.Health});
            var inventory = ItemManager.Instance;
            inventory.SpawnDragItem("Magnet",unit.transform.position,true);
            inventory.SetDragItem(inventory.GetDragItems().Last()); inventory.GiveDragItem(unit);
            var cup = inventory.GetDragItems().Last(d => d && d.GetItemIdentifier()==ids[1]);
            inventory.SetDragItem(cup); inventory.GiveDragItem(second);
            Check(!unit.GetItems().Contains(ids[1]) && second.GetItems().Contains(ids[1]),"native Magnet moved Chalice between real units");
            yield return new WaitForSeconds(3.2f);
            Check(Mathf.Abs(health.GetHitpoints()-start)<.05f,"former wearer loses pending heal on transfer");
            var receiver = second.GetHealth();
            receiver.SetHitPoints(receiver.GetMaxHealth()*.4f);
            receiver.ReceiveHeal(new Heal { healAmount = 5, healType = HealType.Health });
            receiver.Kill(false);
            yield return new WaitForSeconds(3.2f);
            Check(receiver.IsDead() && receiver.GetHitpoints()==0,"pending heal on second wearer cannot revive");
        }
    }

    private void CaptureScene(string name)
    {
        if (Environment.GetEnvironmentVariable("SFK_EQUIP_VISIBLE")=="1")
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(evidence,name+".png"));
            return;
        }
        // A batch player has no swapchain. Render the actual scene cameras and
        // existing UI canvases into one texture without creating replacement UI.
        var cameras = Camera.allCameras.OrderBy(c => c.depth).ToArray();
        var overlays = FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var texture = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
        var previous = RenderTexture.active;
        var ui = new GameObject("Private capture camera").AddComponent<Camera>();
        ui.enabled = false;
        ui.transform.position = new Vector3(0,0,-5000);
        ui.clearFlags = CameraClearFlags.Depth;
        ui.orthographic = true; ui.nearClipPlane = .1f; ui.farClipPlane = 10f;
        ui.targetTexture = texture;
        var planes = overlays.Select(c => c.planeDistance).ToArray();
        var oldCameras = overlays.Select(c => c.worldCamera).ToArray();
        try
        {
            foreach (var c in cameras.OrderBy(c=>c==GameCamera.Instance.GetCamera() ? 1 : 0))
            {
                var old = c.targetTexture;
                try { if (!old) c.targetTexture = texture; c.Render(); }
                finally { c.targetTexture = old; }
            }
            if (name!="continued-kingdom")
            {
                foreach (var canvas in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = ui; canvas.planeDistance = 1;
                }
                Canvas.ForceUpdateCanvases(); ui.Render();
            }
            RenderTexture.active = texture;
            var pixels = new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(evidence,name+".png"),pixels.EncodeToPNG()); Destroy(pixels);
        }
        finally
        {
            for (int i=0;i<overlays.Length;i++)
            {
                overlays[i].worldCamera = oldCameras[i]; overlays[i].planeDistance = planes[i];
                overlays[i].renderMode = RenderMode.ScreenSpaceOverlay;
            }
            Destroy(ui.gameObject); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(texture);
        }
    }

    private void CombatChecks(UnitBase wearer)
    {
        var monster = CombatManager.Instance.SpawnMonster(AddressablesManager.Instance.GetAllMonster().Values.First(m => m && m.GetWave()==1),
            new Vector3(62,60,0));
        var target = monster.GetHealth();
        target.SetMaxHealth(200,true); target.SetHitPoints(200); target.SetShieldPoints(0);
        var projectile = new GameObject("Circular attack fixture").AddComponent<ProjectileS>();
        projectile.enabled = false;
        projectile.transform.position = new Vector3(50,60,0);
        var fields = Traverse.Create(projectile);
        fields.Field("OriginEntity").SetValue(wearer);
        var stats = new DamageLibrary(wearer.GetStats(), wearer);
        stats.critChance.SetBaseValue(0);
        fields.Field("Damage").SetValue(stats);
        var effect = new CombatEffectDamage { value = -10f, directDamage = true, trueDamage = true,
            calculations = Array.Empty<Calculation>(), targetRequirements = Array.Empty<SuperFantasyKingdom.Requirements.EntityRequirement>() };
        projectile.combatEffects = new System.Collections.Generic.List<CombatEffect> {effect};
        var circle = new ProjectileSActionFindTargets();
        Traverse.Create(circle).Field("radius").SetValue(10f);
        Traverse.Create(circle).Field("combatEffects").SetValue(new System.Collections.Generic.List<CombatEffect>());
        circle.Init(projectile); Physics2D.SyncTransforms();
        var trigger = AccessTools.Method(circle.GetType(),"Trigger");
        wearer.RemoveItem(wearer.GetItems().IndexOf(ids[0]));
        trigger.Invoke(circle,new object[]{projectile});
        Check(target.GetHitpoints()==200,"monster outside unmodified search radius");
        wearer.GiveItem(ids[0]);
        target.SetMaxShield(40,false); target.SetShieldPoints(40); effect.directDamage = false;
        trigger.Invoke(circle,new object[]{projectile});
        Check(target.GetHitpoints()==200 && target.GetShieldAmount()<40,"fully shielded circle preserves opener");
        target.SetShieldPoints(0); effect.directDamage = true;
        trigger.Invoke(circle,new object[]{projectile});
        Check(Mathf.Abs(target.GetHitpoints()-184)<.01f,"enlarged physics query reaches monster with 16 HP opener, actual HP "+target.GetHitpoints());
        wearer.RemoveItem(wearer.GetItems().IndexOf(ids[2])); wearer.GiveItem(ids[2]);
        trigger.Invoke(circle,new object[]{projectile});
        Check(Mathf.Abs(target.GetHitpoints()-178)<.01f,"re-equipping Mirror leaves 6 HP follow-up");
        Destroy(projectile.gameObject); Destroy(monster.gameObject);
    }

    private void CompatibleContent(AddressablesManager catalog)
    {
        // The separate QA plugin registers new native prefab IDs just as a content pack would.
        var root = new GameObject("Private content fixtures"); root.SetActive(false);
        var prefab = Instantiate(catalog.GetUnit("Archer2"),root.transform);
        const string unitId = "GKJ_Test_Archer";
        Traverse.Create(prefab).Field("entityIdentifier").SetValue(unitId);
        catalog.GetAllUnits().Add(unitId,prefab);
        var units = UnitManager.Instance;
        int idForSave = units.GetNextIdentifier();
        int position = GridManager.Instance.GetFirstFreeGridPosition(prefab.GetPossibleLocations());
        Check(position>0,"available deployment spot for pack unit");
        var record = new GameData.UnitData { name=unitId, identifier=idForSave, gridPosition=position, rank=1,
            items=new System.Collections.Generic.List<string>() };
        var recruit = units.SpawnUnit(unitId,record);
        units.SetNextIdentifier(idForSave+1);
        Check(recruit && recruit.GetEntityIdentifier()==unitId,"separate plugin native unit spawned");
        var items = ItemManager.Instance;
        var source = new[]{"Bow","Sword","Shield"}.Select(catalog.GetItem).First(i=>i && items.CanHoldItem(recruit,i));
        var gear = Instantiate(source,root.transform);
        const string gearId = "GKJ_Test_CompatibleGear";
        Traverse.Create(gear).Field("identifier").SetValue(gearId); catalog.GetAllItems().Add(gearId,gear);
        Write("Registered equipment based on "+source.GetIdentifier());
        foreach (var id in new[]{gearId,ids[0],ids[2]})
        {
            Check(items.CanHoldItem(recruit,catalog.GetItem(id)), "content-pack unit eligible for "+id);
            items.SpawnDragItem(id,recruit.transform.position,true);
            items.SetDragItem(items.GetDragItems().Last()); items.GiveDragItem(recruit);
        }
        Check(recruit.GetItems().SequenceEqual(new[]{gearId,ids[0],ids[2]}) && !items.CanHoldItem(recruit,catalog.GetItem(ids[1])),
            "mod-added gear coexists in three native slots");
        CombatChecks(recruit);
    }
}
