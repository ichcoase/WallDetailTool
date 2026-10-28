# V0.4 梁侧挂板计算层接口

## 2026-10-28 水平点筋按s递推（基线ea8ac4b，覆盖旧水平均分口径）

用户明确确认仅改变上下水平边：以外侧必需角点为起点，当前实际点的局部X减显示半径≤s即停；超限才沿真实水平内缘相切轨迹向梁侧加一根，真实欧氏中心距必须为s。首步水平步长sqrt(s²-高度差²)，后续同轨迹每步水平s。边界余量留在靠梁端，不均分、不设固定或虚拟靠梁点。递推点超出真实直段/块体，或相切、不重叠、数值/数量条件无法满足时明确拒绝，不调整间距或角点。上下对称、左右共用。外侧竖边保留原有受约束最少数量及均匀策略，不强制其中心距等于s。

300×600试例（保护层25、线宽12、显示点径12、R20、s200）上下各1根；新X=54.670580551，真实中心距200，末端净距48.670580551。旧X约130.328仅作为历史均分值，不再是当前结果。标注/尺寸镜像、自动与手选侧、L/V、弯折、梁边界、记录和事务均沿ea8ac4b，不改变已有块。本轮按用户要求未运行CAD，图面及持久化验收仍待用户完成；不推送或打标签。

## 2026-10-28 最新左右对应布局（基线43c7773，覆盖下方旧固定右侧口径）

默认规格标注跟随伸出方向：右伸在右、左伸在左。窗口提供自动/手动右/手动左三态；自动时切换方向即时跟随，手动选择保持至用户改回自动。不可变请求捕获AnnotationSideManual及有效AnnotationRight，不使用全局pending。高度尺寸侧始终按伸出方向，手动选择另一侧钢筋文字不使尺寸换侧；同侧文字按实际TEXT及引线外界留max(50mm,1.5字高)间隔，左侧向左避让、右侧向右避让。高度定义点取伸出外侧实际顶底，宽度在上。两条水平引线、中部真实点筋锚点、线上规格在上及正向TEXT保持；交接斜杠随几何左右对应，不镜像字形。排筋、L/V、弯折及事务不变。

成功记录/块快照仍为20/31数值，版本升级为2：索引11为-1自动、0手动左、1手动右。v1仅有最终侧别，无法区分历史默认与手选；合法v1侧别校验后按自动跟随方向读取，不把旧右侧当作手动覆盖。旧实例不重写；若旧版曾手选侧别，新窗口需重新明确选择一次。v2重开保留自动/手动语义，取消/失败不保存或迁移记录，成功事务才写v2。以下旧章节仅作历史，冲突以本节为准。

按用户要求本轮未运行CAD；自动编译/布局验证不能替代真实图面、保存重开及UNDO验收。用户示意图保留，不推送或打标签。

本计算层已纳入 WallDetailTool 主项目（.NET Framework 4.8/x64），自身不创建 CAD 对象、不访问文档或设置、不写 DWG。WDTBEAMSIDE / BeamSideCadCommand 为调用者；本文已按2026-10-28反馈同步角色、数量和标注锚点语义。完整产品需求见 [需求书](V04_BEAM_SIDE_BLOCK_PRD.md)，CAD 交付与验收状态见 [清单](../tests/V04_RELEASE_CHECKLIST.md)。

## 1. 坐标与单位

所有接口边界均为世界坐标毫米：`ContourSegment` 输入、`BeamSideRect`、识别候选及最终 `Line`、`Dots`、尺寸点和标注锚点。Elevation 为共同 Z。CAD 层检查二维平面、LINE/LWPOLYLINE 直线段及单位后换算为毫米；不能把 bulge 弧段直接当弦传入。输出按同一比例换回图形单位；现有窗口负责非毫米提示及明确确认。

临时标准空间以连接面块体下端 `(ConnectionX, Block.Bottom)` 为原点，方向符号 `s=(int)Direction`（右 1、左 -1）：

`local=(s*(world.X-ConnectionX), world.Y-Block.Bottom)`

`world=(ConnectionX+s*local.X, Block.Bottom+local.Y)`

