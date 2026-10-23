# V0.2 阶段 2：钢筋几何与生成检查清单

测试命令 `WDTUPSLABREBARTEST` 仅用于阶段 2 验证：创建独立钢筋实体，不创建标注、普通块或参数记录，也没有工具条按钮。`WDTUPSLABCONTOUR` 仍是只读识别命令。

## 测试图

本机 Core Console 在设置 current profile 时失败，无法将案例脚本实际执行并保存为 DWG。`v02_stage2_full_fixture.scr` 和 `v02_stage2_local_fixture.scr` 定义了真实的板上反坎 LINE 完整截面与 LWPOLYLINE 局部轮廓。**这两个脚本尚未经 CAD 执行，不能算作已生成或已验证的测试 DWG。** 在 AutoCAD 图形界面中分别对新建空白图运行脚本并另存为测试 DWG，切勿在用户原图运行；随后使用这些 DWG 的副本验收。现有 `WallDetailTool_V02_Stage1_TestCopy.dwg` 仍只是 V0.1 图副本，不作为板上反坎通过证据。

## 自动测试

- [x] `SlabUpstandRebarTests`：局部矩形／倒 U、完整截面、默认和分别设置 L、H 随板底移动、不同 R、四段圆弧切线、点筋位置、非法几何、左右越界量和确认决策。
- [x] `SlabUpstandGeometryTests`：V0.2 阶段 1 回归。
- [x] `UpstandGeometryStage1Tests` 与 `UpstandRebarStage2Tests`：V0.1 回归。

## AutoCAD 2023／2024（分别记录）

- [ ] 在真实完整截面 DWG 副本运行 `WDTUPSLABCONTOUR`：板顶 300、板底 0、厚 300、左右边界 0／1000；局部图运行该命令并输入 h，板界未知。
- [ ] `WDTUPSLABREBARTEST` 生成单条连续宽 LWPOLYLINE，四处相切圆弧；点筋是独立闭合 LWPOLYLINE，间距和图形宽度正确。
- [ ] 默认 L左／L右 300 mm，分别修改后从各自水平切点量取；变更板厚时 H 数值不变。
- [ ] 完整截面左、右越界分别显示实算值，取消不生成；继续不截短。局部模式提示人工审核，取消不生成。
- [ ] 非法 h/H/L/R 或不可能的圆弧，均不留下钢筋、空块、参数记录；源混凝土实体句柄与几何不变。
- [ ] 一次 UNDO 撤销本次钢筋；REDO 可恢复。与 V0.1 梁上反坎命令做结果对照。

2026-10-23 本轮再次尝试 AutoCAD 2023 Core Console，以测试图副本和完整截面案例脚本启动；退出码 1，`acad.err` 报 `Problem with setting up current profile.`，脚本未执行，未形成真实板上反坎 DWG。2023 图形界面、2024、CAD 运行及用户验收仍待验证。不得把编译和纯几何测试记作 CAD 验收。

用户在图形界面报告 `WDTUPSLABREBARTEST` 生成时报 `eKeyNotFound`。源码复查发现新实体先指定图层、事务后创建图层，与 V0.1 的顺序相反；已调整为同一事务内先建图层再创建钢筋。修复版编译及四组纯几何回归通过；**CAD 中该报错是否消失、实体和撤销行为仍待用户用修复版 DLL 复测**。
