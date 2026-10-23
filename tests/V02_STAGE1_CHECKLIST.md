# V0.2 阶段 1：板上反坎几何识别检查清单

基线：`v0.1.0`；新命令：`WDTUPSLABCONTOUR`。本阶段只识别，不生成钢筋、标注或块。测试必须使用新图或 `WallDetailTool_V02_Stage1_TestCopy.dwg` 副本；不要保存覆盖原 DWG。

## 纯几何

- [x] 新测试 `SlabUpstandGeometryTests`：局部矩形、开放倒 U、完整等厚楼板各字段、反向拆段、1 mm 容差、多候选、无效宽度及板厚、板顶降板／阶梯底变厚／斜板拒绝。
- [x] 原 `UpstandGeometryStage1Tests` 仍通过，V0.1 纯几何结果不变。

## AutoCAD 2023 与 2024（分别记录）

- [ ] 在副本中分别用 LINE、LWPOLYLINE、混合图元构造局部矩形、开放倒 U、完整等厚截面；命令输出的板顶、板底、厚度、左右板界正确。局部模式的板界显示未知。
- [ ] 反向画线、拆段及接点 1 mm／超过 1 mm，结果符合纯几何测试。
- [ ] 同一完整截面含两个反坎时，必须点选目标；取消点选不产生图元。
- [ ] 降板、变厚、斜板、复杂截面和非同一 WCS 标高明确拒绝。
- [ ] 非毫米 INSUNITS 仅提示，按 1 CAD 单位 = 1 mm 识别，不更改 INSUNITS。
- [ ] 命令前后比较源实体数量、句柄和几何；命令不创建／修改／删除 DWG 实体。
- [ ] `WDTUPCONTOUR`、`WDTUPDRAW` 与现有“梁上反坎”按钮按 V0.1 清单回归；板上反坎绘制按钮尚未加入。

CAD 实测记录（2026-10-23）：尝试以 AutoCAD 2023 Core Console 打开测试副本并执行识别脚本，控制台在设置 current profile 时退出（exit code 1），尚未加载 DLL 或执行命令。2023 图形界面及 2024 均待验证。未更改全局安全／信任设置。测试图副本由 `tests/WallDetailTool_V01_Stage1_Contour.dwg` 复制而来，需在该副本中绘制板上反坎案例；它当前不是已经验收的板上反坎测试图。
