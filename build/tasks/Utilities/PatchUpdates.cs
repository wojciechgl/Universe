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
    internal interface IPackage
    {
        string Name { get; set; }
        string Version { get; set; }
    }

    internal class Package : IPackage
    {
        public static Package Parse(ITaskItem item)
            => new Package
            {
                Name = item.GetMetadata("Identity"),
                Version = item.GetMetadata("Version"),
            };

        public string Name { get; set; }
        public string Version { get; set; }

        public override int GetHashCode() => (Name + Version).GetHashCode();
    }

    internal class IPackageComparer : IEqualityComparer<IPackage>
    {
        public bool Equals(IPackage x, IPackage y)
        {
            return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Version, y.Version, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(IPackage obj)
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
                Dependencies = new HashSet<ReleasePackageDependency>(
                    item.GetMetadata("Dependency").Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(d =>
                        new ReleasePackageDependency(d))),
            };

        public string Name { get; private set; }
        public string Version { get; private set; }
        public HashSet<ReleasePackageDependency> Dependencies { get; private set; }
    }

    internal class ReleasePackageDependency : IPackage
    {
        internal ReleasePackageDependency(string dependencyString)
        {
            var dependencyComponents = dependencyString.Split(':');

            if (dependencyComponents.Length != 2)
            {
                throw new ArgumentException($"Expected the dependency {dependencyString} to be parsed in the format <DependencyName>:<DependencyVersion>");
            }

            Name = dependencyComponents[0];
            Version = dependencyComponents[1];
        }
        public string Name { get; set; }
        public string Version { get; set; }
    }
}
