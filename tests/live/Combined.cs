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
    private bool ExpansionPair => Environment.GetEnvironmentVariable("SFK_EQUIP_EXPANSION") == "1";
    private const string Covenant = "gkj_CR_VulturesCovenant";

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
        if (ExpansionPair)
            Check(Chainloader.PluginInfos[combinedPackages[2]].Metadata.Version.ToString()=="0.2.8"
                && Chainloader.PluginInfos[combinedPackages[3]].Metadata.Version.ToString()=="0.2.3"
                && Chainloader.PluginInfos.Values.Count(p=>p.Metadata.GUID!="gkj.local.cursedequipment.qa" && p.Instance)==16,
                "Relics 0.2.8 and Equipment 0.2.3 loaded with the 14 GKJ profile mods");
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
        if (phase == "combined-nights")
        {
            yield return EquipmentNights();
            yield break;
        }
        if (phase == "combined-icons")
        {
            yield return GroundIcons(catalog,hero);
            yield break;
        }
        if (phase == "combined-wave-large")
        {
            var manager=RelicManager.Instance;
            foreach (var id in new[]{Covenant,"gkj_CR_GlassCrown","gkj_CR_RedMercy"})
                if (!manager.HasRelic(id)) manager.AddRelic(id);
            yield return ResumeClock();
            yield return NativeWaves(hero,true);
            yield break;
        }
        var recipient = UnitManager.Instance.GetAllUnits().First(u => u && u != hero && !u.IsDead()
            && u.GetHealth().GetMaxHealth()>0 && u.GetItems().Count<3);
        if (phase == "combined-reload")
        {
            Check(recipient.GetItems().Count(id=>id==ids[1])==1 && !hero.GetItems().Contains(ids[1]),
                "Continue restored one purchased Chalice on the recipient, former wearer has none");
            yield return ResumeClock();
            yield return CheckChalice(recipient, "Continued wearer");
            if (ExpansionPair)
                Check(RelicManager.Instance.HasRelic(Covenant),"Continue also restored Vulture's Covenant");
            overlay.OpenUnitDetails(recipient,true);
            yield return new WaitForSecondsRealtime(.5f);
            CaptureScene("continued-loadout");
            yield return new WaitForSecondsRealtime(2.5f);
            yield break;
        }
        bool padMode = phase.Contains("-pad");
        bool demo = phase == "combined-pad" && Environment.GetEnvironmentVariable("SFK_EQUIP_VISIBLE")=="1";
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
        bool frameOnly=phase.StartsWith("combined-frame-");
        if (ExpansionPair) yield return MixedRow(catalog,forge,pad,mouse,keyboard);
        if (ExpansionPair && frameOnly && pad!=null) yield return SharedRelicRow(catalog,pad);
        if (ExpansionPair && padMode) yield return Roster(catalog,hero,scale,pad);
        if (frameOnly) yield break;

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
        if (demo)
        {
            var sale=items.GetDragItems().Single(d=>d && d.GetItemIdentifier()==ids[1]);
            GameCamera.Instance.SetPosition(sale.transform.position);
            yield return new WaitForSecondsRealtime(.7f);
            CaptureScene("purchased-drop");
            yield return new WaitForSecondsRealtime(2f);
        }
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
        if (ExpansionPair)
        {
            var manager=RelicManager.Instance;
            if (!manager.HasRelic(Covenant)) manager.AddRelic(Covenant);
            Check(manager.HasRelic(Covenant),"fixture granted Vulture's Covenant before the copied checkpoint");
            yield return CheckRelicEquipmentOverlap(recipient);
        }
        GameDataManager.Instance.Save();
        var save=Path.Combine(Application.persistentDataPath,"1_human_game.data");
        Check(File.ReadAllText(save).Contains(ids[1]),"native save wrote the purchased Chalice");
        if (ExpansionPair)
        {
            var preserved=Directory.CreateDirectory(Path.Combine(evidence,"checkpoint")).FullName;
            File.Copy(save,Path.Combine(preserved,Path.GetFileName(save)));
        }
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

    private IEnumerator MixedRow(AddressablesManager catalog, BuildingForge forge, Gamepad pad, Mouse mouse, Keyboard keyboard)
    {
        var overlay=OverlayManager.Instance;
        var shop=overlay.GetMerchantScreen();
        shop.Init("Blacksmith");
        shop.SetActor(DialogueManager.Instance.GetActor(Actors.Blacksmith));
        shop.SetText((string)AccessTools.Method(typeof(BuildingObjectShop),"GetDescription").Invoke(forge,null));
        shop.SetUseCardSelectCallback(true);
        var picks=new[]{"Sword",ids[1],"Bow"}.Select(catalog.GetItem).ToArray();
        var price=(ResourceAmount[])AccessTools.Method(typeof(BuildingForge),"GetPrice").Invoke(forge,new object[]{picks[1]});
        if (!ResourceManager.Instance.CanBeBuilt(price)) ResourceManager.Instance.AddResources(price);
        int next=0;
        shop.Items(()=>picks[next++%picks.Length], _=>price);
        shop.SetCancel("Leave");
        shop.Display();
        yield return new WaitForSecondsRealtime(1.6f);
        var cards=Cards(shop);
        var cursed=cards.Single(card=>CardId(card)==ids[1]);
        var reference=cards.Single(card=>CardId(card)=="Sword");
        var cursedFields=Traverse.Create(cursed);
        var nativeRarity=cursedFields.Field("rarity").GetValue<Image>();
        var kingdomBadge=cursedFields.Field("kingdom").GetValue<Image>();
        Check(nativeRarity && nativeRarity.sprite && nativeRarity.gameObject.activeInHierarchy
            && kingdomBadge && kingdomBadge.sprite && kingdomBadge.gameObject.activeInHierarchy,
            "mixed cursed card retained visible native rarity and kingdom badges");
        foreach (var card in cards)
        {
            var face=CardSurface(card);
            var clip=EquipmentClip(face);
            var wood=face.transform.Find("BG");
            bool custom=ReferenceEquals(card,cursed);
            var field=wood.Find("Cursed Equipment Icon Field");
            Check((clip && clip.gameObject.activeInHierarchy)==custom && wood.gameObject.activeSelf
                && (field && field.gameObject.activeInHierarchy)==custom,CardId(card)+" renders its expected trim and icon field");
        }
        var image=CardSurface(cursed);
        var native=CardSurface(reference);
        var trim=EquipmentClip(image);
        bool frameOnly=phase.StartsWith("combined-frame-");
        reference.OnDeselect();
        cursed.OnDeselect();
        if (frameOnly)
            CheckNativeSilhouette(native,image,trim,"resting");
        reference.OnEnter();
        cursed.OnEnter();
        Check(EquipmentArt(trim).sprite.name=="shared-trim","native hover retains the shared trim");
        if (frameOnly)
        {
            yield return new WaitForSecondsRealtime(.35f);
            CheckNativeSilhouette(native,image,trim,"hover");
            if (pad!=null)
            {
                CaptureScene("mixed-frame-hover");
                yield return new WaitForSecondsRealtime(.45f);
            }
        }
        reference.OnSelect();
        cursed.OnSelect();
        Check(EquipmentArt(trim).sprite.name=="shared-trim","native selection retains the shared trim");
        if (frameOnly)
        {
            yield return new WaitForSecondsRealtime(.35f);
            CheckNativeSilhouette(native,image,trim,"selected");
            if (pad!=null)
            {
                CaptureScene("mixed-frame-selected");
                yield return new WaitForSecondsRealtime(.45f);
            }
        }
        reference.OnDeselect();
        cursed.OnDeselect();
        var buys=shop.transform.Find("Panel/BuyButtons").GetComponentsInChildren<Button>();
        foreach(var button in buys)
        {
            var amount=button.transform.Find("Amount").GetComponent<TMP_Text>();
            var currency=button.transform.Find("Icon").GetComponent<Image>();
            amount.ForceMeshUpdate();
            Check(button.IsInteractable() && amount.text.Trim()==price[0].amount.ToString()
                && !amount.isTextOverflowing && currency.enabled && currency.sprite,
                "mixed native price and currency icon are readable");
        }
        if (pad!=null)
        {
            var target=buys[Array.IndexOf(cards,cursed)];
            for (int move=0;move<6 && EventSystem.current.currentSelectedGameObject!=target.gameObject;move++)
                yield return Press(pad,DirectionTo(target,GamepadButton.DpadUp));
            Check(EventSystem.current.currentSelectedGameObject==target.gameObject,"D-pad focused the cursed offer in the mixed row");
        }
        CaptureScene("mixed-frames");
        yield return new WaitForSecondsRealtime(2f);
        yield return Back(shop,pad,mouse,keyboard);
        Check(!shop.gameObject.activeInHierarchy,"native Back closed the mixed row");
        if (overlay.IsOpen()) overlay.CloseOverlay();
    }

    private IEnumerator SharedRelicRow(AddressablesManager catalog,Gamepad pad)
    {
        var manager=OverlayManager.Instance;
        var shop=manager.GetMerchantScreen();
        shop.Init("Shared cursed frame comparison");
        shop.SetText("Blacksmith items and relic reward");
        shop.SetUseCardSelectCallback(false);
        shop.SetCancel("Leave");
        var price=new[]{new ResourceAmount(ResourceType.Beer,2)};
        var sword=shop.AddItem(catalog.GetItem("Sword"),price).GetComponent<UICardItem>();
        var chalice=shop.AddItem(catalog.GetItem(ids[1]),price).GetComponent<UICardItem>();
        var bell=shop.AddRelic(catalog.GetRelic("gkj_CR_LastWatch"),price,checkIfCanAdd:false).GetComponent<UICardRelic>();
        shop.Display();
        yield return new WaitForSecondsRealtime(1.6f);
        var equipment=CardSurface(chalice);
        var relic=Traverse.Create(bell).Field("card").GetValue<Image>();
        var equipmentBanner=Traverse.Create(chalice).Field("title").GetValue<TMP_Text>().transform.parent;
        var relicBanner=Traverse.Create(bell).Field("title").GetValue<TMP_Text>().transform.parent;
        Check(SameArt(EquipmentArt(EquipmentClip(equipment)),relic.transform.Find("Cursed Relic Trim").GetComponent<Image>())
            && SameArt(equipmentBanner.Find("Cursed Equipment Ribbon").GetComponent<Image>(),relicBanner.Find("Cursed Relic Banner").GetComponent<Image>())
            && SameArt(equipment.transform.Find("Cursed Equipment Sigil").GetComponent<Image>(),relic.transform.Find("Cursed Relic Sigil").GetComponent<Image>())
            && SameArt(equipment.transform.Find("BG/Cursed Equipment Icon Field").GetComponent<Image>(),relic.transform.Find("BG/Cursed Relic Icon Field").GetComponent<Image>()),
            "Equipment and Relics share exact trim, ribbon, sigil, and icon-field pixels at this scale");

        foreach (var state in new[]{"resting","hover","selected"})
        {
            if (state=="resting") { sword.OnDeselect(); chalice.OnDeselect(); bell.OnDeselect(); }
            else if (state=="hover") { sword.OnEnter(); chalice.OnEnter(); bell.OnEnter(); }
            else { sword.OnSelect(); chalice.OnSelect(); bell.OnSelect(); }
            yield return new WaitForSecondsRealtime(.35f);
            CheckNativeSilhouette(CardSurface(sword),equipment,EquipmentClip(equipment),"shared "+state);
            var itemBounds=ScreenBounds(equipment);
            var relicBounds=ScreenBounds(relic);
            Check(Vector2.Distance(itemBounds.size,relicBounds.size)<.1f && Mathf.Abs(itemBounds.yMin-relicBounds.yMin)<.1f
                && equipment.overrideSprite==relic.overrideSprite && SameImageMesh(equipment,relic),
                $"shared {state} item and relic align at {itemBounds.width:0}x{itemBounds.height:0} and use the same native face mesh");
            CaptureScene("shared-row-"+state);
            yield return new WaitForSecondsRealtime(.5f);
        }
        yield return Back(shop,pad,null,null);
        Check(!shop.gameObject.activeInHierarchy,"controller Back closed the shared frame comparison");
        if (manager.IsOpen()) manager.CloseOverlay();
    }

    private static bool SameArt(Image a,Image b) => a && b && a.enabled && b.enabled &&
        a.gameObject.activeInHierarchy && b.gameObject.activeInHierarchy &&
        a.sprite.texture.GetPixels32().SequenceEqual(b.sprite.texture.GetPixels32()) &&
        a.rectTransform.anchorMin==b.rectTransform.anchorMin && a.rectTransform.anchorMax==b.rectTransform.anchorMax;

    private static Image CardSurface(UICardItem card) => Traverse.Create(card).Field("card").GetValue<Image>();

    private static Image EquipmentClip(Image face) => face.transform.Find("Cursed Equipment Clip")?.GetComponent<Image>();

    private static Image EquipmentArt(Image clip) => clip.transform.Find("Cursed Equipment Art").GetComponent<Image>();

    private void CheckNativeSilhouette(Image reference, Image surface, Image clip, string state)
    {
        var ordinaryBounds=ScreenBounds(reference);
        var cursedBounds=ScreenBounds(surface);
        var clipBounds=ScreenBounds(clip);
        Write($"BOUNDS {state}: Sword {ordinaryBounds}, Bitter Chalice {cursedBounds}, clip {clipBounds}");
        Check(Vector2.Distance(ordinaryBounds.size,cursedBounds.size)<.1f
            && Mathf.Abs(ordinaryBounds.yMin-cursedBounds.yMin)<.1f
            && Vector2.Distance(cursedBounds.min,clipBounds.min)<.1f
            && Vector2.Distance(cursedBounds.max,clipBounds.max)<.1f,
            $"{state} on-screen width, height and row alignment match the ordinary card");
        var mask=clip.GetComponent<Mask>();
        Check(reference.overrideSprite==surface.overrideSprite && reference.overrideSprite==clip.overrideSprite
            && reference.type==surface.type && reference.type==clip.type && mask && mask.IsActive() && !mask.showMaskGraphic
            && !clip.raycastTarget && !EquipmentArt(clip).raycastTarget && SameImageMesh(reference,clip),
            $"{state} native face and active art mask share the ordinary image mesh and rounding texture");
    }

    private static Rect ScreenBounds(Image image)
    {
        var corners=new Vector3[4];
        image.rectTransform.GetWorldCorners(corners);
        var canvas=image.canvas.rootCanvas;
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
        var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }

    private static bool SameImageMesh(Image reference,Image clip)
    {
        var populate=AccessTools.Method(typeof(Image),"OnPopulateMesh",new[]{typeof(VertexHelper)});
        using var nativeMesh=new VertexHelper();
        using var clipMesh=new VertexHelper();
        populate.Invoke(reference,new object[]{nativeMesh});
        populate.Invoke(clip,new object[]{clipMesh});
        var nativeVertices=new List<UIVertex>();
        var clipVertices=new List<UIVertex>();
        nativeMesh.GetUIVertexStream(nativeVertices);
        clipMesh.GetUIVertexStream(clipVertices);
        return nativeVertices.Count>0 && nativeVertices.Count==clipVertices.Count
            && nativeVertices.Zip(clipVertices,(a,b)=>Vector3.Distance(a.position,b.position)<.1f
                && Vector4.Distance(a.uv0,b.uv0)<.00001f).All(match=>match);
    }

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

    private IEnumerator CheckRelicEquipmentOverlap(UnitBase wearer)
    {
        const string glass="gkj_CR_GlassCrown", mercy="gkj_CR_RedMercy";
        var manager=RelicManager.Instance;
        var night=Chainloader.PluginInfos[combinedPackages[2]].Instance.GetType().Assembly.GetType("CursedRelics.Night",true);
        var end=AccessTools.Method(night,"End");
        var health=wearer.GetHealth();
        float originalMax=health.GetMaxHealth();
        if (!manager.HasRelic(glass)) manager.AddRelic(glass);
        if (!manager.HasRelic(mercy)) manager.AddRelic(mercy);
        // Apply the exact dusk hook without advancing or fast-forwarding the live clock.
        AccessTools.Method(night,"Dusk").Invoke(null,new object[]{DaytimeManager.Instance.CurrentDay()});
        try
        {
            float max=health.GetMaxHealth();
            Check(Mathf.Abs(max-originalMax*.75f)<.02f,$"Glass Crown changed the wearer's actual max Health from {originalMax} to {max}");
            health.SetHitPoints(max*.35f);
            float before=health.GetHitpoints();
            Check(health.ReceiveHeal(new Heal{healType=HealType.Health,healAmount=2})==0,"Chalice delayed a Red Mercy heal");
            yield return GameSeconds(3.2f);
            Check(Mathf.Abs(health.GetHitpoints()-before-3.9f)<.02f,
                $"Chalice and Red Mercy delivered 3.9 HP, observed {health.GetHitpoints()-before}");
            wearer.GiveItem(expansionIds[5]);
            try
            {
                health.SetHitPoints(max*.7f);
                health.ReceiveHeal(new Heal{healType=HealType.OverHeal,healAmount=max*.4f});
                yield return GameSeconds(3.2f);
                Check(Mathf.Abs(health.GetHitpoints()-max*.75f)<.02f,
                    $"Locket capped amplified healing at 75% of Glass Crown maximum ({max*.75f}), observed {health.GetHitpoints()}");
            }
            finally { wearer.RemoveItem(wearer.GetItems().IndexOf(expansionIds[5])); }
        }
        finally { end.Invoke(null,null); }
        Check(Mathf.Abs(health.GetMaxHealth()-originalMax)<.02f,"ending the controlled dusk restored the native maximum");
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

    private IEnumerator NativeWaves(UnitBase hero,bool supportSurvival=false)
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
            var heroHealth=hero.GetHealth();
            int assistedHeals=0;
            if (supportSurvival)
            {
                WallManager.Instance.AddHitpoints(750f);
                Write("CONTROLLED Added 750 wall durability through the native wall API for the midnight retry");
            }
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
                if (supportSurvival && heroHealth.IsAlive() && heroHealth.GetHealthPercentageAbs()<.5f)
                {
                    heroHealth.SetHitPoints(heroHealth.GetMaxHealth());
                    heroHealth.SetCurrentHealth();
                    assistedHeals++;
                }
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
            if (supportSurvival) Write("CONTROLLED Survival retry restored hero health " + assistedHeals + " times");
            if (game.IsGameOver()) Write("NATIVE Game over before the wave probe finished. Hero alive=" + heroHealth.IsAlive());
            Check(testedOrdinary && testedMidnight && plannedCount>0 && midnight.Count==plannedCount,
                $"natural wave and scheduled midnight group advanced after controlled hit probes, {midnight.Count}/{plannedCount} reinforcements");
            yield return new WaitForSecondsRealtime(10f);
        }
        finally { CombatManager.OnMonsterSpawn -= Observe; }
    }

    private void CheckWaveTarget(UnitBase wearer,MonsterBase target,string wave,bool covenant=true)
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
            float multiplier=ExpansionPair && covenant ? .85f : 1f;
            if (ExpansionPair && covenant) Check(RelicManager.Instance.HasRelic(Covenant),wave+" Covenant active");
            Check(Mathf.Abs(hp.GetHitpoints()-(200-16*multiplier))<.02f,
                wave+" Spellglass and Mirror opener, HP "+hp.GetHitpoints());
            wearer.RemoveItem(wearer.GetItems().IndexOf(ids[2])); wearer.GiveItem(ids[2]);
            trigger.Invoke(circle,new object[]{projectile});
            Check(Mathf.Abs(hp.GetHitpoints()-(200-22*multiplier))<.02f,
                wave+" follow-up after Mirror re-equip, HP "+hp.GetHitpoints());
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
