# V0.5 普通悬挑板候选交付验证

## 2026-10-29 Lin/L上方尺寸等高（当前交付，基线f215032）

用户确认及实现边界见 [等高尺寸](../docs/V05_ALIGNED_DIMENSIONS.md)。下方旧DLL均为历史交付。

- [x] CantileverAnnotationTests：CANTILEVER_COMPACT_ANNOTATION_PASS checks=87644，新增左右模式④共世界Y、两组实测规格和引线共同避让、Lin/L真实长度与比例断言。保留不同L/h/字高/E/Lin、毫米/米/英寸及宽文字布局矩阵。
- [x] CantileverGeometryTests 4714项、CantileverWindowTests 2928项；原五组PASS、HangingNodeTests 2974项、NodeDrawWindowTests 158项，V0.4四组837/51529/1825/3180项。共14组逐项编译并执行通过。
- [x] 与f215032核对几何、点筋、DrawState/DrawForm、旧共用标注及CantileverCadCommand源码一致。唯一生产改动是规格路由结束后统一尺寸Y；沿用同一CAD尺寸样式和字高服务。
- [x] 审查build.ps1及MSBuild/.NET4.8/本机AutoCAD2023引用，显式Release/x64新目录输出，仅DLL/PDB，无CAD程序集副本。原测试CS0649及PNG配置警告不影响通过。
- [ ] AutoCAD2023/2024实际尺寸文字/箭头/界线及图面仍待用户验收；自动测试只证明纯布局性质。本轮未启动CAD、不重试隔离加载、不改安全设置。

DLL：`D:\Waste\WallDtailTool\bin\V05AlignedDimensions20261029\WallDetailTool.dll`

SHA-256：`5CF367DC08A78F98F82CA8869B117BCADE71348E6B5FFDA4468146EEBFEDA571`。

用户验收：新图/副本加载此DLL，WDTCANTILEVER模式④左右各生成一处；检查Lin与L同一水平线、文字同高同样式，两组钢筋规格及下方标注位置保持，Lin不含梁宽。增大字高再生成，确认两尺寸共同上移。旧DLL占用时由用户自行保存后重启CAD。未推送、未打标签。

## 以下为f215032及更早交付历史

## 2026-10-29 紧凑标注（历史交付，基线a3bc0eb）

需求及实现边界见 [紧凑布局](../docs/V05_COMPACT_LAYOUT.md)，覆盖旧的全包围盒外侧出字、底筋固定端点和下方必须向外展开限制。

- [x] CantileverAnnotationTests：CANTILEVER_COMPACT_ANNOTATION_PASS checks=83664。四模式/左右、不同L/h/字高/单位/E/Lin、2根及多根点筋、明确宽文字范围样本；真实锚点、90°、短水平段、文字紧靠折点、半区中心目标、错高避让、文字/引线无交叉、L/Lin基准界线避让、左右对应及长E不推远板侧规格。
- [x] CantileverWindowTests：CANTILEVER_WINDOW_RECORD_ANNOTATION_PASS checks=2928。保留模式草稿、取消、预设、旧记录、独立请求等验证，底筋与点筋按新布局允许向内出字。
- [x] CantileverGeometryTests：CANTILEVER_PREREQUISITES_PASS checks=4714。布局矩阵另逐项比较布局前后全部钢筋顶点/bulge、点筋坐标/数量、EffectiveF及请求编码不变。
- [x] CantileverGeometry/RebarGeometry/Dots/DrawState/DrawForm及UpstandAnnotationGeometry与a3bc0eb逐文件核对一致；旧节点服务未修改，记录及成功写入格式不变。
- [x] 原五组通过明确PASS标记；HangingNodeTests 2974项、NodeDrawWindowTests 158项；V0.4 Contour/Dots/Rebar/Window分别837/51529/1825/3180项。共14组实际编译并执行EXE通过。
- [x] 审查build.ps1、MSBuild/.NET4.8和本机AutoCAD2023托管引用，按相同Release/x64参数明确新输出目录；输出仅DLL/PDB，CAD引用Private=False。测试项目保留原有CS0649字段警告。
- [x] 左右模式4纯布局示意已实际查看，文字范围为明确测试样本，不是CAD实测字体；圆弧仅示意。
- [ ] AutoCAD2023/2024本轮均未运行，真实TEXT范围、字体替代、尺寸文字/箭头/界线及最终图面待用户验收。没有重试隔离NETLOAD、启动/关闭用户CAD或修改安全设置。

