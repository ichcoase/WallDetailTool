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
