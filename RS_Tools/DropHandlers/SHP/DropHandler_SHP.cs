using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.DragDrop;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace RS_Tools.DropHandlers.SHP
{
    internal class DropHandler_SHP : DropHandlerBase
    {
        public override void OnDragOver(DropInfo dropInfo)
        {
            //default is to accept our data types
            dropInfo.Effects = DragDropEffects.All;
        }

        public async override void OnDrop(DropInfo dropInfo)
        {
            var mapView = MapView.Active;
            if (mapView == null)
            {
                MessageBox.Show("Drop Into A Map", "Woah", MessageBoxButton.OK, MessageBoxImage.Stop);
                dropInfo.Handled = true;
                return;
            }

            IList<String> Files = dropInfo.Items.Select(item => item.Data.ToString()).ToList();

            // If there is more than on file promt the user to see if they would like to put the files into a group or not
            if (Files.Count > 1)
            {
                var result = MessageBox.Show(string.Format("Add {0} SHP Files to a group?", Files.Count), "Quick", MessageBoxButton.YesNoCancel);

                switch (result)
                {
                    case MessageBoxResult.Cancel:
                        return;
                    case MessageBoxResult.No:
                        Utilities.ProUtilities.AddFilesToMap(Files, mapView.Map);
                        break;
                    case MessageBoxResult.Yes:

                        GroupLayer group = null;
                        await QueuedTask.Run(() =>
                        {
                            group = LayerFactory.CreateGroupLayer(mapView.Map, 0, "SHP Group");
                        });
                        Utilities.ProUtilities.AddFilesToMap(Files, group);
                        break;
                }
            }
            else
            {
                Utilities.ProUtilities.AddFilesToMap(Files, mapView.Map);
            }

            dropInfo.Handled = true;
        }
    }
}
