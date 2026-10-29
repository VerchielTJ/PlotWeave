# 阶段12：嵌套Bean全链补齐

新增beanType显式绑定。PlotBeanLayout将有界嵌套结构展开成叶成员顺序，保存于原列→行→字符串成员结构；值源恢复嵌套对象，Editor/Game生成契约保留类型。支持标量Bean、Bean列表及多层嵌套；循环类型、悬空引用和超过64层拒绝。

表头members现在对应叶成员数，默认值、行校验、UGUI参数界面、Inspector、Excel元数据、物理成员合并及正反导入均接入同一布局。事件可在传入类型资产后初始化嵌套默认参数，运行时通过ReadBean按同一顺序恢复值源。

真实Luban4.10.2已生成并编译嵌套C#和二进制；Unity中读取后经生成Game适配器验证嵌套float/bool及重复Bean列表。194项联测通过，证据见Records/Stage12/嵌套类型首轮验证.json。样例在Assets/PlotWeaveMigration/Stage12_Nested。

这是数据/生成链路的验收；原UI重建和全部窗口人工体验仍在后续进行。此前文档中“递归Bean未绑定”的限制属于阶段10及更早状态，当前有界嵌套已实现，不把无限递归类型当成可存储的有限行。
