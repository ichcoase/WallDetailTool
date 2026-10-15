# WallDetailTool

AutoCAD 2023/2024 C# 插件，.NET Framework 4.8 / x64。

## 加载及使用

1. 如果当前 AutoCAD 已加载旧版 DLL，请保存图纸并重启 AutoCAD。
2. 输入 NETLOAD，选择 D:\Waste\WallDtailTool\bin\Phase2\WallDetailTool.dll。
3. 如有信任路径提示，将 bin\Phase2 目录加入 AutoCAD 的受信任位置。
4. 输入 WDTPANEL 打开钢筋侧边栏；输入保护层和钢筋直径，点击“选择轮廓并预览”，在图中选择二维多段线并点选偏移方向。红色线仅是临时预览，点击“确认生成”后才写入图纸；点击“取消预览”或关闭面板会清除预览。修改参数后需重新选择轮廓并预览。
5. 也可继续使用 WDTREBAR 命令直接生成。
6. WDTTEST 保留，输出 WallDetailTool loaded successfully!。

WDTREBAR 的操作：

1. 选择开放式、纯直线段的二维多段线。
2. 输入保护层厚度（默认 25 mm）和钢筋直径（默认 12 mm）。
3. 在线段中部旁点选所需的一侧；线上、端点或等距拐角处需重新点选。
4. 生成独立的钢筋多段线，原轮廓保持不变。默认宽度 12 mm、中心线偏移 25 + 12 / 2 = 31 mm。

输入尺寸始终为毫米，按图纸 INSUNITS 换算。未设单位时会提示按 1 图形单位 = 1 mm 处理。
支持轻量二维多段线和非拟合的旧式二维多段线；支持轮廓平面法向和标高。
不支持圆弧、闭合轮廓、三维或拟合多段线，不包含弯钩、锚固、标注或持久化关联。
侧边栏只是最小生成流程，目前尚不管理节点、钢筋组或图纸关联数据。预览期间若原轮廓、单位或绘图空间变化，确认会拒绝并要求重新预览。切换/关闭图纸时清除预览。

## 安全检查

使用 AutoCAD 原生 GetOffsetCurves，不自行计算偏移折线。
源轮廓仅以只读方式打开，所有处理使用临时副本；检查完成后才提交新实体。
检测零长度/过短线段、无效坐标、自交、非相邻接触、重叠和折返。
偏移结果必须是一条开放直线多段线，且线段没有消失；否则取消生成。
检测结果与原轮廓之间的穿越及间距不足。复杂轮廓可能被保守拒绝，应调整轮廓或距离。
几何距离容差为 0.000001 图形单位；间距校验还考虑偏移距离的数值误差。

## 编译

使用本机 AutoCAD 2023 的 AcMgd.dll、AcDbMgd.dll、AcCoreMgd.dll，引用不复制到输出。
使用现有 Visual Studio 2022 MSBuild 和 .NET Framework 4.8 Developer Pack。

powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -OutputPath bin\Phase2\

本次新版输出到 bin\Phase2。AutoCAD 同一会话不能可靠地卸载或替换已经加载的同名 .NET 插件，应重启后加载新版。

## 文件

- Commands.cs：WDTTEST。
- RebarCommands.cs：WDTREBAR、输入及原生几何检查。
- RebarPalette.cs：WDTPANEL 侧边栏、临时预览、确认与取消。
- WallDetailTool.csproj：项目配置。
- build.ps1：编译脚本，支持指定输出目录。
- tests/GeometryTests.cs、GeometryTests.csproj：独立原生几何测试程序集，不编入正式插件。
- tests/geometry.scr：控制台测试脚本，包含几何测试、WDTREBAR 默认参数及 WDTTEST。

测试脚本应仅在独立的 accoreconsole /isolate 配置运行，因为它为测试 DLL 设置了受信任路径；不应在日常工作图纸中运行。
测试不保存图纸，也不修改 AutoCAD 安装目录。测试结果及仍需人工核验的界面项目见 TEST_RESULTS.md。
原生偏移 API 参考：https://help.autodesk.com/cloudhelp/2025/PLK/OARX-DevGuide-Managed/files/GUID-80D106A9-A16F-4F32-BDE2-5C5B1F7C2C84.htm
