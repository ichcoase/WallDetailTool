# 反坎带飘板第一版验证说明

## 2026-11-05 底部开放输入（最新DLL）

用户确认范围：自动识别闭合/仅底部开放L形，不新增模式；开放确认虚拟底边后直接生成。基于HEAD `cf36f7e`及当前大量未提交实现增量维护；已核对上一DLL的SHA-256为`95331BB79B3A606DE38A1828FDFAE525B40BB6CF116AB2B560C6C2A7A5E688E0`，未恢复HEAD或重建项目。

本轮实际重新执行结果：

| 验证 | 本轮结果 | 证据范围 |
|---|---|---|
| TopSlabTests基础 | 17,870通过 | 原闭合、配筋/点筋、请求及草稿保护；原缺边拒绝夹具明确改为缺自由端边 |
| TopSlabRevisionTests | 21,504通过 | 既有三筋、净距、共同顶排、标注/尺寸与96组可算/12组明确拒绝矩阵 |
| TopSlabCollisionTests | 3,626通过 | 既有实际尺寸碰撞计算夹具；并非本轮CAD运行 |
| TopSlabOpenTests | 982通过 | 开放/闭合有效几何、三筋顶点/bulge、所有点筋及派生量、规格锚点/引线/尺寸、v3记录完全等价；左右、梁板、同/异厚、平移、毫米/无单位；正反链、乱序LINE/反向端点、拆边及闭环任意起点；错误缺边、多个开放链、非重复边的局部重叠等拒绝；确认/重选/取消/异常/文档或单位变化及离开后返回时作用域释放 |
| TopSlabReaderContractTests | 52通过（API替身） | 抽取原生产ReadBeamSideSegments及单位类执行；开放n−1实边/最后一段/不读首尾虚段、Closed/顶点不变、只读事务释放；闭合尾段、活动尾弧/非WCS/异标高/其他实体拒绝、LINE乱序反向。不是实际CAD对象或数据库验证 |
| --auto-window-only / --slab-window-only | 12+9通过 | 当前自动A、梁板草稿、h120/L150、H限制及一次选择入口 |
| NodeDrawWindowTests / BeamSideContourTests | 166 / 837通过 | 必要原窗口与原识别保护性回归；不等同真实CAD |

未修改旧共用排筋、点筋或标注规则；原五组/Hanging本轮未运行，历史结果不计本轮。测试编译有原有CS0649未赋值警告，旧窗口渲染有libpng色彩配置警告，均未导致失败。先前测试准备曾有临时输出目录及读取契约依赖缺失，修正后以上最终各项实际通过。

复现：将`TopSlabTests.csproj`构建到独立测试目录，在新建临时工作目录创建`tmp/top-slab-auto-20261104`后运行该EXE（完整专项含开放专项），再运行`--collision-only`、`--auto-window-only`、`--slab-window-only`。读取契约用`tests/run_top_slab_reader_contract.ps1 -OutputPath <独立绝对目录>`编译并运行。旧窗口/识别项目也以独立输出目录构建执行。本轮所有计算SVG输出至`D:\Waste\WallDtailTool\tmp\top-slab-open-20261105`下，没有覆盖docs审阅图、未知tmp文件或DWG。

构建：审阅build.ps1及本机VS2022 MSBuild、AutoCAD2023 AcMgd/AcDbMgd/AcCoreMgd依赖后，以绝对OutputPath独立构建Release/x64/.NET Framework4.8成功，保留AutoCAD2023/2024目标。输出目录仅DLL/PDB，未复制CAD托管程序集。

DLL：`D:\Waste\WallDtailTool\bin\TopSlabOpen20261105\WallDetailTool.dll`。

SHA-256：`8D860CB230FEDB043C1AE3AE76DA251D1570332701DE9B4E56C9639EC205354D`。

