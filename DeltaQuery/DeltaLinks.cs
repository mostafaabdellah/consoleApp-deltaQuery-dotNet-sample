// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using Microsoft.Graph;

namespace DeltaQuery
{
    public class DeltaLinks
    {
        public IDriveItemDeltaCollectionPage DeltaCollection { get; set; }
        public string DeltaLink { get; set; }
        public long LastSyncDate { get; set; }
        public bool NoChanges { get; set; } = false;
    }
}
