# 2026-10-15 阶段1＋2验证记录

源码：本地首次基线 3f95428；本次修改提交号以Git仓库记录为准。编译目标：.NET Framework 4.8 / x64，默认引用AutoCAD 2023托管程序集。

交付DLL：`D:\Waste\WallDtailTool\bin\Phase2\WallDetailTool.dll`。SHA-256：`15888936A75D8E8F332AE59286FB517E3C79056E4804A9B889AF4741BC32C447`。

|检查项|AutoCAD 2023|AutoCAD 2024|证据与范围|
|---|---|---|---|
|几何方向、反向轮廓、无效几何、旧式二维多段线|通过|通过|隔离控制台 `WDT_GEOMETRY_TESTS_PASS`|
|默认宽度12、中心线偏移31、原轮廓不变|通过|通过|`WDT_COMMAND_TEST_PASS`、`WDT_DRAWN_WIDTH_PASS`|
|米单位宽度0.012、偏移0.031|通过|通过|几何测试内的原生实体属性检查|
|撤销新钢筋后保留原轮廓|通过|通过|`WDT_UNDO_PASS`|
|WDTTEST回归|通过|通过|`WallDetailTool loaded successfully!`|
|侧边栏显示、按钮点击、预览颜色与宽度、取消/关闭后清理、多文档切换|待验收|待验收|图形界面启动审批超时，本次未运行实际界面|
|不同直径、非法输入、用户手动取消、旋转坐标系及宽度视觉效果|待验收|待验收|需在实际图形界面补充|

末次编译后的日志保存在本机 `tests/final_2023.log` 与 `tests/final_2024.log`，未上传GitHub。脚本使用 `/isolate` 配置和新图，未覆盖用户DWG。

界面验收建议：重启AutoCAD后NETLOAD新版DLL，在新图画一条开放直线多段线，输入 `WDTPANEL`，使用默认25/12预览并确认；检查生成钢筋宽度12和中心线偏移31，随后撤销。再试一次“预览→取消”，确认图中没有留下钢筋。

## 2026-10-15 阶段3首版层级与DWG关联

目标仍为 .NET Framework 4.8 / x64；使用本机 AutoCAD 2023 托管程序集编译，输出 `D:\Waste\WallDtailTool\bin\Phase3\WallDetailTool.dll`。SHA-256：`9294AF8F790B66E8D5696B6D04F887BA46D1767B74061EA98E073D7F040775D9`。源码提交标识见本阶段最终交付记录。

|检查项|AutoCAD 2023|AutoCAD 2024|证据与限制|
|---|---|---|---|
|几何回归、WDTTEST|通过|通过|`WDT_GEOMETRY_TESTS_PASS`、成功加载消息；阶段1宽度命令回归沿用上节记录|
|节点、组、钢筋关联创建及组默认参数编辑|通过|通过|`WDT_STORAGE_CREATE_PASS`、`WDT_STORAGE_VERIFY_PASS`|
|未知schemaVersion拒绝读取/写入，回滚后数据不变|通过|通过|`WDT_STORAGE_GUARD_PASS`|
|删除标记、重做删除、撤销恢复|通过|通过|两次 `WDT_STORAGE_MISSING_PASS`，最终 `WDT_STORAGE_VERIFY_PASS`|
|测试DWG保存并由新控制台进程重新打开|通过|通过|`phase3_reopen_2023.log`、`phase3_reopen_2024.log` 中 `WDT_STORAGE_VERIFY_PASS`|
|独立数据库不共享节点|通过|通过|`WDT_STORAGE_CREATE_PASS` 测试内部检查|
|真实侧边栏树、按钮、预览、确认与图中定位|待用户验收|待用户验收|隔离控制台没有图形界面；不能用编译或存储测试替代|
|另存、多活动图纸切换、损坏记录、复制/跨图复制|待验证|待验证|复制规则尚未确定；本阶段没有声称支持|

隔离日志在本机 `tests/phase3_final2_2023.log`、`tests/phase3_final2_2024.log`、`tests/phase3_reopen_2023.log`、`tests/phase3_reopen_2024.log`，均未上传。测试只操作新建及专用测试 DWG，未覆盖用户图纸。测试控制台退出时可出现临时日志文件无法删除的提示，不影响上述明确通过标记；不据此推断图形界面已通过。

阶段3交付后，用户反馈“上一步正常”；这是用户对基本操作的确认，并非本次对新版图层与修改界面的验收。

## 2026-10-15 阶段4首版钢筋修改与目标图层

目标 .NET Framework 4.8 / x64，引用本机 AutoCAD 2023 托管程序集；最终 DLL：`D:\Waste\WallDtailTool\bin\Phase4\WallDetailTool.dll`，SHA-256：`EE7ABC6E24A71C5DD16BA3A2FB8FE88B10ABDC5A0AADD2DBEC94865C324F03EF`。源码提交标识见本次交付。

