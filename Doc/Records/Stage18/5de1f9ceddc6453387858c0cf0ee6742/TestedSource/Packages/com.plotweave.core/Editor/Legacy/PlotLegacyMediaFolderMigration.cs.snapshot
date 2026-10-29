using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Legacy.Editor
{
    /// <summary>仅移动导入报告证明由工具创建的媒体目录；不删除、不覆盖，也不触碰未知Resources目录。</summary>
    public static class PlotLegacyMediaFolderMigration
    {
        public const string LatestPath = "Tools/PlotWeaveMigration/media-folders.json";
        [Serializable] public sealed class GuidCheck { public string relative, before, after; }
        [Serializable] public sealed class Move
        {
            public string root, source, destination, status, error;
            public List<string> sourceReports = new List<string>();
            public List<GuidCheck> guids = new List<GuidCheck>();
        }
        [Serializable] public sealed class ResourceFolder
        { public string path, classification; public bool registered; public List<string> assets = new List<string>(); }
        [Serializable] public sealed class DuplicateKey { public string key; public List<string> assets = new List<string>(); }
        [Serializable] public sealed class Report
        {
            public string id, status, error, localRoot, startedUtc, completedUtc;
            public int reportFiles, moved, alreadyMedia, absent, verifiedGuids;
            public List<Move> moves = new List<Move>();
            public List<string> rejectedReports = new List<string>();
            public List<string> skippedScanPaths = new List<string>();
            public List<ResourceFolder> resourcesBefore = new List<ResourceFolder>(), resourcesAfter = new List<ResourceFolder>();
            public List<DuplicateKey> duplicateKeysBefore = new List<DuplicateKey>(), duplicateKeysAfter = new List<DuplicateKey>();
        }
        private static string ProjectRoot => Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        private static string AbsoluteLocalRoot => Path.GetFullPath(Path.Combine(ProjectRoot, PlotLegacyUiMigration.LocalRoot.Replace('/', Path.DirectorySeparatorChar))).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        private static string Absolute(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || assetPath.Contains('\\') || Path.IsPathRooted(assetPath) || assetPath.Split('/').Any(p => p == "" || p == "." || p == "..")) throw new ArgumentException("媒体路径必须是标准项目相对路径");
            var full = Path.GetFullPath(Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(AbsoluteLocalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("媒体路径越出指定LocalRoot");
            return full;
        }
        private static string AssetPath(string absolute)
        {
            var full = Path.GetFullPath(absolute);
            if (!full.StartsWith(AbsoluteLocalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("扫描路径越出LocalRoot");
            return full.Substring(ProjectRoot.Length + 1).Replace('\\', '/');
        }
        private static string ImportRoot(string value)
        {
            var prefix = PlotLegacyUiMigration.LocalRoot + "/";
            if (string.IsNullOrEmpty(value) || !value.StartsWith(prefix, StringComparison.Ordinal)) throw new ArgumentException("报告根目录不属于OriginalUI");
            var leaf = value.Substring(prefix.Length);
            if (!leaf.StartsWith("Import_", StringComparison.Ordinal) || leaf.Length <= "Import_".Length || leaf.Contains('/') || leaf.Contains('\\') || leaf.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || leaf.TrimEnd('.', ' ') != leaf)
                throw new ArgumentException("报告只允许LocalRoot的直接Import_*子目录");
            Absolute(value); return value;
        }
        private static bool Reparse(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        private static void CheckAncestors(string full)
        {
            var current = new DirectoryInfo(Path.GetFullPath(full));
            while (current != null)
            {
                if (current.Exists && Reparse(current.FullName)) throw new IOException("不沿重解析点移动媒体目录: " + current.FullName);
                if (string.Equals(current.FullName.TrimEnd(Path.DirectorySeparatorChar), ProjectRoot, StringComparison.OrdinalIgnoreCase)) return;
                current = current.Parent;
            }
            throw new IOException("媒体目录不在当前项目下");
        }
        private static IEnumerable<string> Directories(string start, Report report, bool strict)
        {
            var stack = new Stack<string>(); stack.Push(start);
            while (stack.Count > 0)
            {
                var directory = stack.Pop();
                if (Reparse(directory))
                { if (strict) throw new IOException("媒体目录包含重解析点: " + directory); report.skippedScanPaths.Add(directory); continue; }
                yield return directory;
                string[] children;
                try { children = Directory.GetDirectories(directory); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                { if (strict) throw; report.skippedScanPaths.Add(directory + ": " + error.Message); continue; }
                foreach (var child in children.OrderByDescending(p => p, StringComparer.Ordinal))
                { if (!Path.GetFullPath(child).StartsWith(AbsoluteLocalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("扫描路径越界"); stack.Push(child); }
            }
        }
        private static IEnumerable<string> Files(string directory, Report report, bool strict)
        {
            foreach (var folder in Directories(directory, report, strict))
            {
                string[] files;
                try { files = Directory.GetFiles(folder); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                { if (strict) throw; report.skippedScanPaths.Add(folder + ": " + error.Message); continue; }
                foreach (var file in files.OrderBy(p => p, StringComparer.Ordinal))
                {
                    if (Reparse(file)) { if (strict) throw new IOException("媒体文件是重解析点: " + file); report.skippedScanPaths.Add(file); continue; }
                    if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) yield return file;
                }
            }
        }
        private static List<ResourceFolder> Scan(Report report, HashSet<string> knownRoots)
        {
            var found = new List<ResourceFolder>(); if (!Directory.Exists(AbsoluteLocalRoot)) return found;
            CheckAncestors(AbsoluteLocalRoot);
            foreach (var directory in Directories(AbsoluteLocalRoot, report, false).Where(p => string.Equals(Path.GetFileName(p), "Resources", StringComparison.OrdinalIgnoreCase)))
            {
                var path = AssetPath(directory); var owner = knownRoots.FirstOrDefault(root => path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
                var item = new ResourceFolder { path = path, classification = owner == null ? "unknown-user-directory-not-moved" : path.Equals(owner + "/Resources", StringComparison.OrdinalIgnoreCase) ? "tool-root-resources" : "nested-resources-not-renamed", registered = AssetDatabase.IsValidFolder(path) };
                foreach (var file in Files(directory, report, false)) { var asset = AssetPath(file); if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(asset))) item.assets.Add(asset); }
                found.Add(item);
            }
            return found.OrderBy(f => f.path, StringComparer.Ordinal).ToList();
        }
        private static List<DuplicateKey> Duplicates(List<ResourceFolder> folders)
        {
            // 嵌套目录中的资产按最内层Resources归组，避免同一资产在两层扫描中重复计数。
            var entries = folders.SelectMany(folder => folder.assets.Select(asset => new { asset, folder = folder.path })).GroupBy(item => item.asset, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(item => item.folder.Length).First())
                .Select(item => new { item.asset, key = Path.ChangeExtension(item.asset.Substring(item.folder.Length + 1), null).Replace('\\', '/') });
            return entries.GroupBy(item => item.key, StringComparer.OrdinalIgnoreCase).Where(g => g.Select(item => item.asset).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                .Select(g => new DuplicateKey { key = g.Key, assets = g.Select(item => item.asset).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.Ordinal).ToList() }).OrderBy(item => item.key, StringComparer.Ordinal).ToList();
        }
        private static void Stamp(Move move, Report report)
        {
            var source = Absolute(move.source); var paths = Directories(source, report, true).Concat(Files(source, report, true)).Select(AssetPath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.Ordinal);
            foreach (var path in paths)
            { var guid = AssetDatabase.AssetPathToGUID(path); if (!string.IsNullOrEmpty(guid)) move.guids.Add(new GuidCheck { relative = path == move.source ? "" : path.Substring(move.source.Length + 1), before = guid }); }
            if (!move.guids.Any(g => g.relative == "" && !string.IsNullOrEmpty(g.before))) throw new IOException("媒体文件夹没有有效GUID: " + move.source);
        }
        private static void Save(Report report)
        {
            var text = JsonUtility.ToJson(report, true); Directory.CreateDirectory("Doc/Records/Stage17"); File.WriteAllText("Doc/Records/Stage17/media-folders-" + report.id + ".json", text);
            Directory.CreateDirectory(Path.GetDirectoryName(LatestPath)); File.WriteAllText(LatestPath, text);
        }
        [MenuItem("Tools/PlotWeave/迁移工具/迁出历史Resources媒体目录")]
        public static void PrepareVerification()
        {
            var report = new Report { id = Guid.NewGuid().ToString("N"), status = "running", localRoot = PlotLegacyUiMigration.LocalRoot, startedUtc = DateTime.UtcNow.ToString("O") };
            var roots = new Dictionary<string, Move>(StringComparer.OrdinalIgnoreCase); Exception failure = null;
            try
            {
                var records = "Doc/Records/Stage13";
                foreach (var path in Directory.Exists(records) ? Directory.GetFiles(records, "ui-*.json").OrderBy(p => p, StringComparer.Ordinal).ToArray() : Array.Empty<string>())
                {
                    report.reportFiles++;
                    try
                    {
                        var ui = JsonUtility.FromJson<PlotLegacyUiReport>(File.ReadAllText(path)); if (ui == null || !Guid.TryParseExact(ui.id, "N", out _)) throw new FormatException("UI报告缺少有效ID");
                        var root = ImportRoot(ui.root);
                        if (!roots.TryGetValue(root, out var move)) { move = new Move { root = root, source = root + "/Resources", destination = root + "/Media", status = "planned" }; roots.Add(root, move); report.moves.Add(move); }
                        move.sourceReports.Add(path.Replace('\\', '/'));
                    }
                    catch (Exception error) { report.rejectedReports.Add(path + ": " + error.Message); }
                }
                var known = new HashSet<string>(roots.Keys, StringComparer.OrdinalIgnoreCase); report.resourcesBefore = Scan(report, known); report.duplicateKeysBefore = Duplicates(report.resourcesBefore);
                foreach (var move in report.moves)
                {
                    try
                    {
                        var root = Absolute(move.root); var source = Absolute(move.source); var destination = Absolute(move.destination); CheckAncestors(root);
                        if (!Directory.Exists(source))
                        { if (File.Exists(source)) throw new IOException("Resources路径不是目录"); move.status = Directory.Exists(destination) ? "already-media" : "absent"; if (move.status == "already-media") report.alreadyMedia++; else report.absent++; continue; }
                        if (!AssetDatabase.IsValidFolder(move.root) || !AssetDatabase.IsValidFolder(move.source)) throw new IOException("报告根或Resources不是有效Unity目录");
                        CheckAncestors(source);
                        if (Directory.Exists(destination) || File.Exists(destination) || File.Exists(destination + ".meta")) throw new IOException("Media目标已存在，不覆盖也不合并");
                        Stamp(move, report);
                    }
                    catch (Exception error) { move.status = "refused"; move.error = error.ToString(); }
                }
                if (report.moves.Any(move => move.status == "refused")) throw new IOException("媒体目录预检存在冲突或无效路径，本次未开始移动；详见迁移报告");
                foreach (var move in report.moves.Where(move => move.status == "planned"))
                {
                    ImportRoot(move.root); CheckAncestors(Absolute(move.source)); var destination = Absolute(move.destination);
                    if (!AssetDatabase.IsValidFolder(move.source) || Directory.Exists(destination) || File.Exists(destination) || File.Exists(destination + ".meta"))
                    { move.status = "refused-late-conflict"; move.error = "预检后Source失效或Media目标出现冲突"; throw new IOException(move.error + ": " + move.root); }
                    var current = new Move { source = move.source }; Stamp(current, report);
                    if (!move.guids.Select(g => g.relative + ":" + g.before).SequenceEqual(current.guids.Select(g => g.relative + ":" + g.before), StringComparer.Ordinal))
                    { move.status = "refused-source-changed"; move.error = "预检后媒体资产路径或GUID发生变化"; throw new IOException(move.error + ": " + move.root); }
                    var error = AssetDatabase.MoveAsset(move.source, move.destination);
                    if (!string.IsNullOrEmpty(error)) { move.status = "failed"; move.error = error; throw new IOException("移动媒体目录失败: " + move.source + ": " + error); }
                    move.status = "moved"; report.moved++;
                }
            }
            catch (Exception error) { failure = error; report.status = "failed"; report.error = error.ToString(); }
            finally
            {
                try
                {
                    AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
                catch (Exception error) { if (failure == null) failure = error; }
                try
                {
                    foreach (var move in report.moves.Where(move => move.status == "moved"))
                        foreach (var check in move.guids)
                        {
                            check.after = AssetDatabase.AssetPathToGUID(move.destination + (check.relative == "" ? "" : "/" + check.relative));
                            if (check.after != check.before) { move.status = "moved-guid-verification-failed"; move.error = "媒体移动后GUID不一致: " + move.destination + "/" + check.relative; if (failure == null) failure = new IOException(move.error); }
                            else report.verifiedGuids++;
                        }
                }
                catch (Exception error) { if (failure == null) failure = error; }
                try
                {
                    report.resourcesAfter = Scan(report, new HashSet<string>(roots.Keys, StringComparer.OrdinalIgnoreCase)); report.duplicateKeysAfter = Duplicates(report.resourcesAfter);
                }
                catch (Exception error) { if (failure == null) failure = error; report.skippedScanPaths.Add("最终Resources扫描未完成: " + error.Message); }
                if (failure != null) { report.status = "failed"; report.error = failure.ToString(); }
                else report.status = report.duplicateKeysAfter.Count > 0 ? "moved-with-duplicate-resource-keys" : report.resourcesAfter.Count > 0 ? "passed-with-residual-resources" : report.skippedScanPaths.Count > 0 ? "passed-with-incomplete-scan" : report.rejectedReports.Count > 0 ? "passed-with-rejected-reports" : "passed";
                report.completedUtc = DateTime.UtcNow.ToString("O"); Save(report);
            }
            if (failure != null) throw new InvalidOperationException("历史媒体目录迁移未完成；报告: Doc/Records/Stage17/media-folders-" + report.id + ".json", failure);
            Debug.Log("PLOTWEAVE_MEDIA_FOLDERS " + report.status + "; moved=" + report.moved + "; residualResources=" + report.resourcesAfter.Count + "; duplicateKeys=" + report.duplicateKeysAfter.Count + "; skippedScan=" + report.skippedScanPaths.Count + "; rejectedReports=" + report.rejectedReports.Count);
        }
    }
}
