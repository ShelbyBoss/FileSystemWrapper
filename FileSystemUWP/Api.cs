﻿using FileSystemCommon;
using FileSystemCommon.Models.Auth;
using FileSystemCommon.Models.FileSystem;
using FileSystemCommon.Models.FileSystem.Files;
using FileSystemCommon.Models.FileSystem.Folders;
using Newtonsoft.Json;
using StdOttStandard.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Windows.Security.Cryptography.Certificates;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace FileSystemUWP
{
    public class Api
    {
        public string[] RawCookies { get; set; }

        public string BaseUrl { get; set; }

        public Api()
        {
        }

        public Task<bool> Ping()
        {
            return Request(GetUri("/api/ping"), HttpMethod.Get);
        }

        public Task<bool> IsAuthorized()
        {
            return Request(GetUri("/api/ping/auth"), HttpMethod.Get);
        }

        public async Task<bool> Login(LoginBody body)
        {
            Uri uri = GetUri("/api/auth/login");
            if (uri == null) return false;

            try
            {
                using (HttpClient client = GetClient())
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri))
                    {
                        request.Content = new HttpStringContent(JsonConvert.SerializeObject(body), UnicodeEncoding.Utf8, "application/json");

                        using (HttpResponseMessage response = await client.SendRequestAsync(request))
                        {
                            if (!response.IsSuccessStatusCode) return false;

                            RawCookies = response.Headers.Where(p => p.Key == "set-cookie").Select(p => p.Value).ToArray();
                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e);
                return false;
            }
        }

        public Task<bool> FolderExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(false);
            return Request<bool>(GetUri($"/api/folders/{Utils.EncodePath(path)}/exists"), HttpMethod.Get);
        }

        public Task<FolderContent> FolderContent(string path)
        {
            return Request<FolderContent>(GetUri($"/api/folders/content/{Utils.EncodePath(path)}"), HttpMethod.Get);
        }

        public Task<FolderItemInfo> GetFolderInfo(string path)
        {
            return Request<FolderItemInfo>(GetUri($"/api/folders/{Utils.EncodePath(path)}/info"), HttpMethod.Get);
        }

        public Task<bool> CreateFolder(string path)
        {
            return Request(GetUri($"/api/folders/{Utils.EncodePath(path)}"), HttpMethod.Post);
        }

        public Task<bool> DeleteFolder(string path, bool recrusive)
        {
            IEnumerable<KeyValuePair<string, string>> values = KeyValuePairsUtils.CreatePairs("recrusive", recrusive.ToString());

            return Request(GetUri($"/api/folders/{Utils.EncodePath(path)}", values), HttpMethod.Delete);
        }

        public Task<bool> FileExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(false);
            return Request<bool>(GetUri($"/api/files/{Utils.EncodePath(path)}/exists"), HttpMethod.Get);
        }

        public Task<FileItemInfo> GetFileInfo(string path)
        {
            return Request<FileItemInfo>(GetUri($"/api/files/{Utils.EncodePath(path)}/info"), HttpMethod.Get);
        }

        public Task<string> GetFileHash(string path)
        {
            return RequestString(GetUri($"/api/files/{Utils.EncodePath(path)}/hash"), HttpMethod.Get);
        }

        public Task<bool> CopyFile(string srcPath, string destPath)
        {
            return Request(GetUri($"/api/files/{Utils.EncodePath(srcPath)}/{Utils.EncodePath(destPath)}/copy"), HttpMethod.Post);
        }

        public Task<bool> MoveFile(string srcPath, string destPath)
        {
            return Request(GetUri($"/api/files/{Utils.EncodePath(srcPath)}/{Utils.EncodePath(destPath)}/move"), HttpMethod.Post);
        }

        public Task<bool> UploadFile(string path, IInputStream stream)
        {
            return Request(GetUri($"/api/files/{Utils.EncodePath(path)}"), HttpMethod.Post, stream);
        }

        public Task<bool> DeleteFile(string path)
        {
            return Request(GetUri($"/api/files/{Utils.EncodePath(path)}"), HttpMethod.Delete);
        }

        public Task<IRandomAccessStreamWithContentType> GetFileRandomAccessStream(string path)
        {
            return RequestRandmomAccessStream(GetUri($"/api/files/{Utils.EncodePath(path)}"), HttpMethod.Get);
        }

        public async Task DownloadFile(string path, StorageFile destFile)
        {
            Uri uri = GetUri($"/api/files/{Utils.EncodePath(path)}/download");
            if (uri == null) return;

            using (IRandomAccessStream fileStream = await destFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                using (HttpClient client = GetClient())
                {
                    using (IInputStream downloadStream = await client.GetInputStreamAsync(uri))
                    {
                        const uint capacity = 1_000_000;
                        Windows.Storage.Streams.Buffer buffer = new Windows.Storage.Streams.Buffer(capacity);
                        while (true)
                        {
                            await downloadStream.ReadAsync(buffer, capacity, InputStreamOptions.None);
                            if (buffer.Length == 0) break;
                            await fileStream.WriteAsync(buffer);
                        }
                    }
                }
            }
        }

        private async Task<TData> Request<TData>(Uri uri, HttpMethod method)
        {
            string responseText;
            if (uri == null) return default(TData);

            try
            {
                using (HttpClient client = GetClient())
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(method, uri))
                    {
                        using (HttpResponseMessage response = await client.SendRequestAsync(request))
                        {
                            if (!response.IsSuccessStatusCode || 
                                response.Content.Headers.ContentType.MediaType != "application/json") return default(TData);

                            responseText = await response.Content.ReadAsStringAsync();
                        }
                    }
                }

                return JsonConvert.DeserializeObject<TData>(responseText);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e);
                return default(TData);
            }
        }

        private async Task<string> RequestString(Uri uri, HttpMethod method)
        {
            if (uri == null) return null;

            try
            {
                using (HttpClient client = GetClient())
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(method, uri))
                    {
                        using (HttpResponseMessage response = await client.SendRequestAsync(request))
                        {
                            if (!response.IsSuccessStatusCode) return null;

                            return await response.Content.ReadAsStringAsync();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e);
                return null;
            }
        }

        private async Task<IRandomAccessStreamWithContentType> RequestRandmomAccessStream(Uri uri, HttpMethod method)
        {
            if (uri == null) return null;

            try
            {
                return await HttpRandomAccessStream.CreateAsync(GetClient(), uri);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e);
                return null;
            }
        }

        private async Task<bool> Request(Uri uri, HttpMethod method, IInputStream body = null)
        {
            if (uri == null) return false;

            try
            {
                using (HttpClient client = GetClient())
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(method, uri))
                    {
                        if (body != null) request.Content = new HttpStreamContent(body);

                        using (HttpResponseMessage response = await client.SendRequestAsync(request))
                        {
                            return response.IsSuccessStatusCode;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e);
                return false;
            }
        }

        public Uri GetUri(string resource)
        {
            return GetUri(resource, null);
        }

        public Uri GetUri(string resource, IEnumerable<KeyValuePair<string, string>> values)
        {
            if (string.IsNullOrWhiteSpace(BaseUrl)) return null;

            IEnumerable<string> queryPairs = values?.Select(p => string.Format("{0}={1}", p.Key, WebUtility.UrlEncode(p.Value)));
            string query = string.Join("&", queryPairs ?? Enumerable.Empty<string>());
            string url = string.Format("{0}/{1}?{2}", BaseUrl?.TrimEnd('/'), resource?.TrimStart('/'), query);

            try
            {
                return new Uri(url);
            }
            catch
            {
                return null;
            }
        }

        private HttpClient GetClient()
        {
            HttpClient client = new HttpClient(GetFilter());
            return client;
        }

        private HttpBaseProtocolFilter GetFilter()
        {
            HttpBaseProtocolFilter filter = new HttpBaseProtocolFilter();
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Expired);
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Untrusted);
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.InvalidName);

            filter.CacheControl.ReadBehavior = HttpCacheReadBehavior.NoCache;
            filter.CacheControl.WriteBehavior = HttpCacheWriteBehavior.NoCache;

            if (RawCookies != null && RawCookies.Length > 0)
            {
                foreach (string rawCookie in RawCookies)
                {
                    HttpCookie cookie = ParseCookie(rawCookie, BaseUrl);
                    if (cookie != null) filter.CookieManager.SetCookie(cookie);
                }
            }

            return filter;
        }

        private static HttpCookie ParseCookie(string raw, string domain)
        {
            bool? secure = null, httpOnly = null;
            string cookieName = null, cookieValue = null,
                rawMaxAge = null, rawExpires = null, path = "/", samesite;

            foreach (string pair in raw.Split(';').Select(p => p.Trim()))
            {
                string key, value;

                int index = pair.IndexOf('=');
                if (index == -1)
                {
                    key = pair;
                    value = null;
                }
                else
                {
                    key = pair.Remove(index);
                    value = pair.Substring(index + 1);
                }

                switch (key.ToLower())
                {
                    case "expires":
                        rawExpires = value;
                        break;

                    case "max-age":
                        rawMaxAge = value;
                        break;

                    case "domain":
                        domain = value;
                        break;

                    case "path":
                        path = value;
                        break;

                    case "secure":
                        secure = true;
                        break;

                    case "httponly":
                        httpOnly = true;
                        break;

                    case "samesite":
                        samesite = value;
                        break;

                    default:
                        if (cookieName == null)
                        {
                            cookieName = key;
                            cookieValue = value;
                        }
                        break;
                }
            }

            DateTime expires;
            if (DateTime.TryParse(rawExpires, out expires) && expires < DateTime.Now) return null;

            try
            {
                HttpCookie cookie = new HttpCookie(cookieName, domain, path);
                cookie.Value = cookieValue;

                if (secure.HasValue) cookie.Secure = secure.Value;
                if (httpOnly.HasValue) cookie.HttpOnly = httpOnly.Value;

                return cookie;
            }
            catch
            {
                return null;
            }
        }
    }
}
