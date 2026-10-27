# V0.4 梁侧挂板计算层交付清单

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
