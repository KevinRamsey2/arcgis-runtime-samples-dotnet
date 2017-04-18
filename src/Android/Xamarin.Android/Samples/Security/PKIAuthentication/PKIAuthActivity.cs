// Copyright 2017 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an 
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific 
// language governing permissions and limitations under the License.

using Android.App;
using Android.Content.Res;
using Android.OS;
using Android.Widget;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.UI.Controls;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace PKIAuthentication
{
    [Activity(Label = "PKIAuthentication", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        //TODO - Add the URL for your PKI-secured portal
        const string SecuredPortalUrl = "https://my.secure.portal.com/gis";

        //TODO - Add the ID for a web map item stored on the secure portal 
        const string WebMapId = "";

        //TODO - Add the name of the certificate (*.pfx file) in the project assets
        const string CertificateFileAsset = "MyCertificate.pfx";

        //TODO - Add the password for your certificate (file above, added to the project as an asset)
        const string MyCertPassword = "";

        // Store the map view displayed in the app
        MapView _myMapView;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Call a function to create the UI
            CreateLayout();

            // Call a function to initialize the app
            Initialize();
        }

        private void CreateLayout()
        {
            // Create a simple UI that contains a map view and a button
            var mainLayout = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };

            // Create a new map view
            _myMapView = new MapView();

            // Create a button to load a web map and set a click event handler
            Button loadMapButton = new Button(this);
            loadMapButton.Text = "Load secure map";
            loadMapButton.Click += LoadSecureMap;

            // Add the elements to the layout 
            mainLayout.AddView(loadMapButton);
            mainLayout.AddView(_myMapView);

            // Apply the layout to the app
            SetContentView(mainLayout);
        }

        private void Initialize()
        {
            // Show the imagery basemap in the map view initially
            var map = new Map(Basemap.CreateImagery());
            _myMapView.Map = map;

            // Set up a challenge handler for AuthenticationManager
            AuthenticationManager.Current.ChallengeHandler = new ChallengeHandler(LoadClientCertificate);
        }

        private Task<Credential> LoadClientCertificate(CredentialRequestInfo info)
        {
            // When a secured resource is accessed, load a client certificate and add a new CertificateCredential to AuthenticationManager
            Credential certificateCredential = null;

            try
            {
                // Open the certificate file (.pfx) stored as a project asset
                AssetManager assetManager = this.Assets;                
                var stream = assetManager.Open(CertificateFileAsset, Access.Buffer);

                // Read the file into a byte array
                var bytes = default(byte[]);
                using (var streamReader = new StreamReader(stream))
                {
                    using (var memstream = new MemoryStream())
                    {
                        streamReader.BaseStream.CopyTo(memstream);
                        bytes = memstream.ToArray();
                    }
                }

                // Import the certificate to the local user certificate store
                X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                store.Certificates.Import(bytes, MyCertPassword, X509KeyStorageFlags.UserKeySet);

                // Create a certificate from the file contents
                X509Certificate2 cert = new X509Certificate2(bytes, MyCertPassword, X509KeyStorageFlags.UserKeySet);

                // Create an ArcGIS credential from the certificate, add it to the AuthenticationManager
                certificateCredential = new CertificateCredential(cert);
                certificateCredential.ServiceUri = new Uri(SecuredPortalUrl);
                AuthenticationManager.Current.AddCredential(certificateCredential);
            }
            catch (Exception ex)
            {
                // Show an alert dialog with the exception message
                var alertBuilder = new AlertDialog.Builder(this);
                alertBuilder.SetTitle("Error adding credential");
                alertBuilder.SetMessage(ex.Message);
                alertBuilder.Show();
            }

            // Return the certificate credential
            return Task.FromResult(certificateCredential);
        }

        // Connect to the portal identified by the SecuredPortalUrl variable and load the web map identified by WebMapId
        private async void LoadSecureMap(object s, EventArgs e)
        {
            // Store messages that describe success or errors connecting to the secured portal and opening the web map
            var messageBuilder = new System.Text.StringBuilder();

            try
            {
                // See if a credential exists for this portal in the AuthenticationManager
                CredentialRequestInfo info = new CredentialRequestInfo
                {
                    ServiceUri = new Uri(SecuredPortalUrl),
                    AuthenticationType = AuthenticationType.Certificate
                };

                Credential cred = await AuthenticationManager.Current.GetCredentialAsync(info, false);

                // Throw an exception if the credential is not found
                if(cred == null) { throw new Exception("Credential not found."); }

                // Create an instance of the PKI-secured portal
                ArcGISPortal pkiSecuredPortal = await ArcGISPortal.CreateAsync(new Uri(SecuredPortalUrl), cred);

                // Report a successful connection
                messageBuilder.AppendLine("Connected to the portal on " + pkiSecuredPortal.Uri.Host);

                // Report the username for this connection
                if (pkiSecuredPortal.User != null)
                {
                    messageBuilder.AppendLine("Connected as: " + pkiSecuredPortal.User.UserName);
                }
                else
                {
                    // This shouldn't happen (if the portal is truly secured)!
                    messageBuilder.AppendLine("Connected anonymously");
                }

                // Get the web map (portal item) to display                
                var webMap = await PortalItem.CreateAsync(pkiSecuredPortal, WebMapId);
                if (webMap != null)
                {
                    // Create a new map from the portal item and display it in the map view
                    var map = new Map(webMap);
                    _myMapView.Map = map;
                }
            }
            catch (Exception ex)
            {
                // Report error
                messageBuilder.AppendLine("**-Exception: " + ex.Message);
            }
            finally
            {
                // Show an alert dialog with the status messages
                var alertBuilder = new AlertDialog.Builder(this);
                alertBuilder.SetTitle("Status");
                alertBuilder.SetMessage(messageBuilder.ToString());
                alertBuilder.Show();
            }
        }
    }
}

