using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SuperFantasyKingdom;
using UnityEngine;

public sealed partial class Probe
{
    private IEnumerator GroundIcons(AddressablesManager catalog, UnitBase hero)
    {
        var native=catalog.GetItem("Sword").GetIcon();
        Check(native.rect.size==new Vector2(16,16) && native.pivot==new Vector2(8,8) && native.pixelsPerUnit==16f,
            "base-game Sword has a centered 16x16 sprite at 16 pixels per world unit");
        yield return ResumeClock();
        GameCamera.Instance.SetPosition(hero.transform.position + new Vector3(3f,-3f));
        yield return new WaitForSecondsRealtime(.8f);
        var camera=GameCamera.Instance.GetCamera();
        float depth=camera.WorldToScreenPoint(Vector3.zero).z;
        var manager=ItemManager.Instance;
        var groups=new[]
        {
            new[]{"Sword",ids[0],ids[1],ids[2]},
            new[]{"Tome",expansionIds[0],expansionIds[1],expansionIds[2]},
            new[]{"Bow",expansionIds[3],expansionIds[4],expansionIds[5]},
            new[]{"Magnet",expansionIds[6]},
        };
        var drops=new List<DragItem>();
        for (int row=0; row<groups.Length; row++)
        {
            for (int col=0; col<groups[row].Length; col++)
            {
                var id=groups[row][col];
                var before=manager.GetDragItems().ToArray();
                var point=camera.ViewportToWorldPoint(new Vector3(.35f+.095f*col,.62f-.09f*row,depth));
                manager.SpawnDragItem(id,point,true);
                drops.Add(manager.GetDragItems().Single(drop=>drop && !before.Contains(drop)));
            }
        }
        yield return new WaitForSecondsRealtime(.65f);
        for (int frame=0; frame<12; frame++)
        {
            HandleNightPrompts();
            yield return null;
        }
        yield return new WaitForSecondsRealtime(.45f);
        Check(!OverlayManager.Instance.IsOpen() && !DialogueManager.Instance.IsInDialogue(),
            "native ground comparison is unobstructed by overlays");
        CaptureScene("ground-icons");
        yield return new WaitForSecondsRealtime(.5f);
        var standard=Traverse.Create(drops[0]);
        var normalRenderer=standard.Field("spriteRenderer").GetValue<SpriteRenderer>();
        var normalShadow=standard.Field("shadow").GetValue<SpriteRenderer>();
        foreach (var drop in drops.Where(d=>d.GetItemIdentifier().StartsWith("GKJ_CE_")))
        {
            var item=catalog.GetItem(drop.GetItemIdentifier());
            var sprite=item.GetIcon();
            var fields=Traverse.Create(drop);
            var renderer=fields.Field("spriteRenderer").GetValue<SpriteRenderer>();
            var shadow=fields.Field("shadow").GetValue<SpriteRenderer>();
            var viewport=camera.WorldToViewportPoint(renderer.bounds.center);
            Check(renderer.sprite==sprite && sprite.rect.size==native.rect.size && sprite.pivot==native.pivot
                && sprite.pixelsPerUnit==native.pixelsPerUnit && sprite.texture.filterMode==FilterMode.Point,
                $"{item.GetTitle()} matches the ordinary ground sprite size, pitch and anchor");
            // The native prefab makes a material instance for each drop so it can tint it independently.
            Check(renderer.sharedMaterial.shader==normalRenderer.sharedMaterial.shader && shadow.sprite==normalShadow.sprite
                && renderer.transform.localScale==normalRenderer.transform.localScale
                && viewport.x>0 && viewport.x<1 && viewport.y>0 && viewport.y<1,
                $"{item.GetTitle()} uses the native ground shader, scale and shadow in view at {viewport}");
        }
        Check(drops.Count==14,"native ground view shows ten cursed items and four ordinary comparisons");
        yield return new WaitForSecondsRealtime(5f);
    }
}
