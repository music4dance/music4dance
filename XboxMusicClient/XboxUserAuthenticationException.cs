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
using System.Net;
using Microsoft.Xbox.Music.Platform.Contract.AuthenticationDataModel;

namespace Microsoft.Xbox.Music.Platform.Client
{
    public class XboxUserAuthenticationException : Exception
    {
        public XboxUserAuthenticationException(HttpStatusCode statusCode, XasTokenResponseError error)
        {
            HttpStatusCode = statusCode;
            Error = error;
        }

        public HttpStatusCode HttpStatusCode { get; set; }

        public XasTokenResponseError Error { get; set; }

        /// <summary>
        /// If this is set to true, the user should sign in to http://music.xbox.com to fix issues with their account.
        /// </summary>
        public bool UserActionRequired
        {
            get
            {
                if (Error == null)
                    return false;

                switch (Error.XErr)
                {
                    case XasErrorCode.GamertagChangeRequired:
                    case XasErrorCode.AccountCreationRequired:
                    case XasErrorCode.AccountTermsOfUseNotAccepted:
                    case XasErrorCode.AccountAgeVerificationRequired:
                        return true;

                    default:
                        return false;
                }
            }
        }

        public override string Message
        {
            get
            {
                return String.Format("Message={0} Code={1} HttpStatusCode={2}",
                    Error != null ? Error.Message : null,
                    Error != null ? Error.XErr : 0,
                    HttpStatusCode);
            }
        }
    }
}