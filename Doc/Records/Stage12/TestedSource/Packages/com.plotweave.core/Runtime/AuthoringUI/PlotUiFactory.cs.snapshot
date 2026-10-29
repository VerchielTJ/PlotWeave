#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlotWeave.Authoring.UI
{
    public static class PlotUiFactory
    {
        public static readonly Color Surface = new Color(.16f, .19f, .23f);
        public static readonly Color Field = new Color(.22f, .26f, .31f);
        public static readonly Color Foreground = new Color(.94f, .95f, .97f);
        public static readonly Color Accent = new Color(.22f, .42f, .61f);
        private static Font _font;
        public static Font Font
        {
            get
            {
                if (_font == null) _font = UnityEngine.Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 16);
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        public static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
        public static LayoutElement Size(GameObject go, float height = -1, float width = -1, bool flexible = false)
        {
            var layout = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (height >= 0) { layout.minHeight = height; layout.preferredHeight = height; }
            if (width >= 0) { layout.minWidth = width; layout.preferredWidth = width; }
            if (flexible) { layout.flexibleHeight = 1; layout.flexibleWidth = 1; }
            return layout;
        }
        public static RectTransform Stack(Transform parent, string name, bool horizontal = false, bool background = false)
        {
            var rect = Rect(name, parent);
            if (background) rect.gameObject.AddComponent<Image>().color = Surface;
            HorizontalOrVerticalLayoutGroup group = horizontal ? (HorizontalOrVerticalLayoutGroup)rect.gameObject.AddComponent<HorizontalLayoutGroup>() : rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 6; group.padding = new RectOffset(8, 8, 8, 8);
            group.childControlWidth = true; group.childControlHeight = true; group.childForceExpandHeight = false; group.childForceExpandWidth = true;
            return rect;
        }
        public static Text Label(Transform parent, string name, string value, float height = 26, int fontSize = 16)
        {
            var rect = Rect(name, parent); var text = rect.gameObject.AddComponent<Text>();
            text.font = Font; text.fontSize = fontSize; text.color = Foreground; text.supportRichText = false;
            text.text = value; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            Size(rect.gameObject, height); return text;
        }
        private static void Style(GameObject go)
        {
            foreach (var text in go.GetComponentsInChildren<Text>(true)) { text.font = Font; text.fontSize = 16; text.color = Foreground; text.supportRichText = false; }
            var image = go.GetComponent<Image>(); if (image != null) image.color = Field;
        }
        public static Button Button(Transform parent, string name, string caption, Action action, float width = -1)
        {
            var go = DefaultControls.CreateButton(new DefaultControls.Resources()); go.name = name; go.transform.SetParent(parent, false); Style(go);
            go.GetComponentInChildren<Text>().text = caption; Size(go, 34, width);
            var button = go.GetComponent<Button>(); if (action != null) button.onClick.AddListener(() => action()); return button;
        }
        public static InputField Input(Transform parent, string name, string value, Action<string> changed, bool multiline = false, string placeholderText = "")
        {
            var go = DefaultControls.CreateInputField(new DefaultControls.Resources()); go.name = name; go.transform.SetParent(parent, false); Style(go);
            var input = go.GetComponent<InputField>(); input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.textComponent.alignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            if (input.placeholder is Text placeholder) { placeholder.text = placeholderText; placeholder.color = new Color(.65f, .69f, .75f); placeholder.raycastTarget = false; }
            input.SetTextWithoutNotify(value ?? ""); Size(go, multiline ? 150 : 34);
            if (changed != null) input.onEndEdit.AddListener(text => changed(text)); return input;
        }
        public static Dropdown Dropdown(Transform parent, string name, List<string> options, int selected, Action<int> changed)
        {
            var go = DefaultControls.CreateDropdown(new DefaultControls.Resources()); go.name = name; go.transform.SetParent(parent, false); Style(go);
            var dropdown = go.GetComponent<Dropdown>(); dropdown.ClearOptions(); dropdown.AddOptions(options); dropdown.SetValueWithoutNotify(selected); Size(go, 34);
            var arrow = go.transform.Find("Arrow");
            if (arrow != null)
            {
                var image = arrow.GetComponent<Image>(); if (image != null) image.enabled = false;
                var glyphRect = Rect("ArrowGlyph", arrow); Stretch(glyphRect);
                var glyph = glyphRect.gameObject.AddComponent<Text>(); glyph.font = Font; glyph.fontSize = 14; glyph.text = "▼"; glyph.color = Foreground; glyph.alignment = TextAnchor.MiddleCenter; glyph.raycastTarget = false;
            }
            foreach (var image in go.GetComponentsInChildren<Image>(true)) if (image.gameObject.name == "Item Background") image.color = Surface;
            if (changed != null) dropdown.onValueChanged.AddListener(index => changed(index)); return dropdown;
        }
        public static ScrollRect Scroll(Transform parent, string name)
        {
            var go = DefaultControls.CreateScrollView(new DefaultControls.Resources()); go.name = name; go.transform.SetParent(parent, false);
            var scroll = go.GetComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            if (scroll.horizontalScrollbar != null) { scroll.horizontalScrollbar.gameObject.SetActive(false); scroll.horizontalScrollbar = null; }
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            if (scroll.verticalScrollbar != null)
            {
                var bar = scroll.verticalScrollbar.GetComponent<Image>(); if (bar != null) bar.color = Field;
                var handle = scroll.verticalScrollbar.handleRect.GetComponent<Image>(); if (handle != null) handle.color = new Color(.47f, .53f, .61f);
            }
            go.GetComponent<Image>().color = Surface; Size(go, flexible: true);
            var viewport = scroll.viewport; viewport.offsetMin = Vector2.zero; viewport.offsetMax = new Vector2(-14, 0);
            scroll.content.anchorMin = new Vector2(0, 1); scroll.content.anchorMax = Vector2.one; scroll.content.pivot = new Vector2(.5f, 1); scroll.content.anchoredPosition = Vector2.zero;
            scroll.content.sizeDelta = new Vector2(0, 0); return scroll;
        }
        public static RectTransform ScrollStack(ScrollRect scroll)
        {
            var content = scroll.content;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 6; layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; return content;
        }
        public static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject; child.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(child); else UnityEngine.Object.DestroyImmediate(child);
            }
        }
    }
}
#endif