上下弯折的布尔值始终为世界坐标向上；左右转换只改变 X 和 bulge 符号。完整梁显示边界校验在世界坐标执行，非零/负基点有专项覆盖。

## 2. 识别及人工选择

`BeamSideContourRecognizer.Recognize(segments, elevation, direction, fullSection)` 返回 `BeamSideRecognition`，不修改输入。

- `fullSection=false`：接受局部闭合矩形及向梁开口 U；若有多个互不接触的候选，`Candidates` 按位置排序，`SelectCandidate(worldX,worldY)` 要求目标内部点，只返回已校验的不可变候选。额外零散线、相交/重叠候选拒绝。
- `fullSection=true`：接受一个相连的完整规则梁/块体截面，可以是整体外边界，也可带完整竖向连接缝；两矩形共享连接面部分的重复边允许，其余重复边拒绝。等高、近等高、上下错位均支持；不能用宽高比例判断梁或块体，方向指定块体所在侧。分支、空洞、多级块体、缺边、水平内部缝及不连通区域拒绝。
- 完整等高外框若没有连接缝，仅凭矩形无法确定梁宽与块体宽，`SplitRegions` 返回该不可变外框及方向。调用者应要求明确选择连接 X，再调用 `SplitRegions[i].Resolve(worldConnectionX)`；它检查两侧均大于 1 mm，并返回完整 `BeamSideGeometry`。这是已实现的人工选择接口，不产生猜测候选；CAD 窗口不得将其默认为局部矩形，当前首次窗口要求明确选择识别模式。

识别容差为 1 mm；有限数值、正交性、连接长度和拓扑均检查。上限 4096 段、每轴 256 个归并坐标，超出明确拒绝。局部尺寸和完整梁/块体尺寸须大于 1 mm。`Candidates` 和 `SplitRegions` 均为只读副本。

`BeamSideGeometry` 输出 Block、可选 Beam、Direction、InputKind、ConnectionX、ConnectionBottom/Top、BasePoint 和 Elevation。局部模式 Beam 为空；完整模式梁与块体必须在指定侧形成有效连接面。

## 3. 排筋唯一入口

`BeamSideRebarGeometry.Calculate(geometry, parameters)` 返回 `BeamSideRebarLayout`，失败抛出 `InvalidOperationException` 并说明原因，不返回部分有效的实体列表。`BeamSideRebarParameters` 为不可变参数，L/V 必须显式提供；其余初始值沿用现有节点。高级设置由完整 CAD 请求传入，不在计算层另存。

中心线偏移为 `Cover+LineDisplayWidth/2`，不取规格直径。`a=L-V-πR/2 > 0`，水平深入距离 `a+R`。统一 R 不缩小；线筋为一条连续多段线，两端竖直 V、四个 90° 圆弧、三个块体侧边及梁内水平 a。线/弧相切、零长度、中心线接触/自交/重叠及显示外轮廓内部重叠均检查；显示外轮廓仅相切不增加无依据净距。开口线端为平端头，不虚增圆帽。完整梁检查实际直线条带与圆环扇区的极值及边界，不是包围盒碰撞推测。

2026-10-28 用户确认：仅固定外侧上下圆角点，取消连接面端点及齐平约束。角点取内侧等距圆弧轨迹的 45° 位置，新增点只在真实三条相切直段上。R 小于 `(线宽+点筋显示直径)/2` 拒绝，等于时轨迹退化为圆心且允许精确容纳。没有端点合并角色或近端点过近的旧拒绝条件；实际点须在块体内且不重叠。

水平从角点起，检查`最近实际中心的局部X - DotDisplayDiameter/2 <= DotSpacing`，达标即停；超限向梁侧加点，首步X减sqrt(s²-高度差²)，后续X减s。每个实际中心距为s，余量留靠梁边界，不均分、不设虚拟点。点超出真实水平相切轨迹或不能容纳时明确拒绝，不缩小s或移动角点。下边使用上边结果上下对称，左右仍统一坐标转换。竖边在上下角点间搜索最少内部点，沿用实际中心距上限和均匀策略。本轮不改变DotSpans和角色语义；上限10000根，绝对精度1e-8 mm，非有限数值拒绝，不推断规范合格。

