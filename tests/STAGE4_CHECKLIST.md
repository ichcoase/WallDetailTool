# V0.1 阶段 4：普通块与参数面板检查清单

## 2026-10-22 面板入口修正

用户反馈 `WDTPANEL` 未看到梁上反坎参数页。新版 `WDTPANEL` 直接显示唯一的“梁上反坎”面板，不再把历史“钢筋”页加入侧边栏。历史命令保留，避免破坏既有 DWG/命令兼容。须重启 AutoCAD 后 `NETLOAD` 新目录 DLL，旧版已加载程序集不会因磁盘 DLL 更新而自动替换。实际界面显示待用户复核。

面板修正版 DLL：`D:\Waste\WallDtailTool\bin\V01BlockStage4PanelFix\WallDetailTool.dll`；SHA-256：`CCAED1BB5191F40516315C5CDDA8CA25FE05AA435F82298CCD763D2B4FEE8FFA`。下文旧 DLL 路径仅是上一轮构建记录，本次验收请使用面板修正版。

基线：2026-10-22 用户反馈阶段 3 的标注样式、图层和字高已人工调到复核预期。此反馈只适用于用户当时的图纸与阶段 3 外观；阶段 4 新 DLL 的块内显示仍需逐项复核。用户随后要求钢筋规格为单行 TEXT，因此块内沿用现有 TEXT，不改回需求书较早的 MTEXT。

标准测试图：`WallDetailTool_V01_Stage4_BlockPanel.dwg`，由阶段 3 标准轮廓测试图复制，尚未运行插件或保存任何生成结果。操作时再复制一份，避免覆盖标准图。阶段 1–3 清单继续有效。

本轮编译 DLL：`D:\Waste\WallDtailTool\bin\V01BlockStage4Final\WallDetailTool.dll`；SHA-256：`A08E1EF088CA1D47147CFBAC1606FA886499CF948335039D01D963E6C9635FDE`。标准测试 DWG SHA-256：`A3382D88FB71EC217E1F8E205180028FEE5B0B59BEEDC33ED6D8B767810EA655`。

| 项目 | 检查操作与预期 | 本轮证据 |
| --- | --- | --- |
| 块内容 | `WDTUPDRAW` 或 `WDTPANEL`→梁上反坎生成，选有效轮廓并确认；块表增加一个新定义，模型空间增加一个 INSERT，内部含 1 条 U 筋、所有点筋、引线、两条 TEXT、两个 RotatedDimension；不含原混凝土轮廓 | 待验证 |
| 基点/角度 | `LIST` 查 INSERT 插入点为反坎左下角、旋转 0°；MOVE 后整体移动 | 待验证 |
| 批次独立 | 在两处生成，两个 INSERT 的 BlockTableRecord 不同；BEDIT 第一批不改变第二批 | 待验证 |
| COPY 同定义 | COPY 第一批 INSERT，两个引用同一 BlockTableRecord；BEDIT 其中一个后另一个同步显示修改 | 待验证 |
| BEDIT/EXPLODE/ERASE | 可改 TEXT、引线和尺寸；EXPLODE 后为独立原生图元；ERASE 引用不改原轮廓 | 待验证 |
| 外观回归 | 与阶段 3 已复核效果逐项比较 TSSD/TSSD_25、文字 62.5、钢筋文字 ByLayer、尺寸文字白色、S-TEXT/s-floor_de_dim、文字位置、引线与斜杠；已有图层/样式属性不改 | 待验证 |
| 取消/关键失败 | 取消预览或过大 R/过窄反坎时不新增块定义、引用、图层或上次确认参数 | 待验证 |
| 标注部分失败 | 故意让一项标注无法创建，剩余有效图元均位于本次同一个块；逐项提示，无游离实体 | 待验证 |
| 撤销重做 | 确认后一次 UNDO 撤销本次块、图层、参数及需要时创建的样式/箭头；一次 REDO 还原；原混凝土不受影响 | 待验证 |
| 保存重开 | 另存测试副本，关闭重开，块内容、每块扩展字典参数、当前 DWG 上次确认参数不变 | 待验证 |
| 面板常用/高级 | WDTPANEL 的“梁上反坎”页中常用参数可见，高级默认折叠；锚固无软件虚构默认；R 初始为 20 | 待验证 |
| 面板持久化 | 保存用户默认值后重启 CAD，在新 DWG 中读取；同一 DWG 确认生成后重开仍读该图上次确认值；取消时不改变后者 | 待验证 |
| 恢复默认 | 面板点击恢复初始值后 R=20、锚固栏为空；默认值立即保存为用户级设置，重启后保持 | 待验证 |
| 单位/回归 | 1 CAD 单位=1 mm；三类轮廓、阶段 1–3 纯计算、WDTTEST、WDTREBAR、WDTUPCONTOUR、WDTUPDOTSIZE 回归 | 纯计算通过；CAD 命令待验证 |

本次已执行：Release/x64/.NET Framework 4.8 编译；阶段 1、2、3 的纯计算测试均输出 PASS。尝试在 AutoCAD 2023 Core Console 加载新 DLL 并运行 `WDTTEST`，进程在设置当前 profile 时退出（exit code 1），未到 `NETLOAD` 或 `WDTTEST`；未改全局安全或信任设置。故 AutoCAD 2023 插件命令/图形界面、AutoCAD 2024 插件命令/图形界面、用户对阶段 4 的人工验收均待验证。此前用户确认阶段 3 视觉效果，不能转记为阶段 4 通过。
