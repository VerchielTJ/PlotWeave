# 阶段09：真实Luban编译与读取

日期：2026-10-28。接通原六表导出、Luban4输入适配、外部真实编译、生成代码和二进制读取；具体输出哈希、Unity测试和运行画面归档于Records/Stage09。

## 配置和生成

创建`PlotLubanProfile`，绑定作者工作区、dotnet可执行文件、Luban.dll、命名空间、自有Assets输出子目录及运行库程序集。Inspector点击“执行Luban生成”。固定样例使用Luban4.10.2与.NET8；项目Tools/ThirdParty保留工具和MIT许可。

每次运行在`Library/PlotWeaveLuban/<id>`导出Raw六表、生成独立Data/schema.xml/luban.conf，隐藏执行dotnet，收集退出码和有上限的日志，超时终止该编译进程。成功输出cs-bin与bin后才发布。自有输出有SHA256清单，手改文件拒绝覆盖；发布失败恢复旧文件。报告另存Doc/Records/Stage09。

转换层恢复新版enum/bean元数据列和顶层合并、归一相同name/alias，并根据作者IsList决定多行星号标记。不会把适配后的metadata覆盖回原六表。工作区有事件轨道时同时输出Data/PlotEvents.json，运行时通过ApplyEventSidecar合入。

## 游戏端

核心包只接收已解码对象/值源，不携带全局Luban库依赖。示例将官方运行库模板生成到独立程序集；已有游戏可继续使用自己的Luban运行库。当前字段为节点契约、演员表和固定五语言文本形状；其他游戏命名须写适配层。

Unity测试实际调用生成Tables和ByteBuf读取.bytes，经生成Game适配器验证列表Bean、枚举、分支与文本，并进入PlotPlayerCore。文本归一层将Luban的KeyID/五列语言转为共同key/value契约。

`LubanPlayback.unity`通过强类型样例脚本从绑定的TextAsset读取二进制并播放，不在Player中调用dotnet。首次生成后等待编译，再点“创建Luban二进制播放场景”。

原工程缺失的Luban目录及cfg不在本次复原范围；不能由这个新样例推出旧游戏配置无需调整。固定工具来自[Luban官方4.10.2发布](https://github.com/focus-creative-games/luban/releases/tag/v4.10.2)，运行库来源提交及MIT许可随模板保存。
