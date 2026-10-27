# V0.4 梁侧挂板交付清单

## 第二阶段交付（2026-10-27）

基线 a48c90114f9233b489a8010e3852a3fb365e0b38。当前状态：窗口→轮廓选择→计算→实体/标注→普通块→成功参数保存已完成源码接入；新 DLL 含可操作入口，供用户在 CAD 中加载验收。以下自动结果不能代替 CAD 动态验证，未宣称用户验收通过。

- [x] 工具条新增“梁侧挂板”及 WDTBEAMSIDE，旧命令保留；独立窗口包含方向、局部/完整模式、规格间距、L/V、两端世界上下弯折和标注侧，示意同步。首次必须明确选择轮廓模式，L/V 留空；已有成功记录保留上次模式及参数，无虚构工程默认。
- [x] 取消/关闭不启动选择、不写草稿；不可变请求捕获参数、共享高级设置、图层和单位解释，闭包目标文档在命令上下文执行，图纸/单位改变拒绝。
- [x] LINE/LWPOLYLINE 只读读取、平面/Elevation/圆弧检查、输入输出同一比例换算；非毫米必须明确选择换算或本次毫米解释，不改 INSUNITS。点选转 WCS，多候选及完整无缝外框明确选择。
- [x] 只消费已提交计算结果；一条连续线筋，各段起终宽和 bulge 设置；每个返回中心一根闭合点筋，规格不套旧节点根数规则，Unknown 提示人工审核。
- [x] 真实锚点普通引线、正向 TEXT、左右标注、原生宽高尺寸，复用既有样式及图层且不覆盖属性。缺少原生建筑标记时，事务内创建独立 WDT-ARCHTICK，不改变 DIMBLK 或全局设置。
- [x] 世界到块局部只转换一次，基点及 Elevation 保留；源混凝土不入块。关键钢筋/块/快照/LAST 同一父事务，可选标注逐项嵌套回滚并报告，外层 UNDO 分组。
- [x] 本类型 WDT_BEAM_SIDE_LAST、WDT_BEAM_SIDE_PARAMS 独立键，版本化完整解码；高级设置仍沿原系统。成功记录和块几何/类型/单位快照同事务更新，失败/取消不更新。
- [x] 全部新增源码进入旧式 csproj；.NET Framework 4.8/x64 构建，生产输出不含 CAD 托管依赖或测试程序集。

### 本轮自动验证及构建证据

| 检查 | 本轮实际结果 |
| --- | --- |
| V0.4 计算三组 | PASS：轮廓837、钢筋1825、点筋45616项；输出 tests/bin/V04CadCalculation |
| 原五组＋HangingNodeTests | 六组均 PASS，Hanging 2974项；输出 tests/bin/V04CadRegression |
| BeamSideWindowTests | BEAM_SIDE_WINDOW_RECORD_PASS checks=1532，含左右/弯折/标注侧、首次模式明确选择、L/V分别必填、确认/取消重开、请求快照、单位明确选择、字段/坏记录/块快照、目标文档/单位守卫、正向标注位置及比例 |
| NodeDrawWindowTests | NODE_DRAW_WINDOW_PASS checks=158；两窗口测试输出 tests/bin/V04WindowFinal |
| 新窗口图像 | 默认毫米、左右及非毫米必须选择的截图已视觉检查；仅独立 WinForms，不是 CAD 内窗口验收 |
| CAD 专项程序集 | BeamSideCadTests 编译通过；真实执行未完成，不记 PASS |
| 主项目 | Release/x64/.NET Framework4.8，AutoCAD2023托管引用，编译成功 |

纯测试因引用旧 UpstandRebarParameters 类型有既有两条 CS0649 警告（左右锚固字段未赋值），测试通过。计算层源码本轮没有修改或复制；共享窗口源码未改，仍运行旧窗口回归以核对兼容。

最终 DLL：D:\Waste\WallDtailTool\bin\V04Delivery20261027Final2\WallDetailTool.dll

SHA-256：C213293EAAF68E963EA1AF57C7736F5BCD9D4599BD4F9264DF1665F6759EE210

程序集方法/类型检查确认含 ShowBeamSideForm、BeamSideDrawForm、BeamSideDrawRequest 和 BeamSideAnnotationGeometry，主项目 Compile 明确包含 BeamSideCadCommand。不是第一阶段无入口 DLL。源码与本清单同次本地提交，提交标识见最终回复及 git log；不推送、不打标签。

### CAD 实测边界与阻碍