静态检查：TopSlabCad源实体仅ForRead，未设置Closed/添加封口实体；非数据库虚线由TransientManager管理，作用域及文档离开/销毁事件清理。生产ReadBeamSideSegments未修改；成功参数仍仅在CreateTopSlabBlock事务中写入并Commit，生成器未接收虚拟边元数据，不升级v3记录，不改旧类型键或历史块。以上代码检查及API替身不能证明实际CAD资源/事务清理成功。

本轮未启动/关闭或操作CAD、未重试隔离NETLOAD、未改信任/安全设置、未覆盖用户DWG，未自动提交/推送/打标签/发布；无本轮提交标识。保留全部既有未提交工作与未知文件。

- [x] 计算及模拟确认作用域验证通过，闭合输入无新增询问。
- [ ] CAD开放多段线及LINE链真实选择、高亮、虚线及中文确认/默认回车、重选/Esc草稿保留；确认/重选/取消/异常/文档切换时清理。
- [ ] CAD源对象Closed/顶点/属性不变、无永久封口线、块内无虚拟边，失败/取消不更新成功记录。
- [ ] CAD事务回滚/撤销重做、保存重开、多文档/单位变化及2024；实际字体、颜色、尺寸样式/避让。

用户一次验收任务：在新图或副本中，用同尺寸同参数的闭合L形与仅缺底边的开放L形各生成一次，核对结果一致、开放虚线确认范围清楚，生成后虚线消失且源轮廓未被补线。

修改文件：TopSlabGeometry.cs、TopSlabContourInput.cs、TopSlabCad.cs、TopSlabForm.cs、WallDetailTool.csproj；tests/TopSlabTests.cs/.csproj、TopSlabOpenTests.cs、TopSlabReaderContractTests.cs/.csproj、run_top_slab_reader_contract.ps1；PRD.md、ARCHITECTURE.md、DEVELOPMENT_PLAN.md、README.md、docs/TOP_SLAB_QUICK_START.md、docs/TOP_SLAB_V1.md、docs/help/README.md及本清单。

## 2026-11-05 板上默认值120/150（历史DLL）

用户确认本组合节点板上h默认120mm，L外/L内默认150mm；只改新节点试用值和对应说明，H口径、梁上默认值、已有成功参数优先及几何不变。本次实际窗口9+12=21项通过，独立Release/x64/.NET4.8构建成功；本轮未重跑计算专项，下方上一轮43,000项为此前结果。新DLL：`D:\Waste\WallDtailTool\bin\TopSlabSlabDefaults20261105\WallDetailTool.dll`；SHA-256 `95331BB79B3A606DE38A1828FDFAE525B40BB6CF116AB2B560C6C2A7A5E688E0`。CAD未加载此DLL；未提交、推送或发布。验收时在无本类型成功记录的新图检查120/150默认值，已有成功记录不会被覆盖。

## 2026-11-05 板上底部参数整理（历史DLL）

- 参数对齐旧板上反坎：h/H/L外/L内，内侧可留空随外侧；试用h200、两侧L300、H=保护层+显示线宽/2，B60。成功记录优先，类型键和事务保存不变；梁上h/H隐藏，草稿分开。
- 本次实际通过板上窗口9项、原本节点窗口12项，覆盖默认H29/31、字段顺序、空内侧解析、H<h阻止选择、梁板切换及源数组保护。节点基础17,870+修正21,504+碰撞夹具3,626=43,000项实际重新运行通过。仅新节点UI/默认参数读取适配变化，未改共用几何，原五组/Hanging本轮未运行；历史结果不计本轮。
- Release/x64/.NET4.8新目录构建成功：`D:\Waste\WallDtailTool\bin\TopSlabSlabUi20261105\WallDetailTool.dll`；SHA-256 `4FBD03AD2AD4FAA649D1397DD3AE0BFC111E2B29E9FF2875D4E1A9759074DD0E`。只含DLL/PDB，无CAD程序集；测试有原有CS0649警告。未提交、推送或发布。
- 真实CAD尚未加载本DLL，实际选择/取消/事务、DWG保存重开及多文档/2024待验；自动窗口不替代CAD。验收任务：NETLOAD新DLL，选板上，检查h/H/L默认值，清空L内，用测试轮廓选择并生成，核对两侧水平直段同长。

