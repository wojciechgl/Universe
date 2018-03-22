// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using RepoTasks.ProjectModel;

namespace RepoTools.BuildGraph
{
    [DebuggerDisplay("{Name}")]
    public class Project
    {
        public Project(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public string Version { get; set; }

        public string Path { get; set; }

        public Repository Repository { get; set; }

        public ISet<PackageReferenceInfo> PackageReferences { get; set; } = new HashSet<PackageReferenceInfo>(new PackageReferenceInfoComparer());
    }
}