构建方式：通过Python子进程执行build.ps1所列MSBuild参数和run_v05_geometry.ps1所列14组测试项目/EXE。SDK目录读取受限后使用权限机制完成构建，没有改变环境安全设置；未把编译等同CAD通过。

新DLL：`D:\Waste\WallDtailTool\bin\V05Compact20261029\WallDetailTool.dll`

SHA-256：`296C333A9EA562D6161DC1C3E90D756137CDE62259EE841F9974A7CB7554C7B9`。

用户单一验收：在新图/副本加载此DLL，WDTCANTILEVER模式4对1500×200轮廓左右各生成一处；核对下方点筋在支座半区、底筋在自由端半区，靠近板体且无重叠，上方文字靠折点且无多余长水平线。再把E从600改10000生成比较，板侧三组标注不应被内跨远端推走。确认双排点筋均接公共竖线且整组只一条规格、文字正向、斜杠和Lin清晰，钢筋/点筋/自动F与上一版一致。旧DLL占用时由用户自行保存后重启CAD。源码提交见本轮Git交付，不推送、不打标签。

## 以下为a3bc0eb及更早历史，旧DLL不是本轮加载路径

## 2026-10-28 直角引线与模式4布局（历史交付，基线5362b96）

最新规则见 [布局说明](../docs/V05_ORTHOGONAL_LAYOUT.md)，覆盖下方历史斜段、单点接触及组别位置。

- [x] CantileverAnnotationTests：CANTILEVER_ORTHOGONAL_ANNOTATION_PASS checks=56774。左右/四模式/不同板厚与字高/单位/两根及多根点筋；水平竖直线、90°转角、正确侧别/上下、真实钢筋/内跨锚点、避开截止符号、上下两点接触、一条规格、无引线交叉或共线重复、交接斜杠、文字线上方及L尺寸行留白。
- [x] CantileverWindowTests：CANTILEVER_WINDOW_RECORD_ANNOTATION_PASS checks=2952，保留草稿/取消/预设/旧记录测试，更新90°及侧别断言。
- [x] CantileverGeometryTests：CANTILEVER_PREREQUISITES_PASS checks=4714。布局矩阵另逐项比较布局前后全部钢筋顶点/bulge、点筋坐标/数量、EffectiveF和请求记录数值，完全相同。
- [x] 与5362b96比较CantileverGeometry/RebarGeometry/Dots/DrawState/DrawForm源码（统一换行符后）一致；CAD文件只改注释，旧节点标注服务未改。
- [x] 原五组及HangingNodeTests通过（后者2974项），NodeDrawWindowTests通过158项；V0.4四组通过837/51529/1825/3180项。共14组编译并执行EXE，均有明确通过标记。
- [x] Release/x64/.NET4.8，使用本机AutoCAD2023引用；仅输出插件DLL/PDB，Private=False，无CAD引用副本。测试项目仍有原有CS0649警告，运行通过。
- [x] 左右模式4纯布局预览已实际查看，仅验证路由，示意圆弧和文字范围不能代替CAD图面。
- [ ] 真实CAD字体、正向文字、实测TEXT终点、L/h/Lin尺寸界线及用户验收待执行。2023/2024本轮均未运行CAD；不重试NETLOAD、不启动/关闭CAD、不改安全设置。

本次按审查后的build.ps1参数显式指定新输出，通过Python子进程逐项运行MSBuild和测试EXE；run_v05_geometry.ps1加入新组，脚本本身未另行重复执行。

新DLL：`D:\Waste\WallDtailTool\bin\V05Orthogonal20261028\WallDetailTool.dll`

SHA-256：`7E7A3102635E20D189B9075E6C104A8B04CDA5285B438EC0804AD51DB04E1DEF`。