## 2026-11-05 两处10mm净距（历史DLL，规则仍适用）

- 用户确认按显示可见边缘净距计算。红蓝水平段净距10；端头至左角圆点按实际平端外角最短净距10；左角点贴红竖段，其余顶点贴蓝水平段，全部同Y同步向下。左右、梁/板及不同线筋计算直径/显示宽度/点径由专项覆盖。请求记录v3兼容读v1/v2，块快照仍保存实际自动A。
- 最终实际通过：基础17,870、修正21,504、实际CAD碰撞夹具3,626，共43,000项；另窗口12项。96组矩阵可算，12组固定行碰撞明确拒绝。旧250间距案例因新标高不再碰撞，独立侧排拒绝夹具更新为实际相撞的240，未放松检查。只改新节点，旧共用规则未改，本次未重跑原五组/Hanging。
- 新独立构建Release/x64/.NET4.8成功：`D:\Waste\WallDtailTool\bin\TopSlabGap20261105\WallDetailTool.dll`；SHA-256 `3DBACBAF6A196990D05BAE4B250438D43CC9C6D49A270D2BCD2497F7A9B8C98B`。最终仅修正源码注释后重建并重新取哈希，测试覆盖的逻辑未变。仅DLL/PDB，无CAD程序集。测试项目有原有CS0649警告。
- 三张SVG/PNG在 docs/top-slab-gap-review，已实际查看，近景用真实显示边缘标两处10mm；颜色仅审阅。完整回归在独立tmp工作目录生成，不覆盖历史审阅图。
- 新DLL未在CAD加载，真实图面/命令上下文、记录事务/保存重开、2024仍待验。验收任务：在新会话NETLOAD本DLL，用原轮廓参数核对两处10mm及顶部共线，选完回车直接生成。未提交/推送/打标签、未覆盖用户DWG。

## 2026-11-04 顶排避弧修复（历史DLL）

- TopSlabDots仅修正固定共同Y下的顶排可用X区间；原三根线筋、共同Y、同步侧排及几何构造不改。实际CAD试例：反坎/飘板厚322.938364858277/322.579945494302，总高1287.12590021827、飘出1451.62391706825；三筋8@150、B60、两端锚300、显示宽8/点径12/R20。顶点X39→39.2020410288673，Y1240.12590021827不变。
- 本次实际通过：TopSlabCollisionTests 3,610项（同一实际尺寸、左右、梁上/板上、间距150/200、全体点筋至全部真实弧/直段距离、顶排间距/同Y、侧排同步及线筋端头保持；250独立侧排碰撞继续拒绝）；原新节点基础17,870项＋修正21,408项。新增专项最终重建后重新执行。共用规则未改，本次未重跑原五组/Hanging，历史结果不计本次。
- 三张SVG及PNG位于 docs/top-slab-collision-review，均由修正后的实际计算结果生成，并实际打开PNG查看。近景白虚线圈为旧点位，仅用于对照；颜色仅审阅。
- Release/x64/.NET4.8构建成功：`D:\Waste\WallDtailTool\bin\TopSlabContact20261104\WallDetailTool.dll`；SHA-256 `8BD8FD146A1C2E2FD40816E0D364009C50983022FB6B1E482F7E99366C210D85`。目录仅DLL/PDB，无CAD程序集。测试项目保留原有CS0649警告。
- 新DLL未加载CAD；本次纯计算通过不代表CAD图面/事务/保存重开或2024验收。没有自动提交/推送/标签，没有改用户DWG。实际验收任务为用同一失败轮廓、原200间距加载新DLL重试，核对顶部避弧及一次选择直接生成。

## 2026-11-04 选择即生成与梁上默认（本轮）

