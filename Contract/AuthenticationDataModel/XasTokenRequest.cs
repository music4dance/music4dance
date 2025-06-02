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

using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.Xbox.Music.Platform.Contract.AuthenticationDataModel
{
    [DataContract]
    public class PropertyBag
    {
        [DataMember(EmitDefaultValue = false)]
        public string AuthMethod { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string SiteName { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string RpsTicket { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string RpsHeader { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public List<string> UserTokens { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string SandboxId { get; set; }
    }

    [DataContract]
    public class XasTokenRequest
    {
        [DataMember]
        public string RelyingParty { get; set; }

        [DataMember]
        public PropertyBag Properties { get; set; }

        [DataMember]
        public string TokenType { get; set; }
    }
}
