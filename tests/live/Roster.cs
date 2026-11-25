using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Clickable;
using SuperFantasyKingdom.Status;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public sealed partial class Probe
{
    private static readonly string[] expansionIds =
    {
        "GKJ_CE_DuelistsFang", "GKJ_CE_MartyrsPlate", "GKJ_CE_GravediggersLantern", "GKJ_CE_HangmansQuarrel",
        "GKJ_CE_EmberwakeBlade", "GKJ_CE_LeadheartLocket", "GKJ_CE_BorrowedHalo"
    };

    private IEnumerator Roster(AddressablesManager catalog, UnitBase hero, float scale, Gamepad pad=null)
    {
        yield return ResumeClock();
        var roster = ids.Concat(expansionIds).ToArray();
        var all = roster.Select(catalog.GetItem).ToArray();
        Check(catalog.GetAllItems().Count(i=>i.Key.StartsWith("GKJ_CE_"))==10 &&
              all.All(i=>i && i.SoldAtBlacksmith() && i.GetIcon()) && all.Select(i=>i.GetIcon()).Distinct().Count()==10,
              "ten distinct items with unique native sprites");
        Check(SettingsManager.Instance.GetUIScaling()==scale,"active scale "+scale);
        var forge = FindObjectOfType<BuildingForge>();
        Check(forge,"copied kingdom has its native Forge");
        var price = (ResourceAmount[])AccessTools.Method(typeof(BuildingForge),"GetPrice")
            .Invoke(forge,new object[]{all[0]});
        if (!ResourceManager.Instance.CanBeBuilt(price)) ResourceManager.Instance.AddResources(price);
        var introduction = (string)AccessTools.Method(typeof(BuildingObjectShop),"GetDescription").Invoke(forge,null);
        for (int start=0;start<all.Length;start+=3)
        {
            var picks = all.Skip(start).Take(3).ToArray();
            var shop = OverlayManager.Instance.GetMerchantScreen();
            shop.Init("Blacksmith");
            if (pad==null) shop.SetActor(DialogueManager.Instance.GetActor(Actors.Blacksmith));
            shop.SetText(introduction);
            shop.SetUseCardSelectCallback(true);
            int index=0;
            shop.Items(()=>picks[index++%picks.Length], _=>price);
            shop.SetCancel("Leave");
            shop.Display();
            yield return new WaitForSecondsRealtime(1.45f);
            var cards=Cards(shop);
            Check(cards.Length==picks.Length,"native cards shown for roster page "+(start/3+1));
            foreach (var card in cards)
            {
                var fields=Traverse.Create(card);
                var title=fields.Field("title").GetValue<TMP_Text>();
                var desc=fields.Field("description").GetValue<TMP_Text>();
                var clip=EquipmentClip(CardSurface(card));
                title.ForceMeshUpdate();
                desc.ForceMeshUpdate();
                Check(!title.isTextOverflowing && !desc.isTextOverflowing && clip && clip.gameObject.activeInHierarchy
                    && EquipmentArt(clip).sprite.name=="shared-trim",title.text+" fits its shared frame");
                var visible=desc.textInfo.characterInfo.Take(desc.textInfo.characterCount).Where(c=>c.isVisible).ToArray();
                bool red(Color32 c) => c.r==153 && c.g==31 && c.b==41;
                Check(visible.Length>0 && !red(visible[0].color) && red(visible.Last().color)
                      && desc.text.Contains("<color=#991F29>Curse:"),
                    title.text+" renders the curse red while Gift keeps native color");
                CheckDescriptionClearance(CardSurface(card),desc,title.text,scale);
            }
            if (pad!=null)
            {
                var buttons=shop.transform.Find("Panel/BuyButtons").GetComponentsInChildren<Button>();
                foreach(var buy in buttons)
                {
                    for (int move=0;move<6 && EventSystem.current.currentSelectedGameObject!=buy.gameObject;move++)
                        yield return Press(pad,DirectionTo(buy,GamepadButton.DpadUp));
                    Check(EventSystem.current.currentSelectedGameObject==buy.gameObject,
                        "D-pad reached the native Buy button for "+picks[Array.IndexOf(buttons,buy)].GetTitle());
                }
            }
            CaptureScene("roster-"+(start/3+1));
            yield return new WaitForSecondsRealtime(.35f);
            if(pad!=null)
            {
                yield return Back(shop,pad,null,null);
                Check(!shop.gameObject.activeInHierarchy,"D-pad Back closed roster page "+(start/3+1));
            }
            if(OverlayManager.Instance.IsOpen()) OverlayManager.Instance.CloseOverlay();
            yield return null;
        }
        if(pad==null) NativeExpansion(hero,catalog);
    }

    private void CheckDescriptionClearance(UnityEngine.UI.Image surface, TMP_Text description, string title, float scale)
    {
        var face=surface.rectTransform;
        var glyphs=description.textBounds;
        var min=face.InverseTransformPoint(description.rectTransform.TransformPoint(glyphs.min));
        var max=face.InverseTransformPoint(description.rectTransform.TransformPoint(glyphs.max));
        var bounds=face.rect;
        float sideInset=bounds.width*8f/72f;
        float side=Mathf.Min(min.x-bounds.xMin-sideInset,bounds.xMax-sideInset-max.x);
        float bottom=min.y-bounds.yMin-bounds.height*5f/96f;
        Check(side>=7f && bottom>=3f, $"PADDING {scale:0.0}x {title} | side={side:0.0} bottom={bottom:0.0} card units");
        var characters=description.textInfo.characterInfo;
        for (int i=0; i+1<description.textInfo.characterCount; i++)
        {
            var sign=characters[i];
            if ((sign.character!='+' && sign.character!='-') || !char.IsDigit(characters[i+1].character)) continue;
            int end=i+1;
            while (end<description.textInfo.characterCount && char.IsDigit(characters[end].character)) end++;
            if (end<description.textInfo.characterCount && characters[end].character=='%')
                Check(characters[end].lineNumber==sign.lineNumber,$"{title} keeps its signed percentage together at {scale:0.0}x");
        }
    }

    private void NativeExpansion(UnitBase hero,AddressablesManager catalog)
    {
        var health=hero.GetHealth();
        float max=health.GetMaxHealth();
        hero.SetItems(new List<string>{expansionIds[1],expansionIds[5]});
        health.SetHitPoints(max*.5f);
        float restored=health.ReceiveHeal(new Heal{healAmount=max*.8f,healType=HealType.OverHeal});
        Check(Mathf.Abs(restored-max*.25f)<.01f && Mathf.Abs(health.GetHitpoints()-max*.75f)<.01f,
            "native overheal obeys Plate and Locket cap");
        hero.SetItems(new List<string>{expansionIds[4]});
        health.SetHitPoints(max*.8f);
        float before=health.GetHitpoints();
        var source=catalog.GetAllMonster().Values.First(m=>m && m.GetWave()==1);
        var enemy=CombatManager.Instance.SpawnMonster(source,new Vector3(55,63,0));
        enemy.Init(1,false);
        var hp=enemy.GetHealth();
        hp.SetMaxHealth(200,true);
        hp.SetHitPoints(200);
        var attack=new Damage{damageAmount=3f,origin=hero,originIdentifier=hero.GetIdentifier(),
            damageElement=DamageElement.None};
        for(int hit=0;hit<3;hit++) hp.ReceiveDamage(attack,true,true);
        Check(!enemy.GetStatusManager().GetAll().Any(s=>s.GetStatusType()==StatusType.Burn) &&
              Mathf.Abs(health.GetHitpoints()-before)<.01f,"first three native hits do not Burn or charge the wearer");
        hp.ReceiveDamage(attack,true,true);
        Check(enemy.GetStatusManager().GetAll().Any(s=>s.GetStatusType()==StatusType.Burn) &&
              Mathf.Abs(health.GetHitpoints()-(before-max*.05f))<.01f,"fourth native hit applies Burn and charges five percent Health");
        enemy.GetStatusManager().RemoveAllStatusEffect();
        health.SetHitPoints(1.1f);
        for(int hit=0;hit<4;hit++) hp.ReceiveDamage(attack,true,true);
        Check(health.GetHitpoints()==1f && enemy.GetStatusManager().GetAll().Any(s=>s.GetStatusType()==StatusType.Burn),
            "Emberwake charges down to 1 HP and applies Burn");
        enemy.GetStatusManager().RemoveAllStatusEffect();
        for(int hit=0;hit<4;hit++) hp.ReceiveDamage(attack,true,true);
        Check(health.GetHitpoints()==1f && enemy.GetStatusManager().GetAll().Any(s=>s.GetStatusType()==StatusType.Burn),
            "Emberwake at 1 HP applies Burn without killing or healing its wearer");
        Destroy(enemy.gameObject);
    }
}