补充真实 CAD 2023 测试：用户指出应使用 NETLOAD，APPLOAD 失败后改用 NETLOAD 加载本轮 DLL，节点窗口可打开；新图首次显示 B60、两侧锚固300及“选择轮廓并生成”。实际绘制闭合右飘轮廓，识别尺寸约反坎厚322.939、板厚322.580、总高1287.126、飘出1451.624 mm。默认线筋均8@150、点筋均6@200时，选完回车直接进入计算，报告点筋与交叉筋冲突并返回原参数窗口；将反坎点筋间距分别改150、250后再次尝试，均报告同一冲突，未生成节点。本次证明窗口/选择及失败恢复实际执行，不证明成功生成路径通过；手绘测试轮廓不同于既往成功试例。未关闭 CAD、未覆盖用户 DWG。

- 梁上首次 B60、外锚300、内锚默认跟随外锚；B是测试试用值，锚固300复用旧梁上试用预设，均非工程设计建议。成功记录优先，手动覆盖内锚后可独立输入；板上值及两类草稿独立。
- 参数有效后点击“选择轮廓并生成”，识别成功直接生成，取消/失败保留草稿。取消窗口不保存，成功记录仍由事务写入；目标文档/单位校验不变。
- 本轮实际通过：TopSlabTests 基础 17,870 项、TopSlabRevisionTests 21,408 项；专项窗口 12 项；NodeDrawWindowTests 166 项。仅修改新节点窗口及流程，未改共用排筋/标注规则；没有将旧五组或 HangingNodeTests 历史结果写为本次通过。完整专项在独立临时工作目录运行，避免覆盖现有审阅图。
- Release/x64/.NET Framework 4.8 构建成功：`D:\Waste\WallDtailTool\bin\TopSlabFlow20261104\WallDetailTool.dll`；SHA-256 `1B8DB887C03611556D18C70E5141EFD6DDFFD07732E58EEE4DFCD92B400D8938`。目录只含 DLL/PDB，未复制 CAD 程序集。测试项目有原有 CS0649 警告。
- 初次交付时未在CAD加载本DLL；后续NETLOAD及冲突/失败恢复已实际执行，见本节段首。成功生成路径、事务、保存重开、多文档与2024未通过本次验证。未提交/推送、未修改用户DWG。

## 2026-11-04 自动A交付（历史DLL）

用户已确认自动端头10mm中心线留距。自动A、绿色下弯避让、贴蓝色水平段下侧的共同顶排及同步侧排完成；只修改新节点适配层，旧共用规则、上下节点共用计算不改。

本轮实际10组全部通过：TopSlabTests（内含TopSlabRevisionTests）、CantileverGeometryTests 4,714、CantileverAnnotationTests 87,644、原五组UpstandGeometryStage1Tests/SlabUpstandGeometryTests/UpstandRebarStage2Tests/SlabUpstandRebarTests/UpstandAnnotationStage3Tests、HangingNodeTests 2,974、NodeDrawWindowTests 166。最终新节点基础17,870项+修正21,408项=39,278项，另--auto-window-only 5项。最后局部改动后已重建并执行最新专项；不是复述上轮结果。

参数矩阵96组成功、12组因固定贴筋排与真实交叉筋冲突而明确拒绝，异常类型/原因均有断言，左右一致。覆盖自动10mm端头、旧手填A不再生效、B继续草稿隔离、v2自动槽及块实际A记录、不同厚度/三筋直径/显示宽度与点径、绿色下弯避让、顶排各点贴蓝色真实直段、同步Y、点筋域归属、实际碰撞、规格双排真实锚点和共线顶面尺寸。

最终Release/x64/.NET4.8 DLL：`D:\Waste\WallDtailTool\bin\TopSlabAuto20261104\WallDetailTool.dll`

SHA-256：`EC6494F3C141D2A7F2D4B6110E22B00F005EAA876604C5CF8BBC4CEB2E6552DD`

已核对build.ps1及本机VS2022/AutoCAD2023依赖。普通沙箱构建读Microsoft SDK目录受拒后，经权限机制完成MSBuild；未改安全/注册表设置。输出未复制CAD托管程序集。无本轮提交标识，未提交/推送/打标签/发布，保留既有工作及未知文件。

