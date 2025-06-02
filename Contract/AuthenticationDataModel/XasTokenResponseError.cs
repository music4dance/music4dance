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

using System.Runtime.Serialization;

namespace Microsoft.Xbox.Music.Platform.Contract.AuthenticationDataModel
{
    [DataContract]
    public class XasTokenResponseError
    {
        [DataMember]
        public XasErrorCode XErr { get; set; }

        [DataMember]
        public string Message { get; set; }
    }

    public enum XasErrorCode : uint
    {
        MicrosoftBan = 0x8015DC03,
        ParentalControls = 0x8015DC05,
        GamertagChangeRequired = 0x8015DC13,
        AccountCreationRequired = 0x8015DC09,
        AccountTermsOfUseNotAccepted = 0x8015DC0A,
        AccountAgeVerificationRequired = 0x8015DC0C,
    }
}
