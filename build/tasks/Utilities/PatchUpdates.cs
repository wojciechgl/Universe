// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using RepoTasks.ProjectModel;

namespace RepoTasks.Utilities
{
    internal class ReleaseUpdate
    {
        public static ReleaseUpdate Parse(ITaskItem item)
            => new ReleaseUpdate
            {
                Name = item.GetMetadata("Identity"),
                Version = item.GetMetadata("Version"),
            };

        public string Name { get; set; }
        public string Version { get; set; }

        public override int GetHashCode() => (Name + Version).GetHashCode();
    }

    internal class PatchPackage
    {
        public static PatchPackage Parse(ITaskItem item)
            => new PatchPackage
            {
                Name = item.GetMetadata("Identity"),
                Version = item.GetMetadata("Version"),
                NewVersion = item.GetMetadata("NewVersion"),
            };

        public string Name { get; private set; }
        public string Version { get; private set; }
        public string NewVersion { get; set; }
    }
}
