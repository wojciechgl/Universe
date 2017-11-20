// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.CommandLineUtils;
using System.Reflection;
using UpdateExternalDependencies.Commands;

namespace UpdateExternalDependencies
{
    internal class RootCommand : CommandBase
    {
        public override void Configure(CommandLineApplication application)
        {

            application.FullName = "UpdateExternalDependencies";

            application.Command("update", new UpdateCommand().Configure, throwOnUnexpectedArg: true);

            application.VersionOption("--version", GetVersion);

            base.Configure(application);
        }

        private static string GetVersion()
                => typeof(RootCommand).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
    }
}
