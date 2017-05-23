// Copyright 2017 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an 
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific 
// language governing permissions and limitations under the License.

using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Offline;
using Esri.ArcGISRuntime.UI;
using System;
using System.Linq;
using System.Windows.Media;

namespace ArcGISRuntime.WPF.Samples.GenerateGeodatabase
{
    public partial class GenerateGeodatabase
    {

        //  
        private Uri _featureServiceUri = new Uri("https://sampleserver6.arcgisonline.com/arcgis/rest/services/Sync/WildfireSync/FeatureServer");

        // 
        private const string GdbPath = @"C:\Temp\wildfire.geodatabase";

        //  
        private GenerateGeodatabaseJob _generateGdbJob;

        public GenerateGeodatabase()
        {
            InitializeComponent();

            // Create the map and extent rectangle to show in the map view
            Initialize();
        }

        private void Initialize()
        {
            // Create new Map with basemap
            Map myMap = new Map(Basemap.CreateTopographic());

            // Create and set initial map location
            MapPoint initialLocation = new MapPoint(-118.33, 38.00, SpatialReferences.Wgs84);
            myMap.InitialViewpoint = new Viewpoint(initialLocation, 100000);

            // Assign the map to the MapView
            MyMapView.Map = myMap;

            // Create a new symbol for the extent graphic
            SimpleLineSymbol lineSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, Colors.Red, 2);

            // Create graphics overlay for the extent graphic and apply a renderer
            GraphicsOverlay extentOverlay = new GraphicsOverlay();
            extentOverlay.Renderer = new SimpleRenderer(lineSymbol);

            // Add graphics overlay to the map view
            MyMapView.GraphicsOverlays.Add(extentOverlay);
          
            // Set up an event handler for when the viewpoint (extent) changes
            MyMapView.ViewpointChanged += MapViewExtentChanged;
        }

        private void MapViewExtentChanged(object sender, EventArgs e)
        {
            // Get the updated extent for the new viewpoint
            Envelope extent = MyMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry).TargetGeometry as Envelope;
            
            // Return if extent is null 
            if (extent == null) { return; }

            // Create an envelope that is a bit smaller than the extent
            EnvelopeBuilder envelopeBldr = new EnvelopeBuilder(extent);
            envelopeBldr.Expand(0.80);

            // Get the (only) graphics overlay in the map view (make sure it exists)
            var extentOverlay = MyMapView.GraphicsOverlays.FirstOrDefault();
            if(extentOverlay == null)
            {
                return;
            }

            // Get the extent graphic 
            Graphic extentGraphic = extentOverlay.Graphics.FirstOrDefault();

            // Create the extent graphic and add it to the overlay if it doesn't exist
            if (extentGraphic == null)
            {
                extentGraphic = new Graphic(envelopeBldr.ToGeometry());
                extentOverlay.Graphics.Add(extentGraphic);
            }
            else
            {
                // Otherwise, simply update the graphic's geometry
                extentGraphic.Geometry = envelopeBldr.ToGeometry();
            }
        }

        private async void GenerateGdbClick(object sender, System.Windows.RoutedEventArgs e)
        {
            // Create a task for generating a geodatabase (GeodatabaseSyncTask)
            GeodatabaseSyncTask gdbSyncTask = await GeodatabaseSyncTask.CreateAsync(_featureServiceUri);
            
            // Get the current extent of the map view
            Envelope extent = MyMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry).TargetGeometry as Envelope;

            // Get the default parameters for the generate geodatabase task
            GenerateGeodatabaseParameters generateParams = await gdbSyncTask.CreateDefaultGenerateGeodatabaseParametersAsync(extent);

            _generateGdbJob = gdbSyncTask.GenerateGeodatabase(generateParams, GdbPath);
            _generateGdbJob.JobChanged += GenerateGdbJobChanged;
            _generateGdbJob.Start();
        }

        private void GenerateGdbJobChanged(object sender, EventArgs e)
        {
            
        }
    }
}
