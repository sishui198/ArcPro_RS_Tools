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
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using System.Windows.Input;
using ArcGIS.Core.Data;
using System.Diagnostics;
using RS_Tools.Tools.DomainAppointer.Buttons;
using static RS_Tools.Tools.DomainAppointer.DataService;

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

            Utilities.Utilities.RunOnUiThread(() =>
            {
                BindingOperations.EnableCollectionSynchronization(_maps, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_layers, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_fields, _lockCollection);
            });
            LayersAddedEvent.Subscribe(OnLayerAdded, false);
            LayersRemovedEvent.Subscribe(OnLayerRemoved, false);
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
                _layers.Clear();
                Utilities.Utilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _selectedMap, value, () => SelectedMap);
                    if (_selectedMap != null)
                    {
                        Utilities.Utilities.OpenAndActivateMap(_selectedMap.URI);
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
                Utilities.Utilities.RunOnUiThread(() =>
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
                Utilities.Utilities.RunOnUiThread(() =>
                {
                    SetProperty(ref _selectedField, value, () => SelectedField);
                });
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

        #endregion

        #region Commands

        private readonly ICommand _getMapsCommand;

        public ICommand GetMapsCommand => _getMapsCommand;

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
            if (_maps.Count < 0) MessageBox.Show("No Maps Exist");
        }

        private void PopulateMapLayers()
        {
            _layers.Clear();
            _fields.Clear();
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

        private void PopulateLayerFields()
        {
            _fields.Clear();
            IEnumerable<Field> fields = null;
            FeatureClass featureclass = null;

            QueuedTask.Run(() =>
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
        }

        internal void ApplyDomain(DomainCode code)
        {
            MessageBox.Show(code.ToString());
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
