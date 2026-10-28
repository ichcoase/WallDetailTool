# V0.5 悬挑板接口与记录

长度均WCS毫米。CantileverFrame：悬挑侧梁面顶点SupportX/TopY，局部+X向自由端、+Y保持世界向上；左右仅镜像X及bulge。BeamWidth仅定位，不是待生成尺寸。

CantileverRebarGeometry.Calculate(frame,specifications,inputs)返回独立不可变钢筋路径列表，索引0主面筋/伸出筋、1底筋、2附加筋（仅模式4）。顶点含世界X/Y及bulge。显示宽度由输入单独控制，底筋自身直径控制偏移；模式4两组面筋共用主筋中心线路径，各自保留直径/间距，不错层、不串接。各筋解析宽度边界自交检查复用BeamSideCurve/Stroke，不调用梁边界CheckInside。

CantileverDots.Calculate(frame,steel,inputs,displayDiameter,spacing)返回Upper/Lower只读列表，顺序自由端到梁侧，X和数量严格相同。令面筋水平轴线y、自由端竖轴x、offset=(显示线宽+显示点径)/2、rho=R-offset，角筋中心=(x-R+rho/√2,y-R+rho/√2)。水平排Y=y-offset，下排Y=底筋轴Y+offset。圆弧位置固定；递推只由上排决定。检查显示边界、各点不重叠、对所有实际线段/圆弧的最短距离及下排真实相切直段范围。最多上下合计10000根，超限明确拒绝。

CantileverDrawRequest持有不可变方向/模式、15数值、5共享参数、4图层、单位比例/原单位代码/本次毫米覆盖。外部数组防御复制，Specs本身不可变。目标Document由命令闭包持有，TargetMatches检查引用同一文档及单位代码。

CantileverDrawState只供一次窗口。初次工程控制输入为空，模式必须明确选择；关闭不保留草稿。窗口不写DWG或用户设置；确认之后ExecuteInCommandContextAsync才选择轮廓并生成。

| LAST索引 | 字段 |
|---|---|
| 0 | 记录版本1 |
| 1、2 | 方向±1、支座模式1..4 |
| 3..9 | b、A、V、E、F、B、Lin（无关模式字段为0） |
| 10..17 | 主面筋直径/间距、底筋直径/间距、附加筋直径/间距、点筋直径/间距 |
| 18..22 | c、显示线宽、显示点径、R、字高 |
| 23..25 | 图形单位/mm、INSUNITS代码、明确毫米覆盖0/1 |

NOD键WDT_CANTILEVER_LAST，26项DXF Real；坏记录完整拒绝并回首次输入，不部分恢复或串读其他类型。块扩展字典键WDT_CANTILEVER_PARAMS：首项DXF Text为CantileverSlab，随后上述26项＋SupportX/TopY/L/h/Elevation（共31项数字），最后4项图层字符串。EncodeSnapshot/DecodeSnapshot作纯数据往返验证，尚无编辑既有块命令。

CAD输出：每组一条开口宽LWPOLYLINE，点筋调用现有闭合两顶点多段线，每个X位置只有上下各一根；模式4另外一条固定26×26向下截止符号。正向TEXT/水平LINE引线及交接斜杠复用既有服务，尺寸复用RotatedDimension，非毫米Dimlfac=1/Scale。普通块内实体仅一次减基点/标高，块引用在该基点插入。源混凝土不纳入块。

自动测试和源码编译不能验证AutoCAD原生实体的实际表现。详见 [交付清单](../tests/V05_RELEASE_CHECKLIST.md)，真实CAD由用户加载验收，不重试已知失败的隔离环境。
