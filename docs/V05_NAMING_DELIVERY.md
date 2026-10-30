# 通用名称与bin交付（2026-10-30）

承接a8b30aa。用户确认通用名称为“梁反坎/下挂”“板反坎/下挂”，工具条、窗口标题、内置离线帮助、当前帮助文档及README同步。窗口标题同时保留当前具体方向（梁上反坎/梁下下挂/板上反坎/板下下挂）。原命令、内部类型、DWG参数键、请求数值、生成、标注和保存逻辑不变。历史记录中的旧名称保留为当时事实，不批量改写历史。

用户本轮明确要求最新bin也上传GitHub，因此仅对白名单`bin/V05Naming20261030/WallDetailTool.dll`及同名PDB开放跟踪，覆盖此前不上传编译产物的要求；其他bin/obj、用户图、CAD托管引用、配置和日志继续忽略。无新节点，无正式1.0或发布标签。

## 工件与验证

本次Release/x64/.NET Framework4.8输出：

`D:\Waste\WallDtailTool\bin\V05Naming20261030\WallDetailTool.dll`

DLL SHA-256：`62A68F3994BC0AB3F201B975EA5998FCFCBCD422DE02E80BBC9D1D298DB86A3C`，实际271360字节。

同目录PDB：497152字节，SHA-256：`935D55A4F1B3CA9065C492B869703125393E269667DA1B40CB1E0B9B1465CF83`。该目录仅这两个文件，无CAD托管引用副本。

Release/x64构建成功，本次实际运行：NodeDrawWindowTests PASS 166项（含上下方向的通用标题/具体方向名称检查）、UsabilityTests PASS 105项、SketchDiagramTests PASS 1048项。独立测试仍有既有CS0649及libpng颜色配置警告，无失败。此次不启动/关闭CAD、不重复隔离加载、不修改信任设置。

本次仅改可见文案及当前文档，不改共用排筋或标注规则；按风险运行NodeDrawWindowTests、UsabilityTests和SketchDiagramTests。此前18组几何/布局等结果属于前次开发证据，不宣称本次全部重跑。实际CAD工具条布局、宿主字体/缩放及已有撤销/持久化、多文档、2024等专项保留待验。

用户验收：加载本次DLL，运行WDTTOOLBAR，确认两个新入口名称；分别打开切换上下，检查通用标题与具体方向同时正确，打开帮助核对新名称。原命令仍可使用。
