// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;
using RepoTasks.Utilities;

namespace RepoTasks
{
    public class AddArchiveReferences : Task
    {
        [Required]
        public string ReferencePackagePath { get; set; }

        [Required]
        public ITaskItem[] References { get; set; }

        [Required]
        public ITaskItem[] Tools { get; set; }

        public override bool Execute()
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(ReferencePackagePath);

            // Project
            var projectElement = xmlDoc.FirstChild;

            // Items
            var itemGroupElement = xmlDoc.CreateElement("ItemGroup");
            Log.LogMessage(MessageImportance.High, $"Archive will include the following packages");

            foreach (var package in References)
            {
                var packageName = package.ItemSpec;
                var packageVersion = package.GetMetadata("Version");

                Log.LogMessage($" - Package: {packageName} Version: {packageVersion}");

                var packageReferenceElement = xmlDoc.CreateElement("PackageReference");
                packageReferenceElement.SetAttribute("Include", packageName);
                packageReferenceElement.SetAttribute("Version", packageVersion);

                itemGroupElement.AppendChild(packageReferenceElement);
            }

            foreach (var package in Tools)
            {
                var packageName = package.ItemSpec;
                var packageVersion = package.GetMetadata("Version");

                Log.LogMessage($" - Tool: {packageName} Version: {packageVersion}");

                var packageReferenceElement = xmlDoc.CreateElement("DotNetCliToolReference");
                packageReferenceElement.SetAttribute("Include", packageName);
                packageReferenceElement.SetAttribute("Version", packageVersion);

                itemGroupElement.AppendChild(packageReferenceElement);
            }

            projectElement.AppendChild(itemGroupElement);

            // Save updated file
            xmlDoc.AppendChild(projectElement);
            xmlDoc.Save(ReferencePackagePath);

            return true;
        }
    }
}
