# 统一界面优化交付与验收（2026-10-30）

基线：5fef3cb阶段收尾；现状/参数映射先提交9c764e8。本轮实际功能提交以包含本清单的Git提交为准。没有新增节点、比例、成功弹窗/通知或1.0发布；没有启动/关闭CAD、重试隔离加载、改安全设置、推送或打标签。

## 本轮完成与构建

- [x] 首次试用数字、有效记录/共享优先、恢复当前草稿、保留其他模式/方向草稿、隐藏非法字段不阻塞、取消不返回请求。
- [x] 统一可调整/滚动窗口、规格表/相关尺寸/低频选项、自动F说明、字段旁错误、输入方式按钮文案。
- [x] 原生示意焦点尺寸与中文起算说明、当前方向/模式离线帮助；[六类示例](../docs/help/README.md)。
- [x] 旧不可变请求/目标校验及生成服务继续使用；生产排筋/标注计算文件与基线一致；既有布局不重排。
- [x] 本机MSBuild Release/x64/.NET Framework4.8构建成功；CAD引用本机2023，Private=False，发布目录仅DLL/PDB，无CAD托管引用副本。

新DLL：`D:\Waste\WallDtailTool\bin\Usability20261030\WallDetailTool.dll`

SHA-256：`6BEFBDF586EE2861AC258763A6FCE567C19DC979F63F7E24C8ACBCE772BCE1B6`

不应继续加载旧TwoPoint目录来验收本轮窗口。源代码/测试/文档提交不包含DLL；用户design-preview和tmp保留不纳入。

## 实际自动测试

以下是本轮最终源码实际构建、运行的结果，不是历史记录或CAD动态测试。统一复现入口：[run_v05_geometry.ps1](run_v05_geometry.ps1)，传入独立OutputPath。测试执行文件位于`tests\bin\Usability20261030`。

| 测试组 | 本轮结果 |
|---|---|
| UsabilityTests | PASS，105项：首次/恢复/其他草稿、保存值优先/共享不变、隐藏非法字段、内联错误、说明及焦点绘制、上下/左右起算、四模式默认、原生帮助、窗口尺寸及字体缩放 |
| NodeDrawWindowTests | PASS，158项 |
| BeamSideWindowTests | PASS，3179项；旧“首次空L/V/模式”的断言按本轮预设要求更新，未删除其他记录/布局检查 |
| CantileverWindowTests | PASS，2928项 |
| RectangleInputTests | PASS，3365项 |
| EndLengthAnnotationTests | PASS，2628项 |
| CantileverAnnotationTests | PASS，87644项 |
| CantileverGeometryTests | PASS，4714项 |
| UpstandGeometryStage1Tests | PASS |
| SlabUpstandGeometryTests | PASS |
| UpstandRebarStage2Tests | PASS |
| SlabUpstandRebarTests | PASS |
| UpstandAnnotationStage3Tests | PASS |
| HangingNodeTests | PASS，2974项 |
| BeamSideContourTests | PASS，837项 |
| BeamSideDotsTests | PASS，51529项 |
| BeamSideRebarTests | PASS，1825项 |

共17组通过，原五组及HangingNodeTests均实际运行。最终仅缩短示意内焦点标题后重建DLL，并再次运行受影响的Usability/NodeDrawWindow/BeamSideWindow/CantileverWindow/RectangleInput五组，结果及计数不变；其余生产几何/标注源码未变。不为未报告计数的组推算断言数。独立测试构建仍有既有CS0649未赋值字段警告，部分窗口运行有libpng颜色配置警告，未导致失败。

静态复核：18个核心几何/方向/标注文件与5fef3cb内容一致；93处所检查本地文档链接有效。最终DLL实际270336字节、PE机器码0x8664（x64），哈希按最终文件重新读取。

窗口验证范围：实际创建WinForms，模拟字体9/11.25/13.5pt及800×560、1000×700、1200×840客户端，检查操作按钮/输入方式可达、数字编辑可见；查看四类窗口位图。帮助测试从窗口打开模态离线原生图及文字并关闭。此证据不是实际96/120/144DPI切换或CAD字体测试。几何/标注测试覆盖既有方向、单位、端部、Lin/L、避让，不能代表实际TEXT测量/尺寸箭头在CAD中全部无冲突。

## 保留待验与限制

- [ ] CAD2023/2024实际加载及所有入口/模式窗口，高DPI、小屏滚动、Tab/Enter/Esc、原生字体与帮助显示。
- [ ] 当前DWG成功记录/旧记录真实读取优先；模式草稿关闭丢弃、成功才保存，共享高级设置文件未被恢复按钮修改。
- [ ] 命令上下文、目标文档切换、单位解释、拾取/原生矩形预示、取消/非法几何清理、普通块/原轮廓保护。
- [ ] 一次UNDO/REDO、保存重开、类型/DWG隔离、多文档及2024专项；未将前阶段基本认可扩大为专项通过。
- [ ] 实际CAD的TEXT范围、交接/数字/尺寸避让与极端长E/短板/大字高；保持原失败说明并人工处理。

仍只支持既有WCS正交二维；梁/板固定毫米，梁侧/悬挑按原单位确认。不新增工程计算；试用值不保证任何轮廓都能生成，空间不足仍拒绝且不自动改参数。历史块不自动修改。[预设及兼容边界](../docs/USABILITY_PRESETS.md)、[实现与标注映射](../docs/USABILITY_IMPLEMENTATION.md)。

## 一个简短验收流程

用户在自己的CAD中加载上述新DLL，在新毫米测试图打开悬挑板窗口：查看首次数字→切模式④并修改Lin→切回①再回④核对草稿→恢复④试用值→聚焦Lin查看起点说明并打开本节点帮助→选择“两点矩形”，点1500×200局部范围生成一例。核对紧凑规格、上下点筋和Lin/L等高；撤销一次后再重做。其他节点可按帮助示例逐类验收，未操作的专项保持待验。
