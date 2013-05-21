//
// GengoClient.cs
//
// Author:
//       Jarl Erik Schmidt <github@jarlerik.com>
//
// Copyright (c) 2013 Jarl Erik Schmidt
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

namespace Winterday.External.Gengo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Mime;
    using System.Text;
    using System.Threading.Tasks;

    using Newtonsoft.Json.Linq;

    using Winterday.External.Gengo.MethodGroups;
    using Winterday.External.Gengo.Payloads;
    using Winterday.External.Gengo.Properties;

    public class GengoClient : IGengoClient, IDisposable
    {
        internal const string ProductionBaseUri = "http://api.gengo.com/v2/";
        internal const string SandboxBaseUri = "http://api.sandbox.gengo.com/v2/";

        internal const string MimeTypeApplicationXml = "application/xml";
        internal const string MimeTypeApplicationJson = "application/json";

        readonly string _privateKey;
        readonly string _publicKey;

        readonly Uri _baseUri;
        readonly HttpClient _client = new HttpClient();

        AccountMethodGroup _account;
        JobMethodGroup _job;
        JobsMethodGroup _jobs;
        ServiceMethodGroup _service;
        OrderMethodGroup _order;

        bool _disposed;

        bool IGengoClient.IsDisposed
        {
            get
            {
                return _disposed;
            }
        }

        public AccountMethodGroup Account
        {
            get { return _account; }
        }

        public JobMethodGroup Job
        {
            get { return _job; }
        }

        public JobsMethodGroup Jobs
        {
            get { return _jobs; }
        }

        public ServiceMethodGroup Service
        {
            get { return _service; }
        }

        public OrderMethodGroup Order
        {
            get { return _order; }
        }

        private IGengoClient that
        {
            get { return this as IGengoClient; }
        }

        public GengoClient(string privateKey, string publicKey)
        {
            if (string.IsNullOrWhiteSpace(privateKey))
                throw new ArgumentException("Private key not specified", "privateKey");

            if (string.IsNullOrWhiteSpace(publicKey))
                throw new ArgumentException("Public key not specified", "publicKey");

            _privateKey = privateKey;
            _publicKey = publicKey;

            _baseUri = new Uri(ProductionBaseUri);

            initClient();
        }

        public GengoClient(string privateKey, string publicKey, ClientMode mode)
        {
            if (string.IsNullOrWhiteSpace(privateKey))
                throw new ArgumentException("Private key not specified", "privateKey");

            if (string.IsNullOrWhiteSpace(publicKey))
                throw new ArgumentException("Public key not specified", "publicKey");

            _privateKey = privateKey;
            _publicKey = publicKey;

            var uri = mode == ClientMode.Production ? ProductionBaseUri : SandboxBaseUri;

            _baseUri = new Uri(uri);

            initClient();
        }

        public GengoClient(string privateKey, string publicKey, String baseUri)
        {
            if (string.IsNullOrWhiteSpace(privateKey))
                throw new ArgumentException("Private key not specified", "privateKey");

            if (string.IsNullOrWhiteSpace(publicKey))
                throw new ArgumentException("Public key not specified", "publicKey");

            if (string.IsNullOrWhiteSpace(baseUri))
                throw new ArgumentException("Base uri not specified", "baseUri");

            if (!Uri.IsWellFormedUriString(baseUri, UriKind.Absolute))
                throw new ArgumentException("Base uri is not an absolute uri", "baseUri");

            _privateKey = privateKey;
            _publicKey = publicKey;

            _baseUri = new Uri(baseUri);

            initClient();
        }

        private void initClient()
        {
            _account = new AccountMethodGroup(this);
            _job = new JobMethodGroup(this);
            _jobs = new JobsMethodGroup(this);
            _service = new ServiceMethodGroup(this);
            _order = new OrderMethodGroup(this);

            var assemblyName = GetType().Assembly.GetName();
            var headers = _client.DefaultRequestHeaders;

            headers.UserAgent.Add(new ProductInfoHeaderValue(assemblyName.Name, assemblyName.Version.ToString()));
            headers.AcceptCharset.Add(new StringWithQualityHeaderValue("utf-8"));
            headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MimeTypeApplicationJson));
        }

        internal void AddAuthData(Dictionary<string, string> dict)
        {
            AddAuthData(dict, true);
        }

        internal void AddAuthData(Dictionary<string, string> dict, bool authenticated)
        {
            if (dict == null) throw new ArgumentNullException("dict");

            dict["api_key"] = _publicKey;

            if (authenticated)
            {
                var ts = DateTime.UtcNow.ToTimeStamp().ToString();
                var hash = _privateKey.SHA1Hash(ts);

                dict["ts"] = ts;
                dict["api_sig"] = hash;

            }
        }

        internal Uri BuildUri(String uriPart, bool authenticated)
        {
            return BuildUri(uriPart, null, authenticated);
        }

        internal Uri BuildUri(String uriPart, Dictionary<string, string> query, bool authenticated)
        {
            if (String.IsNullOrWhiteSpace("uriPart"))
                throw new ArgumentException("Uri part not provided", "baseUri");

            if (!Uri.IsWellFormedUriString(uriPart, UriKind.Relative))
                throw new ArgumentException("Uri part not valid relative uri", "baseUri");

            query = query ?? new Dictionary<string, string>();

            AddAuthData(query, authenticated);

            return new Uri(_baseUri, uriPart + query.ToQueryString());
        }

        async Task<JsonT> IGengoClient.DeleteAsync<JsonT>(String uripart)
        {
            var response = await _client.DeleteAsync(BuildUri(uripart, true));
            var responseStr = await response.Content.ReadAsStringAsync();

            return UnpackJson<JsonT>(responseStr);
        }

        Task<string> IGengoClient.GetStringAsync(String uriPart, bool authenticated)
        {
            return _client.GetStringAsync(BuildUri(uriPart, authenticated));
        }

        Task<string> IGengoClient.GetStringAsync(String uriPart, Dictionary<string, string> values, bool authenticated)
        {
            return _client.GetStringAsync(BuildUri(uriPart, values, authenticated));
        }

        async Task<JsonT> IGengoClient.GetJsonAsync<JsonT>(String uriPart, bool authenticated)
        {
            var rawJson = await that.GetStringAsync(uriPart, authenticated);

            return UnpackJson<JsonT>(rawJson);
        }

        async Task<JsonT> IGengoClient.GetJsonAsync<JsonT>(String uriPart, Dictionary<string, string> values, bool authenticated)
        {
            var rawJson = await that.GetStringAsync(uriPart, values, authenticated);

            return UnpackJson<JsonT>(rawJson);
        }


        async Task<JsonT> IGengoClient.PostFormAsync<JsonT>(String uriPart, Dictionary<string, string> values)
        {
            if (values == null) throw new ArgumentNullException("values");

            var auth = new Dictionary<string, string>();

            AddAuthData(auth);

            var data = new FormUrlEncodedContent(values);

            var response = await _client.PostAsync(new Uri(_baseUri, uriPart), data);
            var responseStr = await response.Content.ReadAsStringAsync();

            return UnpackJson<JsonT>(responseStr);
        }

        Task<JsonT> IGengoClient.PostJsonAsync<JsonT>(
            String uriPart, JToken json
            )
        {
            return that.PostJsonAsync<JsonT>(uriPart, json, null);
        }

        async Task<JsonT> IGengoClient.PostJsonAsync<JsonT>(
            String uriPart, JToken json, IEnumerable<IPostableFile> files
            )
        {
            if (json == null) throw new ArgumentNullException("json");

            return await transferJsonAsync<JsonT>(
                uriPart, json, files, (u, c) => _client.PostAsync(u, c));

        }

        Task<JsonT> IGengoClient.PutJsonAsync<JsonT>(
            String uriPart, JToken json
            )
        {
            if (json == null) throw new ArgumentNullException("json");

            return transferJsonAsync<JsonT>(
                uriPart, json, null, (u, c) => _client.PutAsync(u, c));
        }

        async Task<JsonT> transferJsonAsync<JsonT>(
            String uriPart, JToken json, IEnumerable<IPostableFile> files,
            Func<Uri, HttpContent, Task<HttpResponseMessage>> clientFunc
            ) where JsonT : JToken
        {
            if (clientFunc == null) throw new ArgumentNullException("clientFunc");
            if (json == null) throw new ArgumentNullException("json");

            var auth = new Dictionary<string, string>();

            AddAuthData(auth);

            var multi = new MultipartFormDataContent();

            foreach (var pair in auth)
            {
                multi.Add(new StringContent(pair.Value, Encoding.UTF8, MediaTypeNames.Text.Plain), pair.Key);
            }

            var data = new StringContent(json.ToString(), Encoding.UTF8, MimeTypeApplicationJson);

            multi.Add(data, "data");

            if (files != null)
            {
                foreach (var file in files)
                {
                    multi.Add(file.Content, file.FileKey, file.FileName);
                }
            }

            var response = await clientFunc(new Uri(_baseUri, uriPart), multi);
            var responseStr = await response.Content.ReadAsStringAsync();

            return UnpackJson<JsonT>(responseStr);
        }


        JsonT UnpackJson<JsonT>(String rawJson) where JsonT : JToken
        {
            var json = JObject.Parse(rawJson);

            var opstat = json.Value<string>("opstat");
            var errObj = json.Value<JObject>("err");

            if (errObj == null)
            {
                return json["response"] as JsonT;
            }
            else
            {
                string message = errObj.Value<string>("msg");
                string code = errObj.Value<string>("code");

                throw new GengoException(message, opstat, code);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _client.Dispose();
                _disposed = true;
            }
        }
    }
}

