using System;
using System.Collections.Generic;
using NUnit.Framework;
using PlotWeave.Playback;
using PlotWeave.Legacy;
using PlotWeave.Legacy.Editor;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Playback;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Tests
{
    public sealed class PlotPresentationMigrationTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private PlotPlayerHost Host(string text)
        {
            var go = new GameObject("MigrationTextTest"); _objects.Add(go); var host = go.AddComponent<PlotPlayerHost>(); host.playOnStart = false; host.config.TextSpeed = .1f; host.inlineSprites = TMP_Settings.defaultSpriteAsset;
            var data = new PlotPlaybackDocument(); data.Nodes.Add(new PlotPlaybackNode { ScenesID = 1, Dialog = text }); host.Initialize(data, PlotPlayerRuntimeMode.Editor); host.Play(1); return host;
        }
        [TearDown] public void TearDown() { foreach (var value in _objects) if (value != null) UnityEngine.Object.DestroyImmediate(value); _objects.Clear(); }
        [Test] public void RichTagsDoNotConsumeTypingTimeOrProduceBrokenFragments()
        {
            var host = Host("<color=#FF0000>A<b>B</b></color>"); Assert.That(host.View.VisibleText, Is.Empty); host.Core.Tick(.11f); Assert.That(host.View.VisibleText, Is.EqualTo("A"));
            Assert.That(host.View.DialogText.text, Is.EqualTo("<color=#FF0000>A<b>B</b></color>")); Assert.That(host.View.DialogText.maxVisibleCharacters, Is.EqualTo(1)); host.Core.OnPlayerInput(); Assert.That(host.View.VisibleText, Is.EqualTo("AB"));
        }
        [Test] public void DefaultTmpFontActuallyContainsChineseGlyphs()
        { Assert.That(PlotTMPText.DefaultFont.TryAddCharacters("中文剧情", out string missing), Is.True, "字体缺字: " + missing); }
        [Test] public void InlineSpriteCountsAsOneVisibleElement()
        {
            Assert.That(TMP_Settings.defaultSpriteAsset, Is.Not.Null); var host = Host("A<sprite=0>B"); host.Core.Tick(.11f); Assert.That(host.View.VisibleText, Is.EqualTo("A")); host.Core.Tick(.11f); Assert.That(host.View.VisibleText, Is.EqualTo("A\uFFFC")); host.Core.OnPlayerInput(); Assert.That(host.View.VisibleText, Is.EqualTo("A\uFFFCB"));
        }
        [Test] public void CombiningMarksStayTogetherUnderRichFormatting()
        { var host = Host("<i>e\u0301</i>X"); host.Core.Tick(.11f); Assert.That(host.View.VisibleText, Is.EqualTo("e\u0301")); host.Core.Tick(.11f); Assert.That(host.View.VisibleText, Is.EqualTo("e\u0301X")); }
        [Test] public void SavedPrefabBindingsCanDriveTheSameCore()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlotPresentationMigration.PrefabPath); Assert.That(prefab, Is.Not.Null); prefab.GetComponent<PlotPlayerViewBindings>().Validate();
            var go = new GameObject("PrefabHost"); _objects.Add(go); var host = go.AddComponent<PlotPlayerHost>(); host.playOnStart = false; host.viewPrefab = prefab;
            var data = new PlotPlaybackDocument(); data.Nodes.Add(new PlotPlaybackNode { ScenesID = 1, Dialog = "<b>Prefab</b>" }); host.Initialize(data, PlotPlayerRuntimeMode.Editor); host.Play(1); host.Core.OnPlayerInput(); Assert.That(host.View.VisibleText, Is.EqualTo("Prefab")); Assert.That(host.View.GetComponent<PlotPlayerViewBindings>(), Is.Not.Null);
        }
        [Test] public void LegacyNodeLifecycleMembersAndPortsNowHaveWorkingBehavior()
        {
            var node = ScriptableObject.CreateInstance<PlotLegacyNodeAsset>();
            try
            {
                Assert.That(node.Init<PlotLegacyNodeAsset>("7", Vector2.one), Is.SameAs(node)); node.Define("value", PlotDataType.String); node.SetData("value", "data"); node.SetMember("value", "child", "nested");
                node.Define("next", PlotDataType.OutPortData); node.AddPortData("next", "Atom*8"); node.AddPortData("next", "Atom*8"); Assert.That(node.GetCertain("next", 0), Is.EqualTo("Atom*8")); node.DeletePortData("next", "Atom*8"); Assert.Throws<ArgumentOutOfRangeException>(() => node.GetCertain("next", 0));
                node.NodeMove(new Vector2(2, 3)); Assert.That(node.position, Is.EqualTo(new Vector2(3, 4))); Assert.That(node.GetMember("value", "child"), Is.EqualTo("nested")); Assert.That(node.GetTypeStarID(), Is.EqualTo("Atom*7")); node.NodeDestroy(); Assert.That(node.IsCreated, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
        }
        private enum English { First = 10, Second = 20 }
        private enum Chinese { 乙 = 20, 甲 = 10 }
        [Test] public void StaticEnumCompatibilityMapsByDeclaredValueRatherThanOrder()
        {
            EnumMappingUtility.Register(typeof(English), typeof(Chinese)); Assert.That(EnumMappingUtility.GetChineseValue(English.First), Is.EqualTo(Chinese.甲)); Assert.That(EnumMappingUtility.GetEnglishValue(Chinese.乙, out var result), Is.True); Assert.That(result, Is.EqualTo(English.Second));
            Assert.That(EnumMappingUtility.GetChineseValue<English, Chinese>(English.Second), Is.EqualTo(Chinese.乙)); Assert.That(EnumMappingUtility.GetEnglishValue<English, Chinese>(Chinese.甲), Is.EqualTo(English.First)); Assert.That(EnumMappingUtility.GetChineseEnumValue("甲", out var named), Is.True); Assert.That(named, Is.EqualTo(Chinese.甲)); Assert.Throws<ArgumentException>(() => EnumMappingUtility.GetChineseValue((English)999)); Assert.That(EnumMappingUtility.GetEnglishValue((Chinese)999, out _), Is.False);
        }
        [Test] public void EnumDrawersResolveTheOwnersAssetInsteadOfGlobalFirstMatch()
        {
            var first = ScriptableObject.CreateInstance<PlotEnumAsset>(); var second = ScriptableObject.CreateInstance<PlotEnumAsset>(); var owner = ScriptableObject.CreateInstance<PlotEventTypeAsset>();
            try { owner.parameterEnums = second; Assert.That(PlotLegacyEnumSource.Resolve(new SerializedObject(owner), "parameterEnums"), Is.SameAs(second)); }
            finally { UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
        }
    }
}
