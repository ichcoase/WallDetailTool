# V0.4 梁侧挂板计算层接口

本层已纳入 WallDetailTool 主项目（.NET Framework 4.8/x64），不创建 CAD 对象、不访问文档或设置、不写 DWG。当前没有 CAD 命令入口。本文件是下一接入任务的接口依据，完整产品需求仍见 [需求书](V04_BEAM_SIDE_BLOCK_PRD.md)。

## 1. 坐标与单位

所有接口边界均为世界坐标毫米：`ContourSegment` 输入、`BeamSideRect`、识别候选及最终 `Line`、`Dots`、尺寸点和标注锚点。Elevation 为共同 Z。CAD 层应先检查二维平面、LINE/LWPOLYLINE 直线段及单位，再把几何换算为毫米；不能把 bulge 弧段直接当弦传入。输出实体需按同一单位比例换回图形单位。图纸单位的提示及明确确认属于下一任务。

临时标准空间以连接面块体下端 `(ConnectionX, Block.Bottom)` 为原点，方向符号 `s=(int)Direction`（右 1、左 -1）：

`local=(s*(world.X-ConnectionX), world.Y-Block.Bottom)`

`world=(ConnectionX+s*local.X, Block.Bottom+local.Y)`

上下弯折的布尔值始终为世界坐标向上；左右转换只改变 X 和 bulge 符号。完整梁显示边界校验在世界坐标执行，非零/负基点有专项覆盖。

## 2. 识别及人工选择

`BeamSideContourRecognizer.Recognize(segments, elevation, direction, fullSection)` 返回 `BeamSideRecognition`，不修改输入。

- `fullSection=false`：接受局部闭合矩形及向梁开口 U；若有多个互不接触的候选，`Candidates` 按位置排序，`SelectCandidate(worldX,worldY)` 要求目标内部点，只返回已校验的不可变候选。额外零散线、相交/重叠候选拒绝。
- `fullSection=true`：接受一个相连的完整规则梁/块体截面，可以是整体外边界，也可带完整竖向连接缝；两矩形共享连接面部分的重复边允许，其余重复边拒绝。等高、近等高、上下错位均支持；不能用宽高比例判断梁或块体，方向指定块体所在侧。分支、空洞、多级块体、缺边、水平内部缝及不连通区域拒绝。
- 完整等高外框若没有连接缝，仅凭矩形无法确定梁宽与块体宽，`SplitRegions` 返回该不可变外框及方向。调用者应要求明确选择连接 X，再调用 `SplitRegions[i].Resolve(worldConnectionX)`；它检查两侧均大于 1 mm，并返回完整 `BeamSideGeometry`。这是已实现的人工选择接口，不产生猜测候选；下一窗口不得将其默认为局部矩形。

识别容差为 1 mm；有限数值、正交性、连接长度和拓扑均检查。上限 4096 段、每轴 256 个归并坐标，超出明确拒绝。局部尺寸和完整梁/块体尺寸须大于 1 mm。`Candidates` 和 `SplitRegions` 均为只读副本。

`BeamSideGeometry` 输出 Block、可选 Beam、Direction、InputKind、ConnectionX、ConnectionBottom/Top、BasePoint 和 Elevation。局部模式 Beam 为空；完整模式梁与块体必须在指定侧形成有效连接面。

## 3. 排筋唯一入口

`BeamSideRebarGeometry.Calculate(geometry, parameters)` 返回 `BeamSideRebarLayout`，失败抛出 `InvalidOperationException` 并说明原因，不返回部分有效的实体列表。`BeamSideRebarParameters` 为不可变参数，L/V 必须显式提供；其余初始值沿用现有节点。高级设置由未来 CAD 请求传入，不在计算层另存。

中心线偏移为 `Cover+LineDisplayWidth/2`，不取规格直径。`a=L-V-πR/2 > 0`，水平深入距离 `a+R`。统一 R 不缩小；线筋为一条连续多段线，两端竖直 V、四个 90° 圆弧、三个块体侧边及梁内水平 a。线/弧相切、零长度、中心线接触/自交/重叠及显示外轮廓内部重叠均检查；显示外轮廓仅相切不增加无依据净距。开口线端为平端头，不虚增圆帽。完整梁检查实际直线条带与圆环扇区的极值及边界，不是包围盒碰撞推测。

点筋固定位置为上下连接面端点（显示外缘与连接面齐平）及外侧上下圆角点。普通角点取线筋内侧等距圆弧轨迹的 45° 角平分位置，内部点仅在真实三条相切直段上。端点恰位于圆角内偏移轨迹的水平切点方向时，同一根同时承担端点/角点角色；近似但不能同时满足位置和相切时拒绝，不删除角点。R 小于 `(线宽+点筋显示直径)/2` 拒绝，等于时点筋中心轨迹退化为圆心且可满足相切，允许精确容纳。

每条开放区段在固定两端、直段范围、中心距 `[显示直径, 输入s]` 下按根数递增做解析可达区间搜索；圆角过渡用真实中心坐标的垂直偏差。首个可行数量即该约束下最少根数，中间点等分并优先对称。没有跨连接面首尾闭合检查，也不在中部补点。上限 10000 根。计算精度为绝对 1e-8 mm，超出有限数值计算范围拒绝；不是工程净距或规范合格校核。

## 4. 供 CAD 直接消费的结果

`BeamSideRebarLayout` 保存只读快照：

| 数据 | 含义 |
| --- | --- |
| Geometry / Parameters | 完整识别及参数快照；NodeType 为 BeamSideBlock |
| Line | 世界坐标 12 个顶点，Bulge 属于从本顶点开始的段，开口线；每段起终宽均用 LineWidth |
| Dots | 世界坐标中心及角色位标记；合并点只出现一次，角色可同时含端点/角点 |
| DotSpans | 上边、外侧竖边、下边各段首末索引和最少内部根数；合并的区段首末相同 |
| HorizontalInsideLength / HorizontalDepth | a / a+R |
| TopInsideLength / BottomInsideLength | 分别由线/弧实际长度复算的两端 L |
| LineWidth / DotDisplayDiameter / Elevation | CAD 显示尺寸及 Z |
| BoundaryStatus / BoundaryMessage | Unknown 时必须提示“梁边界未知，梁内端部需人工审核”；Checked 只表示几何容纳 |
| WidthA/B、HeightA/B | 世界坐标尺寸端点，测量块体实际宽高 |
| LineLabelAnchor / DotLabelAnchor | 真实线筋竖段中点、按 AnnotationRight 选择的实际点筋中心 |
| LineSpecification / DotSpecification | 含 %%132 的规格；点筋始终按实际直径和输入 s，绝不改成根数格式 |

连接面顶点索引为 Line[3] 与 Line[8]；圆弧起点为 1、4、6、9。前 3 段和最后 3 段分别组成上端和下端 L。CAD 层不应依赖这些索引重新算长度，优先使用返回的结果字段。

下一任务只负责实体、正向 TEXT/引线布局、尺寸样式、普通块、事务和独立成功记录，不在 CAD 层再复制计算算法。保存参数、取消/失败、UNDO/REDO、保存重开及多文档必须另作真实 CAD 验证。
