# 完整Luban播放器与构建入口

这是工具入口说明，不是运行、测试或构建通过证明。执行结果写入 `Doc/Records/Stage17`，需以实际Unity测试、原生构建报告及运行验收为准；生成成功或静态编译通过均不能替代这些证据。

## 完整原数据二进制播放器

`PlotLegacyLubanPlaybackMigration.PrepareSource()` 读取当前完整数据Luban配置和当前视觉资源配置，在本地验证目录创建独立播放器源码、程序集定义及输入哈希记录。生成的运行时代码强类型实例化实际Luban Tables/ByteBuf，读取节点、演员、对白、常量四份二进制，并启用旧角色槽投影规则。运行时不启动dotnet或表格编译器。

等待Unity完成编译后，调用 `PlotLegacyLubanPlaybackMigration.PrepareScene()` 保存独立播放场景。它检查生成源码、二进制的哈希及配置，拒绝覆盖已有场景。最新指针为 `Tools/PlotWeaveMigration/original-luban-playback.json`。生成场景仍需运行和截图验收。

## 完整原数据C2接口证据

`PlotLegacyOriginalContractsMigration.PrepareVerification()` 读取 `Tools/PlotWeaveMigration/imported-campaign.json` 中当前 `roundtripWorkspace`，对真实485个作者节点所属工作区调用 `PlotDataStructureGenerator.GenerateAllDataStructures()`。它根据原节点表头生成全部Bean、枚举（包含中文枚举键）和节点接口，并同时生成actor、dialog、constant三类表的Editor/Game适配器。

输出目录为当前导入报告的 `<root>/C2Contracts`，即 `Assets/PlotWeaveLocal/Campaign/Import_<导入批次>/C2Contracts`。命名空间为 `PlotWeave.Generated.Original.Run<本次报告ID>`，程序集名为 `PlotWeave.C2.Original.Run<本次报告ID>`，使用 `Run` 前缀避免标识符以数字开头。固定文件名 `PlotWeave.Original.Contracts.asmdef` 只显式引用 `PlotWeave.Runtime`，不依赖Editor程序集或具体Luban生成程序集。

三份生成源码与asmdef一同纳入生成器自己的 `plotweave-generation.json` SHA所有权清单；重复生成只覆盖该清单证明归工具所有、且没有人工改动的旧文件，发现未知文件冲突或人工修改时拒绝覆盖。工具不清空目录。最新报告为 `Tools/PlotWeaveMigration/original-contracts.json`，归档为 `Doc/Records/Stage17/original-contracts-<本次ID>.json`，记录工作区、批次、类型数量、程序集及文件摘要。报告中的 `generated-awaiting-unity-compilation` 表示已生成、等待Unity编译与验收，并不表示测试通过。

等待Unity编译后运行 `PlotLegacyOriginalContractsTests` 的三项测试：

1. `ActualGeneratedEditorAdapterReadsEveryOriginalAuthoringNode`：实例化这次实际生成的 `EditorPlotAtomDataAdapter`，读取全部485个作者节点，按节点ID逐一比较JSON UTF8 SHA与制作工作区编译快照。
2. `ActualGeneratedGameAdapterReadsEveryDecodedOriginalLubanNode`：由真实Luban `Tables/ByteBuf` 解码四份二进制，再由本次生成的 `GamePlotAtomDataAdapter` 包装节点，以 `legacyRoleSlots:true` 读取并逐节点比较同一基线。
3. `OriginalHeaderBeansEnumsAndTablesCompileInProtectedRuntimeOnlyAssembly`：检查全部表头字段、Bean、枚举及中文枚举键，实例化actor/dialog/constant的六个Editor/Game表适配器并核对行数，同时校验生成文件SHA与独立程序集依赖。

两路节点测试均从工作区单独应用事件轨道侧车，比较失败仅输出节点ID和摘要，不在断言中输出原剧情。测试只证明这批原数据与接口的本地一致性，不能替代Android IL2CPP或真机验收。

## Windows构建

`PlotLegacyFullBuildVerification.BuildWindows()` 要求当前平台为StandaloneWindows64。当前构建清单共有9个运行场景：公开合成的Stage18章节与奖励样例为首场景，随后包含最新原章节、完整原数据Luban、原剧情播放，以及通用章节、分支事件、示例Luban、资产播放和JSON播放；不包含编辑制作场景。首场景路径为 `Assets/PlotWeaveMigration/Stage18_CampaignReference/CampaignReference.unity`，需先通过 `PlotCampaignReferenceExample.PrepareVerification()` 创建。输出写入唯一的 `Builds/PlotWeaveFull/<本次ID>/PlotWeave.exe`，不会覆盖已有构建。报告记录场景及源码哈希、错误/警告、结果和体积；入口存在或场景已创建均不表示构建通过。

## Android工具链与构建

`RecordAndroidToolchain()` 只检测模块及当前SDK/NDK/JDK路径，检查Android平台、aapt2、adb、NDK clang++和Java。它不安装或更新工具。

`BeginAndroid()` 是跨域重载的异步流程：保存原活动平台、Android脚本后端与CPU架构，切换到Android，暂用IL2CPP/ARM64构建Development APK，然后恢复后端、架构与活动平台。使用自定义签名的项目会记录不可用结果，不读取或修改签名凭据。生成APK不等于真机验证。

流程锁为 `PlotWeave.Stage17.MobileBuild`，Unity桥在锁存在时应暂停处理其他迁移请求。恢复档案位于 `Tools/PlotWeaveMigration/mobile-build-recovery.json`。编辑器进程意外中断后，可调用 `RestoreInterruptedAndroid()` 恢复；工具只接受原值或本工具临时值，不覆盖其他未知设置。平台切换超时或恢复失败会保留恢复记录，不能当作成功。

## iOS导出

`BuildIosExportForActiveTarget()` 要求当前已是iOS平台，检测模块后尝试导出Development Xcode工程。它不切换平台、不改签名；导出成功也不表示已完成Xcode编译、签名或iOS真机验证。

原剧情和美术引用只保存在忽略提交的本地验证目录。最终公开示例应使用公开候选内容，发布前不能直接包含这些原剧情构建产物。
