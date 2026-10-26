# V0.3.1 统一节点入口交付与验收清单（2026-10-26）

## 用户验收与收尾归档（2026-10-26）

用户已确认完成 V0.3.1 交付验收流程，通过验收。依据为本轮用户明确确认，范围为：

- [x] “梁节点”“板节点”两个统一入口。
- [x] 梁/板窗口的上下方向切换。
- [x] 切换方向保留各自草稿，取消后丢弃草稿。
- [x] 梁上反坎、梁下下挂、板上反坎、板下下挂四类节点生成。
- [x] 重开窗口切换方向，核对四类各自的上次成功参数。

本次用户未另行提供 CAD 版本，不将验收推定为 AutoCAD 2023 或2024版本专项。此前版本证据仍是 AutoCAD 2023 引用编译及隔离控制台启动失败；2024仍未验证。该用户验收与下文历史自动测试、代理 CAD 自动运行结果分别记录，不扩大为全部异常、多文档、保存重开/撤销重做或持久化专项通过。

本轮仅文档收尾，核对源码提交 `d2370c2c288a57cd7782464a4843df2be4a1d630` 和原交付 DLL 路径/哈希一致；不改功能源码，不重新编译或重跑测试。按本轮明确授权，将该已验收源码及收尾文档归档为附注标签 `v0.3.1`，标签目标为本次收尾文档提交。可用 `git rev-parse v0.3.1^{commit}` 查询标签对应提交；实际提交及推送结果见最终交付和远端引用。

首次远端核对：origin 为既有 `https://github.com/ichcoase/WallDetailTool.git`，main 为 `9a5800e795edd72810be9f206f6b45232e96e611`，无 v0.3.1；v0.3.0 仍指向同一提交。推送前再次检查，存在分歧或同名标签则停止报告，不强推、不移动或覆盖已有标签、不改变可见性。仅提交相关收尾文档，原 docs/ 保留，不纳入 DWG、DLL、程序集、配置、凭据、截图或原始日志。归档后停止，不自动启动下一版本。

## 基线与范围

基线 `v0.3.0` 指向 `9a5800e795edd72810be9f206f6b45232e96e611`，未移动已有标签。本轮仅统一操作入口、窗口草稿、完整请求及成功参数记忆。梁上/下共用梁计算，板上/下共用板计算；`UpstandRebarGeometry.cs`、`SlabUpstandRebarGeometry.cs`、`UpstandDotDistribution.cs`、`UpstandAnnotationGeometry.cs`、`NodeOrientation.cs` 与该标签无差异。不改几何、点筋、标注或生成事务策略，不复制独立下挂算法。

V0.3 两类下挂的交付流程已由用户确认通过，范围见 [V0.3 记录](V03_RELEASE_CHECKLIST.md)。用户未另行明确验收所用 CAD 版本，不推定。旧版验收不等于新版窗口/调度或未逐项边界专项已验收，2024 仍未验证。

## 本次交付产物

- 最终 DLL：`D:\Waste\WallDtailTool\bin\V031Delivery20261026Final\WallDetailTool.dll`。
- SHA-256：`ECDE4515CE565CAB3BFB255362C9C96F251D79A0E08825DA5C872C934C0D31FD`。
- Release/x64/.NET Framework 4.8，VS2022 MSBuild，引用本机 AutoCAD 2023 的 AcMgd/AcDbMgd/AcCoreMgd；检查 build.ps1 后显式指定新输出目录。输出仅插件 DLL/PDB，CAD 引用 Private=False。
- 功能源码及开发文档提交：`d2370c2`，`Unify beam and slab node drawing entrypoints for V0.3.1`；本轮收尾文档另行提交。
- 原有未知 docs/ 保留且不纳入提交；DWG、DLL、程序集、测试配置、截图与原始日志不提交。开发交付时未推送或打标签，本轮按用户另行授权归档推送 v0.3.1。

## 开发交付时的实现与证据（用户验收范围见上方）