|检查项|AutoCAD 2023|AutoCAD 2024|证据与限制|
|---|---|---|---|
|原生几何回归、WDTTEST|通过|通过|`phase4_final2_2023.log` / `phase4_final2_2024.log` 中 `WDT_GEOMETRY_TESTS_PASS` 与成功加载消息|
|新钢筋记录源轮廓、方向、宽度及目标图层|通过|通过|`WDT_EDIT_CREATE_PASS`；图层由测试事务新建|
|保护层25→30、直径12→16，中心线31→38，图层变更，源轮廓不变|通过|通过|`WDT_EDIT_APPLY_PASS`|
|CAD直接改路径后，仅改图层保留路径并记为自由编辑|通过|通过|`WDT_EDIT_DIRECT_PASS`、`WDT_EDIT_LAYER_ONLY_PASS`|
|一次撤销/重做同时还原实体与关联|通过|通过|`phase4_fundo_*.log` 中 `WDT_EDIT_UNDO_PASS`；`phase4_fredo_*.log` 中 `WDT_EDIT_REDO_PASS`|
|保存并在新隔离进程重开测试DWG|通过|通过|`phase4_fopen_*.log` 中 `WDT_EDIT_VERIFY_PASS`|
|读取阶段3旧图，首次修改时升级版本1→2|通过|通过|`phase4_old_*.log` 中 `WDT_STORAGE_VERIFY_PASS`；`phase4_migrate_*.log` 中 `WDT_OLD_MIGRATE_PASS`，旧测试DWG未被覆盖|
|真实面板点击、红色预览及取消清理、新图层创建询问|待用户验收|待用户验收|隔离控制台无法替代图形界面|
|实际多文档切换、另存、批量和实时关联更新|待验证/未实现|待验证/未实现|切换时清理预览及重置目标图层已有代码；尚无图形界面验证|

测试使用新图和 `tests` 下专用DWG，不修改用户图纸。自动日志与测试DWG保留在本机且由 `.gitignore` 排除，不上传GitHub。层级/编辑/图层代码均已写入，以上自动测试不等于结构设计审核或新版界面验收。

## 2026-10-16 直径文字标注首版

目标 .NET Framework 4.8 / x64，引用本机 AutoCAD 2023 托管程序集；最终 DLL：`D:\Waste\WallDtailTool\bin\Phase5\WallDetailTool.dll`，SHA-256：`C00C30E00DD3FB5BBC3D3A3C569652BCE83D14F0D17EE07D6EC111C32FEC2E93`。源码提交标识见本次交付。

|检查项|AutoCAD 2023|AutoCAD 2024|证据与限制|
|---|---|---|---|
|最终DLL几何回归、WDTTEST|通过|通过|`phase5_final2_2023.log` / `phase5_final2_2024.log` 中 `WDT_GEOMETRY_TESTS_PASS` 与成功加载消息|
|直径文字、引线、样式名、字高62.5和指定图层|通过|通过|同上日志 `WDT_LABEL_CREATE_PASS`；隔离测试图仅创建名为TSSD的测试文字样式，未验证真实字体外观|
|文字Φ12→Φ16，钢筋直径记录、宽度和中心线不变|通过|通过|`WDT_LABEL_EDIT_PASS`、`WDT_LABEL_VERIFY_PASS`|
|撤销/重做同时还原文字与关联记录|通过|通过|`phase5_label_undo_*.log` 中 `WDT_LABEL_UNDO_PASS`、`phase5_label_redo_*.log` 中 `WDT_LABEL_REDO_PASS`；服务代码在最终UI提示调整前通过|
|保存专用DWG，并在新隔离进程重开|通过|通过|`phase5_label_reopen_*.log` 中 `WDT_LABEL_VERIFY_PASS`；服务代码在最终UI提示调整前通过|
|删除钢筋后的关联状态、独立图纸隔离|通过|通过|`phase5_delete_*.log` 中 `WDT_LABEL_DELETE_PASS`；`WDT_LABEL_CREATE_PASS` 内检查独立数据库不含标注。当前保留标注并提示待处理，不等于自动清理|
|旧版本1、2关联读取及版本1升级到3|通过|通过|`phase5_migrate_*.log` 中 `WDT_OLD_MIGRATE_PASS`、`phase5_schema2_*.log` 中 `WDT_EDIT_VERIFY_PASS`；旧测试DWG未被覆盖|
|真实面板点击、临时预览/取消、TSSD真实字形、样图布局及多活动图切换|待用户验收|待用户验收|隔离控制台不能替代图形界面；暂无用户样图DWG，仅参照截图|
|复制、自动清理、标注跟随钢筋、点筋及项目统一显示宽度|未实现|未实现|复制规则尚待用户确认；本次不声称支持|

测试仅使用新建图及专用测试DWG，不修改用户图纸。日志、测试DWG及隔离配置均由 `.gitignore` 排除，不上传。TSSD_25尺寸样式和S-THIN标高图层不用于本次直径文字，未检验其效果。