用户单一验收：在新图/副本加载新DLL，WDTCANTILEVER模式4左右各生成一个节点。右伸应为伸出筋左上、附加筋右上、底筋右下、点筋左下，均先竖直再水平；左伸对应且字不反。检查上下点筋都接公共竖线、规格仅一条、斜杠仍在，文字与尺寸无重叠。预设、自动F、排筋应与上一版相同。旧DLL若占用，仅由用户自行保存后重启CAD，不自动关闭。提交以本轮Git交付为准，不推送、不打标签。

## 5362b96及更早交付历史（非当前布局口径）

## 2026-10-28 自动F、绘图预设与折线标注修订（历史交付）

源码基线2afc688。本次新增要求覆盖下方历史基线中的手填F、首次空白和水平直引。完整初值见 [预设表](../docs/V05_DRAWING_PRESETS.md)，接口/记录兼容见 [接口](../docs/V05_CALCULATION_API.md)。

- [x] CantileverGeometryTests：`CANTILEVER_PREREQUISITES_PASS checks=4714`。新增不同板厚/保护层/面底筋直径×左右×四模式，端头至实际底筋中心线恒10mm，模式4同路径；旧F=0/999不影响结果，自动F无法容纳R/直段时拒绝。
- [x] CantileverWindowTests：`CANTILEVER_WINDOW_RECORD_ANNOTATION_PASS checks=2364`。首次全部可编辑项有值、四模式可生成请求、无F输入框、成功记录优先/缺项补预设、模式切换草稿、取消不提交、单位比例、实际总数2根数量表达/多根间距（含每排2根但合计4根仍按间距）、单组折线引出/真实锚点、水平末段/文字线上方、左右及模式4引线无交叉（含E/Lin悬殊）、v1/v2记录及快照兼容、读取不改输入旧快照。全13组运行后补充总数4根案例，另单独重跑本组达到2364项；生产源码未再修改。原生TEXT实际宽度与CAD事务仍未运行。
- [x] 原五组及HangingNodeTests通过（后者2974项），NodeDrawWindowTests通过158项。
- [x] V0.4四组Contour/Dots/Rebar/Window通过837/51529/1825/3180项。旧节点标注规则源码没有修改。
- [x] Release/x64/.NET4.8、AutoCAD2023托管引用编译成功，输出仅WallDetailTool.dll/PDB，未复制CAD程序集。测试编译有既有UpstandRebarParameters未赋值字段CS0649警告，不影响运行通过。
- [x] 独立WinForms窗口渲染已查看，F为自动说明，其余数值/按钮显示完整；不等于CAD命令上下文、字体或持久化验收。
- [ ] 真实CAD折线/字体/文字避让、10mm图面量测、两根点筋标注、旧块不重画、UNDO/REDO、保存重开及多文档，均待用户验收。2023/2024均未运行CAD。

本次按审查后的build.ps1相同MSBuild参数，通过Python子进程显式输出新目录；按run_v05_geometry.ps1相同的13个测试项目逐项编译/执行EXE并检查通过标记，未启动CAD/隔离NETLOAD、未改安全设置。没有推送或打标签。

新DLL：`D:\Waste\WallDtailTool\bin\V05AutoF20261028\WallDetailTool.dll`

SHA-256：`FFD537863176178095E0ADABF93AD728DF3AA8EF80B94E994D2C156F2FCF1BA7`。

用户单一验收：新图加载新DLL，运行WDTCANTILEVER，用模式4分别选择1500×200与150×200矩形（左右各一），沿用窗口预设或本图成功值；检查无需填F、面筋和点筋为折线引出、模式4两组面筋规格独立、窄板实际合计2根时标2φ实际直径，再量测自由端头中心线比底筋水平中心线高10mm。测试图/预设不代表工程推荐。旧DLL已加载无法替换时仅由用户自行保存后重启CAD，本工具不自动关闭。

## 2afc688基线交付历史（以下不是本轮测试或当前F/标注口径）

日期2026-10-28，承接计算阶段7f7ad7a，V0.4归档基线e370995。带实际入口的候选DLL，未记用户CAD验收通过。

