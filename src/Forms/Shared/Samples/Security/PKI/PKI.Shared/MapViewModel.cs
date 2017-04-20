// Copyright 2017 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an 
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific 
// language governing permissions and limitations under the License.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Security;
using System.IO;
using Esri.ArcGISRuntime.Portal;
#if WINDOWS_UWP
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Security.Cryptography.Certificates;
#endif
#if __ANDROID__
using Android.App;
using Android.Content.Res;
using System.Security.Cryptography.X509Certificates;
#endif
#if __IOS__
using System.Security.Cryptography.X509Certificates;
using UIKit;
#endif
#if WINDOWS_UWP
using Windows.UI.Popups;
#endif

namespace PKI.Shared
{
    /// <summary>
    /// Provides map data to an application
    /// </summary>
    public class MapViewModel : INotifyPropertyChanged
    {
        //TODO - Add the URL for your PKI-secured portal
        const string SecuredPortalUrl = "https://my.secure.portal.com/gis";

        //TODO - Add the ID for a web map item stored on the secure portal 
        const string WebMapId = "";

        //TODO - Add the name of the certificate (*.pfx file) in the project (Android asset, iOS project file, e.g.)
        const string CertificateFileName = "MyCertificate.pfx";

        //TODO - Add the password for your certificate (file above, added to the project as an asset)
        const string MyCertPassword = "";

        public MapViewModel()
        {
#if __ANDROID__ || __IOS__
            // Set up a challenge handler for AuthenticationManager
            AuthenticationManager.Current.ChallengeHandler = new ChallengeHandler(LoadClientCertificate);
#endif
            LoadSecureMap();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        private async Task<Credential> LoadClientCertificate(CredentialRequestInfo info)
        {
            // When a secured resource is accessed, load a client certificate and add a new CertificateCredential to AuthenticationManager
            Credential certificateCredential = null;
#if __ANDROID__
            // Get the current Android Context
            var context = Xamarin.Forms.Forms.Context;
#endif
            try
            {
#if __ANDROID__
                // Get the current Android Activity
                var activity = context as Activity;

                // Open the certificate file (.pfx) stored as a project asset
                AssetManager assetManager = activity.Assets;
                var stream = assetManager.Open(CertificateFileName, Access.Buffer);

                // Read the file into a byte array
                var certificateData = default(byte[]);
                using (var streamReader = new StreamReader(stream))
                {
                    using (var memstream = new MemoryStream())
                    {
                        streamReader.BaseStream.CopyTo(memstream);
                        certificateData = memstream.ToArray();
                    }
                }
#endif
#if __IOS__
                // Open the certificate file (.pfx) delivered as a project file, read its contents
                var path = Path.Combine(Foundation.NSBundle.MainBundle.BundlePath, CertificateFileName);
                var certificateData = File.ReadAllBytes(path);
#endif
#if __ANDROID__ || __IOS__
                // Import the certificate to the local user certificate store
                X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                store.Certificates.Import(certificateData, MyCertPassword, X509KeyStorageFlags.UserKeySet);

                // Create a certificate from the file contents
                X509Certificate2 cert = new X509Certificate2(certificateData, MyCertPassword, X509KeyStorageFlags.UserKeySet);

                // Create an ArcGIS credential from the certificate, add it to the AuthenticationManager
                certificateCredential = new CertificateCredential(cert);
                certificateCredential.ServiceUri = new Uri(SecuredPortalUrl);
                AuthenticationManager.Current.AddCredential(certificateCredential);
#endif
#if WINDOWS_UWP
                var certificateFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(@"ms-appx:///Assets/" + CertificateFileName));

                // Read the contents of the file
                IBuffer buffer = await FileIO.ReadBufferAsync(certificateFile);
                var certificateString = string.Empty;
                using (DataReader dataReader = DataReader.FromBuffer(buffer))
                {
                    // Store the contents of the file as an encrypted string
                    // The string will be imported as a certificate when the user enters the password
                    byte[] bytes = new byte[buffer.Length];
                    dataReader.ReadBytes(bytes);
                    certificateString = Convert.ToBase64String(bytes);
                }

                // Import the certificate by providing: 
                //   -the encoded certificate string, 
                //   -the password (entered by the user)
                //   -certificate options (export, key protection, install)
                //   -a friendly name (the name of the pfx file)
                await CertificateEnrollmentManager.ImportPfxDataAsync(
                    certificateString,
                    MyCertPassword,
                    ExportOption.Exportable,
                    KeyProtectionLevel.NoConsent,
                    InstallOptions.None,
                    certificateFile.DisplayName); 
#endif
            }
            catch (Exception ex)
            {
                // Display exception message
#if __ANDROID__
                var alertBuilder = new AlertDialog.Builder(context);
                alertBuilder.SetTitle("Error adding credential");
                alertBuilder.SetMessage(ex.Message);
                alertBuilder.Show();
#endif
#if __IOS__
                UIAlertView alert = new UIAlertView("Error", ex.Message, null, "OK");
                alert.Show();
#endif
#if WINDOWS_UWP
                var dialog = new MessageDialog(ex.Message, "Error Loading Certificate");
                dialog.ShowAsync();
#endif
            }

            // Return the certificate credential
            return certificateCredential;
        }

