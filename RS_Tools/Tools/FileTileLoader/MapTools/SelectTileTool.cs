using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using RS_Tools.Tools.FileTileLoader;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace RS_Tools.Tools.FileTileLoader.MapTools
{
    internal class SelectTileTool : MapTool
    {
        FileTileLoaderViewModel reference = null;

        public SelectTileTool()
        {
            IsSketchTool = true;
            SketchType = SketchGeometryType.Point;
            SketchOutputMode = SketchOutputMode.Map;

            reference = FrameworkApplication.DockPaneManager.Find("RS_Tools_Tools_FileTileLoader_FileTileLoader") as FileTileLoaderViewModel;

        }

        protected override Task OnToolActivateAsync(bool active)
        {
            return base.OnToolActivateAsync(active);
        }

        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {   
            if (reference != null)
            {
                reference.LoadFile(geometry as MapPoint);
            } else
            {
                MessageBox.Show("Is that you davy???? Davvvyyyyyyyy....", "Error");
            }
            
            
            return base.OnSketchCompleteAsync(geometry);
        }
    }
}
