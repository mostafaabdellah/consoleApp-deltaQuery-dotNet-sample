// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using Microsoft.Graph;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace DeltaQuery
{
    public class DeltaLinks
    {
        public IDriveItemDeltaCollectionPage DeltaCollection { get; set; }
        public string DeltaLink { get; set; }
        public List<DriveItem> DriveItems { get; set; }
        public long LastSyncDate { get; set; }
        public bool NoChanges { get; set; } = false;
    }

    public class DeltaResponse
    {
        [JsonProperty("@odata.context")]
        public string Context { get; set; }
        [JsonProperty("@odata.nextLink")]
        public string NextLink { get; set; }
        [JsonProperty("@odata.deltaLink")]
        public string DeltaLink { get; set; }
        [JsonProperty("value")]
        public List<DriveItem> DriveItems { get; set; }
    }
}