审阅图：[右飘完整](../docs/top-slab-review/right-full.svg)、[阴角放大](../docs/top-slab-review/right-junction.svg)、[不同厚度左飘](../docs/top-slab-review/left-different-thickness.svg)，同目录有PNG。试例A206mm、下弯X55mm、顶部Y743mm均为计算结果。实际打开查看SVG渲染图，蓝/黄色仅审阅点筋组颜色，不能证明生产CAD颜色或字体通过。

CAD验收任务：在副本中用之前同一轮廓/参数运行WDTTOPSLAB，A保持自动，核对自动端头、绿色下弯与点筋间隙、顶排贴水平段及同步下排，提供完整节点与阴角放大图。实际CAD字体/颜色/尺寸样式/避让、撤销重做、保存重开、多文档和2024仍待验。本轮未启动/关闭CAD、未重试隔离NETLOAD、未覆盖DWG。

本轮修改TopSlabRebar.cs、TopSlabDots.cs、TopSlabForm.cs、TopSlabCad.cs及新节点测试/预览/项目文档。下方为历史交付证据。

## 2026-11-03 交付（历史DLL）

本轮新节点基础专项18,638项、修正专项24,156项，共42,794项通过。覆盖顶面定义点、两水平尺寸线同Y/原X数值；内外侧筋标注侧别；两类点筋各一文字、两排真实锚点；共同顶部与中间点同Y、左右侧排严格同步s；不同线筋直径、厚度、显示尺寸/左右方向；真实碰撞无解拒绝、不独移侧排或删点。三筋原路径逐顶点/bulge回归保留。加大测量文字后的内侧/板下规格不重叠断言通过。

本轮实际运行10组：TopSlabTests（含TopSlabRevisionTests）、CantileverGeometryTests 4,714、CantileverAnnotationTests 87,644、原五组UpstandGeometryStage1Tests/SlabUpstandGeometryTests/UpstandRebarStage2Tests/SlabUpstandRebarTests/UpstandAnnotationStage3Tests、HangingNodeTests 2,974、NodeDrawWindowTests 166。最后适配层修改后已重新构建/执行新节点专项。旧共用规则未改，原五组和Hanging为保护性实际回归；不将旧日志计为本轮结果。

Release/x64/.NET4.8最终构建成功。DLL：`D:\Waste\WallDtailTool\bin\TopSlabAligned20261103\WallDetailTool.dll`

SHA-256：`8C98243A6592BDFAF1849A103F564E99815A4CA0DF368902600925F9F468E365`

审查build.ps1及本机依赖，仍用VS2022 MSBuild和AutoCAD2023托管程序集，未复制发布CAD程序集。当前Git基线cf36f7e；未提交、推送或打标签，保留所有先前未提交工作。

用户确认取消旧两处不同标高的弧相切要求，改共同直段相切。共同顶部必须仍处于顶部混凝土节点域，选全部同步行均无碰撞的最高可行相位；不改线筋、R/A/B。预览试例共同Y729，向下729/529/329/129，间距200mm；数值来自试例，不是新增构造常数。

已更新并渲染查看[右飘完整](../docs/top-slab-review/right-full.svg)、[阴角放大](../docs/top-slab-review/right-junction.svg)、[不同厚度左飘](../docs/top-slab-review/left-different-thickness.svg)，同目录有PNG。放大图标出共同顶部Y及向下间距。SVG使用测量夹具，生产使用CAD实测TEXT范围，不证明CAD字体/样式或避让通过。

尚待用户：在副本中用之前同一轮廓/参数运行WDTTOPSLAB，检查共线顶尺寸、内侧筋标注、点筋单文字双排引出、顶部共线及同步排布，提供完整节点和阴角放大图。CAD实际字体/颜色/尺寸样式/避让、UNDO/REDO、保存重开、多文档和2024均未在本轮验证。不启动/关闭CAD、不重试隔离NETLOAD、不改安全设置、不覆盖DWG。

本轮修改TopSlabDots.cs、TopSlabRebar.cs（共同标高元数据）、TopSlabAnnotationGeometry.cs、TopSlabCad.cs及新节点测试/审阅图/文档，未改变旧通用规则。下方为2026-11-02历史证据，当前DLL以上方为准。

