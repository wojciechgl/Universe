// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using UpdateExternalDependencies.Commands;

namespace UpdateExternalDependencies
{
    public interface IUpdateProvider
    {
        DependenciesProps GetExternalDependencies(DependenciesProps props, string source);

        bool CanHandleSource(string source);
    }
}