| 项目 | 开发交付时取得的证据 | 开发交付时待 CAD 验证（本轮用户通过范围见顶部） |
| --- | --- | --- |
| 梁节点、板节点、高级设置 | 工具条源码恰有3个按钮，命令 WDTBEAMNODE/WDTSLABNODE；高级设置复用原命令 | CAD工具条实际点击及高级设置 |
| 同一梁/板窗口切换上下方向 | NodeDrawForm 原生 WinForms；下拉、标题、说明、H、示意及生成按钮一致 | CAD内字体/DPI/模态窗口、实际选择流程 |
| 四个旧窗口命令预选方向 | 静态映射核对及窗口两种初始方向测试；原28命令保留，新增2命令，无重名 | CAD内命令实际启动及快捷入口 |
| 两方向窗口草稿 | 原始文字克隆保存，切换保留空白及无效编辑；取消/关闭后新窗口恢复成功记录种子 | CAD连续开窗与取消后无选择 |
| 完整一次性请求 | NodeDrawRequest 克隆设计与高级参数，类型/方向只读；局部闭包传目标文档，执行前复核；无全局pending字段或请求命令字符串 | ExecuteInCommandContextAsync 实际CAD执行、取消、活动文档切换和重入 |
| 四类型成功参数独立 | 沿用四个独立 NOD 键，开窗只读，成功事务原位置写入；板上3/4项兼容、双方8项解码纯测试 | 实际DWG写入/撤销重做/保存重开/多文档 |
| 原计算及构造 | 五个共用算法/方向源码与v0.3.0无差异；原六组实际回归PASS | 新UI请求触发四类真实生成 |

新统一入口每次默认向上，没有新增“最后方向”的持久设置。旧窗口入口 WDTUPFORM/WDTPANEL/WDTUPDRAW/WDTUPPANELDRAW 预选梁上，WDTBEAMHANGDRAW 预选梁下，WDTUPSLABDRAW 预选板上，WDTSLABHANGDRAW 预选板下。旧内部 WDTUPFORMDRAW/WDTUPSLABFORMDRAW 改为对应向上窗口别名，不重放残留请求；旧识别、WDTUPSLABREBARTEST、WDTTEST、WDTREBAR 等仍在。

板节点 H 提示向上为“板底至水平筋中心线距离 H”，向下为“板顶至水平筋中心线距离 H”；仅表达调整。基础正数/有限数校验在提交请求前完成，右侧空白随左侧。局部h可先留空供完整截面识别；是否局部、H<h及圆弧/空间约束仍需选轮廓后由原核心校验，不在窗口假定完整截面的板厚。取消窗口不生成请求，不写任何记录。

## 历史开发交付时实际运行的测试（收尾未重跑）

开发交付时，所有下列测试均以当时源码重新构建并执行，退出码0；本轮仅核对记录，未重新编译或运行：

| 测试 | 实际输出 |
| --- | --- |
| UpstandGeometryStage1Tests | UPSTAND_STAGE1_GEOMETRY_PASS |
| SlabUpstandGeometryTests | SLAB_UPSTAND_STAGE1_GEOMETRY_PASS |
| UpstandRebarStage2Tests | UPSTAND_STAGE2_GEOMETRY_PASS |
| SlabUpstandRebarTests | SLAB_UPSTAND_STAGE2_REBAR_PASS |
| UpstandAnnotationStage3Tests | UPSTAND_STAGE3_ANNOTATION_PASS |
| HangingNodeTests | HANGING_NODE_GEOMETRY_PASS checks=2974 |
| NodeDrawWindowTests | NODE_DRAW_WINDOW_PASS checks=158 |

