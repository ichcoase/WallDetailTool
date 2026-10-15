using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(WallDetailTool.Commands))]

namespace WallDetailTool
{
    public partial class Commands
    {
        [CommandMethod("WDTTEST", CommandFlags.Modal)]
        public void Test()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document != null)
            {
                document.Editor.WriteMessage("\nWallDetailTool loaded successfully!");
            }
        }
    }
}
