// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Microsoft.Build.Framework;
using RepoTasks.ProjectModel;

namespace RepoTasks.Utilities
{
    internal class RepoUpdate
    {
        public static RepoUpdate Parse(ITaskItem item)
            => new RepoUpdate { RepoName = item.GetMetadata("Identity") };

        public string RepoName { get; private set; }
    }

    internal class PackageUpdate
    {
        public static PackageUpdate Parse(ITaskItem item)
            => new PackageUpdate
            {
                PackageName = item.GetMetadata("Identity"),
                PackageVersion = item.GetMetadata("Version"),
            };

        public string PackageName { get; set; }
        public string PackageVersion { get; set; }

        public override int GetHashCode() => (PackageName + PackageVersion).GetHashCode();
    }

    internal class PatchPackage
    {
        public static PatchPackage Parse(ITaskItem item)
            => new PatchPackage
            {
                PackageName = item.GetMetadata("Identity"),
                CurrentVersion = item.GetMetadata("CurrentVersion"),
                PatchedVersion = item.GetMetadata("PatchedVersion"),
            };

        public string PackageName { get; private set; }
        public string CurrentVersion { get; private set; }
        public string PatchedVersion { get; set; }
    }
}
