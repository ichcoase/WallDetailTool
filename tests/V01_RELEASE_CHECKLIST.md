# V0.1 稳定基线复核（2026-10-23）

本清单以本次源码和 DLL 为准。用户表示 V0.1 实际使用效果“基本认可”，这是整体使用反馈，不等于下表每项已在本轮 CAD 实测。阶段 1–4 清单保留原始逐项记录；其中旧版预览和侧边栏预期已被 2026-10-22 的最新界面决定取代。

| 功能 | 本次证据 | 仍需实际验收 |
| --- | --- | --- |
| 三类轮廓、1 mm 容差、异常拒绝、源轮廓只读 | 阶段 1 纯几何测试 PASS；源码检查 | AutoCAD 点选/框选与测试 DWG 实测待验证 |
| U 形线筋、点筋、参数与相切布置 | 阶段 2 纯几何测试 PASS；用户曾反馈阶段 2 绘图正常 | AutoCAD 2023/2024 本轮回归待验证 |
| 钢筋文字、引线、宽高尺寸及图层 | 阶段 3 纯布局测试 PASS；用户曾反馈阶段 3 外观已调到预期 | 块内外观、尺寸实际测量待验证 |
| 每批一个普通块、BEDIT/COPY/MOVE/ERASE/EXPLODE | 源码检查、Release/x64 编译 | AutoCAD 实际操作待验证 |
| 双按钮工具条、两个独立弹窗、确定/取消、持久化 | 源码检查、Release/x64 编译 | 图形界面与重启后恢复待验证 |
| 直接生成，无钢筋生成预览 | 源码检查正式绘制路径 | AutoCAD 操作待验证 |
| 关键失败回滚、非关键标注部分成功 | 源码检查 | AutoCAD 故障注入待验证 |
| 一次 UNDO/REDO、保存重开 | 源码含 UNDO 组和单次事务 | AutoCAD 实测待验证 |
| WDTTEST、WDTREBAR 与旧命令 | 命令注册源码、编译 | 本轮 CAD 回归待验证 |

## 本轮操作清单

在 `WallDetailTool_V01_Stage4_BlockPanel.dwg` 的**副本**中 `NETLOAD` 交付 DLL：① `WDTTOOLBAR`，分别点“反坎绘制”和“高级设置”，检查确定、取消和重启持久化；② 分别选择完整截面、闭合矩形、开放倒 U，生成后检查钢筋、标注和单个普通块，确认没有预览确认提示；③ 尝试非法轮廓或过大 R、取消参数输入，确认无半成品；④ COPY、BEDIT、MOVE、EXPLODE、ERASE，并用一次 UNDO/REDO 恢复生成；⑤ 保存副本、重开检查块和参数；⑥ 运行 `WDTTEST`、`WDTREBAR`。逐项记录 CAD 版本和结果。不要覆盖标准 DWG 或用户图纸。

本轮纯测试：`UPSTAND_STAGE1_GEOMETRY_PASS`、`UPSTAND_STAGE2_GEOMETRY_PASS`、`UPSTAND_STAGE3_ANNOTATION_PASS`。AutoCAD 2023/2024 的新版本命令和图形界面本轮尚无通过证据。测试 DWG 均在本机 `tests/`，由 `.gitignore` 排除，不上传 Git。

AutoCAD 2023 Core Console 曾以阶段 4 测试 DWG 的副本尝试启动本次冒烟脚本；首次命令因 `/isolate` 参数格式不符合该版本要求而在加载 DWG 前退出（exit -1），故没有插件运行结果。未改变全局信任或安全设置。AutoCAD 2024 可执行文件在约定路径未找到；图形界面未自动操作。