日期：2026-11-02。源码基线：`cf36f7e`，本轮未提交、未推送、未打标签。用户图集/SVG仅本地查看；初始未跟踪design-preview与tmp保留。

## 2026-11-02 上一轮四项修正的实际证据（历史）

本节为此次修正重新运行的结果。下方首次交付结果仅作历史，不替代本次记录。

- 新节点基础专项17,486项、新增归属/交接/布局专项24,652项，共42,138项通过。最终补充的真实角筋—侧排及顶排整链间距断言，已单独重建并执行最新专项。
- 两个专项覆盖：反坎域中无飘板点筋（含显示圆外缘）、反坎真实角筋复用旧公式、无冲突保留原SideFirst、有交叉障碍整段重排、各排/角筋真实中心距、交接不重叠、两组间距互不驱动、不同厚度/计算规格/显示大小、左右镜像、三根原始路径逐顶点和bulge固定值回归；标注分区、实测宽高夹具与加大文字后的层间距、水平尺寸上置及原起止点不变。
- 本轮实际通过共10组：TopSlabTests（内含新增TopSlabRevisionTests）、CantileverGeometryTests（4,714）、CantileverAnnotationTests（87,644）、UpstandGeometryStage1Tests、SlabUpstandGeometryTests、UpstandRebarStage2Tests、SlabUpstandRebarTests、UpstandAnnotationStage3Tests、HangingNodeTests（2,974）、NodeDrawWindowTests（166）。不是复述上轮通过结果。
- 本轮没有修改共用几何/点筋/标注规则；原五组及Hanging仍完整运行作为保护性回归，没有新增共用BUG或下挂分支。
- 审查build.ps1及本机依赖，使用同一VS2022 MSBuild主项目配置；本次最终Release/x64/.NET Framework4.8构建成功，引用本机AutoCAD2023目录三项托管程序集，输出目录未复制它们。AutoCAD2024未运行。
- 新目录DLL：`D:\Waste\WallDtailTool\bin\TopSlabFix20261102\WallDetailTool.dll`
- SHA-256：`C3BD1A7070E41525BC4C6DA87CDFDAB5359D99B4A6BADF09B2BB5E32E6F447AF`
- 无本轮提交标识；未提交、推送、打标签或发布。初始未提交修改及未知文件保留。

审阅图已实际渲染并查看：[右飘完整SVG](../docs/top-slab-review/right-full.svg)、[同例阴角SVG](../docs/top-slab-review/right-junction.svg)、[不同厚度左飘SVG](../docs/top-slab-review/left-different-thickness.svg)。同目录提供PNG；蓝色反坎点筋、黄色飘板点筋、虚线内侧分界，仅审阅色。示例首列中心偏距54.670580551mm、外缘净距48.670580551mm是旧递推规则的计算余量；不是固定首根偏距。

颜色证据为源码核对和本次编译：原梁上/板上创建过程规格/引线ByLayer默认71，尺寸线/界线ByLayer默认70、文字显式7。新节点文字/引线/斜杠显式71，尺寸实例/Dimclrd/Dimclre显式70，Dimclrt显式7；保留字体/TSSD_25与箭头服务，已有层色/样式不修改。不能把这项静态核对说成CAD颜色验收。

## 2026-11-02 CAD验收任务（历史，最新任务见文首）

在图纸副本中加载本轮新DLL，用之前出问题的**同一轮廓和参数**运行 `WDTTOPSLAB` 重新生成，核对：①规格/引线/尺寸颜色；②三组竖向规格在外侧、两组飘板规格在下方；③反坎厚和飘出长度均在上方；④反坎域内没有黄色示意所代表的飘板点筋，阴角三根线筋保持交叉。请提供**CAD完整节点及阴角放大图**，不要覆盖原DWG。

