using UnityEngine;
using UnityEngine.EventSystems;

namespace LittleDriver.Prototype
{
    public sealed class SprintSeatBeltDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private SprintPrototypeCar car;
        [SerializeField] private RectTransform buckle;
        [SerializeField] private RectTransform strap;
        [SerializeField] private UnityEngine.UI.Image handleImage;
        [SerializeField] private float snapDistance = 85f;
        private RectTransform handle;
        private RectTransform panel;
        private Vector2 home;
        private Vector2 dragOffset;
        private int? pointerId;

        private void Awake()
        {
            handle = (RectTransform)transform;
            panel = (RectTransform)transform.parent;
            home = handle.anchoredPosition;
            UpdateStrap();
        }

        private void OnEnable() { if (car != null) car.Restarted += ResetBelt; }
        private void OnDisable()
        {
            if (car != null) car.Restarted -= ResetBelt;
            pointerId = null;
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (car.IsBuckled || pointerId.HasValue) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, data.pressPosition, data.pressEventCamera, out var local)) return;
            pointerId = data.pointerId;
            dragOffset = handle.anchoredPosition - local;
        }

        public void OnDrag(PointerEventData data)
        {
            if (pointerId != data.pointerId || car.IsBuckled) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, data.position, data.pressEventCamera, out var local)) return;
            Vector2 half = handle.rect.size * 0.5f;
            handle.anchoredPosition = new Vector2(
                Mathf.Clamp(local.x + dragOffset.x, panel.rect.xMin + half.x, panel.rect.xMax - half.x),
                Mathf.Clamp(local.y + dragOffset.y, panel.rect.yMin + half.y, panel.rect.yMax - half.y));
            UpdateStrap();
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (pointerId != data.pointerId || car.IsBuckled) return;
            pointerId = null;
            if (Vector2.Distance(handle.anchoredPosition, buckle.anchoredPosition) <= snapDistance)
            {
                handle.anchoredPosition = buckle.anchoredPosition;
                handleImage.color = new Color(0.75f, 0.82f, 0.75f);
                car.Buckle();
            }
            else handle.anchoredPosition = home;
            UpdateStrap();
        }

        public void ResetBelt()
        {
            if (handle == null) return;
            pointerId = null;
            handle.anchoredPosition = home;
            handleImage.color = new Color(0.78f, 0.78f, 0.78f);
            UpdateStrap();
        }

        private void UpdateStrap()
        {
            Vector2 delta = handle.anchoredPosition - home;
            strap.anchoredPosition = home + delta * 0.5f;
            strap.sizeDelta = new Vector2(Mathf.Max(25, delta.magnitude), 24);
            strap.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }
}
