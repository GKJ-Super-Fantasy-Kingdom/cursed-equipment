using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CursedEquipment
{
    // The native merchant briefly disables priced buttons while animating them.
    // Unity may clear selection, even though GamepadManager remembers the button.
    [HarmonyPatch(typeof(UIOverlayMerchant), nameof(UIOverlayMerchant.AddItem))]
    internal sealed class ShopFocus : MonoBehaviour
    {
        private Transform purchases;

        private static void Postfix(UIOverlayMerchant __instance, Item item, ResourceAmount[] costs)
        {
            if (costs == null || costs.Length == 0 || !Catalog.IsCursed(item)) return;
            var focus = __instance.GetComponent<ShopFocus>() ?? __instance.gameObject.AddComponent<ShopFocus>();
            focus.purchases = __instance.transform.Find("Panel/BuyButtons");
            focus.enabled = true;
        }

        private void LateUpdate()
        {
            var manager = GamepadManager.Instance;
            var events = EventSystem.current;
            if (!manager || !manager.IsGamepadInUse() || !events || !purchases || !purchases.gameObject.activeInHierarchy)
                return;
            var preferred = manager.GetGameObjectToSelect();
            if (!preferred || !preferred.transform.IsChildOf(purchases)) return;
            var buy = preferred.GetComponent<Button>();
            if (!buy || !buy.isActiveAndEnabled || !buy.IsInteractable()) return;
            if (!events.currentSelectedGameObject)
                manager.SetGameObjectToSelect(preferred, true);
            enabled = false;
        }

        private void OnDisable() { enabled = false; }
    }
}
