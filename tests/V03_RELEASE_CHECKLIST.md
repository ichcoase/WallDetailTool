# V0.3 梁下下挂／板下下挂验收记录（2026-10-26）

## 交付证据

- 源码基线：`a0497ee`，V0.2 归档。V0.3 源码提交标识见 Git 中提交 `Implement V0.3 beam and slab hanging nodes`；本清单随该提交一同保存，最终交付回复提供实际短哈希。不打标签、不自动推送。
- 用户本轮确认：梁下与梁上、板下与板上分别为连接面上下翻转；此依据覆盖旧路线图。参数、构造关系及点筋规则沿用，文字须正向。本轮代码已实现，CAD 实测与用户验收未完成。
- 最终 DLL：`D:\Waste\WallDtailTool\bin\V03Delivery20261026\WallDetailTool.dll`。
- SHA-256：`2A9863DF25FF8E9A61D254C25E1841A71A4B1C65B4D1193A293DA04DD5611AF1`。
- 本次使用 VS2022 MSBuild、AutoCAD 2023 托管程序集，Release/x64/.NET Framework 4.8 编译成功；输出目录只含插件 DLL/PDB，CAD 引用 Private=False。AutoCAD 2024 本轮未编译/运行。
- 用户原 `docs/` 未纳入本次修改/提交；源 DWG 未覆盖。测试使用 `tests/V03_CAD_TestCopy.dwg` 副本，不提交 DWG、产物、配置、原始日志。

## 本次实际自动测试

六个独立纯计算 EXE 均以本次源码重编译并执行，退出码均为 0，输出位于 `tests/bin/V03Verification/`（不提交）。板筋测试编译器保留原有两个未赋值字段警告 CS0649；不是插件编译警告或测试失败。

| 测试项目 | 实际输出 |
| --- | --- |
| UpstandGeometryStage1Tests | UPSTAND_STAGE1_GEOMETRY_PASS |
| SlabUpstandGeometryTests | SLAB_UPSTAND_STAGE1_GEOMETRY_PASS |
| UpstandRebarStage2Tests | UPSTAND_STAGE2_GEOMETRY_PASS |
| SlabUpstandRebarTests | SLAB_UPSTAND_STAGE2_REBAR_PASS |
| UpstandAnnotationStage3Tests | UPSTAND_STAGE3_ANNOTATION_PASS |
| HangingNodeTests | HANGING_NODE_GEOMETRY_PASS checks=2974 |

新增测试遍历连接面 0／725／−420、下挂高度 150／500、局部矩形／开放 U／完整截面，输入打乱且交替反向；核对源线段副本不变、候选点选转换、左右 350／470 锚固向上、板筋 L左250／L右420、H40、加厚后 H 不变。逐顶点对比上下布局 X/Y/bulge、弦长/弧长不变和四分之一圆弧半径及切点坐标差；逐点核对四类点筋数量和中心位置；核对保护层、宽度、显示直径及左右越界139／309与继续/取消计算规则。标注覆盖左右引出、单排3φ6/三分支、多排间距、文字高于引线4 mm、单排文字处于下挂下方及高度尺寸排序。拒绝 H＝h、R空间不足、负锚固、角部点筋放不下和斜线输入。既有五组继续覆盖其完整非法参数、相切/间距与识别回归。

这些是纯计算证据，不覆盖 CAD 实体的实际显示、段宽、点筋闭合或数据库事务。新增 CAD 适配静态核对：线筋 AddVertexAt 的起终宽度均取 layout.LineWidth；点筋仍由原 UpstandDot 生成 Closed=true、两半圆宽多段线；DBText 未调用镜像变换，只作块局部平移。新块类型3/4及独立 NOD 键与旧记录分离；失败/取消在提交前返回，源对象仅 OpenMode.ForRead。这些均不能代替以下动态项目。

## CAD 环境及未验证项

2023 Core Console 版本 `T.161.0.0`。本次在副本上用审查后的 `v03_smoke.scr` 尝试 NETLOAD/WDTTEST，使用 `/isolate WDT_V03 tests/profile_v03`。实际启动退出码1，报 `Can't open HKCU ... AutoCAD/R24.2 ... (5)` 及 `Failed to create registry keys or files necessary when using the /isolate switch.`，未执行插件命令。原始本机日志 `tests/v03_cad.log` 不上传。未修改全局信任或安全设置。

computer-use 只检查了 AutoCAD 2023 图形窗口，标题 Drawing1.dwg，命令历史可见旧 V0.2 DLL 路径；没有关闭/重启该进程、没有在该图执行新版生成或保存。故没有本次新版 CAD 运行通过证据。2024 对应依赖/可执行文件未找到，未运行。用户对旧 V0.2 的“基本没问题”不作为 V0.3 验收。

- [ ] 两个新工具条入口均打开正确窗口，取消不选轮廓/存参数；高级设置立即应用。
- [ ] 两类分别覆盖局部闭合矩形、开放 U、完整截面，乱序/反向、多候选点选及取消。
- [ ] 与上反坎对照的关键坐标、长度、保护层、起终宽度、bulge、圆弧相切、闭合点筋及间距；300宽端部三根。
- [ ] TEXT 正向且在引线上方、尺寸正向、四层、普通块/BEDIT及连接面基点。
- [ ] 完整板左右/双侧越界继续不截短，取消无残留；局部板沿用无提示规则。
- [ ] 非法参数、空间不足及各阶段取消无新块/图元/参数残留，原轮廓不变。
- [ ] 每类一次 UNDO/REDO，保存新测试 DWG 后重开，块和参数恢复；四类型上次值互不覆盖，多文档不串值。
- [ ] V0.1/V0.2 原命令和按钮的实际 CAD 回归；AutoCAD 2024；用户验收。

## 一个用户验收流程

在方便时先保存自己的工作并自行重启 AutoCAD，NETLOAD 上述 DLL，打开新毫米图，运行 WDTTOOLBAR。画两处300宽、150高的下挂局部轮廓（上端为连接面），依次点“梁下下挂”（左/右锚固350/470）和“板下下挂”（h200、H40、L左250/L右420）。检查钢筋向上进入构件、板筋外弯、端部三点及3φ6正向文字；每类各做一次撤销/重做，再另存新测试图并重开，重开两个窗口核对各自参数。不要覆盖原用户图。其他边界项按上表继续专项验证。

## 限制及下一阶段条件

只支持既有正交二维/毫米输入及等厚水平板；局部轮廓没有板左右边界。没有结构规范自动结论、La推荐、设计参数自动调整、复杂板/悬挑或关联更新。继承标注逐项失败时保留有效钢筋并报告未完成项的策略。完成上述 CAD 验收及反馈修正后，再确定后续范围；本次交付不是正式发布验收完成。
