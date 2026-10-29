# V0.5 悬挑板接口与记录

## 2026-10-29 端部长度文字接口

EndLengthAnnotationGeometry.Cantilever/BeamSide/Slab分别提取各节点真实端段及其原始长度参数。EndLengthNote的A/B已是图形坐标，Text始终为毫米数值，Height/Gap已按当前比例换算；Rotation仅0或π/2。Place接收实测轴向TEXT宽高和障碍回调，仅返回新增文字范围，不修改钢筋或原标注。CAD适配器复用字体/图层/可选事务，文字追加到当前新普通块。三种长度语义及旧板节点单位边界见 [接口说明](V05_END_LENGTH_TEXT.md)。Lin/L共Y和记录接口保持。

## 2026-10-29 最终尺寸行

模式④ApplyMeasuredLayout末尾统一计算最终Dimensions.WidthLine.Y与LinLine.Y，避让两组上方文字实测上界和引线最高点。只更新尺寸行Y，保留定义点、X、Lin数值及所有规格布局；非模式④不变。CAD继续同一尺寸服务和样式。见 [等高规则](V05_ALIGNED_DIMENSIONS.md)。

## 2026-10-29 标注接口补充

CantileverAnnotationGeometry.Calculate仍从正式steel/dots结果提取真实接触段，不修改输入。新增CantileverTextSize(width,height)为图形单位的实测矩形；CAD测量事务复用BuildUpstandLabel取得实际DBText.GeometricExtents，随后ApplyMeasuredLayout(result,sizes)选择紧凑真实锚点、左右方向及行位。最终BuildCantileverLabel将实际字形近侧/下侧边界对齐TextAnchor，末条水平线到实测远侧边。无字符数估算、全节点包围盒推远或新设置系统。规格身份及记录接口不变。布局详见 [最新契约](V05_COMPACT_LAYOUT.md)；下方原接口中的固定底筋端点/向外出字为历史布局。

长度均WCS毫米。CantileverFrame：悬挑侧梁面顶点SupportX/TopY，局部+X向自由端、+Y保持世界向上；左右仅镜像X及bulge。BeamWidth仅定位，不是待生成尺寸。

CantileverRebarGeometry.Calculate(frame,specifications,inputs)返回独立不可变钢筋路径列表，索引0主面筋/伸出筋、1底筋、2附加筋（仅模式4）。顶点含世界X/Y及bulge。显示宽度由输入单独控制，底筋自身直径控制偏移；模式4两组面筋共用主筋中心线路径，各自保留直径/间距，不错层、不串接。各筋解析宽度边界自交检查复用BeamSideCurve/Stroke，不调用梁边界CheckInside。

CantileverDots.Calculate(frame,steel,inputs,displayDiameter,spacing)返回Upper/Lower只读列表，顺序自由端到梁侧，X和数量严格相同。令面筋水平轴线y、自由端竖轴x、offset=(显示线宽+显示点径)/2、rho=R-offset，角筋中心=(x-R+rho/√2,y-R+rho/√2)。水平排Y=y-offset，下排Y=底筋轴Y+offset。圆弧位置固定；递推只由上排决定。检查显示边界、各点不重叠、对所有实际线段/圆弧的最短距离及下排真实相切直段范围。最多上下合计10000根，超限明确拒绝。

CantileverDrawRequest持有不可变方向/模式、15数值、5共享参数、4图层、单位比例/原单位代码/本次毫米覆盖。外部数组防御复制，Specs本身不可变。目标Document由命令闭包持有，TargetMatches检查引用同一文档及单位代码。

CantileverDrawState只供一次窗口，所有可编辑项有绘图初值，首次默认模式1/向右；成功记录优先于共享值和绘图预设。旧记录无关模式的0字段由预设补齐，切换保留原始文字；关闭不保留草稿。F没有输入框，固定显示自动说明；L/h仅取轮廓。窗口不写DWG或用户设置，确认后才进入命令上下文。见 [完整预设表](V05_DRAWING_PRESETS.md)。

自动F只在CantileverRebarGeometry.Calculate：从正式底筋水平段结果读取Y，端头Y=底筋Y+10mm；EffectiveF返回实际投影。CantileverPathInputs.F只是旧接口保留槽，不控制计算；请求Value(4)固定0。窗口和CAD直接使用计算顶点，不重复保护层/直径公式。自动F≤R或不能容纳合法直段时拒绝，不降10mm或缩R。

| LAST索引 | 字段 |
|---|---|
| 0 | 新记录版本2；兼容读取v1 |
| 1、2 | 方向±1、支座模式1..4 |
| 3..9 | b、A、V、E、保留F槽0、B、Lin（无关模式字段为0） |
| 10..17 | 主面筋直径/间距、底筋直径/间距、附加筋直径/间距、点筋直径/间距 |
| 18..22 | c、显示线宽、显示点径、R、字高 |
| 23..25 | 图形单位/mm、INSUNITS代码、明确毫米覆盖0/1 |

NOD键WDT_CANTILEVER_LAST，26项DXF Real；坏记录完整拒绝并回绘图初值，不部分恢复或串读其他类型。v1索引7须为合法正数旧F，读入后忽略；v2索引7必须0。其余旧参数保留。块扩展字典键WDT_CANTILEVER_PARAMS：首项DXF Text为CantileverSlab，随后上述26项＋SupportX/TopY/L/h/Elevation（共31项数字），最后4项图层字符串。EncodeSnapshot/DecodeSnapshot仅作内存数据读取，读取不回写旧块，无编辑既有块命令。仅新生成事务成功写新记录v2。

CAD输出的钢筋、点筋、截止符号、普通块及单位换算不变。所有规格改为竖直段＋水平段的90°引线。伸出筋取本组真实内跨锚点向内跨上方，附加筋取悬挑段锚点向自由端上方，底筋取真实自由端点向自由端下方，点筋向支座下方。点筋公共竖线由真实上点经过同X真实下点再转水平，仅一个规格，合并共线分支不叠画。CAD继续正向TEXT、交接斜杠及实测最后水平段终点，不删竖线；2根/多根统计、排筋、F、预设和记录均不变。详见 [最新布局契约](V05_ORTHOGONAL_LAYOUT.md)。

自动测试和源码编译不能验证AutoCAD原生实体的实际表现。详见 [交付清单](../tests/V05_RELEASE_CHECKLIST.md)，真实CAD由用户加载验收，不重试已知失败的隔离环境。
