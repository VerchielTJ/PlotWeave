using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PlotWeave.Authoring;
using PlotWeave.Authoring.Luban;
using PlotWeave.Authoring.Playback;
using PlotWeave.Generation;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Legacy.Editor
{
    /// <summary>对全部原制作数据生成独立C2接口，源码及asmdef统一由生成器的SHA清单保护。</summary>
    public static class PlotLegacyOriginalContractsMigration
    {
        public const string LatestPath = "Tools/PlotWeaveMigration/original-contracts.json";
        public const int ExpectedNodes = 485;
        public const string ActorTableName = "PlotThespianData", DialogTableName = "PlotDialogKey", ConstantTableName = "PlotConstantKey";
        private const string CampaignRoot = "Assets/PlotWeaveLocal/Campaign/";
        [Serializable] public sealed class FileStamp { public string path, sha256; }
        [Serializable] public sealed class Report
        {
            public string id, sourceImportId, status, error, root, workspace, generatedRoot, generatedNamespace, assemblyName, lubanProfile;
            public int nodes, beans, enums, columns, actors, dialogKeys, constantKeys;
            public List<FileStamp> files = new List<FileStamp>();
        }
        [Serializable] private sealed class AssemblyDefinition
        {
            public string name;
            public string[] references = { "PlotWeave.Runtime" };
            public bool autoReferenced = true;
        }
        private static string ProjectRoot => Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        private static string Hash(byte[] data)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
        private static string LocalPath(string relative)
        {
            if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || relative.Contains('\\') || relative.Split('/').Any(p => p == "" || p == "." || p == ".."))
                throw new ArgumentException("要求当前项目内的标准相对路径");
            var full = Path.GetFullPath(Path.Combine(ProjectRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(ProjectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("路径越出当前项目");
            // 不沿符号链接或目录连接写入生成文件与报告。
            for (var current = full; !string.Equals(current, ProjectRoot, StringComparison.OrdinalIgnoreCase); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("生成路径包含重解析点");
            return full;
        }
        private static void ValidateCampaignRoot(string root)
        {
            if (string.IsNullOrEmpty(root) || !root.StartsWith(CampaignRoot, StringComparison.Ordinal)) throw new IOException("原表报告根目录不属于Campaign");
            var leaf = root.Substring(CampaignRoot.Length);
            if (!leaf.StartsWith("Import_", StringComparison.Ordinal) || leaf.Length <= 7 || leaf.Contains('/') || leaf.Contains('\\') || leaf.TrimEnd('.', ' ') != leaf || leaf.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new IOException("只允许Campaign下的直接Import_*目录");
            if (!Directory.Exists(LocalPath(root)) || !AssetDatabase.IsValidFolder(root)) throw new IOException("原表导入目录不存在");
        }
        [MenuItem("Tools/PlotWeave/迁移工具/生成全部原数据C2接口证据")]
        public static void PrepareVerification()
        {
            var report = new Report { id = Guid.NewGuid().ToString("N"), status = "running" };
            try
            {
                var source = JsonUtility.FromJson<PlotLegacyCampaignImport.Report>(File.ReadAllText(LocalPath(PlotLegacyCampaignImport.LatestPath)));
                if (source == null || string.IsNullOrEmpty(source.id) || source.nodes != ExpectedNodes) throw new InvalidOperationException("原表导入报告或485节点基线无效");
                ValidateCampaignRoot(source.root);
                if (string.IsNullOrEmpty(source.roundtripWorkspace) || !source.roundtripWorkspace.StartsWith("Assets/PlotWeaveLocal/ImportedOriginal/Roundtrip_", StringComparison.Ordinal))
                    throw new IOException("制作工作区不属于原资产往返验证目录");
                LocalPath(source.roundtripWorkspace);
                if (source.lubanProfile != source.root + "/LubanProfile.asset") throw new IOException("Luban配置不属于当前原表导入根目录");
                LocalPath(source.lubanProfile);
                var project = AssetDatabase.LoadAssetAtPath<PlotAuthoringProject>(source.roundtripWorkspace);
                var profile = AssetDatabase.LoadAssetAtPath<PlotLubanProfile>(source.lubanProfile);
                if (project == null || project.header == null || project.actors == null || project.dialogs == null || project.constants == null || profile == null || profile.project != project)
                    throw new InvalidOperationException("原表制作工作区、表头或Luban配置引用不完整");
                var rows = new PlotEditorManager(project).ListRows();
                if (rows.Count != ExpectedNodes || PlotPlaybackCompiler.Compile(project).Nodes.Count != ExpectedNodes) throw new InvalidOperationException("原表制作与编译节点数量不符");
                report.sourceImportId = source.id; report.root = source.root; report.workspace = source.roundtripWorkspace; report.lubanProfile = source.lubanProfile;
                report.generatedRoot = source.root + "/C2Contracts";
                report.generatedNamespace = "PlotWeave.Generated.Original.Run" + report.id;
                report.assemblyName = "PlotWeave.C2.Original.Run" + report.id;
                report.nodes = rows.Count; report.columns = project.header.sheets.Count;
                report.beans = project.header.parameterBean?.eventTypes?.Count ?? 0; report.enums = project.header.parameterEnums?.dynamicLists?.Count ?? 0;
                report.actors = project.actors.rows.Count; report.dialogKeys = project.dialogs.GetAllDialogData().Count; report.constantKeys = project.constants.GetAllConstantData().Count;
                var bundle = PlotDataStructureGenerator.GenerateAllDataStructures(project.header,
                    new[] { new PlotTableGeneration { Asset = project.actors, Name = ActorTableName, IsThespianTable = true } },
                    new[] { new PlotKeyValueGeneration { Name = DialogTableName }, new PlotKeyValueGeneration { Name = ConstantTableName } }, report.generatedNamespace);
                // 固定文件名避免重复生成遗留多个asmdef；动态程序集名避免与其他样例冲突。
                bundle.Files.Add("PlotWeave.Original.Contracts.asmdef", JsonUtility.ToJson(new AssemblyDefinition { name = report.assemblyName }, true));
                var folder = LocalPath(report.generatedRoot);
                foreach (var file in bundle.Files.Keys) LocalPath(report.generatedRoot + "/" + file);
                LocalPath(report.generatedRoot + "/plotweave-generation.json");
                if (Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.asmdef", SearchOption.AllDirectories).Any(path => Path.GetFullPath(path) != Path.Combine(folder, "PlotWeave.Original.Contracts.asmdef")))
                    throw new IOException("C2生成目录已有其他程序集定义，拒绝制造嵌套或重复程序集");
                bundle.Write(folder);
                foreach (var file in bundle.Files.Keys.Concat(new[] { "plotweave-generation.json" }).OrderBy(path => path, StringComparer.Ordinal))
                    report.files.Add(new FileStamp { path = report.generatedRoot + "/" + file, sha256 = Hash(File.ReadAllBytes(Path.Combine(folder, file))) });
                report.status = "generated-awaiting-unity-compilation";
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            }
            catch (Exception error) { report.status = "failed"; report.error = error.GetType().FullName; throw; }
            finally
            {
                var records = LocalPath("Doc/Records/Stage17"); Directory.CreateDirectory(records);
                var latest = LocalPath(LatestPath); Directory.CreateDirectory(Path.GetDirectoryName(latest));
                var json = JsonUtility.ToJson(report, true);
                File.WriteAllText(Path.Combine(records, "original-contracts-" + report.id + ".json"), json, new UTF8Encoding(false));
                File.WriteAllText(latest, json, new UTF8Encoding(false));
            }
        }
    }
}
