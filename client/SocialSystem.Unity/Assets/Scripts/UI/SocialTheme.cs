using UnityEngine;
using UnityEngine.UI;

namespace SocialSystem.Client.UI
{
    public static class SocialTheme
    {
        public const int BodySize = 18;
        public const int SmallSize = 16;
        public const int ButtonSize = 18;
        public const int Gap = 12;
        public const int Padding = 16;
        public static readonly Color Background = new Color(0.055f, 0.078f, 0.12f);
        public static readonly Color Surface = new Color(0.10f, 0.14f, 0.20f);
        public static readonly Color InputSurface = new Color(0.065f, 0.095f, 0.145f);
        public static readonly Color Accent = new Color(0.18f, 0.43f, 0.76f);
        public static readonly Color TextColor = new Color(0.93f, 0.95f, 1f);
        public static readonly Color Muted = new Color(0.62f, 0.69f, 0.79f);
        private static Font font;
        public static Font Font => font != null ? font : (font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "Segoe UI", "Arial" }, BodySize));

        public static void Text(Text text, int size = BodySize)
        {
            text.font = Font;
            text.fontSize = size;
            text.color = TextColor;
            text.supportRichText = false;
        }
        public static void Button(Button button)
        {
            var image = button.GetComponent<Image>();
            if (image != null) { image.color = Accent; button.targetGraphic = image; }
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.selectedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            colors.disabledColor = new Color(0.48f, 0.48f, 0.48f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;
        }
        public static void Input(InputField field)
        {
            var image = field.GetComponent<Image>();
            if (image != null) { image.color = InputSurface; field.targetGraphic = image; }
            Text(field.textComponent);
            if (field.placeholder is Text placeholder)
            {
                Text(placeholder, SmallSize);
                placeholder.color = Muted;
            }
            field.customCaretColor = true;
            field.caretColor = TextColor;
            field.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.55f);
        }
    }
}