1. 使用新建 tests/profile_v04_integration20261027/V04_CAD_Copy.dwg（来自既有测试图的副本），未覆盖原图、不关闭用户 AutoCAD。
2. 普通权限的 AutoCAD2023 accoreconsole 启动因 HKCU AutoCAD 根键访问错误5退出1；经工具权限机制运行 /isolate 后启动/退出成功。没有手工更改注册表、TRUSTEDPATHS 或 SECURELOAD。
3. 隔离控制台加载 Test2 插件及测试程序集均报告“无法加载…程序集”，WDTBEAMSIDEAUTOTEST 为未知命令；没有 BEAM_SIDE_CAD_PASS，未执行实体/持久化/UNDO 专项。控制台自身返回0不代表测试通过。
4. 同配置加载此前已交付的 V031Delivery20261026Final/WallDetailTool.dll 也失败，WDTTEST 未注册。只读诊断：SECURELOAD=1，TRUSTEDPATHS 不含项目输出目录，findfile 能找到 DLL。加载未提供更详细异常，不能将原因认定为新代码错误或认定只有信任路径一个原因；未通过更改安全设置绕过。
5. 原始日志、测试图和隔离配置保留在上述 ignored 测试目录，不提交。Windows 图形界面及 AutoCAD2024 本轮未运行。

测试专用 [BeamSideCadTests](BeamSideCadTests.cs) 拒绝非 V04_CAD_Copy.dwg：计划覆盖真实宽度/bulge/基点/Elevation、实体数量、两条 TEXT/原生尺寸、块快照与 Last、旧类型键/图层属性、关键失败回滚和可选标注失败、保存重开及一次 UNDO/REDO。可用 [安全测试入口](run_v04_cad.ps1) 在合适环境复制指定 DWG、构建专用程序集后运行；不含关闭安全设置的命令。该计划和编译成功不等同实际通过。

- [ ] CAD 内入口与完整流程；左右、四种弯折、局部/完整、多候选/连接 X 选择和非毫米单位。
- [ ] 实际钢筋与标注外观、BEDIT、原图保护、取消/失败无残留及无参数更新。
- [ ] 一次 UNDO/REDO、保存重开、本类型与旧四类及多文档参数隔离、活动文档切换。
- [ ] AutoCAD2023/2024 的图形界面及用户验收。

### 一个简短用户验收流程

若本会话已加载旧版，请在方便时手动重启 CAD 后再测试。在毫米新图/副本中画一个 300×600 的局部矩形，NETLOAD 加载上方 DLL（若询问可仅本次加载），运行 WDTBEAMSIDE，选“向右/局部”，以 L=400、V=100 作界面和算术试例后选择矩形。核对普通块、两条规格、宽高尺寸和原矩形保留；做一次撤销/重做，另存测试图重开，再开窗口核对本类型成功参数。该数值只是测试，不是工程锚固推荐。左右/完整及其他专项继续按未验证清单验收。

## 第一阶段历史记录（计算层，2026-10-27）

日期：2026-10-27。本轮用户将任务收紧为“计算层完成、主项目正式集成、测试及本地提交”。状态：计算层完成，CAD 入口尚未接入；整体 V0.4 未作为可操作 CAD 功能交付或验收。

## 已实现并自动验证

- [x] 原草稿整理为正式几何、识别、圆弧校验和点筋模块，加入旧式主项目 Compile；测试使用同一正式源码，无草稿第二路径或失效引用。
- [x] 局部矩形/开放 U 左右识别、乱序反向、共线分段、源输入只读和多候选选择。
- [x] 完整规则梁/块体：带缝双矩形或整体外边界，等高/近等高/上下错位、连接范围、缺边/重复外边/分支/复杂及不连通输入拒绝。
- [x] 等高完整外框无连接缝时返回 SplitRegions，由 Resolve(连接 X) 完成明确人工选择及校验，不猜测连接面。
- [x] 左右及两端四种弯折、L/V 中心线展开长、R/bulge/相切、零长度/自交/重叠。
- [x] 显示宽度条带/圆环扇区的解析重叠检查，含圆弧与平端头临界；完整梁真实显示轮廓容纳检查。
- [x] 世界坐标毫米契约，非零/负基点及 Elevation；X、Y 同时正确还原；局部返回 Unknown 状态及人工审核提示。
- [x] 三边开放点筋、端点外缘齐平、必需圆角点唯一、真实内缘相切和实际中心距。
- [x] 端点与角点精确可合并及过近不可合并；R 刚好容纳与不足；中心距/不重叠及圆角过渡临界。
- [x] 每条直段受约束的最少内部根数；删除内部点、甚至把其余点全部重排也不能减少数量的独立区间传播检验。
- [x] 只读统一结果含类型/方向、显示尺寸、线筋、带角色点筋、尺寸端点、真实标注锚点、规格及边界状态；实际直径不反向控制显示尺寸。
- [x] 主项目 Release/x64/.NET Framework 4.8 编译，CAD 托管引用不复制发布；原五组及 HangingNodeTests 回归通过。

