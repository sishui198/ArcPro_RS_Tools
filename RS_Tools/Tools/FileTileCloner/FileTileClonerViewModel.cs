using ArcGIS.Core.Data;
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using static RS_Tools.Tools.FileTileCloner.DataService;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace RS_Tools.Tools.FileTileCloner
{
    internal class FileTileClonerViewModel : DockPane
    {
        private const string _dockPaneID = "RS_Tools_Tools_FileTileCloner_FileTileCloner";

        private string _saveFolder = string.Empty;
        private string _saveFile = "ExtensionList.txt";
        private string _saveFullPath = string.Empty;

        private readonly ObservableCollection<Map> _maps = new ObservableCollection<Map>();
        private readonly ObservableCollection<FeatureLayer> _featureLayers = new ObservableCollection<FeatureLayer>();
        private readonly ObservableCollection<string> _fields = new ObservableCollection<string>();
        private readonly ObservableCollection<string> _fileExtensions = new ObservableCollection<string>();

        private Map _selectedMap = null;
        private FeatureLayer _selectedFeatureLayer = null;
        private string _selectedField = string.Empty;
        private string _prefix = string.Empty;
        private string _suffix = string.Empty;
        private string _fileExtension = string.Empty;
        private string _fileWorkspace = string.Empty;
        private string _destinationWorkspace = string.Empty;
        private EnumFileCloningMethod _fileCloningMethod;
        private IDictionary<string, Boolean> _fileList = new Dictionary<string, bool>(); // Key - File Path, Value - If The File Exists

        private readonly object _lockCollection = new object();

        // Constructor
        protected FileTileClonerViewModel()
        {
            _saveFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"RS_Tools\Pro\FileTileCloner");
            _saveFullPath = System.IO.Path.Combine(_saveFolder, _saveFile);

            _getMapsCommand = new RelayCommand(() => GetMaps(), () => true);
            _cloneFileCommand = new RelayCommand(() => LoadFiles(), () => true);
            _getFileWorkspaceCommand = new RelayCommand(() => GetFileWorkspace(), () => true);
            _getDestinationWorkspaceCommand = new RelayCommand(() => GetDestinationWorkspace(), () => true);

            Utilities.ProUtilities.RunOnUiThread(() =>
            {
                BindingOperations.EnableCollectionSynchronization(_maps, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_featureLayers, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_fields, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_fileExtensions, _lockCollection);
            });

            LayersAddedEvent.Subscribe(OnLayersAdded, false);
            LayersRemovedEvent.Subscribe(OnLayersRemoved, false);
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
        private string _heading = "File Tile Cloner";
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

        public EnumFileCloningMethod FileCloningMethod
        {
            get { return _fileCloningMethod; }
            set
            {
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _fileCloningMethod, value, () => FileCloningMethod);
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

        public ObservableCollection<string> Fields => _fields;

        public string SelectedField
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

        public string Prefix
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

        public string Suffix
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

        public ObservableCollection<string> FileExtensions => _fileExtensions;

        public string FileExtension
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

        public string DestinationWorkspace
        {
            get
            {
                return _destinationWorkspace;
            } set
            {
                Utilities.ProUtilities.RunOnUiThread(() => 
                {
                    SetProperty(ref _destinationWorkspace, value, () => DestinationWorkspace);
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
            _fileList.Clear();
            Prefix = string.Empty;
            Suffix = string.Empty;
            FileWorkspace = string.Empty;
            DestinationWorkspace = string.Empty;
            FileCloningMethod = EnumFileCloningMethod.None;
        }

        #endregion

        #region Commands

        private readonly ICommand _getMapsCommand;

        public ICommand GetMapsCommand => _getMapsCommand;

        private readonly ICommand _getFileWorkspaceCommand;

        public ICommand GetFileWorkspaceCommand => _getFileWorkspaceCommand;

        private readonly ICommand _getDestinationWorkspaceCommand;

        public ICommand GetDestinationWorkspaceCommand => _getDestinationWorkspaceCommand;

        private readonly ICommand _cloneFileCommand;

        public ICommand CloneFileCommand => _cloneFileCommand;

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

        /// <summary>
        /// Prompts the user to choose a file workspace
        /// </summary>
        private void GetFileWorkspace()
        {
            OpenItemDialog dialog = new OpenItemDialog();
            dialog.Title = "Select A File Workspace";
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

        private void GetDestinationWorkspace()
        {
            OpenItemDialog dialog = new OpenItemDialog();
            dialog.Title = "Select A Destination Workspace";
            dialog.MultiSelect = false;
            dialog.Filter = ItemFilters.folders;

            if (Directory.Exists(_destinationWorkspace))
            {
                dialog.InitialLocation = _destinationWorkspace;
            }


            if (dialog.ShowDialog() == true)
            {
                DestinationWorkspace = dialog.Items.First().Path;
            }
        }

        /// <summary>
        /// Generates File List and Attempts To Load it
        /// </summary>
        private async void LoadFiles()
        {
            if (await CheckRequirements())
            {
                await GenerateFileList();
                if (ValidateFileList())
                {
                    var maxCount = Convert.ToUInt32(_fileList.Count());
                    var pd = new ArcGIS.Desktop.Framework.Threading.Tasks.ProgressDialog("Copying Files", "Canceled", maxCount, false);

                    await CloneFiles(new CancelableProgressorSource(pd), maxCount);
                }
                else
                {
                    MessageBox.Show("Current Configuration Could Not Match Any Files in the File Workspace", "Something Went Wrong ...");
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Check to make sure the enviorment is set up correctly before processing the users request
        /// </summary>
        /// <returns></returns>
        private async Task<bool> CheckRequirements()
        {
            if (_selectedMap == null)
            {
                MessageBox.Show("Select a Map in File Tile Cloner Settings", "Oops");
                return false;
            }

            if (_selectedFeatureLayer == null)
            {
                MessageBox.Show("Select a Feature Layer In File Tile Cloner Settings", "Oops");
                return false;
            }

            if (string.IsNullOrEmpty(_selectedField))
            {
                MessageBox.Show("Select A Field In File Tile Cloner Settings", "Oops");
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
                MessageBox.Show("This field '" + _selectedField + "' is Missing From '" + _selectedFeatureLayer.Name + "' Feature Layer", "Oops");
                return false;
            }

            // No need to check for whitespace. I disallow this in the 'view'.
            if (string.IsNullOrEmpty(_fileExtension))
            {
                MessageBox.Show("Type or Choose a File Extension");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_fileWorkspace))
            {
                MessageBox.Show("Type or Choose a File Workspace");
                return false;
            }

            if (_fileCloningMethod == EnumFileCloningMethod.None)
            {
                MessageBox.Show("Select a File Cloner Method", "Oops");
                return false;
            }

            if (_fileCloningMethod == EnumFileCloningMethod.Selected)
            {
                var result = await QueuedTask.Run(() =>
                {
                    var selection = _selectedFeatureLayer.GetSelection();
                    if (selection.GetCount() <= 0)
                    {
                        MessageBox.Show("Select at least one feature");
                        return false;
                    }
                    return true;
                });
                if (!result) return result;
            }

            return true;
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
            if (!_fileExtensions.Contains(newExtension, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrEmpty(newExtension))
            {
                _fileExtensions.Add(newExtension);
            }
            using (TextWriter tw = new StreamWriter(_saveFullPath))
            {
                tw.Write(string.Join(",", _fileExtensions.ToArray()));
            }
        }

        /// <summary>
        /// Generates File List 
        /// </summary>
        private async Task GenerateFileList()
        {
            _fileList.Clear();

            switch (_fileCloningMethod)
            {
                // All Features will be Processed
                case EnumFileCloningMethod.All:
                    await QueuedTask.Run(() =>
                    {
                        using (RowCursor cursor = _selectedFeatureLayer.GetFeatureClass().Search(null, false))
                        {
                            int fieldIndex = cursor.FindField(_selectedField);
                            while (cursor.MoveNext())
                            {
                                using (Row row = cursor.Current)
                                {
                                    string value = Convert.ToString(row.GetOriginalValue(fieldIndex));
                                    if (!_fileList.ContainsKey(value))
                                    {
                                        _fileList.Add(value, true);
                                    }
                                }
                            }
                        }
                    });
                    break;

                // Only Selected Features will be Processed
                case EnumFileCloningMethod.Selected:
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
                                        string value = Convert.ToString(row.GetOriginalValue(fieldIndex));
                                        if (!_fileList.ContainsKey(value))
                                        {
                                            _fileList.Add(value, true);
                                        }
                                    }
                                }
                            }
                        }

                    });
                    break;

                // Exhaust The Enumerator
                case EnumFileCloningMethod.None:
                    break;
            }
        }

        /// <summary>
        /// Goes through each file in the file list and validates that it exists on the file system
        /// </summary>
        private bool ValidateFileList()
        {
            IDictionary<string, Boolean> tempList = new Dictionary<string, Boolean>();

            bool AtLeastOneFileExists = false;

            foreach (KeyValuePair<string, Boolean> file in _fileList)
            {
                string filePath = _fileWorkspace + @"\" + AddPrefixAndSuffix(file.Key) + _fileExtension;
                if (!File.Exists(filePath))
                {
                    // File Does Not Exist
                    tempList.Add(filePath, false);
                }
                else
                {
                    // File Does Exist
                    tempList.Add(filePath, true);
                    AtLeastOneFileExists = true;
                }
            }

            _fileList = tempList;
            return AtLeastOneFileExists;
        }

        /// <summary>
        /// Attempts to copy each file the file list to the destination
        /// </summary>
        /// <returns></returns>
        private Task CloneFiles(CancelableProgressorSource CancelableProgressorSource, UInt32 TotalNumberOfFiles)
        {
            return QueuedTask.Run(() => {
                bool itWorked = false;

                CancelableProgressorSource.Progressor.Max = TotalNumberOfFiles;

                foreach (KeyValuePair<string, Boolean> file in _fileList)
                {
                    if (CancelableProgressorSource.Progressor.CancellationToken.IsCancellationRequested)
                    {
                        CancelableProgressorSource.Progressor.Message = "Cancelling";
                        CancelableProgressorSource.Progressor.Status = "I didn't finish...";
                        break;
                    }

                    if (file.Value)
                    {
                        if (!itWorked)
                            SaveFileExtensionsToDisk(_fileExtension);

                        itWorked = true;

                        try
                        {
                            var sourceFile = file.Key;
                            var destinationFile = _destinationWorkspace + @"\" + System.IO.Path.GetFileName(file.Key);

                            System.IO.File.Copy(sourceFile, destinationFile);
                        }
                        catch (Exception yourBest) // But you don't succeed
                        {
                            yourBest.ToString();
                            // Just So We Get No Crashes ;) 
                        }
                    }
                    CancelableProgressorSource.Progressor.Value += 1;
                    CancelableProgressorSource.Progressor.Status = string.Format("Copied {0} of {1} files", CancelableProgressorSource.Progressor.Value, TotalNumberOfFiles);
                    CancelableProgressorSource.Progressor.Message = "Copying Files - " + Math.Round(((Convert.ToDouble(CancelableProgressorSource.Progressor.Value) / Convert.ToDouble(CancelableProgressorSource.Progressor.Max)) * 100.0), 0) + "% Complete";
                }

            }, CancelableProgressorSource.Progressor);
        }

        /// <summary>
        /// Adds the prefix and suffix from the UI to the given string.
        /// </summary>
        /// <param name="Name"></param>
        /// <returns></returns>
        private string AddPrefixAndSuffix(string Name)
        {
            string filename = string.Empty;
            string temp = string.Empty;

            if (!string.IsNullOrEmpty(_prefix))
            {
                temp = _prefix;
                temp.Replace(" ", string.Empty);
                filename += temp;
            }

            filename += Name;

            if (!string.IsNullOrEmpty(_suffix))
            {
                temp = _suffix;
                temp.Replace(" ", string.Empty);
                filename += temp;
            }
            return filename;
        }


        #endregion
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class FileTileCloner_ShowButton : Button
    {
        protected override void OnClick()
        {
            FileTileClonerViewModel.Show();
        }
    }
}
