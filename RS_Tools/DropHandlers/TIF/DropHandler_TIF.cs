using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.DragDrop;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace RS_Tools.DropHandlers.TIF
{
    internal class DropHandler_TIF : DropHandlerBase
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

            int currentLayerCount = mapView.Map.GetLayersAsFlattenedList().Count;
            MessageBox.Show(string.Format("Current Layer Count: {0}", currentLayerCount));

            IList<String> Files = dropInfo.Items.Select(item => item.Data.ToString()).ToList();


            if (Files.Count > 1)
            {
                var result = MessageBox.Show(string.Format("Add {0} TIF Files to a group?", Files.Count), "Quick", MessageBoxButton.YesNoCancel);

                switch (result)
                {
                    case MessageBoxResult.Cancel:
                        return;
                    case MessageBoxResult.No:
                        AddFilesToMap(Files, mapView.Map);
                        break;
                    case MessageBoxResult.Yes:

                        GroupLayer group = null;
                        await QueuedTask.Run(() =>
                        {
                            group = LayerFactory.CreateGroupLayer(mapView.Map, 0, "TIF Group");
                        });
                        AddFilesToMap(Files, group);
                        break;
                }
            }
            else
            {
                AddFilesToMap(Files, mapView.Map);
            }


        }

        private async void AddFilesToMap(IList<String> filePaths, ILayerContainerEdit mapOrGroupLayer)
        {
            foreach (string filePath in filePaths)
            {
                await QueuedTask.Run(() =>
                {
                    Uri uri = new Uri(filePath);
                    LayerFactory.CreateLayer(uri, mapOrGroupLayer).SetExpanded(false);
                });

            }
        }

        
    }
}