- [x] V0.4归档提交/标签/验收DLL SHA-256只读核对一致。
- [x] 图集PDF第120页（印刷2-54）全页渲染实际查看。
- [x] CantileverGeometryTests运行：`CANTILEVER_PREREQUISITES_PASS checks=2599`。四模式×左右路径、圆弧相切/R/bulge、投影、Lin/b、26×26符号、独立规格、允许支座突出、非法输入及点筋规则。点筋覆盖不同板长/间距/显示点径、上下共X、首步真实s、后续s、不均分、靠梁净距、仅角筋窄板、R不足/显示重叠/下排无法相切的拒绝。
- [x] CantileverWindowTests运行：`CANTILEVER_WINDOW_RECORD_ANNOTATION_PASS checks=547`。模式和方向切换、无效草稿保留、取消/关闭无请求、首次控制长度空白、成功记录重开、请求防御复制、目标文档/单位谓词、米/英寸比例/覆盖、记录和块快照往返/坏记录、独立规格、水平引线、文字位于线上、左右布局和Lin。独立WinForms渲染图已实际查看，控件及按钮可见；不代替CAD窗口和DWG验收。
- [x] 原五组及HangingNodeTests实际运行通过；后者2974项断言。
- [x] NodeDrawWindowTests通过158项（旧窗口，不代表悬挑窗口完成）。
- [x] V0.4 BeamSideContourTests/BeamSideDotsTests/BeamSideRebarTests/BeamSideWindowTests分别通过837/51529/1825/3180项。
- [x] Release/x64/.NET4.8，使用AutoCAD2023引用编译；Private=False，输出仅插件DLL/PDB。
- [x] 点筋、窗口、请求、标注布局及独立记录代码实现和自动测试。
- [x] CAD实体、正向规格、水平引线、左右布局、Lin原生尺寸、不生成b尺寸、块与记录源码编译；尚未在CAD运行。
- [ ] 悬挑板在真实CAD窗口取消、文档切换、类型间/DWG间隔离。
- [ ] 普通块、源图只读、失败回滚、UNDO/REDO、保存重开、多文档。
- [ ] 用户CAD验收；2023/2024本轮均未运行CAD。

旧BeamSideWindowTests首次编译有原有未赋值字段CS0649警告，其运行通过。没有启动/关闭CAD、重试NETLOAD或改动安全设置。本次通过Python子进程逐项执行MSBuild和测试EXE；新增可复现入口 [run_v05_geometry.ps1](run_v05_geometry.ps1)，与本次执行项目/参数相同，脚本本身未另行执行。

候选交付DLL：`D:\Waste\WallDtailTool\bin\V05Cantilever20261028\WallDetailTool.dll`

SHA-256：`C85911EBB17ECF7753602DD98DFCE553AB1143F333D5DB83BB81BB464C38FBFA`。

入口：`WDTCANTILEVER`或`WDTTOOLBAR`→悬挑板。源码提交见最终Git交付记录，未打标签或推送。编译引用本机AutoCAD2023，仅生产插件DLL/PDB输出，无CAD托管程序集副本；未安装2024依赖，本轮未验证2024。

## 用户单一验收流程

在新图/副本NETLOAD上述DLL（旧DLL若已加载且不能替换，由用户自行保存后重启CAD；不自动关闭）。绘制两处互不重叠的1500×200局部矩形，运行WDTCANTILEVER，以模式4分别右伸/左伸生成。测试输入b=300、E=600、F=100、B=200、Lin=450；主筋12@150、底筋8@200、附加筋16@100、点筋6@200；高级c25、显示线宽12、显示点径12、R20、字高62.5。这些仅是测试数据，不是工程推荐。

检查两处各为独立普通块，主筋与附加筋共路径但保留不同规格，面底筋不连接；各8根上点筋/8根下点筋逐根对齐，无竖边中部点筋，斜勾左右都向下，Lin尺寸450且没有b尺寸，文字正向/引线水平。继续取消重开窗口核对成功参数，然后对一个新块UNDO/REDO及另存重开。真实表现、遗漏标注提示、CAD版本和结果由用户反馈；本清单仍保留未验证状态。
