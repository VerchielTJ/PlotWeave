using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PlotWeave.Data;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PlotWeave.Legacy.Editor
{
    /// <summary>在本地验证目录重新创建纹理/精灵/动画，所有Unity资产取得新GUID。</summary>
    public sealed class PlotLegacyUiResources
    {
        private readonly string _sourceRoot, _output;
        private readonly Dictionary<string, PlotLegacyResource> _data;
        private readonly Dictionary<string, UnityEngine.Object> _cache = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, Type> _scriptTypes;
        private readonly PlotLegacyUiReport _report;
        public PlotLegacyUiResources(PlotLegacyUiBundle bundle, string sourceRoot, string output, PlotLegacyUiReport report)
        {
            _sourceRoot = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar); _output = output; _report = report; _data = bundle.resources.ToDictionary(r => r.key, StringComparer.Ordinal);
            _scriptTypes = bundle.layouts.SelectMany(l => l.components).Where(c => !string.IsNullOrEmpty(c.sourceScriptGuid) && PlotLegacyUiRebuilder.ComponentType(c) != null).GroupBy(c => c.sourceScriptGuid).ToDictionary(g => g.Key, g => PlotLegacyUiRebuilder.ComponentType(g.First()), StringComparer.Ordinal);
            Directory.CreateDirectory(output); AssetDatabase.Refresh();
        }
        public UnityEngine.Object Resolve(string key)
        {
            if (_cache.TryGetValue(key, out var value)) return value;
            if (!_data.TryGetValue(key, out var item)) return null;
            var source = Path.GetFullPath(Path.Combine(_sourceRoot, item.source)); if (!source.StartsWith(_sourceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("资源路径越出原工程");
            var stem = PlotLegacyAssetImport.Hash(item.source + ":" + item.fileID).Substring(0, 16);
            switch (item.kind)
            {
                case ".png": case ".jpg": case ".jpeg":
                    if (!_textures.TryGetValue(item.source, out var texture))
                    {
                        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); texture.name = Path.GetFileNameWithoutExtension(item.source); if (!texture.LoadImage(File.ReadAllBytes(source), false)) { UnityEngine.Object.DestroyImmediate(texture); throw new IOException("图片解码失败: " + item.source); }
                        // Unity JsonUtility会把JSON null嵌套类读成全零实例；缺省范围明确表示整图。
                        var explicitRect=item.hasRect||(item.rect!=null&&(item.rect.x!=0||item.rect.y!=0||item.rect.width!=0||item.rect.height!=0));
                        if(explicitRect&&(item.rect==null||item.rect.x!=0||item.rect.y!=0||item.rect.width!=texture.width||item.rect.height!=texture.height))throw new NotSupportedException("原资源包含非整图精灵，需要明确切片描述: "+item.source);
                        // 新编码PNG交给TextureImporter生成可重导入的Sprite渲染数据，不把临时Sprite塞入Texture2D原生.asset。
                        var texturePath=_output+"/Texture_"+PlotLegacyAssetImport.Hash(item.source).Substring(0,16)+".png";File.WriteAllBytes(texturePath,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
                        var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.isReadable=true;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=16384;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.spritePixelsPerUnit=item.pixelsPerUnit>0?item.pixelsPerUnit:100;
                        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=item.pivot;settings.spriteBorder=item.border;importer.SetTextureSettings(settings);importer.SaveAndReimport();texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);_textures.Add(item.source,texture);
                    }
                    if (item.fileID == "2800000") value = texture;
                    else
                    {
                        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(texture));if(sprite==null||sprite.rect.width<=0||sprite.rect.height<=0||sprite.textureRect.width<=0||sprite.textureRect.height<=0||sprite.vertices.Length<3)throw new IOException("新图片的Sprite渲染范围无效: "+item.source);value=sprite;
                    }
                    AssetDatabase.SaveAssetIfDirty(texture); break;
                case ".mat": value = TMP_Settings.defaultFontAsset.material; _report.issues.Add(new PlotLegacyUiIssue { source = item.source, kind = "font-material-rebound", detail = "原字体材质改绑新工程字体；运行时使用随包Noto字体" }); break;
                case ".anim": value = CreateClip(item, stem); break;
                case ".controller": value = CreateController(item, stem); break;
                default: _report.issues.Add(new PlotLegacyUiIssue { source = item.source, kind = "unsupported-resource", detail = item.kind }); return null;
            }
            _cache[key] = value; _report.resources.Add(key); return value;
        }
        private static Dictionary<string, object> Map(object value) => value as Dictionary<string, object> ?? new Dictionary<string, object>();
        private static List<object> List(object value) => value as List<object> ?? new List<object>();
        private static object Get(Dictionary<string, object> data, string key, object fallback = null) => data.TryGetValue(key, out var value) ? value : fallback;
        private static string Text(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        private static float Number(object value, float fallback = 0)
        { if (value == null) return fallback; var text = Text(value); return text == "Infinity" || text == "+Infinity" ? float.PositiveInfinity : text == "-Infinity" ? float.NegativeInfinity : float.Parse(text, CultureInfo.InvariantCulture); }
        private static bool Flag(object value) => value is bool flag ? flag : Text(value) == "1" || Text(value) == "true";
        private static string Ref(object value) => Text(Get(Map(value), "fileID", "0"));
        private UnityEngine.Object External(object value) { var map = Map(value); return Resolve(Text(Get(map,"guid")) + ":" + Ref(value)); }
        private Type BindingType(Dictionary<string, object> curve)
        {
            var classID = (int)Number(Get(curve, "classID")); if (classID == 4) return typeof(Transform); if (classID == 224) return typeof(RectTransform); if (classID == 225) return typeof(CanvasGroup); if (classID == 1) return typeof(GameObject);
            var guid = Text(Get(Map(Get(curve, "script")), "guid")); if (_scriptTypes.TryGetValue(guid, out var type)) return type;
            throw new NotSupportedException("动画属性绑定类型无法确定: " + classID + "/" + guid);
        }
        private AnimationCurve Curve(Dictionary<string, object> source, string axis = null)
        {
            var keys = new List<Keyframe>();
            foreach (var raw in List(Get(source, "m_Curve")))
            {
                var key = Map(raw); object Component(string name) => axis == null ? Get(key, name) : Get(Map(Get(key, name)), axis);
                var frame = new Keyframe(Number(Get(key, "time")), Number(Component("value")), Number(Component("inSlope")), Number(Component("outSlope")), Number(Component("inWeight"), 1f/3), Number(Component("outWeight"), 1f/3)); frame.weightedMode = (WeightedMode)(int)Number(Get(key, "weightedMode")); keys.Add(frame);
            }
            return new AnimationCurve(keys.ToArray());
        }
        private AnimationClip CreateClip(PlotLegacyResource item, string stem)
        {
            var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(item.source) }; _cache[item.key] = clip; AssetDatabase.CreateAsset(clip, _output + "/Clip_" + stem + ".anim");
            var data = PlotJson.Object(item.objects.Single(o => o.type == 74).json); clip.name = Text(Get(data,"m_Name",clip.name)); clip.frameRate = Number(Get(data, "m_SampleRate"), 60); clip.legacy = Flag(Get(data, "m_Legacy"));
            var floats = List(Get(data, "m_FloatCurves")); if (floats.Count == 0) floats = List(Get(data, "m_EditorCurves"));
            foreach (var raw in floats)
            { var curve = Map(raw); AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(Text(Get(curve, "path")), BindingType(curve), Text(Get(curve, "attribute"))), Curve(Map(Get(curve, "curve")))); }
            foreach (var kind in new[] { "m_PositionCurves", "m_ScaleCurves", "m_EulerCurves", "m_RotationCurves" }) foreach (var raw in List(Get(data, kind)))
            {
                var curve = Map(raw); var property = kind == "m_PositionCurves" ? "m_LocalPosition" : kind == "m_ScaleCurves" ? "m_LocalScale" : kind == "m_RotationCurves" ? "m_LocalRotation" : "localEulerAnglesRaw";
                foreach (var axis in kind == "m_RotationCurves" ? new[] { "x", "y", "z", "w" } : new[] { "x", "y", "z" }) AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(Text(Get(curve, "path")), typeof(Transform), property + "." + axis), Curve(Map(Get(curve, "curve")), axis));
            }
            foreach (var raw in List(Get(data, "m_PPtrCurves")))
            {
                var curve = Map(raw); var keys = List(Get(curve, "curve")).Select(v => { var key = Map(v); return new ObjectReferenceKeyframe { time = Number(Get(key,"time")), value = External(Get(key,"value")) }; }).ToArray();
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(Text(Get(curve, "path")), BindingType(curve), Text(Get(curve,"attribute"))), keys);
            }
            var events = List(Get(data, "m_Events")); if (events.Count != 0) throw new NotSupportedException("动画事件需要显式迁移处理器: " + item.source);
            var settings = AnimationUtility.GetAnimationClipSettings(clip); var originalSettings=Map(Get(data,"m_AnimationClipSettings")); settings.loopTime = Flag(Get(originalSettings, "m_LoopTime")); settings.startTime=Number(Get(originalSettings,"m_StartTime")); settings.stopTime=Number(Get(originalSettings,"m_StopTime"),clip.length); settings.cycleOffset=Number(Get(originalSettings,"m_CycleOffset")); AnimationUtility.SetAnimationClipSettings(clip, settings); AssetDatabase.SaveAssetIfDirty(clip); return clip;
        }
        private AnimatorController CreateController(PlotLegacyResource item, string stem)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(_output + "/Controller_" + stem + ".controller"); _cache[item.key] = controller;
            var objects = item.objects.ToDictionary(o => o.id, o => PlotJson.Object(o.json), StringComparer.Ordinal); var root = PlotJson.Object(item.objects.Single(o => o.type == 91).json);
            foreach (var raw in List(Get(root, "m_AnimatorParameters")))
            { var p = Map(raw); controller.AddParameter(new AnimatorControllerParameter { name = Text(Get(p,"m_Name")), type = (AnimatorControllerParameterType)(int)Number(Get(p,"m_Type")), defaultBool = Flag(Get(p,"m_DefaultBool")), defaultFloat = Number(Get(p,"m_DefaultFloat")), defaultInt = (int)Number(Get(p,"m_DefaultInt")) }); }
            var states = new Dictionary<string, AnimatorState>(StringComparer.Ordinal); var machines = new Dictionary<string, AnimatorStateMachine>(StringComparer.Ordinal);
            Motion MotionValue(object reference)
            {
                var map=Map(reference);var local=Ref(reference);if(local=="0")return null;if(!string.IsNullOrEmpty(Text(Get(map,"guid"))))return External(reference) as Motion;
                var embedded=item.objects.FirstOrDefault(o=>o.id==local && o.type==74);if(embedded==null)throw new NotSupportedException("未迁移的控制器内嵌Motion: "+item.source+"#"+local);
                var key=item.key+"#"+local;if(_cache.TryGetValue(key,out var cached))return cached as Motion;
                return CreateClip(new PlotLegacyResource { key=key,source=item.source,objects=new List<PlotLegacyResourceObject>{embedded} },PlotLegacyAssetImport.Hash(key).Substring(0,16));
            }
            AnimatorStateMachine Machine(string id)
            {
                if (machines.TryGetValue(id, out var existing)) return existing; if (!objects.TryGetValue(id,out var source)) throw new FormatException("动画状态机引用缺失: " + id);
                var machine = new AnimatorStateMachine { name = Text(Get(source,"m_Name")) }; machines.Add(id,machine); AssetDatabase.AddObjectToAsset(machine, controller);
                foreach (var raw in List(Get(source,"m_ChildStates")))
                { var child = Map(raw); var stateId = Ref(Get(child,"m_State")); var values = objects[stateId]; var position = Map(Get(child,"m_Position")); var state = machine.AddState(Text(Get(values,"m_Name")),new Vector3(Number(Get(position,"x")),Number(Get(position,"y")),Number(Get(position,"z")))); states.Add(stateId,state); state.speed = Number(Get(values,"m_Speed"),1); state.writeDefaultValues = Flag(Get(values,"m_WriteDefaultValues")); state.motion=MotionValue(Get(values,"m_Motion")); }
                foreach (var raw in List(Get(source,"m_ChildStateMachines"))) { var child=Map(raw); var nested=Machine(Ref(Get(child,"m_StateMachine"))); machine.AddStateMachine(nested,Vector3.zero); }
                var defaultId=Ref(Get(source,"m_DefaultState")); if (states.TryGetValue(defaultId,out var defaultState)) machine.defaultState=defaultState; return machine;
            }
            var layers=new List<AnimatorControllerLayer>();
            foreach (var raw in List(Get(root,"m_AnimatorLayers"))) { var layer=Map(raw); layers.Add(new AnimatorControllerLayer { name=Text(Get(layer,"m_Name")),stateMachine=Machine(Ref(Get(layer,"m_StateMachine"))),defaultWeight=Number(Get(layer,"m_DefaultWeight"),1),blendingMode=(AnimatorLayerBlendingMode)(int)Number(Get(layer,"m_BlendingMode")),iKPass=Flag(Get(layer,"m_IKPass")) }); }
            controller.layers=layers.ToArray();
            void Configure(AnimatorStateTransition transition, Dictionary<string,object> values)
            {
                transition.hasExitTime=Flag(Get(values,"m_HasExitTime"));transition.exitTime=Number(Get(values,"m_ExitTime"));transition.duration=Number(Get(values,"m_TransitionDuration"));transition.offset=Number(Get(values,"m_TransitionOffset"));transition.hasFixedDuration=Flag(Get(values,"m_HasFixedDuration"));transition.canTransitionToSelf=Flag(Get(values,"m_CanTransitionToSelf"));transition.mute=Flag(Get(values,"m_Mute"));transition.solo=Flag(Get(values,"m_Solo"));
                foreach(var raw in List(Get(values,"m_Conditions"))) { var condition=Map(raw);transition.AddCondition((AnimatorConditionMode)(int)Number(Get(condition,"m_ConditionMode")),Number(Get(condition,"m_EventTreshold")),Text(Get(condition,"m_ConditionEvent"))); }
            }
            AnimatorStateTransition Transition(AnimatorState state, AnimatorStateMachine machine, Dictionary<string,object> values)
            {
                var destination=Ref(Get(values,"m_DstState"));if(states.TryGetValue(destination,out var target))return state!=null?state.AddTransition(target):machine.AddAnyStateTransition(target);
                var stateMachine=Ref(Get(values,"m_DstStateMachine"));if(machines.TryGetValue(stateMachine,out var targetMachine))return state!=null?state.AddTransition(targetMachine):machine.AddAnyStateTransition(targetMachine);
                if(state!=null && Flag(Get(values,"m_IsExit")))return state.AddExitTransition();throw new NotSupportedException("动画过渡目标未绑定: "+item.source);
            }
            foreach(var pair in states)foreach(var raw in List(Get(objects[pair.Key],"m_Transitions"))) { var values=objects[Ref(raw)];Configure(Transition(pair.Value,null,values),values); }
            foreach(var pair in machines)foreach(var raw in List(Get(objects[pair.Key],"m_AnyStateTransitions"))) { var values=objects[Ref(raw)];Configure(Transition(null,pair.Value,values),values); }
            AssetDatabase.SaveAssetIfDirty(controller);return controller;
        }
    }
}
