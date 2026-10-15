# WallDetailTool

AutoCAD 2023/2024 C# 插件，.NET Framework 4.8 / x64。

## 加载及使用

1. 如果当前 AutoCAD 已加载旧版 DLL，请保存图纸并重启 AutoCAD。
2. 输入 NETLOAD，选择 D:\Waste\WallDtailTool\bin\Phase4\WallDetailTool.dll。
3. 如有信任路径提示，按 AutoCAD 提示信任本项目的 bin\Phase4 目录。
4. 输入 WDTPANEL 打开侧边栏。在新图中先填节点名称、保护层和直径，点击“新建节点”；选中节点后填钢筋组名称，点击“新建钢筋组”。选中钢筋组，从“目标钢筋图层”选择图层或输入新名称，再点击“选择轮廓并预览”，在图中选择二维多段线并点选偏移方向。红色线仅是临时预览，点击“确认生成”后才写入图纸和关联数据；新图层会先询问是否创建。“取消预览”不留正式对象。
5. 树形列表显示节点→钢筋组→钢筋。选中一条钢筋后，可修改保护层、直径或目标图层，点击“预览修改所选钢筋”，检查红色结果和提示，再点击“确认修改”。修改保护层或直径会按原轮廓重新计算中心线；旧版钢筋若没有源轮廓记录，会提示重新选择轮廓和方向。只改图层会保留现有路径。CAD 中手工改过路径时，重新计算前会提示将替换该形状。取消预览不改图纸；确认后可以用 AutoCAD 撤销。
6. “刷新列表”可查看 CAD 中删除、手工调整或撤销后的状态；“在列表定位钢筋”可选择图中钢筋并找到对应条目。选中节点或组后可修改名称及默认保护层/直径，再点击“保存所选节点/组的名称与默认尺寸”。组继承创建时所选节点的默认尺寸，新筋使用所选组的默认尺寸；输入框手动更改后仅影响本次新筋。修改默认值不会改变已有钢筋。
7. 也可继续使用 WDTREBAR 命令直接生成；此旧命令生成的钢筋目前不会自动加入层级。
8. WDTTEST 保留，输出 WallDetailTool loaded successfully!。

WDTREBAR 的操作：

1. 选择开放式、纯直线段的二维多段线。
2. 输入保护层厚度（默认 25 mm）和钢筋直径（默认 12 mm）。
3. 在线段中部旁点选所需的一侧；线上、端点或等距拐角处需重新点选。
4. 生成独立的钢筋多段线，原轮廓保持不变。默认宽度 12 mm、中心线偏移 25 + 12 / 2 = 31 mm。

输入尺寸始终为毫米，按图纸 INSUNITS 换算。未设单位时会提示按 1 图形单位 = 1 mm 处理。
支持轻量二维多段线和非拟合的旧式二维多段线；支持轮廓平面法向和标高。
不支持圆弧、闭合轮廓、三维或拟合多段线，不包含弯钩、锚固、标注或自动结构校核。
侧边栏新生成的节点、组、钢筋关联保存在当前 DWG 中。预览期间若原轮廓、钢筋、组、参数、单位或图层变化，确认会拒绝并要求重新预览。切换/关闭图纸时清除预览。删除钢筋后关联暂留并显示“已删除”，撤销可恢复。对 CAD 中直接编辑的检测目前在刷新或重新选择列表项时进行，不会实时同步其他对象；复制及跨图复制规则尚未实现。新版编辑和图层界面仍待人工验收。

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

powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -OutputPath bin\Phase4\

本次新版输出到 bin\Phase4。AutoCAD 同一会话不能可靠地卸载或替换已经加载的同名 .NET 插件，应重启后加载新版。

## 文件

- Commands.cs：WDTTEST。
- RebarCommands.cs：WDTREBAR、输入及原生几何检查。
- RebarPalette.cs：WDTPANEL 侧边栏、临时预览、确认与取消。
- DrawingData.cs：节点、组、单筋的 DWG 记录、稳定标识及版本检查。
- RebarEditing.cs、RebarEditService.cs、LayerTools.cs：修改预览、事务提交与目标图层。
- WallDetailTool.csproj：项目配置。
- build.ps1：编译脚本，支持指定输出目录。
- tests/GeometryTests.cs、GeometryTests.csproj：独立原生几何测试程序集，不编入正式插件。
- tests/geometry.scr：控制台测试脚本，包含几何测试、WDTREBAR 默认参数及 WDTTEST。
- tests/PersistenceTests.cs、phase3.scr、phase3_reopen.scr：独立的层级、编辑、撤销重做、删除、版本防护及重开验证代码/脚本。
- tests/EditTests.cs、phase4*.scr：独立的编辑、图层、旧数据兼容及撤销重做验证代码/脚本。

测试脚本应仅在独立的 accoreconsole /isolate 配置运行，因为它为测试 DLL 设置了受信任路径；不应在日常工作图纸中运行。
测试只保存专用测试 DWG，不覆盖用户图纸，也不修改 AutoCAD 安装目录。测试结果及仍需人工核验的界面项目见 TEST_RESULTS.md。
原生偏移 API 参考：https://help.autodesk.com/cloudhelp/2025/PLK/OARX-DevGuide-Managed/files/GUID-80D106A9-A16F-4F32-BDE2-5C5B1F7C2C84.htm
