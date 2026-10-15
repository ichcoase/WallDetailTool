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
