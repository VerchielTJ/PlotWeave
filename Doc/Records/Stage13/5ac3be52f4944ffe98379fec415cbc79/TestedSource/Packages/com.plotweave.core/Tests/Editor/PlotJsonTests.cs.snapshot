using System;
using System.Collections.Generic;
using NUnit.Framework;
using PlotWeave.Data;

namespace PlotWeave.Tests
{
    public sealed class PlotJsonTests
    {
        [Test] public void UiReferencesKeepTheirFull64BitIdentifiers()
        { var value = PlotJson.Object("{\"fileID\":\"4899582637791516200\",\"number\":4899582637791516200}"); Assert.That(value["fileID"], Is.EqualTo("4899582637791516200")); Assert.That(value["number"], Is.EqualTo(4899582637791516200L)); Assert.That(PlotJson.Object(PlotJson.Write(value))["number"], Is.EqualTo(4899582637791516200L)); }
        [Test] public void EscapedChineseAndNestedComponentPropertiesRoundTrip()
        { var text = "中文\n\"\\\t"; var value = new Dictionary<string, object> { { "label", text }, { "nested", new List<object> { true, null, 1.25 } } }; var result = PlotJson.Object(PlotJson.Write(value)); Assert.That(result["label"], Is.EqualTo(text)); Assert.That(((List<object>)result["nested"])[0], Is.EqualTo(true)); }
        [TestCase("{\"key\":1,\"key\":2}")][TestCase("[1,]")][TestCase("01")][TestCase("1.")][TestCase("{\"x\":true}garbage")][TestCase("\"\\x\"")]
        public void InvalidOrAmbiguousInputIsRejected(string json) => Assert.Throws<FormatException>(() => PlotJson.Parse(json));
        [Test] public void NonFiniteOutputCannotReachUnitySerializedProperties() => Assert.Throws<FormatException>(() => PlotJson.Write(double.NaN));
    }
}
