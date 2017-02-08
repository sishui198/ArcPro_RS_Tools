﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System.Windows.Input;
using System.Collections.ObjectModel;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Data;
using ArcGIS.Core.Events;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Editing;
using ArcGIS.Core.Geometry;

namespace RS_Tools.Tools.Inspector
{
    internal class InspectorSettingsViewModel : DockPane
    {
        private const string _dockPaneID = "RS_Tools_Tools_Inspector_InspectorSettings";

        private readonly ObservableCollection<Map> _maps = new ObservableCollection<Map>();
        private readonly ObservableCollection<FeatureLayer> _layers = new ObservableCollection<FeatureLayer>();
        private Map _selectedMap = null;
        private FeatureLayer _selectedLayer = null;
        private static string InpsectorFieldName = "RSINS";

        /// <summary>
        /// used to lock collections for use by multiple threads
        /// </summary>
        private readonly object _lockCollection = new object();

        protected InspectorSettingsViewModel() {

            _getMapsCommand = new RelayCommand(() => GetMaps(), () => true);

            Utilities.Utilities.RunOnUiThread(() =>
            {
                BindingOperations.EnableCollectionSynchronization(_maps, _lockCollection);
                BindingOperations.EnableCollectionSynchronization(_layers, _lockCollection);
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
        private string _heading = "Tracker";
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

        /// <summary>
        /// Collection of map items.  Bind to this property in the view. 
        /// </summary>
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
            if (_maps.Count < 0)
            {
                MessageBox.Show("No Maps Exist");
            }
        }


        private void PopulateMapLayers()
        {
            if (_selectedLayer != null)
            {
            }

            _layers.Clear();
            if (_selectedMap != null) {
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

        private async Task<bool> PrepStatus()
        {
            if (_selectedMap == null)
            {
                MessageBox.Show("Select A Map In Inspector Settings");
                return false;
            }

            if (_selectedLayer == null)
            {
                MessageBox.Show("Select A Layer in Inspector Settings");
                return false;
            }

            IEnumerable<Field> fields = null;
            FeatureClass featureclass = null;

            await QueuedTask.Run(() =>
            {
                // Get the fields
                Table table = (_selectedLayer as FeatureLayer).GetTable();

                if (table is FeatureClass)
                {
                    featureclass = table as FeatureClass;
                    using (FeatureClassDefinition def = featureclass.GetDefinition())
                    {
                        fields = def.GetFields();
                    }
                }
            });


            var match = fields.FirstOrDefault(field => field.Name.ToLower().Contains(InpsectorFieldName.ToLower()) && (field.FieldType == FieldType.SmallInteger || field.FieldType == FieldType.Integer));
            if (match == null)
            {
                MessageBox.Show("Add field named '" + InpsectorFieldName + "' to '" + _selectedLayer.Name + "' layer of type Integer");
                return false;
            }
            return true;
        }

        private async Task<bool> FeaturesSelected(bool showMessage)
        {
            int featurecount = 0;
            await QueuedTask.Run(() =>
            {
                // Get the number of selected features
                featurecount = (_selectedLayer as BasicFeatureLayer).GetSelection().GetCount();
            });

            if (featurecount <= 0)
            {
                if (showMessage) MessageBox.Show("Select At Least One Feature from '" + _selectedLayer.Name + "' layer");
                return false;
            }
            return true;
        }
             
        public async void OkNext()
        {
            bool proceed = false;

            proceed = await PrepStatus();

            if (!proceed) return;

            if (await FeaturesSelected(false))
            {
                Update(1, _selectedLayer, "Ok, Next");
            }

            ZoomToNext(_selectedLayer);            
        }

        public async void OkStay()
        {
            bool proceed = await PrepStatus();

            if (!proceed) return;
            
            if (await FeaturesSelected(true)) Update(1, _selectedLayer, "Okay Stay");

        }

        public void Update(int status, Layer featureLayer, string editName)
        {
            QueuedTask.Run(async () => {
                try
                {
                    var basicfeaturelayer = _selectedLayer as BasicFeatureLayer;
                    var selection = basicfeaturelayer.GetSelection();
                    var oidset = selection.GetObjectIDs();
                    


                    var insp = new ArcGIS.Desktop.Editing.Attributes.Inspector(true);
                    insp.Load(basicfeaturelayer, oidset);
                    insp[InpsectorFieldName] = 1;
                    await insp.ApplyAsync();
                    

                    basicfeaturelayer.ClearSelection();
                } catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                }
                
            });
        }

        public async void ZoomToNext(Layer featureLayer)
        {
            var basicfeaturelayer = _selectedLayer as BasicFeatureLayer;

            QueryFilter queryfilter = new QueryFilter();
            queryfilter.WhereClause = InpsectorFieldName + " IS NULL";
            try
            {
                await QueuedTask.Run(async () =>
                {
                    basicfeaturelayer.ClearSelection();
                    FeatureClass featureclass = Utilities.Utilities.LayerToFeatureClass(_selectedLayer);

                    using (RowCursor cursor = featureclass.Search(queryfilter, false))
                    {
                        while (cursor.MoveNext())
                        {
                            using (Feature feature = (Feature)cursor.Current)
                            {
                                var shape = feature.GetShape() as Geometry;
                                
                                await MapView.Active.ZoomToAsync(shape.Extent, null, false);
                                
                                MapView.Active.SelectFeatures(shape);

                                return;

                            }
                        }
                    }


                });
            } catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
            
        }
        #endregion

    }

    

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class InspectorSettings_ShowButton : Button
    {
        protected override void OnClick()
        {
            IPlugInWrapper wrapper = FrameworkApplication.GetPlugInWrapper("RS_Tools_Tools_Inspector_Buttons_OKNext");
            var command = wrapper as ICommand;
            InspectorSettingsViewModel.Show();
        }
    }
}