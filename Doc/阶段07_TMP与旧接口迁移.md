# 阶段07：TMP与旧接口迁移

日期：2026-10-28。本批实现并通过137项Unity联测；真实三路径截图和Windows构建已归档于Records/Stage07。

## 补齐的迁移内容

播放对话、角色名与幕布标题改用TextMeshPro。TMP负责解析完整富文本，Core按解析后的文字单元推进，使用maxVisibleCharacters显示，不截断标签。内联Sprite视为一个文字单元，组合音标保持完整。

补入Noto Sans CJK SC字体，随包保留SIL OFL许可。曾发现系统字体经TMP载入后缺少中文字形，一项端到端测试失败；新增字形测试并改用随包字体后，中文显示及137项联测通过。

新增PlotPlayerViewBindings，可在Inspector显式绑定对话、幕布、舞台和控制按钮。新生成的TMPPlayer.prefab可以保存、编辑并由Host实例化。角色支持按资源Key指定自有UI预制体，不必只使用默认图片对象。没有复制原游戏预制体、GUID或美术。

旧INodeDataControl、INodeLifecycle、INodeMoveable和PlotNodeDataBase已迁为可用兼容层，补入成员、结构字段、列表/端口、ID、坐标和生命周期行为。原GraphView连线清理及字符串转换、Excel占位入口已接实际工具。静态中文枚举可显式注册映射，旧动态枚举特性绘制从当前对象取资产，避免拿全工程首个枚举资产。

这些是明确补齐的新实现，并非声称原空方法已经具有这些行为；兼容类型位于PlotWeave.Legacy命名空间。

## 入口

`Tools / PlotWeave / 迁移样例 / 补齐TMP与可编辑播放预制体`。预制体位于 `Assets/PlotWeaveMigration/Stage07_Migration/TMPPlayer.prefab`。Host的viewPrefab、actorPrefabs、fontAsset和inlineSprites均可配置；制作工作区visuals提供对应预览配置。

## Luban探针发现

原路径下缺少可执行工具，已取得官方固定4.10.2版本，在独立验证目录运行编译器，并实际编译生成的C#、读取其二进制数据。核对了两条角色行为、Shake枚举和两条分支。

实际编译暴露了此前自有往返测试没有覆盖的差异：枚举/Bean顶层表头需要合并；新版schema有group、alias等列；与name完全相同的alias需归一化。原布局的星号在Luban表示多行读取标记，作者侧IsList仍独立决定容器类型；不能把两者混为一条规则。探针已通过，尚待阶段09将适配、执行和运行时读取集成为工程内工作流。

固定工具来自 [Luban官方发布](https://github.com/focus-creative-games/luban/releases/tag/v4.10.2)，布局依据 [官方Excel格式文档](https://www.datable.cn/docs/3.x/manual/exceladvanced) 与官方示例。字体来自 [Noto CJK](https://github.com/notofonts/noto-cjk)，源文件SHA256与许可随本批记录保存。

## 证据与后续

新增8项测试覆盖富文本逐字、内联图标、中文字体、组合文字、预制体、旧节点端口、枚举映射与资产来源隔离。动作/表情、背景过渡、分支与事件执行在后续批次完成。Luban探针不能替代完整Unity内编表集成，仍需分别记录。
