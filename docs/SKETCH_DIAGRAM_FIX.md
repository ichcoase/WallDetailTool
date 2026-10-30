# 梁/板示意与悬挑高亮修正（2026-10-30）

基线9b89b99388a2a5b16790753c3fbb33ecafe8f04d。用户指出“梁节点和板节点里面的梁示意和板示意。悬挑板里面的高亮位置”。本次只修窗口解释性图形及焦点目标，不改试用预设、请求数值、CAD实体、排筋、标注、自动F或保存。

## 问题与修正

| 发现 | 当前修正 |
|---|---|
| 支承单独画封闭矩形，矩形边贯通反坎/下挂连接部位；凸出混凝土未填色 | 梁/板与凸出部分使用连续填充外轮廓，连接部不画虚假贯通线，梁比薄板具有清晰的示意厚度区别；上下准确对应 |
| 底图与高亮分别写坐标，悬挑水平高亮统一位于上方空白 | 底图和参数目标共用屏幕模型；b对应两梁面，A/V对应支座面筋，E对应内跨伸出段，B贴合底筋入支座段，Lin对应内跨梁面到附加筋截止端（斜勾起点），不含斜勾 |
| 切模式后隐藏V等参数可能遗留高亮 | 模式不适用的目标返回空，绘制也忽略已隐藏/禁用字段，不留下旧高亮或标题 |

NodeSketchDiagram和CantileverSketchDiagram只含屏幕坐标，当前Control绘制与IParameterSketch取点共用；响应窗口宽度，左右只镜像X。板h跨两板面、H按上下方向从板底/板顶至水平筋中心线；左右L各指自身水平直段。梁左右锚固从连接面至各自端头。示意不按输入比例，不能作为工程几何结果。画布高度调整为240px，主体仍可滚动，不影响底部操作按钮。

上一版测试覆盖窗口可见/方向及高亮存在，未充分检查连续混凝土填充与高亮-真实画线的同点关系。本次新增专项补充这类性质检查，不以“有橙色”代替位置正确。

## 构建与自动验证

本次MSBuild Release/x64/.NET Framework4.8构建通过，CAD托管引用使用本机2023且不复制发布；产品目标仍含2023/2024，不代表CAD2024实测通过。

最终DLL：`D:\Waste\WallDtailTool\bin\SketchFix20261030\WallDetailTool.dll`

SHA-256：`128213D6492E3C35BAAF3943ED1249DFCCEE390C0BE4B0A7A04B2BF9DA3C9F2B`

实际文件271360字节、x64机器码0x8664，输出目录只有DLL/PDB。提交不包含编译产物或预览图。本次功能提交以包含本记录的Git提交为准。

18组独立测试本次全部实际构建/运行通过，统一入口为[run_v05_geometry.ps1](../tests/run_v05_geometry.ps1)：

| 测试组 | 本次结果 |
|---|---|
| SketchDiagramTests | PASS，1048项 |
| UsabilityTests | PASS，105项 |
| NodeDrawWindowTests | PASS，158项 |
| BeamSideWindowTests | PASS，3179项 |
| CantileverWindowTests | PASS，2928项 |
| RectangleInputTests | PASS，3365项 |
| EndLengthAnnotationTests | PASS，2628项 |
| CantileverAnnotationTests | PASS，87644项 |
| CantileverGeometryTests | PASS，4714项 |
| 原五组UpstandGeometryStage1Tests、SlabUpstandGeometryTests、UpstandRebarStage2Tests、SlabUpstandRebarTests、UpstandAnnotationStage3Tests | 五组各PASS |
| HangingNodeTests | PASS，2974项 |
| BeamSideContourTests | PASS，837项 |
| BeamSideDotsTests | PASS，51529项 |
| BeamSideRebarTests | PASS，1825项 |

独立测试的既有CS0649和libpng颜色配置警告仍有出现，不影响本次通过。18个核心几何/方向/标注文件与9b89b99内容一致，所检查89处本地文档链接有效。测试输出和三张原生对照位图位于`tests\bin\SketchFix20261030`，已实际查看上下节点和左右悬挑图；不把独立WinForms位图等同CAD动态验收。

新增SketchDiagramTests：240/350/480px示意宽度，梁/板上下连续填充/连接处不留分割线，支承含端部、H/h/L/锚固起点，悬挑四模式/左右的实际路径及投影目标、截止符号世界向下；实际窗口中B触及底筋、隐藏V无残留；生成原生位图供查看。测试不接入AutoCAD。

## 用户验收与保留边界

加载本次新DLL，打开梁/板窗口切上下查看连续混凝土示意，再打开悬挑依次聚焦A/V、切模式③聚焦E、④聚焦B/Lin并切左右；橙色应贴合相应路径/起算位置，不留隐藏V。此任务不需要生成实体。

真实CAD宿主、字体/高DPI、命令上下文及DWG撤销/重做、保存重开、多文档、2024等仍按原清单待验。本次没有启动/关闭CAD、重复隔离加载、改安全设置、推送或打标签。用户design-preview、tmp及其他文件保留，不纳入提交。