真实CAD字体/颜色/尺寸样式及图面避让、命令上下文、高亮/取消、UNDO/REDO、保存重开、多文档和2024均未在本轮验证。独立SVG及自动测试不能证明这些通过。本轮未启动/关闭用户CAD、未尝试隔离NETLOAD或改变安全设置。

## 首次交付实际完成（历史记录，不作为本轮重新通过）

- Release / x64 / .NET Framework 4.8；审查build.ps1后使用其同一MSBuild及主项目配置，以独立目录构建。引用本机AutoCAD2023程序集，不复制发布。目标仍为AutoCAD2023/2024，但不表示2024已实测。
- 新节点专项 `TopSlabTests`：20,942项通过。左右、梁上/板上、同厚/不同厚、三筋不同直径及独立交叉、自由端复用10mm规则、点筋内侧/直段相切/行间距、起点/顺逆/边端反向/选择洗牌/共线拆边、非法轮廓、非法A/B/单位、不可变请求及目标文档、类型独立键、窗口原始草稿防御复制及输入不改写。
- 原五组全部实际通过：UpstandGeometryStage1Tests、SlabUpstandGeometryTests、UpstandRebarStage2Tests、SlabUpstandRebarTests、UpstandAnnotationStage3Tests。
- HangingNodeTests 2,974项、NodeDrawWindowTests 166项、CantileverGeometryTests 4,714项、CantileverWindowTests 2,928项通过。共10组（包括新专项）。旧共用计算无算法修改，Round仅开放内部可见性。
- 新专项会输出纯计算SVG和独立WinForms截图至本轮专属tmp目录，两者已打开查看。独立窗口测试并非CAD动态验收。
- Git已跟踪文件差异空白检查通过；新文件纳入旧式csproj；未将CAD托管程序集复制到输出目录。

## 首次交付工件（历史，当前请使用上方新目录）

DLL：`D:\Waste\WallDtailTool\bin\TopSlab20261102Final\WallDetailTool.dll`

SHA-256：`8DD9E26DDE2E0036A63E440368E51CAE03B7D288669E229BEC12309459E6E116`

本次实际依赖：`D:\Software\AutoCAD 2023` 的AcMgd/AcDbMgd/AcCoreMgd，Visual Studio 2022 Community MSBuild。本目录未复制上述CAD程序集。生成节点所需的DLL已构建，CAD未启动；无本轮提交标识。

## 未验证（不得当作通过）

- CAD真实窗口焦点/DPI、一次选择全部组成实体、轮廓高亮/取消清理、目标文档切换及异常命令上下文。
- 生成普通块图面、实测字体范围、规格引线/交接与四项尺寸避让、锁层及事务异常无残留。
- 实际成功键/块快照的DWG事务、类型隔离、UNDO/REDO、保存重开、多文档、复制删除及AutoCAD2024。
- 原生BEDIT后参数查看为生成时快照；侧边栏关联、参数驱动重生成未实现，界面/命令均明确说明。
- 结构构造的工程适用性和设计数值仍需工程师审核。

## 首次交付建议任务（历史，本轮任务以上方四项核对为准）

在毫米新图或图纸副本加载上方DLL，运行`WDTTOPSLAB`：选择一条完整L形闭合边界（可用反坎厚300、总高800、飘板厚200、飘出900的试图），填写经设计确认的A/B与底部锚固参数，生成后检查三筋交叉、点筋在内侧及四项尺寸，再试一次UNDO/REDO。不要覆盖原DWG。首次A/B和底部长度为空，是要求输入设计值，不是缺省15d或La。

设计依据、参数起止、文件和边界见 [本节点说明](../docs/TOP_SLAB_V1.md)。

修改文件：TopSlabGeometry.cs、TopSlabRebar.cs、TopSlabForm.cs、TopSlabCad.cs；CantileverRebarGeometry.cs、UpstandToolbar.cs、WallDetailTool.csproj；TopSlabTests.cs/.csproj；PRD.md、ARCHITECTURE.md、DEVELOPMENT_PLAN.md、本说明、docs/TOP_SLAB_V1.md及docs/top-slab-detail-options.svg。没有批量纳入未知文件。