## 本次证据

基线提交为 1dfe0bbc1f5d2c399ba303bcd4e3fb1e297445c7。本清单、正式源码及接口文档在本轮同一提交中；确切提交标识见最终回复及 git log。不推送、不打标签。只纳入已明确来源的两份需求/提示词及本轮接口文档，不包含原有用户示意图、DWG、DLL、配置或原始日志。

运行 [run_v04_calculation.ps1](run_v04_calculation.ps1)，三组均退出 0：

| 专项 | 实际结果 |
| --- | --- |
| BeamSideContourTests | BEAM_SIDE_CONTOUR_PASS，837 项检查 |
| BeamSideRebarTests | BEAM_SIDE_REBAR_PASS，1825 项检查 |
| BeamSideDotsTests | BEAM_SIDE_DOTS_PASS，45616 项检查 |

测试输出目录为 D:\Waste\WallDtailTool\tests\bin\V04CalculationFinal\。点筋覆盖 224 组宽/高/R/s 组合及合并、过近、R 下限、圆角真实过渡、中心距临界等专项；左右、非零基点和四种弯折由集成计算测试覆盖。检查数量不代表 CAD 操作次数或工程规范校核。

随后运行 [run_v03_geometry.ps1](run_v03_geometry.ps1) 到 bin\V04CalculationRegression\：UpstandGeometryStage1Tests、SlabUpstandGeometryTests、UpstandRebarStage2Tests、SlabUpstandRebarTests、UpstandAnnotationStage3Tests 均输出 PASS；HangingNodeTests 输出 HANGING_NODE_GEOMETRY_PASS checks=2974。板筋测试有既有两个 CS0649（锚固字段未赋值）警告，编译及执行均成功。无窗口修改，本轮不重跑 NodeDrawWindowTests。

主项目通过已审查的 build.ps1，使用本机 AutoCAD 2023 托管引用进行 Release/x64 构建，输出：

D:\Waste\WallDtailTool\bin\V04Calculation20261027Final\WallDetailTool.dll

SHA-256：8B306E1ED832167107FD937D675F4E4DA167F7B431D1AF5119F84F4CCF6EEF2F

另通过程序集类型读取确认最终 DLL 含 BeamSideGeometry、BeamSideContourRecognizer、BeamSideRebarGeometry、BeamSideRebarLayout。此前尝试 ReflectionOnlyLoadFrom 在当前 PowerShell/.NET 平台报不支持，已改用 LoadFile 只读类型检查成功；此检查没有启动 CAD。构建产物仅 DLL/PDB，没有复制 CAD 托管程序集。当前 DLL 确含计算层，但没有梁侧挂板命令入口，不要求用户加载验收。

## 下一任务及 CAD 待验证

- [ ] 一个参数窗口和轻量示意图、工具条/命令、完整不可变请求及目标文档校验。
- [ ] 世界坐标正向 TEXT、左右点筋引出、尺寸样式、普通块和同事务创建。
- [ ] 独立本类型成功参数与块快照，取消/失败不写，与旧四类及其他 DWG 隔离。
- [ ] CAD 2023/2024 实际运行；取消/失败无残留、源对象保护、BEDIT、一次 UNDO/REDO、保存重开、多文档及用户验收。
- [ ] CAD 窗口及事务接入后，再安排一个简短用户验收流程。

下一任务直接按 [计算层接口](../docs/V04_CALCULATION_API.md) 消费结果；不在 CAD 层重新排筋。输入单位确认/换算和二维图元提取仍由 CAD 层负责。当前只支持规则二维正交矩形梁/块体，复杂/曲线截面明确拒绝；未开发任何规范自动推荐或结构合格判定。

## 历史证据边界

前两轮仅形成草稿与基线编译，不能视为当时完成。旧 bin/V04BaselineCheck/WallDetailTool.dll 的 SHA-256 为 ECDE4515CE565CAB3BFB255362C9C96F251D79A0E08825DA5C872C934C0D31FD，不含 V0.4；本轮以上方新目录和新哈希为准。历史窗口测试 158 项和其他旧 CAD/用户反馈不扩大为本轮 V0.4 验收。
