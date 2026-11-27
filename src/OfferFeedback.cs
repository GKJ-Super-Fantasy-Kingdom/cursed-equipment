using System;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Clickable;
using SuperFantasyKingdom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CursedEquipment
{
    [HarmonyPatch(typeof(UIOverlayMerchant), "AddBuyButton")]
    internal sealed class OfferFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private Button buy;
        private UICardItem card;
        private Image edge;
        private bool hovered;
        private bool pressed;
        private bool keyboardSelected;
        private bool pointerSelected;

        private static void Postfix(UICard spawned, ResourceAmount[] costs, string inactiveReason,
            Action<int, ResourceAmount[]> ___m_Callback, Transform __result)
        {
            // Blacksmith prices contain one resource. Leave custom reasons, sales and multi-resource controls native.
            if (!(___m_Callback?.Target is BuildingForge) || !(spawned is UICardItem item) || !__result || inactiveReason != null
                || costs == null || costs.Length != 1 || costs[0].amount <= 0 || !ResourceManager.Instance)
                return;
            var cost = costs[0];
            bool unaffordable = !ResourceManager.Instance.CanBeBuilt(costs);
            var button = __result.GetComponent<Button>();
            if (!button || !CardFrame.ShowAsPricedOffer(item, unaffordable)) return;
            if (unaffordable)
            {
                int missing = cost.amount - ResourceManager.Instance.GetResourceAvailableAmount(cost.type);
                ShowShortfall(button, missing);
            }
            var feedback = button.gameObject.AddComponent<OfferFeedback>();
            feedback.card = item;
            feedback.buy = button;
            feedback.edge = CreateEdge(button);
        }

        private static Image CreateEdge(Button button)
        {
            var target = button.image;
            var selected = button.spriteState.selectedSprite;
            if (!target || !selected || selected.border == Vector4.zero) return null;

            // Draw the native selected sprite's two-tone rails over the button's own hover or pressed fill.
            var edgeObject = new GameObject("Cursed Equipment Buy Edge", typeof(RectTransform),
                typeof(LayoutElement), typeof(Image));
            edgeObject.SetActive(false);
            edgeObject.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = edgeObject.GetComponent<Image>();
            image.sprite = selected;
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.pixelsPerUnitMultiplier = target.pixelsPerUnitMultiplier;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.SetParent(target.rectTransform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return image;
        }

        private static void ShowShortfall(Button button, int missing)
        {
            var amount = button.transform.Find("Amount")?.GetComponent<TextMeshProUGUI>();
            if (!amount || missing <= 0) return;

            // Use the space below the native price without changing the layout or disabled styling.
            var labelObject = new GameObject("Cursed Equipment Shortfall", typeof(RectTransform),
                typeof(LayoutElement), typeof(TextMeshProUGUI));
            labelObject.GetComponent<LayoutElement>().ignoreLayout = true;
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            var rect = label.rectTransform;
            rect.SetParent(button.transform, false);
            rect.anchorMin = new Vector2(-.12f, -.57f);
            rect.anchorMax = new Vector2(1.12f, -.04f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            label.font = amount.font;
            label.fontSharedMaterial = amount.fontSharedMaterial;
            label.fontSize = amount.fontSize * .72f;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            label.color = new Color32(255, 197, 173, 255);
            label.text = $"Need {missing} more";
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            keyboardSelected = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            if (!pressed) ReleasePointerSelection();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !buy || !buy.IsInteractable()) return;
            pressed = true;
            keyboardSelected = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            pressed = false;
            if (!hovered) ReleasePointerSelection();
        }

        public void OnSelect(BaseEventData eventData)
        {
            keyboardSelected = eventData is AxisEventData;
            pointerSelected = eventData is PointerEventData;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            keyboardSelected = false;
            pointerSelected = false;
        }

        private void ReleasePointerSelection()
        {
            var events = EventSystem.current;
            var manager = GamepadManager.Instance;
            if (pointerSelected && events && events.currentSelectedGameObject == gameObject
                && (!manager || !manager.IsGamepadInUse()))
                events.SetSelectedGameObject(null);
        }

        private void LateUpdate()
        {
            var manager = GamepadManager.Instance;
            var events = EventSystem.current;
            bool selected = events && events.currentSelectedGameObject == gameObject;
            bool active = buy && buy.isActiveAndEnabled && buy.IsInteractable()
                && (manager && manager.IsGamepadInUse() ? selected : hovered || pressed || (selected && keyboardSelected));
            if (edge && edge.gameObject.activeSelf != active) edge.gameObject.SetActive(active);
            CardFrame.SetBuyFocus(card, active);
        }

        private void OnDisable()
        {
            hovered = false;
            pressed = false;
            keyboardSelected = false;
            pointerSelected = false;
            if (edge) edge.gameObject.SetActive(false);
            CardFrame.SetBuyFocus(card, false);
        }
    }
}