        // Connect to the portal identified by the SecuredPortalUrl variable and load the web map identified by WebMapId
        private async void LoadSecureMap()
        {
            // Store messages that describe success or errors connecting to the secured portal and opening the web map
            var messageBuilder = new System.Text.StringBuilder();
#if __ANDROID__
            // Get the current Android Context
            var context = Xamarin.Forms.Forms.Context;
#endif
            try
            {
#if __ANDROID__ || __IOS__
                // See if a credential exists for this portal in the AuthenticationManager
                CredentialRequestInfo info = new CredentialRequestInfo
                {
                    ServiceUri = new Uri(SecuredPortalUrl),
                    AuthenticationType = AuthenticationType.Certificate
                };

                Credential cred = await AuthenticationManager.Current.GetCredentialAsync(info, false);

                // Throw an exception if the credential is not found
                if (cred == null) { throw new Exception("Credential not found."); }

                // Create an instance of the PKI-secured portal
                ArcGISPortal pkiSecuredPortal = await ArcGISPortal.CreateAsync(new Uri(SecuredPortalUrl), cred);
#else
                // Create an instance of the PKI-secured portal
                ArcGISPortal pkiSecuredPortal = await ArcGISPortal.CreateAsync(new Uri(SecuredPortalUrl));
#endif
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
                    Map = map;
                }
            }
            catch (Exception ex)
            {
                // Report error
                messageBuilder.AppendLine("**-Exception: " + ex.Message);
            }
            finally
            {
                // Display the status of the login
#if __ANDROID__
                var alertBuilder = new AlertDialog.Builder(context);
                alertBuilder.SetTitle("Status");
                alertBuilder.SetMessage(messageBuilder.ToString());
                alertBuilder.Show();
#endif
#if __IOS__
                UIAlertView alert = new UIAlertView("Status", messageBuilder.ToString(), null, "OK");
                alert.Show();
#endif
#if WINDOWS_UWP
                var dialog = new MessageDialog(messageBuilder.ToString());
                dialog.ShowAsync();
#endif
            }
        }

        private Map _map = new Map(Basemap.CreateStreetsVector());

        /// <summary>
        /// Gets or sets the map
        /// </summary>
        public Map Map
        {
            get { return _map; }
            set { _map = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Raises the <see cref="MapViewModel.PropertyChanged" /> event
        /// </summary>
        /// <param name="propertyName">The name of the property that has changed</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var propertyChangedHandler = PropertyChanged;
            if (propertyChangedHandler != null)
                propertyChangedHandler(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}