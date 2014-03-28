﻿using System;
using System.Globalization;
using System.IO;
using System.Net;
using NuGet.Resources;
using NuGet.Versioning;

namespace NuGet
{
    public class PackageServer
    {
        private const string ServiceEndpoint = "/api/v2/package";
        private const string ApiKeyHeader = "X-NuGet-ApiKey";

        private readonly Lazy<Uri> _baseUri;
        private readonly string _source;
        private readonly string _userAgent;

        public event EventHandler<WebRequestEventArgs> SendingRequest = delegate { };

        public PackageServer(string source, string userAgent)
        {
            if (String.IsNullOrEmpty(source))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "source");
            }
            _source = source;
            _userAgent = userAgent;
            _baseUri = new Lazy<Uri>(ResolveBaseUrl);
        }

        public string Source
        {
            get { return _source; }
        }

        /// <summary>
        /// Pushes a package to the server that is represented by the stream.
        /// </summary>
        /// <param name="apiKey">API key to be used to push the package.</param>
        /// <param name="packageStream">Stream representing the package.</param>
        /// <param name="timeout">Time in milliseconds to timeout the server request.</param>
        [Obsolete("This overload is obsolete, please use the overload which takes a Func<Stream>")]
        public void PushPackage(string apiKey, Stream packageStream, long packageSize, int timeout)
        {
            PushPackageToServer(apiKey, () => packageStream, packageSize, timeout);
        }

        /// <summary>
        /// Pushes a package to the Source.
        /// </summary>
        /// <param name="apiKey">API key to be used to push the package.</param>
        /// <param name="package">The package to be pushed.</param>
        /// <param name="timeout">Time in milliseconds to timeout the server request.</param>
        public void PushPackage(string apiKey, IPackage package, long packageSize, int timeout) 
        {
            var sourceUri = new Uri(Source);
            if (sourceUri.IsFile)
            {
                PushPackageToFileSystem(
                    new PhysicalFileSystem(sourceUri.LocalPath),
                    package);
            }
            else
            {
                PushPackageToServer(apiKey, package.GetStream, packageSize, timeout);
            }
        }

        /// <summary>
        /// Pushes a package to the server that is represented by the stream.
        /// </summary>
        /// <param name="apiKey">API key to be used to push the package.</param>
        /// <param name="packageStreamFactory">A delegate which can be used to open a stream for the package file.</param>
        /// <param name="contentLength">Size of the package to be pushed.</param>
        /// <param name="timeout">Time in milliseconds to timeout the server request.</param>
        private void PushPackageToServer(
            string apiKey, 
            Func<Stream> packageStreamFactory, 
            long packageSize,
            int timeout) 
        {
            HttpClient client = GetClient("", "PUT", "application/octet-stream");
            
            client.SendingRequest += (sender, e) =>
            {
                SendingRequest(this, e);
                var request = (HttpWebRequest)e.Request;
                request.AllowWriteStreamBuffering = false;
                request.KeepAlive = false;

                // Set the timeout
                if (timeout <= 0)
                {
                    timeout = request.ReadWriteTimeout; // Default to 5 minutes if the value is invalid.
                }

                request.Timeout = timeout;
                request.ReadWriteTimeout = timeout;
                if (!String.IsNullOrEmpty(apiKey))
                {
                    request.Headers.Add(ApiKeyHeader, apiKey);
                }

                var multiPartRequest = new MultipartWebRequest();
                multiPartRequest.AddFile(packageStreamFactory, "package", packageSize);

                multiPartRequest.CreateMultipartRequest(request);
            };

            EnsureSuccessfulResponse(client);
        }

        /// <summary>
        /// Pushes a package to a FileSystem.
        /// </summary>
        /// <param name="fileSystem">The FileSystem that the package is pushed to.</param>
        /// <param name="package">The package to be pushed.</param>
        private static void PushPackageToFileSystem(IFileSystem fileSystem, IPackage package)
        {
            var pathResolver = new DefaultPackagePathResolver(fileSystem);
            var packageFileName = pathResolver.GetPackageFileName(package);
            using (var stream = package.GetStream())
            {
                fileSystem.AddFile(packageFileName, stream);
            }
        }

        /// <summary>
        /// Deletes a package from the Source.
        /// </summary>
        /// <param name="apiKey">API key to be used to delete the package.</param>
        /// <param name="packageId">The package Id.</param>
        /// <param name="packageVersion">The package version.</param>
        public void DeletePackage(string apiKey, string packageId, string packageVersion)
        {
            var sourceUri = new Uri(Source);
            if (sourceUri.IsFile)
            {
                DeletePackageFromFileSystem(
                    new PhysicalFileSystem(sourceUri.LocalPath),
                    packageId,
                    packageVersion);
            }
            else
            {
                DeletePackageFromServer(apiKey, packageId, packageVersion);
            }
        }

        /// <summary>
        /// Deletes a package from the server represented by the Source.
        /// </summary>
        /// <param name="apiKey">API key to be used to delete the package.</param>
        /// <param name="packageId">The package Id.</param>
        /// <param name="packageVersion">The package Id.</param>
        private void DeletePackageFromServer(string apiKey, string packageId, string packageVersion)
        {
            // Review: Do these values need to be encoded in any way?
            var url = String.Join("/", packageId, packageVersion);
            HttpClient client = GetClient(url, "DELETE", "text/html");
            
            client.SendingRequest += (sender, e) =>
            {
                SendingRequest(this, e);
                var request = (HttpWebRequest)e.Request;
                request.Headers.Add(ApiKeyHeader, apiKey);
            };
            EnsureSuccessfulResponse(client);
        }

        /// <summary>
        /// Deletes a package from a FileSystem.
        /// </summary>
        /// <param name="fileSystem">The FileSystem where the specified package is deleted.</param>
        /// <param name="packageId">The package Id.</param>
        /// <param name="packageVersion">The package Id.</param>
        private static void DeletePackageFromFileSystem(IFileSystem fileSystem, string packageId, string packageVersion)
        {
            var pathResolver = new DefaultPackagePathResolver(fileSystem);
            var packageFileName = pathResolver.GetPackageFileName(packageId, NuGetVersion.Parse(packageVersion));
            fileSystem.DeleteFile(packageFileName);
        }
        
        private HttpClient GetClient(string path, string method, string contentType)
        {
            var baseUrl = _baseUri.Value;
            Uri requestUri = GetServiceEndpointUrl(baseUrl, path);

            var client = new HttpClient(requestUri)
            {
                ContentType = contentType,
                Method = method
            };

            if (!String.IsNullOrEmpty(_userAgent))
            {
                client.UserAgent = HttpUtility.CreateUserAgentString(_userAgent);
            }

            return client;
        }

        internal static Uri GetServiceEndpointUrl(Uri baseUrl, string path)
        {
            Uri requestUri;
            if (String.IsNullOrEmpty(baseUrl.AbsolutePath.TrimStart('/')))
            {
                // If there's no host portion specified, append the url to the client.
                requestUri = new Uri(baseUrl, ServiceEndpoint + '/' + path);
            }
            else
            {
                requestUri = new Uri(baseUrl, path);
            }
            return requestUri;
        }

        private static void EnsureSuccessfulResponse(HttpClient client, HttpStatusCode? expectedStatusCode = null)
        {
            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)client.GetResponse();
                if (response != null && 
                    ((expectedStatusCode.HasValue && expectedStatusCode.Value != response.StatusCode) || 

                    // If expected status code isn't provided, just look for anything 400 (Client Errors) or higher (incl. 500-series, Server Errors)
                    // 100-series is protocol changes, 200-series is success, 300-series is redirect.
                    (!expectedStatusCode.HasValue && (int)response.StatusCode >= 400)))
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, NuGetResources.PackageServerError, response.StatusDescription, String.Empty));
                }
            }
            catch (WebException e)
            {
                if (e.Response == null)
                {
                    throw;
                }
                response = (HttpWebResponse)e.Response;
                if (expectedStatusCode != response.StatusCode)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, NuGetResources.PackageServerError, response.StatusDescription, e.Message), e);
                }
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                    response = null;
                }
            }
        }

        private Uri ResolveBaseUrl()
        {
            Uri uri;

            try
            {
                var client = new RedirectedHttpClient(new Uri(Source));
                uri = client.Uri;
            }
            catch (WebException ex)
            {
                var response = (HttpWebResponse)ex.Response;
                if (response == null)
                {
                    throw;
                }

                uri = response.ResponseUri;
            }

            return EnsureTrailingSlash(uri);
        }

        private static Uri EnsureTrailingSlash(Uri uri)
        {
            string value = uri.OriginalString;
            if (!value.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                value += "/";
            }
            return new Uri(value);
        }
    }
}
