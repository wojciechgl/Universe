// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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

    internal class ReleaseUpdateComparer : IEqualityComparer<ReleaseUpdate>
    {
        public bool Equals(ReleaseUpdate x, ReleaseUpdate y)
        {
            return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Version, y.Version, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(ReleaseUpdate obj)
        {
            return (obj.Name + obj.Version).GetHashCode();
        }
    }

    internal class ReleasePackage
    {
        public static ReleasePackage Parse(ITaskItem item)
            => new ReleasePackage
            {
                Name = item.GetMetadata("Identity"),
                Version = item.GetMetadata("Version"),
                Dependencies = new HashSet<string>(item.GetMetadata("Dependency").Split(';')),
            };

        public string Name { get; private set; }
        public string Version { get; private set; }
        public HashSet<string> Dependencies { get; private set; }
    }
}