六组计算测试通过 `tests/run_v03_geometry.ps1 -OutputPath bin\V031Regression\` 执行；板筋测试保留既有两条未赋值字段 CS0649 警告，不是测试失败。独立窗口项目通过 VS2022 MSBuild 重建 `tests/NodeDrawWindowTests.csproj`，再运行 `tests/bin/V031Window/NodeDrawWindowTests.exe`。插件最终编译无警告/错误。

NodeDrawWindowTests 实际实例化原生窗口并显示/隐藏以绘制截图，自动操作控件：梁/板两种初始方向，向上/向下标题、按钮、H、描述和示意方向；切换时无效文字/空白保留，右侧留空随左侧，负数/0/NaN/Infinity拒绝；高级参数及确认请求快照不随调用者数组/后续编辑变化；关闭/取消按钮不提交，重新打开丢弃草稿，生成按钮提交所选类型/方向。另验证板上旧3/4项及双方8项记录解码、截断/非法规格拒绝，下挂不误读上反坎旧格式。该程序不引用CAD，成功记录是独立种子，不代表DWG实体或真实保存/撤销通过。

本轮四种窗口 PNG 在 `tests/bin/V031Window/beam_up.png`、`beam_down.png`、`slab_up.png`、`slab_down.png`，已逐张视觉检查；绘图区域实际绘出钢筋颜色也有自动断言。截图/EXE不提交。初次隐藏窗截图没有子控件，已修正为实际显示后渲染并重新运行，最终检查的是完整窗口。

额外源码检查输出 `V031_ENTRYPOINT_STATIC_PASS old_commands=28 current_commands=30`：比对v0.3.0全部根目录命令，无删除或重名；工具条3按钮、旧四个窗口方法固定方向、无全局pending请求和SendStringToExecute转交、存在目标文档复核。此项是静态检查，不作为CAD命令运行通过证据。`git diff --check`及旧式csproj Compile列表已检查。

## CAD 环境实际结果与未验证项

此前开发交付时，AutoCAD 2023 Core Console `T.161.0.0` 在测试DWG副本 `tests/bin/V031CadSmoke/InputCopy.dwg` 上以 `/isolate WDT_V031 .../profile` 尝试加载最终DLL并运行WDTTEST。实际退出码1，报无法打开 HKCU 的 AutoCAD/R24.2/ACAD-6101:804 根键（5）、无法创建隔离所需注册表键或文件；NETLOAD与插件命令未执行。原始本机日志 `tests/bin/V031CadSmoke/console.log` 不提交。未修改全局信任或安全设置，未关闭用户AutoCAD，未覆盖用户DWG。本次用户验收不改变该次自动运行受阻的真实记录。

此前代理未在用户AutoCAD进程中加载新版并生成，不把独立WinForms测试当作CAD宿主运行。本轮新增的是上方明确范围的用户验收通过，不是代理自动CAD运行通过。AutoCAD 2024依赖/可执行文件此前未找到，未编译/运行，本轮仍未验证。以下复合专项尚未逐项完成，包含用户已验证内容的检查项不整体勾选：

- [ ] CAD内两个绘制入口、高级设置、方向切换及DPI/字体显示。
- [ ] 四个旧绘制命令正确预选、旧识别/测试命令CAD回归。
- [ ] 两窗口连续打开/取消/关闭/重开，非法输入不启动选择，无残留请求串用。
- [ ] 四类节点原构造外观的完整专项回归（交付流程中的四类生成及对应方向切换已通过用户验收）。
- [ ] 四类成功参数实际DWG隔离，旧记录兼容，取消/失败不保存；UNDO/REDO、另存重开、多文档及请求目标文档切换。
- [ ] AutoCAD 2024版本专项；超出上述用户验收范围的异常与多文档等专项。

## 已完成的用户验收流程（保留为范围依据）

方便时保存工作并自行重启CAD，NETLOAD本次新DLL，在新毫米图运行WDTTOOLBAR。先在“梁节点”上/下分别填不同锚固长度，切换确认各自草稿、示意和按钮，再取消重开确认草稿丢弃；随后用对应上下轮廓各生成一次。再在“板节点”上下分别填不同H和左右水平长度，核对板底/板顶说明、切换草稿后各生成一次。最后重开两个窗口分别切换上下，确认四类成功参数独立且生成方向正确。不要覆盖原用户图；上述为一个串联流程，其他专项仍按清单记录。

## 限制与下一步

保留既有正交二维/毫米及等厚板范围，局部板无左右边界；无新结构规则或规范结论。V0.3.1 交付流程已通过用户验收，本轮按授权归档 v0.3.1，不新增功能；归档不代表全部异常、多文档或持久化专项完成。完成后停止，下一版本须用户另行启动。