## 4. 供 CAD 直接消费的结果

`BeamSideRebarLayout` 保存只读快照：

| 数据 | 含义 |
| --- | --- |
| Geometry / Parameters | 完整识别及参数快照；NodeType 为 BeamSideBlock |
| Line | 世界坐标 12 个顶点，Bulge 属于从本顶点开始的段，开口线；每段起终宽均用 LineWidth |
| Dots | 世界坐标中心及角色 UpperHorizontal、UpperCorner、OuterVertical、LowerCorner、LowerHorizontal；角点唯一，无 Endpoint 或虚拟点 |
| DotSpans | 上水平、外侧竖直、下水平各段实际 First/Last 索引及 MinimumAdditionalCount（不计必需角点）；水平0根时首末均为对应角点；竖边含上下角点 |
| HorizontalInsideLength / HorizontalDepth | a / a+R |
| TopInsideLength / BottomInsideLength | 分别由线/弧实际长度复算的两端 L |
| LineWidth / DotDisplayDiameter / Elevation | CAD 显示尺寸及 Z |
| BoundaryStatus / BoundaryMessage | Unknown 时必须提示“梁边界未知，梁内端部需人工审核”；Checked 只表示几何容纳 |
| WidthA/B、HeightA/B | 世界坐标实际宽高尺寸端点；高度点取块体右边实际顶底，两种伸出方向相同 |
| LineLabelAnchor / DotLabelAnchor | 优先选择最接近块体中部且可排布的真实点筋中心，再沿真实外侧竖直直段向上调整线筋锚点；两条水平引线，线筋规格在上，不为布局增筋或移筋 |
| LineSpecification / DotSpecification | 含 %%132 的规格；点筋始终按实际直径和输入 s，绝不改成根数格式 |

连接面顶点索引为 Line[3] 与 Line[8]；圆弧起点为 1、4、6、9。前 3 段和最后 3 段分别组成上端和下端 L。CAD 层不应依赖这些索引重新算长度，优先使用返回的结果字段。

2026-10-28完整新指令覆盖先前侧别和锚点优先级：`BeamSideAnnotationGeometry` 两种规格仍水平直引，点筋选靠近竖向中部的已有点，线筋锚点在其上方，保留交接斜杠。TEXT正向在上，至少1.6字高加基线偏移的行间空间。高度尺寸两方向均在块体右侧，宽度在上；输出为图形单位。`HeightLineOutsideLabels(layout, actualRightEdge, drawingScale)` 接收同为图形单位的实际TEXT/引线右界，返回在原右侧位置与`右界+max(50mm×比例,1.5×图形字高)`之间更靠右的尺寸点，Y不变。CAD在两条可选标注构建后、创建尺寸前更新该位置，最后只做一次块局部位移。不用字符数猜字体宽度；空间不足/文字边界不可用明确报告，不造空白锚点。记录结构不变，旧块不自动重布。

300宽默认参数经独立测量符合边界净距和最少数量，生产BeamSideDots算法本轮未改；完整坐标和实际距离见 [300宽核查](V04_300_WIDTH_CHECK.md)。新增测试覆盖不同显示直径、临界、非零基点及左右变换。本轮按用户要求未运行CAD，历史失败不作为新测试结果。

现有 BeamSideCadCommand 负责实体、正向 TEXT/引线、尺寸、普通块、事务及独立记录，没有复制排筋。BeamSideDrawRequest 捕获参数、图层及单位解释；BeamSideUnitTransform 统一双向比例，块局部只做一次基点位移。点选先由 Editor.CurrentUserCoordinateSystem 转 WCS 再换为毫米，依据 [Autodesk UCS .NET 示例](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-DevGuide-Managed/files/GUID-096085E3-5AD5-4454-BF10-C9177FDB5979.htm)。BeamSideAnnotationGeometry 只排标注位置，其输出已为图形单位。保存参数、取消/失败、UNDO/REDO、保存重开及多文档仍须另作真实 CAD 验证，不能以编解码测试替代。
