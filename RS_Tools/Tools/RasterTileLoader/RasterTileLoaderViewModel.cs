using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System.Collections.ObjectModel;
using ArcGIS.Desktop.Mapping;
using System.Windows.Data;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using System.Windows.Input;
using static RS_Tools.Tools.RasterTileLoader.DataService;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using System.IO;

namespace RS_Tools.Tools.RasterTileLoader
{
    internal class RasterTileLoaderViewModel : DockPane
    {
        private const string _dockPaneID = "RS_Tools_Tools_RasterTileLoader_RasterTileLoader";

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
        private String _rasterWorkspace = String.Empty;
        private EnumRasterLoadingMethod _rasterLoadingMethod;
        private IDictionary<String, Boolean> _rasterList = new Dictionary<string, bool>();
        
        private readonly object _lockCollection = new object();

        protected RasterTileLoaderViewModel()
        {
            _saveFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"RS_Tools\Pro\RasterTileLoader");
            _saveFullPath = System.IO.Path.Combine(_saveFolder, _saveFile);

            _getMapsCommand = new RelayCommand(() => GetMaps(), () => true);
            _loadRasterCommand = new RelayCommand(() => LoadRasters(), () => true);
            _getRasterWorkspaceCommand = new RelayCommand(() => GetRasterWorkspace(), () => true);

            Utilities.ProUtilities.RunOnUiThread(() =>
            {
                BindingOperations.EnableCollectionSynchronization(_maps, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_featureLayers, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_fields, _lockCollection);
            });

            LayersAddedEvent.Subscribe(OnLayersAdded, false);
            LayersRemovedEvent.Subscribe(OnLayersRemoved, false);

            ReadFileExtensionsFromDisk();
        }

        #region DockPane

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
        private string _heading = "My DockPane";
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

