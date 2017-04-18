// Copyright 2017 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an 
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific 
// language governing permissions and limitations under the License.

using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.UI.Controls;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using UIKit;

namespace PKIAuthentication
{
    public partial class PKIViewController : UIViewController
    {
        // Store the map view displayed in the app
        MapView _myMapView;

        //TODO - Add the URL for your PKI-secured portal
        const string SecuredPortalUrl = "https://my.secure.portal.com/gis/sharing";

        //TODO - Add the ID for a web map item stored on the secure portal 
        const string WebMapId = "";

        //TODO - Add the name of the certificate (*.pfx file) delivered with the app
        private const string CertificateFileName = "Cert/MyCertificate.pfx";

        //TODO - Add the password for your certificate (file above, added to the project)
        private const string CertificatePassword = "";

        public PKIViewController(IntPtr handle) : base(handle)
        {
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            // Call a function to create the UI
            CreateLayout();

            // Call a function to initialize the app
            Initialize();
        }

        private void CreateLayout()
        {
            // Setup the visual frame for the MapView
            var mapViewRect = new CoreGraphics.CGRect(0, 90, View.Bounds.Width, View.Bounds.Height - 90);

            // Create a map view with a basemap
            _myMapView = new MapView();
            _myMapView.Map = new Map(Basemap.CreateImagery());
            _myMapView.Frame = mapViewRect;

            // Create a button to load a web map
            var buttonRect = new CoreGraphics.CGRect(40, 50, View.Bounds.Width - 80, 30);
            UIButton loadWebMapButton = new UIButton(buttonRect);
            loadWebMapButton.SetTitleColor(UIColor.Blue, UIControlState.Normal);
            loadWebMapButton.SetTitle("Load secure web map", UIControlState.Normal);
            loadWebMapButton.TouchUpInside += LoadWebMapButton_TouchUpInside;

            // Add the map view and button to the page
            View.AddSubviews(loadWebMapButton, _myMapView);
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
                // Open the certificate file (.pfx) delivered as a project file, read its contents
                var path = System.IO.Path.Combine(Foundation.NSBundle.MainBundle.BundlePath, CertificateFileName);
                var certificateData = System.IO.File.ReadAllBytes(path);

                // Import the certificate to the local user certificate store
                X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                store.Certificates.Import(certificateData, CertificatePassword, X509KeyStorageFlags.UserKeySet);

                // Create a certificate credential from the file data
                var certificate = new X509Certificate(certificateData, CertificatePassword);
                certificateCredential = new CertificateCredential(certificate);
                certificateCredential.ServiceUri = info.ServiceUri;

                // Add the credential to the authentication manager
                AuthenticationManager.Current.AddCredential(certificateCredential);
            }
            catch (Exception exp)
            {
                // Display exception message
                UIAlertView alert = new UIAlertView("Error", exp.Message, null, "OK");
                alert.Show();
            }

            // Return the credential
            return Task.FromResult(certificateCredential);
        }

        private async void LoadWebMapButton_TouchUpInside(object sender, EventArgs e)
        {
            // Store messages that describe success or errors connecting to the secured portal and opening the web map
            var messageBuilder = new System.Text.StringBuilder();

            try
            {
                // See if a credential exists for this portal in the AuthenticationManager
                // If a credential is not found, the user will be prompted for login info
                CredentialRequestInfo info = new CredentialRequestInfo
                {
                    ServiceUri = new Uri(SecuredPortalUrl),
                    AuthenticationType = AuthenticationType.Certificate
                };
                Credential cred = await AuthenticationManager.Current.GetCredentialAsync(info, false);

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
            catch (TaskCanceledException)
            {
                // Report canceled login
                messageBuilder.AppendLine("Login was canceled");
            }
            catch (Exception ex)
            {
                // Report error
                messageBuilder.AppendLine("Exception: " + ex.Message);
            }
            finally
            {
                // Display the status of the login
                UIAlertView alert = new UIAlertView("Status", messageBuilder.ToString(), null, "OK");
                alert.Show();
            }
        }
    }
}