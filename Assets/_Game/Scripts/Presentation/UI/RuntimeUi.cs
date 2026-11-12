using UnityEngine;
using UnityEngine.UI;

namespace DontTrustYourLuck.Presentation.UI
{
    /// <summary>
    /// Tiny uGUI factory for HUD pieces created at runtime (series scoreboard and intermission, R1), so they work
    /// in the existing GameHUD prefab without regenerating the scene. Placeholder look: flat paper colors from
    /// <see cref="HudPalette"/>; a later art pass can replace them with builder-made prefabs.
    /// </summary>
    internal static class RuntimeUi
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Image(string name, Transform parent, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static Text Text(string name, Transform parent, Font font, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static LayoutElement Layout(Component c, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>(); // no ?? on UnityEngine.Object
            le.preferredWidth = preferredWidth;
            le.preferredHeight = preferredHeight;
            le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static HorizontalLayoutGroup Row(Component c, float spacing, TextAnchor align = TextAnchor.MiddleCenter, int padding = 0)
        {
            var row = c.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = align;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.padding = new RectOffset(padding, padding, padding, padding);
            return row;
        }

        public static VerticalLayoutGroup Column(Component c, float spacing, int padding)
        {
            var col = c.gameObject.AddComponent<VerticalLayoutGroup>();
            col.spacing = spacing;
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = true;
            col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;
            col.padding = new RectOffset(padding, padding, padding, padding);
            return col;
        }

        /// <summary>Font of the existing HUD (keeps the look consistent), or Unity's built-in fallback.</summary>
        public static Font FontFrom(Component root)
        {
            var existing = root != null ? root.GetComponentInChildren<Text>(true) : null;
            if (existing != null && existing.font != null) return existing.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
