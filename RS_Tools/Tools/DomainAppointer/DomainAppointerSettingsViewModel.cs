using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Events;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using static RS_Tools.Tools.DomainAppointer.DataService;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace RS_Tools.Tools.DomainAppointer
{
    internal class DomainAppointerSettingsViewModel : DockPane
    {
        private const string _dockPaneID = "RS_Tools_Tools_DomainAppointer_DomainAppointerSettings";

        private readonly ObservableCollection<Map> _maps = new ObservableCollection<Map>();
        private readonly ObservableCollection<FeatureLayer> _layers = new ObservableCollection<FeatureLayer>();
        private readonly ObservableCollection<String> _fields = new ObservableCollection<string>();
        private Map _selectedMap = null;
        private FeatureLayer _selectedLayer = null;
        private String _selectedField = String.Empty;

        private readonly object _lockCollection = new object();

        protected DomainAppointerSettingsViewModel() {

            _getMapsCommand = new RelayCommand(() => GetMaps(), () => true);

            Utilities.ProUtilities.RunOnUiThread(() =>
            {
                BindingOperations.EnableCollectionSynchronization(_maps, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_layers, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_fields, _lockCollection);
            });

            LayersAddedEvent.Subscribe(OnLayerAdded, false);
            LayersRemovedEvent.Subscribe(OnLayerRemoved, false);
            ProjectOpenedEvent.Subscribe(OnProjectOpened, false);
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

        public ObservableCollection<Map> Maps => _maps;

        public Map SelectedMap
        {
            get
            {
                return _selectedMap;
            } set
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

        public ObservableCollection<FeatureLayer> Layers => _layers;

        public FeatureLayer SelectedLayer
        {
            get
            {
                return _selectedLayer;
            } set
            {
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _selectedLayer, value, () => SelectedLayer);
                    if (_selectedLayer != null)
                    {
                        PopulateLayerFields();
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
            } set
            {
                Utilities.ProUtilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _selectedField, value, () => SelectedField);
                });
                if (_selectedField == null)
                {
                    MainModule.SetState("domainappointer_update_state", false);
                }
                else
                {
                    MainModule.SetState("domainappointer_update_state", true);
                }
            }
        }

        #endregion

        #region Overrides
        #endregion

        #region Subscribed Events
        
        private void OnLayerRemoved(LayerEventsArgs args)
        {
            PopulateMapLayers();
        }

        private void OnLayerAdded(LayerEventsArgs args)
        {
            PopulateMapLayers();
        }

        /// <summary>
        ///  Clear out the configuration
        /// </summary>
        /// <param name="args"></param>
        private void OnProjectOpened(ProjectEventArgs args)
        {
            Maps.Clear();
            Layers.Clear();
        }

        #endregion

        #region Commands

        private readonly ICommand _getMapsCommand;

        public ICommand GetMapsCommand => _getMapsCommand;

        #endregion

        #region Methods

        /// <summary>
        /// Gets all maps in the current project and adds them to the drop down list
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
        /// Gets all layers in the currently selected map and adds them to the drop down list
        /// </summary>
        private void PopulateMapLayers()
        {
            _layers.Clear();
            if (_selectedMap != null)
            {
                QueuedTask.Run(() =>
                {
                    foreach (var layer in _selectedMap.Layers.OfType<FeatureLayer>().Where(
                        lyr => lyr.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPoint ||
                        lyr.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPolygon ||
                        lyr.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPolyline))
                    {
                        _layers.Add(layer);
                    }
                });
            }
        }

        /// <summary>
        /// Gets all the field in the selected layer that meets requirement and adds them to the drop down list
        /// </summary>
        private async void PopulateLayerFields()
        {
            _fields.Clear();
            IEnumerable<Field> fields = null;
            FeatureClass featureclass = null;

            await QueuedTask.Run(() =>
            {
                Table table = _selectedLayer.GetTable();

                if (table is FeatureClass)
                {
                    featureclass = table as FeatureClass;
                    using (FeatureClassDefinition def = featureclass.GetDefinition())
                    {
                        fields = def.GetFields();
                    }

                    foreach (Field field in fields)
                    {
                        Domain domain = field.GetDomain();

                        if (domain != null)
                        {
                            FieldType fieldType = domain.GetFieldType();
                            if (fieldType == FieldType.SmallInteger || fieldType == FieldType.Integer)
                            {
                                _fields.Add(field.Name);
                            };
                        }
                    }
                } 
            });   
            if (_fields.Count <= 0)
            {
                MessageBox.Show("No Valid Fields in '" + _selectedLayer.Name + "'  Feature Layer");
            }
        }

        /// <summary>
        /// Check to make sure the enviornment is set up correctly before processing the users request
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task<bool> CheckRequirements()
        {
            if (_selectedMap == null)
            {
                MessageBox.Show("Select A Map In Domain Appointer Settings");
                return false;
            }

            if (_selectedLayer == null)
            {
                MessageBox.Show("Select A Layer in Domain Appointer Settings");
                return false;
            }

            if (_selectedField == null)
            {
                MessageBox.Show("Select a Field in Domain Appointer Settings");
            }
        
            bool canEditData = false;

            await QueuedTask.Run(() =>
            {
                canEditData = _selectedLayer.CanEditData();
            });

            if (!canEditData)
            {
                MessageBox.Show("Feature Layer '" + _selectedLayer.Name + "' Is not Editable");
                return false;
            }

            IEnumerable<Field> fields = null;

            await QueuedTask.Run(() =>
            {
                Table table = _selectedLayer.GetTable();
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
                MessageBox.Show("The field '" + _selectedField + "' is Missing From '" + _selectedLayer.Name + "' Feature Layer");
                return false;
            }
           

            return true;
        }

        /// <summary>
        /// Applys the domain code to the currenly selected feature
        /// </summary>
        /// <param name="code"></param>
        internal async void ApplyDomain(DomainCode code)
        {
            if (await CheckRequirements())
            {
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        Selection selection = _selectedLayer.GetSelection();
                        var oidset = selection.GetObjectIDs();

                        var insp = new ArcGIS.Desktop.Editing.Attributes.Inspector();
                        insp.Load(_selectedLayer, oidset);
                        insp[_selectedField] = (int)code;

                        var op = new EditOperation();
                        op.Name = "Set Domain to " + ((int)code).ToString();
                        op.Modify(insp);
                        op.Execute();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error: " + ex.Message);
                    }

                });
            }
        }

        #endregion
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class DomainAppointerSettings_ShowButton : Button
    {
        protected override void OnClick()
        {
            DomainAppointerSettingsViewModel.Show();
        }
    }
}
