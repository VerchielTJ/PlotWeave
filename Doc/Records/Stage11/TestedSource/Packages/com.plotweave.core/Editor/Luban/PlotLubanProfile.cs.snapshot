using UnityEngine;

namespace PlotWeave.Authoring.Luban
{
    [CreateAssetMenu(menuName = "PlotWeave/Luban生成配置")]
    public sealed class PlotLubanProfile : ScriptableObject
    {
        public PlotAuthoringProject project;
        [Tooltip("dotnet可执行文件；不经过Shell执行")]
        public string dotnetPath = "dotnet";
        public string compilerDll = "Tools/ThirdParty/Luban-4.10.2/Luban/Luban.dll";
        public string generatedRoot = "Assets/PlotWeaveGenerated";
        public string generatedNamespace = "PlotWeaveLuban";
        [Tooltip("已存在的Luban运行库程序集；示例使用独立运行库，核心包不注入全局Luban类型")]
        public string runtimeAssembly = "PlotWeave.LubanRuntime.Sample";
        public string assemblyName = "PlotWeave.Luban.Generated";
        [Range(5, 60)] public int timeoutSeconds = 45;
    }
}
