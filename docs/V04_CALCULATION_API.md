# V0.4 梁侧挂板计算层接口

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

水平直段从 0 根搜索。用户补充的宽块约束为 `最近实际中心的局部X - DotDisplayDiameter/2 <= DotSpacing`，即靠梁边界至最近点筋显示外缘的水平空距上限。角点满足时不加水平点；超限才在真实直段搜索最少新增点，最近点不固定在边界，不以虚拟点的欧氏中心距替代此约束。竖边在上下角点间搜索最少内部点。实际相邻中心距范围为 `[显示直径, 输入s]`，圆角过渡使用真实垂直偏差及可达区间，优先等分、上下对称。不跨连接面闭合或补点；上限 10000 根，绝对精度 1e-8 mm，非有限数值拒绝；不推断工程净距或规范合格。

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
| WidthA/B、HeightA/B | 世界坐标实际宽高尺寸端点；高度点取连接面上下端 |
| LineLabelAnchor / DotLabelAnchor | 不同高度的真实外侧竖直直段接触点和实际点筋中心；优先少移动线筋锚点，再选低位点筋，不为布局增筋 |
| LineSpecification / DotSpecification | 含 %%132 的规格；点筋始终按实际直径和输入 s，绝不改成根数格式 |

连接面顶点索引为 Line[3] 与 Line[8]；圆弧起点为 1、4、6、9。前 3 段和最后 3 段分别组成上端和下端 L。CAD 层不应依赖这些索引重新算长度，优先使用返回的结果字段。

`BeamSideAnnotationGeometry` 两种规格均在真实锚点高度水平直引，无斜向接驳，保留线筋交接斜杠。TEXT 正向在上，两行预留至少 1.6 字高加基线偏移的高度差。高度尺寸左伸在块右、右伸在块左，与标注侧无关；宽度在上，输出已为图形单位。空间不足时明确报告非关键标注失败；CAD 层按实际 TEXT 边界再次校验上下避让，将单条水平 LINE 截至真实文字末端，失败不造空白锚点。参数/块快照结构不变，已有旧块不自动重布。

现有 BeamSideCadCommand 负责实体、正向 TEXT/引线、尺寸、普通块、事务及独立记录，没有复制排筋。BeamSideDrawRequest 捕获参数、图层及单位解释；BeamSideUnitTransform 统一双向比例，块局部只做一次基点位移。点选先由 Editor.CurrentUserCoordinateSystem 转 WCS 再换为毫米，依据 [Autodesk UCS .NET 示例](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-DevGuide-Managed/files/GUID-096085E3-5AD5-4454-BF10-C9177FDB5979.htm)。BeamSideAnnotationGeometry 只排标注位置，其输出已为图形单位。保存参数、取消/失败、UNDO/REDO、保存重开及多文档仍须另作真实 CAD 验证，不能以编解码测试替代。
