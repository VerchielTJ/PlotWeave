using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Luban;
using PlotWeave.Authoring.Playback;
using PlotWeave.Legacy.Editor;
using PlotWeave.Playback;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Tests
{
    /// <summary>只在本机完整原数据生成后运行；失败信息仅包含节点ID、摘要或异常类型。</summary>
    public sealed class PlotLegacyOriginalContractsTests
    {
        public sealed class Loader<T>
        {
            public string root;
            public T Load(string name) => (T)Activator.CreateInstance(typeof(T), new object[] { File.ReadAllBytes(Path.Combine(root, name + ".bytes")) });
        }
        private sealed class Context
        {
            public PlotLegacyOriginalContractsMigration.Report report;
            public PlotAuthoringProject project;
            public PlotLubanProfile profile;
            public Assembly contracts;
        }
        private static string Hash(byte[] bytes)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static string NodeHash(PlotPlaybackNode node) => Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(node)));
        private static Assembly Assembly(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == name);
        private static Context ReadContext()
        {
            if (!File.Exists(PlotLegacyOriginalContractsMigration.LatestPath)) { Assert.Ignore("尚未生成本地全部原数据C2接口"); return null; }
            var report = JsonUtility.FromJson<PlotLegacyOriginalContractsMigration.Report>(File.ReadAllText(PlotLegacyOriginalContractsMigration.LatestPath));
            if (report == null || report.status != "generated-awaiting-unity-compilation") throw new InvalidOperationException("C2生成报告不可用");
            var current = JsonUtility.FromJson<PlotLegacyCampaignImport.Report>(File.ReadAllText(PlotLegacyCampaignImport.LatestPath));
            if (current.id != report.sourceImportId || current.roundtripWorkspace != report.workspace || current.lubanProfile != report.lubanProfile) throw new InvalidOperationException("原表与C2证据不是同一批次");
            var context = new Context { report = report, project = AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(report.workspace), profile = AssetDatabase.LoadAssetAtPath<PlotLubanProfile>(report.lubanProfile), contracts = Assembly(report.assemblyName) };
            if (context.project == null || context.profile == null || context.profile.project != context.project) throw new InvalidOperationException("原制作数据引用不完整");
            return context;
        }
        private static T Safe<T>(Func<T> action, string stage = "unspecified")
        {
            try { return action(); }
            catch (IgnoreException) { throw; }
            catch (Exception error)
            {
                var cause = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                Assert.Fail("完整原数据接口验证异常；stage=" + stage + "; exceptionType=" + cause.GetType().FullName); return default;
            }
        }
        private static Type Contract(Context context, string name) => context.contracts.GetType(context.report.generatedNamespace + "." + name, true);
        private static object Create(Type type, object value) => Activator.CreateInstance(type, new[] { value });
        private static void ApplyEvents(Context context, PlotPlaybackDocument document)
        {
            if (context.project.eventTracks != null) PlotLubanTableReader.ApplyEventSidecar(document, context.project.eventTracks.ExportJson());
        }
        private static void AssertNodes(PlotPlaybackDocument actual, PlotPlaybackDocument expected)
        {
            Assert.That(actual.Nodes.Count, Is.EqualTo(PlotLegacyOriginalContractsMigration.ExpectedNodes));
            Assert.That(expected.Nodes.Count, Is.EqualTo(PlotLegacyOriginalContractsMigration.ExpectedNodes));
            var expectedHashes = expected.Nodes.ToDictionary(node => node.ScenesID, NodeHash);
            Assert.That(actual.Nodes.Select(node => node.ScenesID).Distinct().Count(), Is.EqualTo(actual.Nodes.Count), "接口输出节点ID不得重复");
            foreach (var node in actual.Nodes)
            {
                Assert.That(expectedHashes.ContainsKey(node.ScenesID), Is.True, "接口输出未知节点ID: " + node.ScenesID);
                Assert.That(NodeHash(node), Is.EqualTo(expectedHashes[node.ScenesID]), "节点JSON UTF8摘要不同，节点ID: " + node.ScenesID);
            }
        }
        private static object LoadTables(Context context)
        {
            var buffer = Assembly(context.profile.runtimeAssembly).GetType("Luban.ByteBuf", true);
            var loader = Activator.CreateInstance(typeof(Loader<>).MakeGenericType(buffer));
            loader.GetType().GetField("root").SetValue(loader, context.profile.generatedRoot + "/Data");
            var callback = Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(typeof(string), buffer), loader, "Load");
            var tablesType = Assembly(context.profile.assemblyName).GetType(context.profile.generatedNamespace + ".Tables", true);
            return Activator.CreateInstance(tablesType, new object[] { callback });
        }
        private static IEnumerable Rows(object tables, string property)
        {
            var table = tables.GetType().GetProperty(property).GetValue(tables);
            return (IEnumerable)table.GetType().GetProperty("DataList").GetValue(table);
        }
        private static string TextHash(string value) => Hash(Encoding.UTF8.GetBytes(value ?? ""));
        private static string Probe(Context context)
        {
            var header = context.project.header; var nodeContract = Contract(context, "IPlotAtomData");
            for (var i = 0; i < header.sheets.Count; i++)
            {
                var column = header.sheets[i];
                // 与生成器FieldFor完全一致：*标记来自旧表头，不属于生成后的C#属性名。
                var property = column.columnName.TrimStart('*');
                if (nodeContract.GetProperty(property) == null)
                    return "stage=header-property; columnIndex=" + i + "; normalizedNameSha256=" + TextHash(property);
            }
            for (var i = 0; i < header.parameterBean.eventTypes.Count; i++)
            {
                var bean = header.parameterBean.eventTypes[i]; var type = Contract(context, bean.eventKey); var contract = Contract(context, "I" + bean.eventKey);
                if (!contract.IsAssignableFrom(type)) return "stage=bean-contract; beanIndex=" + i + "; typeNameSha256=" + TextHash(bean.eventKey);
                for (var j = 0; j < bean.parameters.Count; j++)
                {
                    var property = bean.parameters[j].paramKey.TrimStart('*');
                    if (type.GetProperty(property) == null) return "stage=bean-property; beanIndex=" + i + "; fieldIndex=" + j + "; normalizedNameSha256=" + TextHash(property);
                }
            }
            for (var i = 0; i < header.parameterEnums.dynamicLists.Count; i++)
            {
                var definition = header.parameterEnums.dynamicLists[i]; var type = Contract(context, definition.enumName);
                if (!type.IsEnum) return "stage=enum-type; enumIndex=" + i + "; typeNameSha256=" + TextHash(definition.enumName);
                var actual = Enum.GetNames(type); var expected = definition.values.Select(value => value.key).ToArray();
                if (!actual.SequenceEqual(expected)) return "stage=enum-keys; enumIndex=" + i + "; expectedSha256=" + TextHash(string.Join("\n", expected)) + "; actualSha256=" + TextHash(string.Join("\n", actual));
            }
            for (var i = 0; i < context.report.files.Count; i++)
            {
                var file = context.report.files[i];
                if (!File.Exists(file.path)) return "stage=owned-file-missing; fileIndex=" + i + "; pathSha256=" + TextHash(file.path);
                var actual = Hash(File.ReadAllBytes(file.path));
                if (actual != file.sha256) return "stage=owned-file-sha; fileIndex=" + i + "; expectedSha256=" + file.sha256 + "; actualSha256=" + actual;
            }
            var references = context.contracts.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();
            var businessReferences = references.Where(name => name.StartsWith("PlotWeave.", StringComparison.Ordinal)).ToArray();
            if (!businessReferences.SequenceEqual(new[] { "PlotWeave.Runtime" })) return "stage=runtime-business-reference; count=" + businessReferences.Length + "; namesSha256=" + TextHash(string.Join("\n", businessReferences));
            if (references.Any(name => name.StartsWith("UnityEditor", StringComparison.Ordinal))) return "stage=forbidden-editor-reference; namesSha256=" + TextHash(string.Join("\n", references));
            return null;
        }
        [Test] public void ActualGeneratedEditorAdapterReadsEveryOriginalAuthoringNode()
        {
            var context = Safe(ReadContext);
            var expected = Safe(() => PlotPlaybackCompiler.Compile(context.project));
            var actual = Safe(() =>
            {
                var adapter = Contract(context, "EditorPlotAtomDataAdapter");
                var result = new PlotPlaybackDocument { Actors = expected.Actors, Dialogs = expected.Dialogs, Constants = expected.Constants };
                foreach (var row in new PlotEditorManager(context.project).ListRows()) result.Nodes.Add(PlotPlaybackNode.Read(Create(adapter, row), legacyRoleSlots: true));
                ApplyEvents(context, result); return result;
            });
            AssertNodes(actual, expected);
        }
        [Test] public void ActualGeneratedGameAdapterReadsEveryDecodedOriginalLubanNode()
        {
            var context = Safe(ReadContext);
            var actual = Safe(() =>
            {
                var tables = LoadTables(context); var adapter = Contract(context, "GamePlotAtomDataAdapter");
                var result = PlotLubanTableReader.Read(Rows(tables, "TbPlotAtom"), Rows(tables, "TbActor"), Rows(tables, "TbDialog"), Rows(tables, "TbConstant"), raw => Create(adapter, raw), legacyRoleSlots: true);
                ApplyEvents(context, result); return result;
            });
            AssertNodes(actual, Safe(() => PlotPlaybackCompiler.Compile(context.project)));
        }
        [Test] public void OriginalHeaderBeansEnumsAndTablesCompileInProtectedRuntimeOnlyAssembly()
        {
            var context = Safe(ReadContext);
            Assert.That(context.report.nodes, Is.EqualTo(PlotLegacyOriginalContractsMigration.ExpectedNodes));
            Assert.That(context.report.columns, Is.EqualTo(context.project.header.sheets.Count));
            Assert.That(context.report.beans, Is.EqualTo(context.project.header.parameterBean.eventTypes.Count));
            Assert.That(context.report.enums, Is.EqualTo(context.project.header.parameterEnums.dynamicLists.Count));
            var issue = Safe(() => Probe(context), "schema-files-and-references");
            Assert.That(issue, Is.Null, "真实C2接口Probe失败: " + issue);
            Assert.That(Safe(() => Count(Contract(context, "Editor" + PlotLegacyOriginalContractsMigration.ActorTableName + "TableAdapter"), context.project.actors), "editor-actor-table"), Is.EqualTo(context.report.actors));
            Assert.That(Safe(() => Count(Contract(context, "Editor" + PlotLegacyOriginalContractsMigration.DialogTableName + "TableAdapter"), context.project.dialogs.GetAllDialogData()), "editor-dialog-table"), Is.EqualTo(context.report.dialogKeys));
            Assert.That(Safe(() => Count(Contract(context, "Editor" + PlotLegacyOriginalContractsMigration.ConstantTableName + "TableAdapter"), context.project.constants.GetAllConstantData()), "editor-constant-table"), Is.EqualTo(context.report.constantKeys));
            var counts = Safe(() =>
            {
                var tables = LoadTables(context);
                return new[] {
                    Count(Contract(context, "Game" + PlotLegacyOriginalContractsMigration.ActorTableName + "TableAdapter"), Rows(tables, "TbActor")),
                    Count(Contract(context, "Game" + PlotLegacyOriginalContractsMigration.DialogTableName + "TableAdapter"), Rows(tables, "TbDialog").Cast<object>().Select(PlotLubanTableReader.TextValueSource)),
                    Count(Contract(context, "Game" + PlotLegacyOriginalContractsMigration.ConstantTableName + "TableAdapter"), Rows(tables, "TbConstant").Cast<object>().Select(PlotLubanTableReader.TextValueSource)) };
            }, "game-actor-dialog-constant-tables");
            Assert.That(counts, Is.EqualTo(new[] { context.report.actors, context.report.dialogKeys, context.report.constantKeys }));
        }
        private static int Count(Type tableType, object records)
        {
            var table = Create(tableType, records);
            return ((IEnumerable)tableType.GetMethod("GetAllItems").Invoke(table, null)).Cast<object>().Count();
        }
    }
}
