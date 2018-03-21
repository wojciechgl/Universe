// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;
using RepoTasks.ProjectModel;
using RepoTasks.Utilities;

namespace RepoTasks
{
    public class GenerateReleaseManifest : Task
    {
        [Required]
        public string ReleaseManifestPath { get; set; }

        [Required]
        public string Properties { get; set; }

        [Required]
        public ITaskItem[] Solutions { get; set; }

        public override bool Execute()
        {
            var root = new XElement("ItemGroup");
            var doc = new XDocument(new XElement("Project", root));
            var factory = new SolutionInfoFactory(Log, BuildEngine5);
            var props = MSBuildListSplitter.GetNamedProperties(Properties);
            var solutions = factory.Create(Solutions, props, CancellationToken.None);

            foreach (var solution in solutions)
            foreach (var project in solution.Projects)
            {
                if (string.IsNullOrEmpty(project.PackageId))
                {
                    Log.LogError($"{solution.FullPath}: {project.FullPath}");
                }
                else
                {
                    var packageElement = new XElement("ReleasePackages");
                    packageElement.Add(new XAttribute("Include", project.PackageId));
                    // Take the Major.Minor.Patch as the released package
                    packageElement.Add(new XAttribute("Version", NuGetVersion.Parse(project.PackageVersion).Version.ToString(3)));

                    var dependencyBuilder = new StringBuilder();
                    foreach (var tfm in project.Frameworks)
                    foreach (var dependency in tfm.Dependencies)
                    {
                        dependencyBuilder.Append($"{dependency.Value.Id};");
                    }
                    packageElement.Add(new XElement("Dependency", dependencyBuilder.ToString()));

                    root.Add(packageElement);
                }
            }

            using (var writer = XmlWriter.Create(ReleaseManifestPath, new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
            }))
            {
                Log.LogMessage(MessageImportance.Normal, $"Generate {ReleaseManifestPath}");
                doc.Save(writer);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
