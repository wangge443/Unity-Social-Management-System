using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SocialSystem.Client.UI
{
    public static class SocialUi
    {

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
        public static void Stretch(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
        public static void Height(GameObject go, float height)
        {
            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = height;
            layout.minWidth = layout.preferredWidth = 0;
            layout.flexibleWidth = 1;
        }
        public static RectTransform Column(string name, Transform parent)
        {
            var rect = Rect(name, parent);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = SocialTheme.Gap;
            layout.padding = new RectOffset(SocialTheme.Padding, SocialTheme.Padding, SocialTheme.Padding, SocialTheme.Padding);
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return rect;
        }
        public static RectTransform Row(string name, Transform parent, float height = 42)
        {
            var rect = Rect(name, parent);
            Height(rect.gameObject, height);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = SocialTheme.Gap;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            return rect;
        }
        public static Text Label(Transform parent, string value, int size = 18, float height = 38)
        {
            var rect = Rect("Label", parent);
            var text = rect.gameObject.AddComponent<Text>();
            SocialTheme.Text(text, size);
            text.text = value;
            text.fontSize = size;
            text.color = SocialTheme.TextColor;
            text.supportRichText = false;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Height(rect.gameObject, height);
            text.raycastTarget = false;
            return text;
        }
        public static Text Body(Transform parent, string value)
        {
            var text = Label(parent, value);
            var size = text.GetComponent<LayoutElement>();
            size.minHeight = 30;
            size.preferredHeight = -1;
            return text;
        }
        public static Button Button(Transform parent, string title, UnityAction click)
        {
            var rect = Rect(title, parent);
            Height(rect.gameObject, 40);
            rect.gameObject.AddComponent<Image>().color = SocialTheme.Accent;
            var button = rect.gameObject.AddComponent<Button>();
            SocialTheme.Button(button);
            button.onClick.AddListener(click);
            var label = Label(rect, title, SocialTheme.ButtonSize);
            UnityEngine.Object.Destroy(label.GetComponent<LayoutElement>());
            Stretch(label.rectTransform, 8, 2, 8, 2);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        public static InputField Input(Transform parent, string hint, int limit, bool multiline = false)
        {
            var rect = Rect(hint, parent);
            Height(rect.gameObject, multiline ? 82 : 40);
            rect.gameObject.AddComponent<Image>().color = SocialTheme.InputSurface;
            var field = rect.gameObject.AddComponent<InputField>();
            var text = Label(rect, "", 18);
            UnityEngine.Object.Destroy(text.GetComponent<LayoutElement>());
            Stretch(text.rectTransform, 10, 5, 10, 5);
            text.color = SocialTheme.TextColor;
            var placeholder = Label(rect, hint, 16);
            UnityEngine.Object.Destroy(placeholder.GetComponent<LayoutElement>());
            Stretch(placeholder.rectTransform, 10, 5, 10, 5);
            placeholder.color = SocialTheme.Muted;
            field.textComponent = text;
            field.placeholder = placeholder;
            SocialTheme.Input(field);
            field.characterLimit = limit;
            field.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            return field;
        }
        public static RectTransform Scroll(Transform parent)
        {
            var root = Rect("Scroll", parent);
            var element = root.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1;
            element.minHeight = 100;
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = SocialTheme.Background;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Column("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            return content;
        }
        public static void Clear(Transform parent)
        {
            foreach (Transform child in parent)
            {
                child.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
        public static string Time(string value) => DateTimeOffset.TryParse(value, out var time)
            ? time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : value;
    }
}
