using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace RS_Tools.Tools.FileTileOpener
{
    internal class FileTileOpenerViewModel : DockPane
    {
        private const string _dockPaneID = "RS_Tools_Tools_FileTileOpener_FileTileOpener";

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
        protected FileTileOpenerViewModel()
        {
            _saveFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"RS_Tools\Pro\FileTileLoader");
            _saveFullPath = System.IO.Path.Combine(_saveFolder, _saveFile);

            _getMapsCommand = new RelayCommand(() => GetMaps(), () => true);
            _getFileWorkspaceCommand = new RelayCommand(() => GetFileWorkspace(), () => true);
            _selectTileCommand = FrameworkApplication.GetPlugInWrapper("RS_Tools_Tools_FileTileOpener_MapTools_SelectTileTool") as ICommand;

            Utilities.ProUtilities.RunOnUiThread(() =>
            {
                BindingOperations.EnableCollectionSynchronization(_maps, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_featureLayers, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_fields, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_fileExtensions, _lockCollection);
            });

            LayersAddedEvent.Subscribe(OnLayersAdded, false);
            LayersRemovedEvent.Subscribe(OnLayersAdded, false);
            ProjectOpenedEvent.Subscribe(OnProjectOpened, false);

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
        private string _heading = "File Tile Opener Settings";
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
                
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _selectedMap, value, () => SelectedMap);

                    if (_selectedMap != null)
                    {
                        Utilities.ProUtilities.OpenAndActivateMap(_selectedMap.URI);
                        PopulateMapLayers();
                    }         
                   
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
            PopulateMapLayers();
        }

        private void OnLayersRemoved(LayerEventsArgs args)
        {
            PopulateMapLayers();
        }

        /// <summary>
        /// Clear out the configuration
        /// </summary>
        /// <param name="args"></param>
        private void OnProjectOpened(ProjectEventArgs args)
        {
            Maps.Clear();
            FeatureLayers.Clear();
            Fields.Clear();
            Prefix = string.Empty;
            Suffix = string.Empty;
            FileExtension = string.Empty;
            FileWorkspace = string.Empty;
            
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

        public async void LoadFile(MapPoint point)
        {
            if (await CheckRequirements())
            {
                await QueuedTask.Run(() =>
                {
                    SpatialQueryFilter spatialFilter = new SpatialQueryFilter();
                    spatialFilter.FilterGeometry = point;
                    spatialFilter.SpatialRelationship = SpatialRelationship.Intersects;

                    using (RowCursor cursor = _selectedFeatureLayer.GetFeatureClass().Search(spatialFilter, false))
                    {

                        int fieldindex = cursor.FindField(_selectedField);

                        while (cursor.MoveNext())
                        {
                            using (Row row = cursor.Current)
                            {
                                string rowValue = Convert.ToString(row.GetOriginalValue(fieldindex));

                                string filePath = _fileWorkspace + @"\" + AddPrefixAndSuffix(rowValue) + _fileExtension;

                                if (!File.Exists(filePath))
                                {
                                    MessageBox.Show("File Does Not Exist", "Hmm...");
                                    return;
                                }

                                SaveFileExtensionsToDisk(_fileExtension);

                                Process.Start(filePath);
                            }

                            return;
                        }
                        MessageBox.Show("Select a feature from the '" + _selectedFeatureLayer.Name + "' feature layer", "Woah Woah Woah");
                    }
                });
            }
        }

        private async Task<Boolean> CheckRequirements()
        {
            if (_selectedMap == null)
            {
                MessageBox.Show("Select A Map In File Tile Opener Settings");
                return false;
            }

            if (_selectedFeatureLayer == null)
            {
                MessageBox.Show("Select A Layer in File Tile Opener Settings");
                return false;
            }

            if (_selectedField == null)
            {
                MessageBox.Show("Select a Field in File Tile Opener Settings");
            }

            IEnumerable<Field> fields = null;

            await QueuedTask.Run(() =>
            {
                Table table = _selectedFeatureLayer.GetTable();
                if (table is FeatureClass)
                {
                    FeatureClass featureclass = table as FeatureClass;
                    using (FeatureClassDefinition def = featureclass.GetDefinition())
                    {
                        fields = def.GetFields();
                    }
                }
            });

            var match = fields.FirstOrDefault(field => field.Name.ToLower().Contains(_selectedField.ToLower()));
            if (match == null)
            {
                MessageBox.Show("This field '" + _selectedField + "' is Missing From '" + _selectedFeatureLayer.Name + "' Feature Layer", "Oops");
                return false;
            }

            // No need to check for whitespace. I disallow this in the 'view'.
            if (String.IsNullOrEmpty(_fileExtension))
            {
                MessageBox.Show("Type or Choose a File Extension in File Tile Opener Settings");
                return false;
            }

            if (String.IsNullOrWhiteSpace(_fileWorkspace))
            {
                MessageBox.Show("Type or Choose a File Workspace in File Tile Opener Settings");
                return false;
            }

            return true;
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

        /// <summary>
        /// Adds the prefix and suffix from the UI to the given string.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private String AddPrefixAndSuffix(string name)
        {
            string filename = String.Empty;
            string temp = string.Empty;

            if (!String.IsNullOrEmpty(_prefix))
            {
                temp = _prefix;
                temp.Replace(" ", String.Empty);
                filename += temp;
            }

            filename += name;

            if (!String.IsNullOrEmpty(_suffix))
            {
                temp = _suffix;
                temp.Replace(" ", String.Empty);
                filename += temp;
            }
            return filename;
        }

        #endregion
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class FileTileOpener_ShowButton : Button
    {
        protected override void OnClick()
        {
            FileTileOpenerViewModel.Show();
        }
    }
}
