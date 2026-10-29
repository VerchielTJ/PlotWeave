using System;
using System.Collections.Generic;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Legacy;
using ValueType = PlotWeave.Authoring.PlotEventTypeAsset.ValueType;

namespace PlotWeave.Tests
{
    public sealed class PlotLegacyEventTests
    {
        [Test] public void ClonePreservesIdentityMetadataAndDeepCopiesParameters()
        {
            var original = new PlotEventJsonData("demo", 3, "example") { callbackList = "1,2" }; original.AddParameters("a", "b"); var clone = original.Clone(); clone.SetParameter(0, "changed");
            Assert.That(clone.eventId, Is.EqualTo(original.eventId)); Assert.That(clone.createdAt, Is.EqualTo(original.createdAt)); Assert.That(clone.callbackList, Is.EqualTo("1,2")); Assert.That(original.GetParameter(0), Is.EqualTo("a"));
        }
        [Test] public void BeanDefaultsAndCallbackFieldsSurviveIndependentJsonRoundTrip()
        {
            var definition = new PlotEventTypeAsset.EventTypeDefinition { eventKey = "demo", defaultCallbackValue = 2, defaultCallbackList = "3,4", parameters = new List<PlotEventTypeAsset.EventParameter> {
                new PlotEventTypeAsset.EventParameter { valueType = ValueType.Int }, new PlotEventTypeAsset.EventParameter { valueType = ValueType.Float },
                new PlotEventTypeAsset.EventParameter { valueType = ValueType.Bool }, new PlotEventTypeAsset.EventParameter { valueType = ValueType.Vector3 }, new PlotEventTypeAsset.EventParameter { valueType = ValueType.CustomEnum, enumValue = "Ready" } } };
            var value = new PlotEventJsonData(); value.InitFromEventType(definition); var container = new PlotEventsJsonContainer(); container.AddEvent(value);
            var result = PlotEventsJsonContainer.FromJson(container.ToJson()).FindEventById(value.eventId);
            CollectionAssert.AreEqual(new[] { "0", "0.0", "false", "0,0,0", "Ready" }, result.parameters); Assert.That(result.executionOrder, Is.EqualTo(2)); CollectionAssert.AreEqual(new[] { 3, 4 }, result.GetCallbackIds());
        }
        [Test] public void CallbackParserRetainsLegacyToleranceButValidationReportsBadTokens()
        {
            var data = new PlotEventJsonData("demo") { callbackList = "1, bad, 2" }; CollectionAssert.AreEqual(new[] { 1, 2 }, data.GetCallbackIds());
            var container = new PlotEventsJsonContainer(); container.AddEvent(data); Assert.That(container.Validate().IsValid, Is.False); Assert.Throws<FormatException>(() => container.ToJson());
        }
        [Test] public void ContainerRejectsDuplicateIdentityAndSortsStableExecutionOrder()
        {
            var one = new PlotEventJsonData("demo", 2); var two = new PlotEventJsonData("demo", -1); var container = new PlotEventsJsonContainer(); container.AddEvent(one); container.AddEvent(two); container.SortByExecutionOrder();
            Assert.That(container.events[0], Is.SameAs(two)); Assert.That(container.FindEventsByName("demo").Count, Is.EqualTo(2)); container.AddEvent(one.Clone()); Assert.That(container.Validate().IsValid, Is.False);
        }
        [Test] public void InvalidParameterIndexAndNestedBeanDoNotPartiallyModifyData()
        {
            var data = new PlotEventJsonData("original"); data.AddParameter("preserved"); Assert.Throws<ArgumentOutOfRangeException>(() => data.SetParameter(-1, "bad"));
            var definition = new PlotEventTypeAsset.EventTypeDefinition { eventKey = "changed", parameters = new List<PlotEventTypeAsset.EventParameter> { new PlotEventTypeAsset.EventParameter { valueType = ValueType.CustomBean } } };
            Assert.Throws<NotSupportedException>(() => data.InitFromEventType(definition)); Assert.That(data.eventName, Is.EqualTo("original")); Assert.That(data.GetParameter(0), Is.EqualTo("preserved"));
        }
    }
}
