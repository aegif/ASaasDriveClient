﻿using DotCMIS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CmisSync.Auth.Specific.NemakiWare
{
    class AuthTokenManager
    {
        private static RestClient buildRestClient(Dictionary<string, string> parameters)
        {
            string user = parameters[SessionParameter.User];
            string password = parameters[SessionParameter.Password];
            string repositoryId = parameters[SessionParameter.RepositoryId];
            string atompubUrl = parameters[SessionParameter.AtomPubUrl];

            Uri u = new Uri(atompubUrl);
            String authority = u.Authority;
            string endPoint = u.GetLeftPart(UriPartial.Authority) + "/core/rest/repo/" + repositoryId;
            var client = new RestClient(endPoint);
            client.Authenticator = new HttpBasicAuthenticator(user, password);

            return client;
        }

        private static JToken processJson(Dictionary<string, string> parameters, RestRequest request, Func<JContainer, JToken> process)
        {
            JToken result = null;

            //Call REST API
            RestClient client = buildRestClient(parameters);
            IRestResponse response = client.Execute(request);

            //Parse json response
            var json = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JContainer>(response.Content);
            if ("success" == json["status"].Value<String>())
            {
                result = process(json);
            }
            return result;
        }

        private static JToken processAuthTokenJson(Dictionary<string, string> parameters, RestRequest request, Func<JContainer, JToken> process)
        {
            string userId = parameters[SessionParameter.User];
            request.AddParameter("userId", userId, ParameterType.UrlSegment);
            request.AddQueryParameter("app", "cmissync");
            return processJson(parameters, request, process);
        }

        private static JToken register(Dictionary<string, string> parameters)
        {
            var request = new RestRequest("authtoken/{userId}/register");
            return processAuthTokenJson(parameters, request, json => json["value"]);

        }

        private static JToken get(Dictionary<string, string> parameters)
        {
            var request = new RestRequest("authtoken/{userId}");
            return processAuthTokenJson(parameters, request, json => json["value"]);

        }

        public static string getOrRegister(Dictionary<string, string> parameters)
        {
            string user = parameters[SessionParameter.User];
            string repositoryId = parameters[SessionParameter.RepositoryId];

            var store = AuthTokenStore.get(repositoryId, user);
            store.

            JToken getResult = get(parameters);
            if (getResult == null
                || String.IsNullOrEmpty(getResult["token"].Value<String>())
                || getResult["expiration"].Value<Int64>() < CurrentTimeMillis())
            {
                JToken registerResult = register(parameters);
                return registerResult["token"].Value<String>();
            }
            else
            {
                return getResult["token"].Value<String>();
            }
        }

        private static readonly DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static long CurrentTimeMillis()
        {
            return (long)(DateTime.UtcNow - Jan1st1970).TotalMilliseconds;
        }

    }
}
