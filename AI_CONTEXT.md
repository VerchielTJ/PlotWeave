# PlotWeave：AI工程导航

这是Unity 2022.3剧情制作与播放工具的工程上下文。先读取本文件与结构化索引，再定位具体源码。索引不替代实现，验收报告也不代表逐方法覆盖率。

本仓库为公开分发版，许可证为 [MIT](LICENSE)。先读[公开版首次运行](Doc/公开版首次运行.md)与[本次公开整理检查](Doc/Records/PublicExport/verification.json)。`Doc/Records/Current`保留本地源工程的历史证据，不等于在公开克隆上重新完成全部测试；部分私有输入与截图未分发。

当前开放问题见[2026-10-29设计审查](Doc/Review/设计审查_2026-10-29.md)与[机器问题清单](Doc/Review/design-review-2026-10-29.json)。优先处理旧草稿冲突和跳转引用完整性；历史277项通过不表示这些新增场景已覆盖。Windows管道采集查询输出时，目前先使用`python -X utf8`；编码和导航工件验证缺口也已落档。

## 快速读取

项目根目录执行，Python脚本只读本地文件，不启动Unity、不联网：

```sh
python Tools/Review/query.py summary
python Tools/Review/query.py verify
python Tools/Review/query.py find PlotPlayer
python Tools/Review/query.py show PlotPlayerCore
python Tools/Review/query.py relations core
python Tools/Review/query.py module exchange
python Tools/Review/query.py status
```

全局参数放在子命令之前，例如 `python Tools/Review/query.py --limit 5 find Adapter`。从其他目录调用时使用 `--project <项目绝对路径>`。成功结果输出JSON；`verify`不匹配、类型不唯一或读取失败返回退出码2。列表默认最多12条，可设1—50条；`find`同时搜索精选类型与完整词法声明索引。

## 数据入口

- [engineering-index.json](Doc/Review/engineering-index.json)：精选类型、真实成员、关系与源码行号，设计沿革、当前实现及接入状态。
- [engineering-index.schema.json](Doc/Review/engineering-index.schema.json)：评审索引的版本化结构说明。
- [source-inventory.json](Doc/Review/source-inventory.json)：整个包的文件摘要与类型声明导航，包含测试、嵌套类型、partial声明和历史原型；它是词法提取，不是C#语义模型，声明条目数不等于唯一类数。
- [provenance.json](Doc/Review/provenance.json)：原设计主线与本次AI协作实现的证据、功能级区别和本人待确认项。
- [当前验收总览](Doc/Records/Current/验收总览.json)：2026-10-28的277项测试与Windows/Android构建事实，以及当时精确源码摘要。

`sourceSnapshot.acceptedSourceMatches`表示生成索引时的比对结果，不能当作实时磁盘状态。`verify`重新比较当前源码、精选关系输入、来源说明和当前验收指针；不会重新运行测试。文件被修改后，应先检查差异，再更新验证与索引，不把历史通过数写成新一轮结果。

## 现行链路

类型与表头 → 作者资产与编辑事务 → 播放快照 → 共享 `PlotPlayerCore` → 视图与Unity宿主。Asset、JSON、制作预览和Luban解码均可进入同一核心。

制作主入口为 `Tools/PlotWeave/打开剧情编辑器`，会打开工作场景、进入Play Mode并显示Game视图。本机有已迁移的原制作场景时优先使用它；公开克隆没有本机内容时回退通用制作场景。静态场景菜单只预览、不初始化会话。不要将通用 Stage03 演示当作原制作 UI 已完整还原的证据。

原场景中的输入、参数与列表写入 `PlotEditingSession` 的工作副本。`PlotPlaybackCompiler.Compile(session)` 用工作副本覆盖当前节点，并采用会话文本表，其他节点从同一 Store 读取；试跑与刷新不保存作者资产。`EditorPlotPlayerUI` 将共享 Core/View 嵌入原 `WindowMask`。表格入口显式绑定当前工作区；Excel 读写前处理未保存输入，导入后重新载入编辑会话。编辑与表格不会在每次键入时自动双向同步。

最新范围见 [阶段20](Doc/阶段20_启动与多尺寸适配.md)及独立的 [制作闭环验收](Doc/Records/Current/制作闭环验收.json)：40项定向测试通过；1920×1080、1280×720、1092×905、1024×768的制作操作检查通过。最低工作区1024×600；900×600和720×900仅诊断，仍有密度限制并显示扩大窗口提示，不能写成六尺寸全部通过。[阶段19](Doc/阶段19_制作输入与内嵌试跑.md)的23项结果另有固定归档。

索引的 `evidence.authoringWorkflow` 单独列出当前源摘要匹配、覆盖类型与最小视口；`scope-verified` 只表示指定流程通过，`historical-verified` 表示文件与旧验收记录一致、未重新执行全量回归。此前277项及构建仍是历史版本事实，不能替代新改动的测试结果。

Editor/Game共同契约生成是可选接入支路。`PlotPlaybackCompiler`可经`AuthoringPlotValueSource`直接生成快照；LubanReader的nodeAdapter也可选。不要推断所有入口必须先运行C2生成。

动态类型支持结构数据，不代表任意新增业务字段自动获得播放语义。新语义需投影到标准播放节点，或接事件、表现与资源服务。

章节会话分离加载、播放、结算确认及返回；离线账本与受控远端接入实现已提供。生产Transport、原道具库及区域权限仍由宿主注入。

旧GraphView/固定七表是历史原型；现行主链为UGUI/六表/共享Core。不要把两者混为同一套工作区或交换格式。

## 来源与边界

设计沿革与当前实现是两条独立轴。原数据模型、接口式双端生成、六表往返等保留原设计主线；当前独立代码由本次AI协作迁移、重写和补齐。用户对原整体80%本人/20%AI的陈述不分摊成逐类、逐文件或逐行比例。来源待确认不等于功能未实现。

公开演示使用合成Stage18。`Assets/PlotWeaveLocal`和`Tools/PlotWeaveMigration/Inputs`是本机原内容验证区；完整9场景构建也包含原内容。不要把这些本机内容当作公开示例。

历史源工程的Windows和Android构建已验证；Android/iOS真机未验收，iOS构建未验证。公开源码采用MIT，第三方资源保留原许可，见[第三方声明](THIRD_PARTY_NOTICES.md)。完整本地原数据、原布局截图与9场景构建不随仓库分发。

## 更新评审资料

```sh
python Tools/Review/build_index.py
python Tools/BuildReviewPresentation.py --root .
python Tools/Review/query.py verify
```

精选关系输入为`Doc/Review/architecture-curation.json`；来源输入为`Doc/Review/provenance.json`。代码变更后核对真实声明、签名、关系及行号；生成器会拒绝失效声明锚点和悬空关系。实现、测试与构建入口见[快速上手](Doc/快速上手.md)及分阶段档案。
