# V0.5 普通悬挑板计算层阶段验证

日期2026-10-28，基线e370995。不是悬挑板成品交付。

- [x] V0.4归档提交/标签/验收DLL SHA-256只读核对一致。
- [x] 图集PDF第120页（印刷2-54）全页渲染实际查看。
- [x] 新增四模式×左右路径、投影端点、相切、圆弧半径、bulge、Lin/b、向下符号、分组规格、突出不拒绝及非法输入测试运行通过：`CANTILEVER_PREREQUISITES_PASS checks=269`，包含固定26×26符号。
- [x] 原五组及HangingNodeTests实际运行通过；后者2974项断言。
- [x] NodeDrawWindowTests通过158项（旧窗口，不代表悬挑窗口完成）。
- [x] V0.4 BeamSideContourTests/BeamSideDotsTests/BeamSideRebarTests/BeamSideWindowTests分别通过837/51529/1825/3180项。
- [x] Release/x64/.NET4.8，使用AutoCAD2023引用编译；Private=False，输出仅插件DLL/PDB。
- [ ] 点筋具体布置与测试（原则已确认，布置边等细则待答复）。
- [ ] 悬挑板窗口、取消、请求、目标文档及成功记录隔离。
- [ ] CAD实体、正向规格、水平引线、左右布局、Lin原生尺寸、不生成b尺寸。
- [ ] 普通块、源图只读、失败回滚、UNDO/REDO、保存重开、多文档。
- [ ] 用户CAD验收；2023/2024本轮均未运行CAD。

旧BeamSideWindowTests首次编译有原有未赋值字段CS0649警告，其运行通过。没有启动/关闭CAD、重试NETLOAD或改动安全设置。本次通过Python子进程逐项执行MSBuild和测试EXE；新增可复现入口 [run_v05_geometry.ps1](run_v05_geometry.ps1)，与本次执行项目/参数相同，脚本本身未另行执行。

计算阶段DLL：`D:\Waste\WallDtailTool\bin\V05Prerequisites20261028\WallDetailTool.dll`

SHA-256：`1630DAB8C1E323C48C2A33455BB602DC5B79BAB957DF129DA0E50ADB25FF1F72`。

该DLL没有悬挑板CAD入口，不要求用户加载。源码提交见Git交付记录；未打标签或推送。
