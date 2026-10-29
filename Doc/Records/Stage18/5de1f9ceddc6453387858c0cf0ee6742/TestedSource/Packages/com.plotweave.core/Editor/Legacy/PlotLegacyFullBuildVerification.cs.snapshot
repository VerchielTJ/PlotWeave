using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using PlotWeave.Authoring.Luban;
using PlotWeave.Authoring.Playback;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PlotWeave.Legacy.Editor
{
    /// <summary>本地全迁移场景构建验收。构建产物使用唯一目录；Android临时设置在域重载后仍可恢复。</summary>
    [InitializeOnLoad] public static class PlotLegacyFullBuildVerification
    {
        public const string LatestPath = "Tools/PlotWeaveMigration/full-build.json";
        public const string MobileSessionKey = "PlotWeave.Stage17.MobileBuild";
        private const string RecoveryPath = "Tools/PlotWeaveMigration/mobile-build-recovery.json";
        [Serializable] public sealed class Toolchain
        { public bool targetSupported, sdkReady, ndkReady, jdkReady; public string sdk, ndk, jdk; public string[] platforms = Array.Empty<string>(); }
        [Serializable] public sealed class Source { public string path, sha256; }
        [Serializable] public sealed class Report
        {
            public string id, target, status, error, output, unityVersion, startedUtc, completedUtc, originalTarget;
            public bool settingsRestored, targetRestored;
            public int errors, warnings;
            public ulong sizeBytes;
            public Toolchain toolchain;
            public List<Source> scenes = new List<Source>();
            public List<Source> sources = new List<Source>();
            public List<string> diagnostics = new List<string>();
        }
        [Serializable] private sealed class MobileState
        { public string id, phase; public int originalTarget, originalBackend, originalArchitectures; public bool changedSettings; public double deadline; public Report report; }
        private static bool _busy;
        static PlotLegacyFullBuildVerification() { EditorApplication.update += PollMobile; }
        private static string Hash(string path)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
        private static void Save(Report report)
        { Directory.CreateDirectory("Doc/Records/Stage17"); var text = JsonUtility.ToJson(report, true); File.WriteAllText("Doc/Records/Stage17/build-" + report.id + ".json", text); Directory.CreateDirectory(Path.GetDirectoryName(LatestPath)); File.WriteAllText(LatestPath, text); }
        private static void SaveMobile(MobileState state)
        { var text = JsonUtility.ToJson(state, true); SessionState.SetString(MobileSessionKey, text); Directory.CreateDirectory(Path.GetDirectoryName(RecoveryPath)); File.WriteAllText(RecoveryPath, text); Save(state.report); }
        private static MobileState ReadMobile()
        { var text = SessionState.GetString(MobileSessionKey, ""); return text == "" ? null : JsonUtility.FromJson<MobileState>(text); }
        private static string[] SourcePaths()
        {
            // 除本包外也覆盖Assets中的接入脚本；路径集合本身参与比较，新增源码不能绕过已有文件的哈希检查。
            return new[] { "Packages/com.plotweave.core", "Assets" }.Where(Directory.Exists)
                .SelectMany(folder => Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetFileName(path), "link.xml", StringComparison.OrdinalIgnoreCase))
                .Select(path => path.Replace('\\', '/')).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }
        private static string[] Scenes()
        {
            var interaction = JsonUtility.FromJson<PlotLegacyInteractionMigration.Report>(File.ReadAllText(PlotLegacyInteractionMigration.LatestPath));
            var campaign = JsonUtility.FromJson<PlotLegacyCampaignMigration.Report>(File.ReadAllText(PlotLegacyCampaignMigration.LatestPath));
            var luban = JsonUtility.FromJson<PlotLegacyLubanPlaybackMigration.Report>(File.ReadAllText(PlotLegacyLubanPlaybackMigration.LatestPath));
            if (luban.status != "scene-ready-runtime-verification-pending") throw new InvalidOperationException("完整原数据Luban场景尚未准备好");
            var scenes = new[] { PlotCampaignReferenceExample.ScenePath,campaign.scene, luban.scene, interaction.playbackScene, PlotChapterExample.ScenePath, PlotAdvancedExample.ScenePath, PlotLubanExample.ScenePath, PlotPlaybackExample.GameScene, PlotPlaybackExample.JsonScene }.Distinct(StringComparer.Ordinal).ToArray();
            foreach (var scene in scenes)
                if (string.IsNullOrEmpty(scene) || !scene.StartsWith("Assets/", StringComparison.Ordinal) || scene.Split('/').Contains("..") || !File.Exists(scene) || !scene.EndsWith(".unity", StringComparison.Ordinal) || scene.Contains("OriginalAuthoring")) throw new IOException("运行时构建场景无效或尚未生成: " + scene);
            return scenes;
        }
        private static Report NewReport(BuildTarget target)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("编辑器忙于编译、刷新或运行，不能启动构建");
            if (ReadMobile() != null) throw new InvalidOperationException("已有移动构建或恢复任务");
            if (File.Exists(RecoveryPath))
            { var previous = JsonUtility.FromJson<MobileState>(File.ReadAllText(RecoveryPath)); if (previous == null || previous.phase != "finished") throw new InvalidOperationException("上次移动验证尚未恢复，先执行恢复入口，避免覆盖恢复档案"); }
            var report = new Report { id = Guid.NewGuid().ToString("N"), target = target.ToString(), status = "running", unityVersion = Application.unityVersion, startedUtc = DateTime.UtcNow.ToString("O"), originalTarget = EditorUserBuildSettings.activeBuildTarget.ToString() };
            foreach (var scene in Scenes()) report.scenes.Add(new Source { path = scene, sha256 = Hash(scene) });
            foreach (var file in SourcePaths()) report.sources.Add(new Source { path = file, sha256 = Hash(file) });
            report.output = "Builds/PlotWeaveFull/" + report.id + "/" + (target == BuildTarget.Android ? "PlotWeave.apk" : target == BuildTarget.iOS ? "Xcode" : "PlotWeave.exe"); return report;
        }
        private static void Build(Report report, BuildTarget target)
        {
            if (target == BuildTarget.Android)
            {
                if (EditorUserBuildSettings.buildAppBundle) throw new InvalidOperationException("Android验收只构建APK；等待平台切换期间AAB模式已启用，未改动此设置");
                if (EditorUserBuildSettings.exportAsGoogleAndroidProject) throw new InvalidOperationException("Android验收只构建APK；等待平台切换期间启用了Export Project，将导出Gradle工程，已停止构建且未改动此设置");
                if (PlayerSettings.Android.useCustomKeystore) throw new InvalidOperationException("等待平台切换期间启用了自定义签名；停止构建，不读取或修改签名凭据");
            }
            foreach (var scene in report.scenes) if (!File.Exists(scene.path) || Hash(scene.path) != scene.sha256) throw new IOException("构建前场景发生变化: " + scene.path);
            if (report.sources == null) throw new IOException("构建源码清单缺失，请重新启动验证");
            var expected = report.sources.Select(source => source.path.Replace('\\', '/')).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var current = SourcePaths();
            if (expected.Distinct(StringComparer.Ordinal).Count() != expected.Length || !expected.SequenceEqual(current, StringComparer.Ordinal))
                throw new IOException("构建前源码路径清单发生变化；新增: " + string.Join(", ", current.Except(expected, StringComparer.Ordinal).Take(5)) + "；删除: " + string.Join(", ", expected.Except(current, StringComparer.Ordinal).Take(5)));
            foreach (var source in report.sources) if (!File.Exists(source.path) || Hash(source.path) != source.sha256) throw new IOException("构建前源码发生变化: " + source.path);
            Directory.CreateDirectory(Path.GetDirectoryName(report.output));
            var built = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = report.scenes.Select(s => s.path).ToArray(), locationPathName = report.output, target = target, options = BuildOptions.Development });
            report.errors = (int)built.summary.totalErrors; report.warnings = (int)built.summary.totalWarnings; report.sizeBytes = built.summary.totalSize;
            foreach (var step in built.steps) foreach (var message in step.messages)
                if ((message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Warning) && report.diagnostics.Count < 100) report.diagnostics.Add(message.content);
            if (built.summary.result != BuildResult.Succeeded || built.summary.totalErrors != 0) throw new InvalidOperationException("构建失败: " + built.summary.result + ", errors=" + built.summary.totalErrors);
            if (target == BuildTarget.Android)
            {
                if (!report.output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) || !File.Exists(report.output) || new FileInfo(report.output).Length < 4)
                    throw new IOException("Android构建返回成功，但目标不是存在的非空APK文件；未将Gradle工程目录或缺失产物记为通过");
                using (var stream = File.OpenRead(report.output))
                    if (stream.ReadByte() != 0x50 || stream.ReadByte() != 0x4b || stream.ReadByte() != 0x03 || stream.ReadByte() != 0x04)
                        throw new IOException("Android构建产物缺少APK所需的ZIP本地文件头；未将无效产物记为通过");
                report.diagnostics.Add("APK产物已核实：文件存在、非空且ZIP文件头有效；尚未进行安装或真机运行。");
            }
            report.status = "passed";
        }
        [MenuItem("Tools/PlotWeave/迁移验收/构建全部Windows运行场景")]
        public static void BuildWindows()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64) throw new InvalidOperationException("Windows构建入口要求当前活动平台为StandaloneWindows64，避免隐式切换引发域重载");
            var report = NewReport(BuildTarget.StandaloneWindows64);
            try { if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64)) { report.status = "unavailable"; report.error = "缺少Windows构建模块"; return; } Build(report, BuildTarget.StandaloneWindows64); }
            catch (Exception error) { report.status = "failed"; report.error = error.ToString(); throw; }
            finally { report.settingsRestored = true; report.targetRestored = EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64; report.completedUtc = DateTime.UtcNow.ToString("O"); Save(report); }
        }
        private static string AndroidPath(string property, string preference, string bundled)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.Android.AndroidExternalToolsSettings")).FirstOrDefault(t => t != null);
            var configured = type?.GetProperty(property, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
            configured = EditorPrefs.GetString(preference, ""); return !string.IsNullOrWhiteSpace(configured) ? configured : Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/" + bundled);
        }
        public static Toolchain ProbeAndroid()
        {
            var result = new Toolchain { targetSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android), sdk = AndroidPath("sdkRootPath", "AndroidSdkRoot", "SDK"), ndk = AndroidPath("ndkRootPath", "AndroidNdkRootR16b", "NDK"), jdk = AndroidPath("jdkRootPath", "JdkPath", "OpenJDK") };
            result.sdkReady = Directory.Exists(Path.Combine(result.sdk, "platforms")) && Directory.Exists(Path.Combine(result.sdk, "build-tools")) && File.Exists(Path.Combine(result.sdk, "platform-tools", Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb"));
            if (Directory.Exists(Path.Combine(result.sdk, "platforms"))) result.platforms = Directory.GetDirectories(Path.Combine(result.sdk, "platforms"), "android-*").Select(Path.GetFileName).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            result.sdkReady &= result.platforms.Length > 0 && Directory.Exists(Path.Combine(result.sdk,"build-tools")) && Directory.GetDirectories(Path.Combine(result.sdk,"build-tools")).Any(dir => File.Exists(Path.Combine(dir, Application.platform == RuntimePlatform.WindowsEditor ? "aapt2.exe" : "aapt2")));
            var prebuilt = Path.Combine(result.ndk, "toolchains/llvm/prebuilt");
            result.ndkReady = File.Exists(Path.Combine(result.ndk, "source.properties")) && Directory.Exists(prebuilt) && Directory.GetDirectories(prebuilt).Any(dir => File.Exists(Path.Combine(dir,"bin",Application.platform == RuntimePlatform.WindowsEditor ? "clang++.exe" : "clang++")));
            result.jdkReady = File.Exists(Path.Combine(result.jdk, "bin", Application.platform == RuntimePlatform.WindowsEditor ? "java.exe" : "java")); return result;
        }
        [MenuItem("Tools/PlotWeave/迁移验收/检测Android工具链")]
        public static void RecordAndroidToolchain()
        { var report = new Report { id = Guid.NewGuid().ToString("N"), target = "Android", status = "toolchain-probe", unityVersion = Application.unityVersion, startedUtc = DateTime.UtcNow.ToString("O"), toolchain = ProbeAndroid(), settingsRestored = true, targetRestored = true }; report.completedUtc = DateTime.UtcNow.ToString("O"); Save(report); }
        [MenuItem("Tools/PlotWeave/迁移验收/构建AndroidARM64IL2CPP并恢复平台")]
        public static void BeginAndroid()
        {
            var report = NewReport(BuildTarget.Android); report.toolchain = ProbeAndroid();
            if (EditorUserBuildSettings.buildAppBundle)
            { report.status = "unavailable"; report.error = "项目启用了AAB模式；此验证入口只构建APK，未改动用户的AAB设置"; report.settingsRestored = report.targetRestored = true; report.completedUtc = DateTime.UtcNow.ToString("O"); Save(report); return; }
            if (EditorUserBuildSettings.exportAsGoogleAndroidProject)
            { report.status = "unavailable"; report.error = "项目启用了Export Project，将导出Gradle工程；此验证入口只构建APK，未改动用户的导出设置"; report.settingsRestored = report.targetRestored = true; report.completedUtc = DateTime.UtcNow.ToString("O"); Save(report); return; }
            if (!report.toolchain.targetSupported || !report.toolchain.sdkReady || !report.toolchain.ndkReady || !report.toolchain.jdkReady)
            { report.status = "unavailable"; report.error = "Android模块或SDK/NDK/JDK不完整，未改变项目设置"; report.settingsRestored = report.targetRestored = true; report.completedUtc = DateTime.UtcNow.ToString("O"); Save(report); return; }
            if (PlayerSettings.Android.useCustomKeystore)
            { report.status = "unavailable"; report.error = "项目使用自定义签名；此验证入口不读取或修改签名凭据"; report.settingsRestored = report.targetRestored = true; report.completedUtc = DateTime.UtcNow.ToString("O"); Save(report); return; }
            var state = new MobileState { id = report.id, phase = "switching", report = report, originalTarget = (int)EditorUserBuildSettings.activeBuildTarget, originalBackend = (int)PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android), originalArchitectures = (int)PlayerSettings.Android.targetArchitectures, deadline = EditorApplication.timeSinceStartup + 600 };
            SaveMobile(state);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android && !EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android))
            { state.report.status = "failed"; state.report.error = "无法切换到Android"; state.phase = "restoring"; SaveMobile(state); }
        }
        private static void PollMobile()
        {
            if (_busy || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return; var state = ReadMobile(); if (state == null) return;
            _busy = true;
            try
            {
                if (state.phase == "switching")
                {
                    if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) { if (EditorApplication.timeSinceStartup > state.deadline) throw new TimeoutException("等待Android平台切换超时，保留恢复档案"); return; }
                    var currentBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android);
                    var currentArchitectures = PlayerSettings.Android.targetArchitectures;
                    if (currentBackend != (ScriptingImplementation)state.originalBackend || currentArchitectures != (AndroidArchitecture)state.originalArchitectures)
                        throw new InvalidOperationException("等待Android平台切换期间脚本后端或CPU架构被外部更改；未应用任何临时配置，保留恢复档案且不覆盖用户设置");
                    state.phase = "configured"; state.changedSettings = true; SaveMobile(state);
                    PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP); PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; return;
                }
                if (state.phase == "configured")
                {
                    if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw new InvalidOperationException("Android构建期间活动平台发生外部更改");
                    if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP || PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64) throw new InvalidOperationException("Android临时配置未生效或被外部修改");
                    state.phase = "building"; SaveMobile(state);
                    try { Build(state.report, BuildTarget.Android); }
                    catch (Exception error) { state.report.status = "failed"; state.report.error = error.ToString(); }
                    state.phase = "restoring"; SaveMobile(state);
                }
                if (state.phase == "restoring")
                {
                    state.phase = state.changedSettings ? "restoring-settings" : "restoring-target"; SaveMobile(state);
                }
                if (state.phase == "restoring-settings")
                {
                    // 在任一字段赋值后域重载/进程中断，恢复入口都能重新检查原值或本工具的临时值。
                    var backend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android); var architectures = PlayerSettings.Android.targetArchitectures;
                    if ((backend != ScriptingImplementation.IL2CPP && backend != (ScriptingImplementation)state.originalBackend) || (architectures != AndroidArchitecture.ARM64 && architectures != (AndroidArchitecture)state.originalArchitectures))
                        throw new InvalidOperationException("恢复前发现Android配置被外部修改；保留恢复档案，不覆盖未知变更");
                    if (backend != (ScriptingImplementation)state.originalBackend) PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, (ScriptingImplementation)state.originalBackend);
                    if (architectures != (AndroidArchitecture)state.originalArchitectures) PlayerSettings.Android.targetArchitectures = (AndroidArchitecture)state.originalArchitectures;
                    state.changedSettings = false; state.phase = "restoring-target"; SaveMobile(state);
                }
                if (state.phase == "restoring-target")
                {
                    state.report.settingsRestored = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) == (ScriptingImplementation)state.originalBackend && PlayerSettings.Android.targetArchitectures == (AndroidArchitecture)state.originalArchitectures;
                    var original = (BuildTarget)state.originalTarget;
                    if (EditorUserBuildSettings.activeBuildTarget != original)
                    { if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw new InvalidOperationException("活动平台被外部改为第三个平台；保留恢复档案，未覆盖"); state.phase = "waiting-original-target"; state.deadline = EditorApplication.timeSinceStartup + 600; SaveMobile(state); if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildPipeline.GetBuildTargetGroup(original), original)) throw new InvalidOperationException("构建结束后无法恢复原活动平台"); return; }
                    FinishMobile(state);
                }
                else if (state.phase == "waiting-original-target")
                { if (EditorUserBuildSettings.activeBuildTarget == (BuildTarget)state.originalTarget) FinishMobile(state); else if (EditorApplication.timeSinceStartup > state.deadline) throw new TimeoutException("等待恢复原活动平台超时，保留恢复档案"); }
            }
            catch (Exception error)
            { state.report.status = "failed-restoration-pending"; state.report.error = error.ToString(); state.phase = "manual-recovery-needed"; SaveMobile(state); SessionState.EraseString(MobileSessionKey); }
            finally { _busy = false; }
        }
        private static void FinishMobile(MobileState state)
        {
            state.report.targetRestored = EditorUserBuildSettings.activeBuildTarget == (BuildTarget)state.originalTarget;
            state.report.settingsRestored = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) == (ScriptingImplementation)state.originalBackend && PlayerSettings.Android.targetArchitectures == (AndroidArchitecture)state.originalArchitectures;
            if (!state.report.targetRestored || !state.report.settingsRestored) throw new InvalidOperationException("移动构建恢复检查不通过");
            state.report.completedUtc = DateTime.UtcNow.ToString("O"); state.phase = "finished"; SaveMobile(state); SessionState.EraseString(MobileSessionKey);
        }
        [MenuItem("Tools/PlotWeave/迁移验收/恢复中断的Android验证设置")]
        public static void RestoreInterruptedAndroid()
        {
            if (ReadMobile() != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("已有运行任务，不能恢复");
            if (!File.Exists(RecoveryPath)) throw new FileNotFoundException("没有移动验证恢复记录");
            var state = JsonUtility.FromJson<MobileState>(File.ReadAllText(RecoveryPath)); if (state == null || state.report == null || state.id != state.report.id || !Guid.TryParseExact(state.id, "N", out _)) throw new FormatException("移动恢复记录无效");
            if (!Enum.IsDefined(typeof(BuildTarget),state.originalTarget) || !Enum.IsDefined(typeof(ScriptingImplementation),state.originalBackend)) throw new FormatException("移动恢复枚举无效");
            if (state.phase == "finished") return;
            if (state.report.status == "running") state.report.status = "interrupted"; state.phase = "restoring"; SaveMobile(state);
        }
        [MenuItem("Tools/PlotWeave/迁移验收/导出当前iOS平台Xcode工程")]
        public static void BuildIosExportForActiveTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS) throw new InvalidOperationException("iOS导出入口要求已切换到iOS平台；不会隐式切换或更改签名");
            var report = NewReport(BuildTarget.iOS);
            try { if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS)) { report.status = "unavailable"; report.error = "缺少iOS模块"; return; } Build(report, BuildTarget.iOS); report.diagnostics.Add("这里只验证Xcode工程导出；未进行Xcode签名、安装或真机运行。"); }
            catch (Exception error) { report.status = "failed"; report.error = error.ToString(); throw; }
            finally { report.settingsRestored = true; report.targetRestored = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS; report.completedUtc = DateTime.UtcNow.ToString("O"); Save(report); }
        }
    }
}
