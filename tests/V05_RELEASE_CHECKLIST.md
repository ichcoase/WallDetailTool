# V0.5 普通悬挑板候选交付验证

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
