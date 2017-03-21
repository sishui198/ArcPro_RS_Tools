using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace RS_Tools.Tools.FileTileLoader
{
    internal class FileTileLoaderViewModel : DockPane
    {
        private const string _dockPaneID = "RS_Tools_Tools_FileTileLoader_FileTileLoader";

        private string _saveFolder = String.Empty;
        private string _saveFile = "ExtensionList.txt";
        private string _saveFullPath = String.Empty;

        private readonly ObservableCollection<Map> _maps = new ObservableCollection<Map>();
        private readonly ObservableCollection<FeatureLayer> _featureLayers = new ObservableCollection<FeatureLayer>();
        private readonly ObservableCollection<String> _fields = new ObservableCollection<string>();
        private readonly ObservableCollection<String> _fileExtensions = new ObservableCollection<string>();


        private Map _selectedMap = null;
        private FeatureLayer _selectedFeatureLayer = null;
        private String _selectedField = String.Empty;
        private String _prefix = String.Empty;
        private String _suffix = String.Empty;
        private String _fileExtension = String.Empty;
        private String _fileWorkspace = String.Empty;

        private readonly object _lockCollection = new object();

        // Constructor
        protected FileTileLoaderViewModel()
        {
            _saveFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"RS_Tools\Pro\FileTileLoader");
            _saveFullPath = System.IO.Path.Combine(_saveFolder, _saveFile);

            _getMapsCommand = new RelayCommand(() => GetMaps(), () => true);
            _getFileWorkspaceCommand = new RelayCommand(() => GetFileWorkspace(), () => true);
            _selectTileCommand = FrameworkApplication.GetPlugInWrapper("RS_Tools_Tools_FileTileLoader_MapTools_SelectTileTool") as ICommand;

            Utilities.ProUtilities.RunOnUiThread(() =>
            {
                BindingOperations.EnableCollectionSynchronization(_maps, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_featureLayers, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_fields, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_fileExtensions, _lockCollection);
            });

            LayersAddedEvent.Subscribe(OnLayersAdded, false);
            LayersRemovedEvent.Subscribe(OnLayersAdded, false);

            ReadFileExtensionsFromDisk();
        }

        #region Dockpane

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "File Tile Loader Settings";
        public string Heading
        {
            get { return _heading; }
            set
            {
                SetProperty(ref _heading, value, () => Heading);
            }
        }

        #endregion

        #region Properties

        public ObservableCollection<Map> Maps => _maps;

        public Map SelectedMap
        {
            get
            {
                return _selectedMap;
            }
            set
            {
                SetProperty(ref _selectedMap, value, () => SelectedMap);
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    Utilities.ProUtilities.OpenAndActivateMap(_selectedMap.URI);
                    PopulateMapLayers();
                });
            }
        }

        public ObservableCollection<FeatureLayer> FeatureLayers => _featureLayers;

        public FeatureLayer SelectedFeatureLayer
        {
            get
            {
                return _selectedFeatureLayer;
            }
            set
            {
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _selectedFeatureLayer, value, () => SelectedFeatureLayer);
                    if (_selectedFeatureLayer != null)
                    {
                        PopulateFeatureLayerFields();
                    }
                });
            }
        }

        public ObservableCollection<String> Fields => _fields;

        public String SelectedField
        {
            get
            {
                return _selectedField;
            }
            set
            {
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _selectedField, value, () => SelectedField);
                });
            }
        }

        public String Prefix
        {
            get
            {
                return _prefix;
            }
            set
            {
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _prefix, value, () => Prefix);
                });
            }
        }

        public String Suffix
        {
            get
            {
                return _suffix;
            }
            set
            {
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _suffix, value, () => Suffix);
                });
            }
        }

        public ObservableCollection<String> FileExtensions => _fileExtensions;

        public String FileExtension
        {
            get
            {
                return _fileExtension;
            }
            set
            {
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _fileExtension, value, () => FileExtension);
                });
            }
        }

        public string FileWorkspace
        {
            get
            {
                return _fileWorkspace;
            }
            set
            {
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _fileWorkspace, value, () => FileWorkspace);
                });
            }
        }

        #endregion

        #region Overrides


        #endregion

        #region Subscribed Events

        private void OnLayersAdded(LayerEventsArgs args)
        {

        }

        private void OnLayersRemoved(LayerEventsArgs args)
        {

        }

        #endregion

        #region Commands

        private ICommand _getMapsCommand;

        public ICommand GetMapsCommand => _getMapsCommand;

        private ICommand _getFileWorkspaceCommand;

        public ICommand GetFileWorkspaceCommand => _getFileWorkspaceCommand;

        private ICommand _selectTileCommand;

        public ICommand SelectTileCommand => _selectTileCommand;

        #endregion

        #region Command Methods

        /// <summary>
        /// Adds the projects maps to the '_maps' Collection
        /// </summary>
        private async void GetMaps()
        {
            _maps.Clear();
            if (Project.Current != null)
            {
                await QueuedTask.Run(() =>
                {
                    // GetMap needs to be on the MCT
                    foreach (var map in Project.Current.GetItems<MapProjectItem>())
                    {
                        _maps.Add(map.GetMap());
                    }
                });
            }
            if (_maps.Count <= 0) MessageBox.Show("No Maps Exist");
        }

        private void GetFileWorkspace()
        {
            OpenItemDialog dialog = new OpenItemDialog();
            dialog.Title = "Select A Raster Workspace";
            dialog.MultiSelect = false;
            dialog.Filter = ItemFilters.folders;

            if (Directory.Exists(_fileWorkspace))
            {
                dialog.InitialLocation = _fileWorkspace;
            }


            if (dialog.ShowDialog() == true)
            {
                FileWorkspace = dialog.Items.First().Path;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        ///  Adds the selected map's features layers to the '_featureLayers' collection
        /// </summary>
        private void PopulateMapLayers()
        {
            if (_selectedMap != null)
            {
                QueuedTask.Run(() =>
                {
                    _featureLayers.Clear();
                    foreach (var layer in _selectedMap.Layers.OfType<FeatureLayer>().Where(
                        lyr => lyr.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPolygon))
                    {
                        _featureLayers.Add(layer);
                    }
                });
            }
        }

        /// <summary>
        ///  Adds the selected feature layer's field to the '_fields' collection
        /// </summary>
        private async void PopulateFeatureLayerFields()
        {
            _fields.Clear();
            IEnumerable<Field> fields = null;
            FeatureClass featureclass = null;

            await QueuedTask.Run(() =>
            {
                Table table = _selectedFeatureLayer.GetTable();

                if (table is FeatureClass)
                {
                    featureclass = table as FeatureClass;
                    using (FeatureClassDefinition def = featureclass.GetDefinition())
                    {
                        fields = def.GetFields();
                    }

                    foreach (Field field in fields)
                    {
                        FieldType fieldType = field.FieldType;
                        // Change field type acceptance here
                        if (fieldType == FieldType.SmallInteger || fieldType == FieldType.Integer || fieldType == FieldType.String || fieldType == FieldType.Double || fieldType == FieldType.Single || fieldType == FieldType.GUID)
                        {
                            _fields.Add(field.Name);
                        };
                    }
                }
            });
            if (_fields.Count <= 0)
            {
                MessageBox.Show("No Valid Fields in '" + _selectedFeatureLayer.Name + "'  Feature Layer");
            }
        }

        public void LoadFile(MapPoint point)
        {
            MessageBox.Show(point.X.ToString() + ", " + point.Y.ToString());
        }

        /// <summary>
        /// Gets file extensions from disk and loads them in for the user to select
        /// </summary>
        private void ReadFileExtensionsFromDisk()
        {
            _fileExtensions.Clear();

            if (!Directory.Exists(_saveFolder)) Directory.CreateDirectory(_saveFolder);

            if (File.Exists(_saveFullPath))
            {
                using (StreamReader sStreamReader = new StreamReader(_saveFullPath))
                {
                    string AllData = sStreamReader.ReadToEnd();
                    foreach (var item in AllData.Split(",".ToCharArray()).ToArray()) _fileExtensions.Add(item);
                }
            }
        }

        /// <summary>
        /// Saves file extension to disk for the user in the future
        /// </summary>
        /// <param name="newExtension"></param>
        private void SaveFileExtensionsToDisk(string newExtension)
        {
            if (!_fileExtensions.Contains(newExtension, StringComparer.OrdinalIgnoreCase) && !String.IsNullOrEmpty(newExtension))
            {
                _fileExtensions.Add(newExtension);
            }
            using (TextWriter tw = new StreamWriter(_saveFullPath))
            {
                tw.Write(String.Join(",", _fileExtensions.ToArray()));
            }
        }


        #endregion





    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class FileTileLoader_ShowButton : Button
    {
        protected override void OnClick()
        {
            FileTileLoaderViewModel.Show();
        }
    }
}
