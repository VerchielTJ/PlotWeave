using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;
using PlotWeave.Authoring.Luban;
using PlotWeave.Data;
using PlotWeave.Legacy.Editor;
using UnityEditor;
using UnityEngine;

namespace PlotWeave.Tests
{
    /// <summary>检查真实生成类型的反射读面有明确保留边界；通过不代表已替代IL2CPP真机验收。</summary>
    public sealed class PlotLubanAotPreservationTests
    {
        [TestCase("Assets/PlotWeaveMigration/Stage09_Luban/LubanProfile.asset")]
        [TestCase("Assets/PlotWeaveMigration/Stage12_Nested/LubanProfile.asset")]
        public void RealGeneratedLubanMembersHaveExplicitStrippingPreservation(string profilePath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<PlotLubanProfile>(profilePath);
            if (profile == null) { Assert.Ignore("尚未生成此Luban验证样例"); return; }
            CheckProfile(profile);
        }
        [Test] public void CompleteOriginalLubanMembersHaveExplicitStrippingPreservation()
        {
            if (!File.Exists(PlotLegacyCampaignImport.LatestPath)) { Assert.Ignore("本地原数据未导入"); return; }
            var report = JsonUtility.FromJson<PlotLegacyCampaignImport.Report>(File.ReadAllText(PlotLegacyCampaignImport.LatestPath));
            var profile = AssetDatabase.LoadAssetAtPath<PlotLubanProfile>(report.lubanProfile); Assert.That(profile, Is.Not.Null); CheckProfile(profile);
        }
        private static void CheckProfile(PlotLubanProfile profile)
        {
            Assert.That(profile.generatedRoot.Replace('\\', '/'), Does.StartWith("Assets/"));
            var path = profile.generatedRoot + "/link.xml";
            Assert.That(File.Exists(path), Is.True, "Luban对象通过运行时Type和字符串读成员，生成目录须提供明确的link.xml保留边界");
            var document = XDocument.Load(path); Assert.That(document.Root?.Name.LocalName, Is.EqualTo("linker"));
            var assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == profile.assemblyName);
            Assert.That(assembly, Is.Not.Null, "等待真实生成程序集编译后再验收");
            var types = assembly.GetTypes().Where(t => t.Namespace == profile.generatedNamespace && !t.IsInterface).ToArray(); Assert.That(types, Is.Not.Empty);
            var checkedMembers = 0;
            foreach (var type in types)
            {
                var flags = BindingFlags.Public | BindingFlags.DeclaredOnly | (type.IsEnum ? BindingFlags.Static : BindingFlags.Instance);
                foreach (var field in type.GetFields(flags)) { Assert.That(Covers(document, type, field), Is.True, "未保留字段/枚举键: " + type.FullName + "." + field.Name); checkedMembers++; }
                foreach (var property in type.GetProperties(flags).Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
                { Assert.That(Covers(document, type, property), Is.True, "未保留可读属性: " + type.FullName + "." + property.Name); checkedMembers++; }
            }
            Assert.That(checkedMembers, Is.GreaterThan(10), "应覆盖真实节点、嵌套Bean、文本、演员和表接口，而不是空描述文件");
            var manifest = Path.Combine(profile.generatedRoot, ".plotweave-luban-manifest.json");
            Assert.That(File.Exists(manifest), Is.True); var ownership = JsonUtility.FromJson<Ownership>(File.ReadAllText(manifest));
            Assert.That(ownership.files.Any(f => f.path.Replace('\\', '/') == "link.xml"), Is.True, "保留描述也应属于生成器SHA事务，不能成为未跟踪的旁路文件");
            using(var sha=SHA256.Create())Assert.That(ownership.files.Single(f=>f.path.Replace('\\','/')=="link.xml").sha256,Is.EqualTo(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant()),"保留描述的当前内容必须与生成事务哈希一致");
        }
        [Serializable] private sealed class Ownership { public Stamp[] files=Array.Empty<Stamp>(); }
        [Serializable] private sealed class Stamp { public string path="", sha256=""; }

        [TestCase("PlotWeave.Generated.Stage02.Default.GamePlotAtomDataAdapter")]
        [TestCase("PlotWeave.Generated.Stage12.Nested.CameraCue")]
        public void GeneratedC2AdapterAndNestedBeanAvoidUnprotectedReflection(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null);
            if (type == null) { Assert.Ignore("尚未生成此C2验证样例"); return; }
            // 实现IPlotValueSource意味着Wrap直接分派，生成TryGetValue可静态引用每个成员。
            if (typeof(IPlotValueSource).IsAssignableFrom(type)) return;
            var documents = Directory.GetFiles("Assets/PlotWeaveMigration", "link.xml", SearchOption.AllDirectories).Select(XDocument.Load).ToArray();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(p => p.CanRead && p.GetIndexParameters().Length == 0).ToArray();
            Assert.That(properties, Is.Not.Empty);
            foreach (var property in properties)
                Assert.That(Preserved(property) || documents.Any(d => Covers(d, type, property)), Is.True,
                    "生成类最终仍经ObjectPlotValueSource反射读取；类上的Preserve只保留类型/默认构造器，须保留属性读口或改为IPlotValueSource: " + fullName + "." + property.Name);
        }
        private static bool Preserved(MemberInfo member)
            => member.GetCustomAttributes(false).Any(a => a.GetType().Name == "PreserveAttribute");
        private static bool Matches(string pattern, string value)
            => !string.IsNullOrEmpty(pattern) && Regex.IsMatch(value, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.CultureInvariant);
        private static bool Covers(XDocument document, Type type, MemberInfo member)
        {
            if (document.Root == null || document.Root.Name.LocalName != "linker") return false;
            foreach (var assembly in document.Root.Elements("assembly").Where(a => ((string)a.Attribute("fullname") ?? "").Split(',')[0] == type.Assembly.GetName().Name))
            {
                var preservation = (string)assembly.Attribute("preserve");
                if (preservation == "all" || (preservation == null && !assembly.Elements().Any())) return true;
                foreach (var definition in assembly.Elements("type").Where(t => Matches((string)t.Attribute("fullname"), type.FullName.Replace('+', '/'))))
                {
                    preservation = (string)definition.Attribute("preserve");
                    if (preservation == "all" || (preservation == null && !definition.Elements().Any())) return true;
                    if (member is FieldInfo && preservation == "fields") return true;
                    var tag = member is FieldInfo ? "field" : "property";
                    if (definition.Elements(tag).Any(e => Matches((string)e.Attribute("name"), member.Name) || ((string)e.Attribute("signature") ?? "").EndsWith(" " + member.Name, StringComparison.Ordinal))) return true;
                }
            }
            return false;
        }
    }
}
