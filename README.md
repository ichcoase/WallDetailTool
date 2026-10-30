# WallDetailTool

用于AutoCAD二维墙身节点配筋大样的C#插件。通过参数窗口、已有轮廓或两点矩形生成钢筋、点筋、规格文字、尺寸及独立普通块，保留原始轮廓。

当前为 **V0.5 开发交付阶段**，最新功能源码提交`2a4f821`（2026-10-30）：统一参数界面、试用预设、本地帮助，以及梁/板示意和悬挑参数高亮修正。未发布正式1.0。产品目标为AutoCAD 2023/2024、.NET Framework 4.8、x64；2024仍待实际验证。

## 已有功能

| 工具条入口 | 支持构造 | 窗口命令 |
|---|---|---|
| 梁节点 | 梁上反坎、梁下下挂 | `WDTBEAMNODE` |
| 板节点 | 板上反坎、板下下挂 | `WDTSLABNODE` |
| 梁侧挂板 | 向左/向右、局部/完整截面识别、上下端弯折方向 | `WDTBEAMSIDE` |
| 悬挑板 | 普通水平等厚板、向左/向右、四种支座模式 | `WDTCANTILEVER` |
| 高级设置 | 保护层、显示宽度/点径、R、字高及图层 | `WDTUPSETTINGS` |

悬挑板四种模式：无内跨支座端向上、无内跨向下、全部由内跨板伸出、内跨伸出筋＋独立附加筋。模式④两组筋沿同一路径表达，各自保留规格和身份；面筋与底筋独立。自动F使面筋自由端端头高于底筋水平中心线10mm；Lin从内跨侧梁面起算，不含梁宽。

各窗口提供当前模式相关输入、原生示意、参数焦点高亮/起算说明、恢复试用预设和离线帮助。优先当前DWG本类型有效成功参数，其次已有共享设置，再补首次预设。恢复只改当前模式/方向的数字草稿，不重置共享高级设置；取消丢弃草稿，成功生成才保存。**预设是绘图试用值，不是规范推荐。**

新生成结果使用普通块，原轮廓只读；历史块不会自动更新。保留旧命令，包括`WDTUPFORM`、`WDTBEAMHANGDRAW`、`WDTUPSLABDRAW`、`WDTSLABHANGDRAW`及原基础命令。

## 快速使用

1. 在测试新图或图纸副本中，用`NETLOAD`加载当前构建的`WallDetailTool.dll`，输入`WDTTOOLBAR`打开工具条。同一CAD会话已加载旧版时，请先保存工作，按需重启CAD后加载新版。
2. 打开节点窗口，先选方向、构造模式和输入方式；默认“选择已有轮廓”。按需调整数字，聚焦字段查看起算说明，也可打开“本节点示例与帮助”。
3. 点击“选择轮廓并生成”选择现有受支持轮廓；或选“两点矩形”，点击“点两个对角点并生成”后点取两个对角点。顺序不限，第二点使用原生矩形范围预示；只定义局部计算范围，不创建混凝土矩形，不代表完整支承边界。
4. 核对普通块、配筋和标注。按本节点限制调整不适合轮廓的参数；工具不会为放入轮廓自动缩R或改设计长度。真实CAD的撤销/重做等专项状态见下文。

试画可参考：梁/板节点局部300×400，梁侧挂板300×600，悬挑板1500×200；参数见[完整试用预设表](docs/USABILITY_PRESETS.md)和[六类本地示例](docs/help/README.md)。这些尺寸不保证适合已有的自定义高级设置。

## 最新本机交付工件

以下是开发机实际核对的文件，**不是GitHub下载链接**。仓库提交源码、文档和测试，不提交DLL、AutoCAD程序集、用户DWG、配置或日志；其他电脑需按下文构建。

```text
D:\Waste\WallDtailTool\bin\SketchFix20261030\WallDetailTool.dll
SHA-256: 128213D6492E3C35BAAF3943ED1249DFCCEE390C0BE4B0A7A04B2BF9DA3C9F2B
```

该工件为Release/x64/.NET Framework4.8，输出目录仅DLL/PDB，CAD托管引用未复制。完整交付见[最新示意修正记录](docs/SKETCH_DIAGRAM_FIX.md)。本次README更新只改文档，不重编译或重跑测试。

## 构建与自动测试

需要Windows、Visual Studio 2022 MSBuild、.NET Framework 4.8 Developer Pack及本机AutoCAD托管引用。`build.ps1`支持指定AutoCAD安装目录和独立输出目录；MSBuild路径按现有脚本配置。

```powershell
.\build.ps1 -AutoCADDir 'D:\Software\AutoCAD 2023' -OutputPath 'bin\LocalBuild\'
.\tests\run_v05_geometry.ps1 -OutputPath 'bin\LocalChecks\'
```

最近功能交付实际运行18组独立测试均通过，包含原五组及HangingNodeTests、窗口/记录、两点输入、悬挑/梁侧几何与标注，以及新增示意专项1048项、易用性105项。该入口只运行独立.NET测试，不启动CAD。计数和结果见[最新记录](docs/SKETCH_DIAGRAM_FIX.md)；自动测试通过不等于真实CAD专项通过。

## 验证状态与限制

- V0.3.1、V0.4已有用户总体认可/验收记录；V0.5前阶段用户对效果基本认可并收口，后续统一界面及示意修正已构建和自动测试，真实CAD仍待用户验收。旧反馈不扩展为每个专项都通过。
- 只支持既有WCS正交二维范围。两点方式不支持旋转/倾斜UCS或非正交顶视图；不能提供完整梁/板边界，局部板厚、梁宽等仍需窗口参数。
- 输入参数为毫米；梁上/下、板上/下保持1CAD单位＝1mm。梁侧/悬挑沿用已有单位解释/换算，不修改INSUNITS；未开发绘图/出图比例功能。
- 悬挑暂不包含加反坎、下挂、变厚/斜板或内跨高差；不自动推荐La或输出规范合格结论。结构构造须人工审核。
- 实际CAD字体/TEXT测量、避让、高DPI、命令上下文、拾取/动态预示及取消清理、一次UNDO/REDO、保存重开、多文档/参数隔离和AutoCAD2024等保留待验状态。窗口位图和纯计算测试不能替代这些专项。
- 历史块不自动修改；新节点的既有块编辑联动、复制关联和复杂轮廓扩展未在本阶段接入。本轮不启动CAD、不重复已失败的隔离加载或改信任设置。

## 文档导航

- [产品需求](PRD.md)、[架构](ARCHITECTURE.md)、[开发计划](DEVELOPMENT_PLAN.md)
- [完整预设及记录兼容](docs/USABILITY_PRESETS.md)、[帮助与六类示例](docs/help/README.md)
- [统一界面与标注映射](docs/USABILITY_IMPLEMENTATION.md)、[统一界面验收清单](tests/USABILITY_CHECKLIST.md)
- [最新示意修正与验证](docs/SKETCH_DIAGRAM_FIX.md)、[V0.5前阶段收尾](docs/V05_STAGE_CLOSEOUT.md)
- [两点矩形输入边界](docs/V05_TWO_POINT_RECTANGLE.md)、[端部长度数字定义](docs/V05_END_LENGTH_TEXT.md)
- [历史版本说明](VERSION_HISTORY.md)