        public EnumRasterLoadingMethod RasterLoadingMethod
        {
            get { return _rasterLoadingMethod; }
            set
            {
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _rasterLoadingMethod, value, () => RasterLoadingMethod);
                });
            }
        }

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

        public string RasterWorkspace
        {
            get
            {
                return _rasterWorkspace;
            }
            set
            {
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _rasterWorkspace, value, () => RasterWorkspace);
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

        #endregion

        #region Commands

        private readonly ICommand _getMapsCommand;

        public ICommand GetMapsCommand => _getMapsCommand;

        private readonly ICommand _loadRasterCommand;

        public ICommand LoadRasterCommand => _loadRasterCommand;

        private readonly ICommand _getRasterWorkspaceCommand;

        public ICommand GetRasterWorkspaceCommand => _getRasterWorkspaceCommand;

        #endregion

        #region Methods
        
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
                        if (fieldType == FieldType.SmallInteger || fieldType == FieldType.Integer  || fieldType == FieldType.String || fieldType == FieldType.Double || fieldType == FieldType.Single || fieldType == FieldType.GUID)
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

        /// <summary>
        /// Command To Load Rasters Into Current Selected Map
        /// </summary>
        private async void LoadRasters()
        {
            await GenerateRasterList();
            ValidateRasterList();
            await LoadRasterList();
            
        }

        /// <summary>
        /// Command to Get Raster Workspace
        /// </summary>
        private void GetRasterWorkspace()
        {
            OpenItemDialog dialog = new OpenItemDialog();
            dialog.Title = "Select A Raster Workspace";
            dialog.MultiSelect = false;
            dialog.Filter = ItemFilters.folders;

            if (Directory.Exists(_rasterWorkspace))
            {
                dialog.InitialLocation = _rasterWorkspace;
            }

            
            if (dialog.ShowDialog() == true)
            {
                RasterWorkspace = dialog.Items.First().Path;
            }
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
        /// <param name="type"></param>
        private void SaveFileExtensions(string type)
        {
            if (!_fileExtensions.Contains(type, StringComparer.OrdinalIgnoreCase) && !String.IsNullOrEmpty(type))
            {
                _fileExtensions.Add(type);
            }
            using (TextWriter tw = new StreamWriter(_saveFullPath))
            {
                tw.Write(String.Join(",", _fileExtensions.ToArray()));
            }
        }
        
        /// <summary>
        /// Check to make sure the enviorment is set up correctly before processing the users request
        /// </summary>
        /// <returns></returns>
        private async Task<bool> CheckRequirements()
        {
            if (_selectedMap == null)
            {
                MessageBox.Show("Select a Map in Raster Tile Loader Settings");
                return false;
            }

            if (_selectedFeatureLayer == null)
            {
                MessageBox.Show("Select a Feature Layer In Raster Tile Loader Settings");
                return false;
            }

            if (_selectedField == null)
            {
                MessageBox.Show("Select A Field In Raster Tile Loader Settings");
                return false;
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
                MessageBox.Show("This field '" + _selectedField + "' is Missing From '" + _selectedFeatureLayer.Name + "' Feature Layer");
                return false;
            }

            if (_rasterLoadingMethod == EnumRasterLoadingMethod.None)
            {
                MessageBox.Show("Select a Raster Loading Method");
                return false;
            }

            // No need to check for whitespace. I disallow this in the 'view'.
            if (String.IsNullOrEmpty(_fileExtension))
            {
                MessageBox.Show("Type or Choose a File Extension");
                return false;
            }

            if (String.IsNullOrWhiteSpace(_rasterWorkspace))
            {
                MessageBox.Show("Type or Choose a Raster Workspace");
                return false;
            }

            return true;

        }

        /// <summary>
        /// Generates Raster List 
        /// </summary>
        private async Task GenerateRasterList()
        {
            _rasterList.Clear();

            if (await CheckRequirements())
            {
                switch (_rasterLoadingMethod)
                {
                    case EnumRasterLoadingMethod.All:
                        await QueuedTask.Run(() =>
                        {
                            using (RowCursor cursor = _selectedFeatureLayer.GetFeatureClass().Search(null, false))
                            {
                                int fieldIndex = cursor.FindField(_selectedField);
                                while (cursor.MoveNext())
                                {                                    
                                    using (Row row = cursor.Current)
                                    {
                                        String value = Convert.ToString(row.GetOriginalValue(fieldIndex));
                                        if (!_rasterList.ContainsKey(value))
                                        {
                                            _rasterList.Add(value, true);
                                        }
                                    }
                                }
                            }
                        });
                        break;
                    case EnumRasterLoadingMethod.Selected:
                        await QueuedTask.Run(() =>
                        {
                            Selection selection = _selectedFeatureLayer.GetSelection();
                            IReadOnlyList<long> oidset = selection.GetObjectIDs();
                            FeatureClass featureclass = _selectedFeatureLayer.GetFeatureClass();

                            using (RowCursor cursor = featureclass.Search(null, false))
                            {
                                // Get Users Selected Field From Feature Class
                                int fieldIndex = cursor.FindField(_selectedField);

                                // TODO: Cycle through all features... This can be slow update when sdk updates
                                while (cursor.MoveNext())
                                {
                                    using (Row row = cursor.Current)
                                    {
                                        long oid = row.GetObjectID();
                                        // Check if current feature is in selected feature OID set
                                        if (oidset.Contains(oid))
                                        {
                                            String value = Convert.ToString(row.GetOriginalValue(fieldIndex));
                                            if (!_rasterList.ContainsKey(value))
                                            {
                                                _rasterList.Add(value, true);
                                            }
                                        }
                                    }
                                }
                            }
                            
                        });
                        break;
                    case EnumRasterLoadingMethod.None:
                        // Exhaust The Enumerator
                        break;
                }
            }
        }

        private void ValidateRasterList()
        {
            IDictionary<String, Boolean> tempList = new Dictionary<String, Boolean>();

            foreach (KeyValuePair<String, Boolean> raster in _rasterList)
            {
                string filePath = _rasterWorkspace + @"\" + AddPrefixAndSuffix(raster.Key) + _fileExtension;
                if (!File.Exists(filePath))
                {
                    // File Does Not Exist
                    tempList.Add(filePath, false);
                }
                else
                {
                    // File Does Exist
                    tempList.Add(filePath, true);
                }
            }

            _rasterList = tempList;
        }

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
        
        private async Task LoadRasterList()
        {
            bool itWorked = false;
            foreach (KeyValuePair<String, Boolean> raster in _rasterList)
            {
                if (raster.Value)
                {
                    if (!itWorked)
                        SaveFileExtensions(_fileExtension);

                    itWorked = true;

                    try
                    {
                        Uri uri = new Uri(raster.Key);
                        await QueuedTask.Run(() =>
                        {
                            LayerFactory.CreateLayer(uri, _selectedMap);

                        });
                    }
                    catch (Exception yourBest) // But you don't succeed
                    {
                        // Just So We Get No Crashes ;) 
                    }
                }
            }
        }

        #endregion
        

        
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class RasterTileLoader_ShowButton : Button
    {
        protected override void OnClick()
        {
            RasterTileLoaderViewModel.Show();
        }
    }
}
