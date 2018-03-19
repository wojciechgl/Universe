// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using RepoTasks.ProjectModel;
using RepoTasks.Utilities;

namespace RepoTasks
{
    public class GeneratePatchManifest : Task
    {
        [Required]
        public string PatchManifestPath { get; set; }

        [Required]
        public string Properties { get; set; }

        [Required]
        public ITaskItem[] Solutions { get; set; }

        public override bool Execute()
        {
            var root = new XElement("PatchMaifest");
            var doc = new XDocument(root);
            var factory = new SolutionInfoFactory(Log, BuildEngine5);
            var props = MSBuildListSplitter.GetNamedProperties(Properties);
            var solutions = factory.Create(Solutions, props, CancellationToken.None);

            foreach (var solution in solutions)
            foreach (var project in solution.Projects)
            {
                var packageElement = new XElement("Package");
                packageElement.Add(new XAttribute("Name", "Microsoft.AspNetCore.All"));
                packageElement.Add(new XAttribute("CurrentVersion", "2.0.0"));
                packageElement.Add(new XAttribute("PatchedVersion", "2.0.0"));
                root.Add(packageElement);
            }

            using (var writer = XmlWriter.Create(PatchManifestPath, new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
            }))
            {
                Log.LogMessage(MessageImportance.Normal, $"Generate {PatchManifestPath}");
                doc.Save(writer);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
