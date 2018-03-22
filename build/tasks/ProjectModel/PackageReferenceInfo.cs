// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace RepoTasks.ProjectModel
{
    public class PackageReferenceInfo
    {
        public PackageReferenceInfo(string id, string version, bool isImplicitlyDefined, IReadOnlyList<string> noWarn)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(nameof(id));
            }

            Id = id;
            Version = version;
            IsImplicitlyDefined = isImplicitlyDefined;
            NoWarn = noWarn;
        }

        public string Id { get; }
        public string Version { get; }
        public bool IsImplicitlyDefined { get; }
        public IReadOnlyList<string> NoWarn { get; }
    }

    public class PackageReferenceInfoComparer : IEqualityComparer<PackageReferenceInfo>
    {
        public bool Equals(PackageReferenceInfo x, PackageReferenceInfo y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id);
        }

        public int GetHashCode(PackageReferenceInfo obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id);
        }
    }
}
