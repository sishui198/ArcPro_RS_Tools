using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace RS_Tools.Utilities
{
    internal class ProUtilities
    {

        /// <summary>
        /// utility function to open and activate a map given the map url.
        /// </summary>
        /// <param name="url">unique map identifier</param>
        internal static async void OpenAndActivateMap(string url)
        {
            try
            {
                // if active map is the correct one, we're done
                if ((MapView.Active != null) && (MapView.Active.Map != null) && (MapView.Active.Map.URI == url))
                    return;

                // get the map from the project item
                Map map = null;
                await QueuedTask.Run(() =>
                {
                    var mapItem = Project.Current.GetItems<MapProjectItem>().FirstOrDefault(i => i.Path == url);
                    if (mapItem != null) map = mapItem.GetMap();
                });

                // url is not a project item - oops
                if (map == null)
                    return;

                // check the open panes to see if it's open but just needs activating
                IEnumerable<IMapPane> mapPanes = FrameworkApplication.Panes.OfType<IMapPane>();
                foreach (var mapPane in mapPanes)
                {
                    if (mapPane.MapView?.Map?.URI == null) continue;
                    if (mapPane.MapView.Map.URI != url) continue;
                    var pane = mapPane as Pane;
                    pane?.Activate();
                    return;
                }

                // it's not currently open... so open it
                await FrameworkApplication.Panes.CreateMapPaneAsync(map);
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"Error in OpenAndActivateMap: {ex.Message}");
            }
        }

        /// <summary>
        /// utility function to enable an action to run on the UI thread (if not already)
        /// </summary>
        /// <param name="action">the action to execute</param>
        /// <returns></returns>
        internal static void RunOnUiThread(Action action)
        {
            try
            {
                if (IsOnUiThread)
                    action();
                else
                    Application.Current.Dispatcher.Invoke(action);
            }
            catch (Exception ex)
            {
                MessageBox.Show($@"Error in OpenAndActivateMap: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines whether the calling thread is the thread associated with this 
        /// System.Windows.Threading.Dispatcher, the UI thread.
        /// 
        /// If called from a View model test it always returns true.
        /// </summary>
        public static bool IsOnUiThread => ArcGIS.Desktop.Framework.FrameworkApplication.TestMode || System.Windows.Application.Current.Dispatcher.CheckAccess();

        /// <summary>
        /// Takes a layer and attempts to return a feature class. This Method must be called on the GPT
        /// </summary>
        /// <param name="layer">Layer</param>
        /// <returns>Feature Class</returns>
        internal static FeatureClass LayerToFeatureClass(Layer layer)
        {
            if (layer is FeatureLayer)
            {
                Table table = (layer as FeatureLayer).GetTable();
                if (table is FeatureClass) return table as FeatureClass;
            }
            return null;
        }

        /// <summary>
        /// Returns File Name Extension with or without dot at the beginning
        /// </summary>
        /// <param name="filePathOrName"></param>
        /// <param name="dot"></param>
        /// <returns></returns>
        public static string GetFileExtension(string filePathOrName, bool dot = true)
        {
            string extension = System.IO.Path.GetExtension(filePathOrName);
            return dot ? extension : extension.Remove(0, 1);
        }

        /// <summary>
        /// Adds A List of Files to A Map Or Group
        /// </summary>
        /// <param name="filePaths"></param>
        /// <param name="mapOrGroupLayer"></param>
        public static async void AddFilesToMap(IList<String> filePaths, ILayerContainerEdit mapOrGroupLayer)
        {
            foreach (string filePath in filePaths)
            {
                await QueuedTask.Run(() =>
                {
                    Uri uri = new Uri(filePath);
                    LayerFactory.Instance.CreateLayer(uri, mapOrGroupLayer).SetExpanded(false);
                });

            }
        }


    }
}
