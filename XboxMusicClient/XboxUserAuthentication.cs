// Copyright (c) Microsoft Corporation
// All rights reserved. 
//
// Licensed under the Apache License, Version 2.0 (the ""License""); you may
// not use this file except in compliance with the License. You may obtain a
// copy of the License at http://www.apache.org/licenses/LICENSE-2.0 
//
// THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT. 
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

// DO NOT PUBLISH THIS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xbox.Music.Platform.Contract.AuthenticationDataModel;

namespace Microsoft.Xbox.Music.Platform.Client
{
    public class XboxUserAuthentication : SimpleServiceClient
    {
        private readonly Uri xasuHostname = new Uri("https://user.auth.xboxlive.com/");
        private readonly Uri xstsHostname = new Uri("https://xsts.auth.xboxlive.com");

        public async Task<TXToken> GetTokenAsync<TXToken>(string rpsTicket, string rpsSiteName, CancellationToken cancellationToken) where TXToken : IXToken, new()
        {
            // Authenticate the user
            XasTokenResponse userToken = await GetUserTokenAsync(rpsTicket, rpsSiteName, cancellationToken);
            // Get an authorization token
            XasTokenResponse xToken = await GetXTokenAsync(userToken.Token, cancellationToken);

            // Create the HTTP Authorization header value
            string userHash = GetUserHash(userToken);
            string authorizationHeaderValue = null;
            if (userHash != null && !String.IsNullOrEmpty(xToken.Token))
            {
                authorizationHeaderValue = "XBL3.0 x=" + userHash + ";" + xToken.Token;
            }

            return new TXToken
            {
                IssueInstant = xToken.IssueInstant,
                NotAfter = xToken.NotAfter,
                AuthorizationHeaderValue = authorizationHeaderValue,
            };
        }

        // Extract the user hash from a user authentication token response
        private string GetUserHash(XasTokenResponse userToken)
        {
            if (userToken.DisplayClaims != null &&
                userToken.DisplayClaims.Xui != null)
            {
                // As we're doing single user authentication, we only expect to find a single user identiy
                Dictionary<string, string> userIdentity = userToken.DisplayClaims.Xui.FirstOrDefault();
                if (userIdentity != null)
                {
                    return userIdentity["uhs"];
                }
            }
            return null;
        }

        private async Task<XasTokenResponse> GetUserTokenAsync(string rpsTicket, string rpsSiteName, CancellationToken cancellationToken)
        {
            // Issue the user authentication request
            XasTokenRequest request = new XasTokenRequest
            {
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT",
                Properties = new PropertyBag
                {
                    AuthMethod = "RPS",
                    RpsTicket = rpsTicket,
                    SiteName = rpsSiteName,
                },
            };
            SimpleServiceResult<XasTokenResponse, XasTokenResponseError> tokenResponse =
                await PostAsync<XasTokenResponse, XasTokenResponseError, XasTokenRequest>(xasuHostname,
                    "/user/authenticate", request,
                    cancellationToken,
                    extraHeaders: new Dictionary<string, string> {{"x-xbl-contract-version", "0"}});

            return HandleXasResult(tokenResponse);
        }

        // Get an xbox music authorization token for a specific user
        private async Task<XasTokenResponse> GetXTokenAsync(string userToken, CancellationToken cancellationToken)
        {
            XasTokenRequest request = new XasTokenRequest()
            {
                RelyingParty = "http://music.xboxlive.com",
                TokenType = "JWT",
                Properties = new PropertyBag
                {
                    UserTokens = new List<string> {userToken},
                    SandboxId = "RETAIL",
                },
            };

            SimpleServiceResult<XasTokenResponse, XasTokenResponseError> tokenResponse =
                await PostAsync<XasTokenResponse, XasTokenResponseError, XasTokenRequest>(xstsHostname, "/xsts/authorize",
                    request,
                    cancellationToken,
                    extraHeaders: new Dictionary<string, string> { { "x-xbl-contract-version", "1" } });

            return HandleXasResult(tokenResponse);
        }

        private static XasTokenResponse HandleXasResult(SimpleServiceResult<XasTokenResponse, XasTokenResponseError> tokenResponse)
        {
            switch (tokenResponse.HttpStatusCode)
            {
                case HttpStatusCode.OK:
                    return tokenResponse.Result;

                default:
                    throw new XboxUserAuthenticationException(tokenResponse.HttpStatusCode, tokenResponse.ErrorResult);
            }
        }
    }
}
