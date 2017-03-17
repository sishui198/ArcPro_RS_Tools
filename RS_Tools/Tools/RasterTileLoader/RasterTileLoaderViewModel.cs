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
        private EnumRasterLoadingMethod _rasterLoadingMethod;
        private String _rasterWorkspace = String.Empty;
        private String _selectedFileExtension = String.Empty;
        private List<String> _rasterList = new List<string>();
        
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

            LoadFileExtensions();
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

        public ObservableCollection<String> FileExtensions => _fileExtensions;

        public String SelectedFileExtension
        {
            get
            {
                return _selectedFileExtension;
            }
            set
            {
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _selectedFileExtension, value, () => SelectedFileExtension);
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

        private void PopulateMapLayers()
        {
            _featureLayers.Clear();
            if (_selectedMap != null)
            {
                QueuedTask.Run(() =>
                {
                    foreach (var layer in _selectedMap.Layers.OfType<FeatureLayer>().Where( 
                        lyr => lyr.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPolygon))
                    {
                        _featureLayers.Add(layer);
                    }
                });
            }
        }

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

        private void LoadRasters()
        {
            MessageBox.Show(_saveFullPath);
            SaveFileExtensions(".txt");
        }

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

        private void LoadFileExtensions()
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
                MessageBox.Show("This field '");
            }

        }

        private void BuildRasterList()
        {
            _rasterList.Clear();

            switch (_rasterLoadingMethod)
            {
                case EnumRasterLoadingMethod.None:
                    break;
                case EnumRasterLoadingMethod.All:

                    break;
                case EnumRasterLoadingMethod.Selected:
                    break;
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
