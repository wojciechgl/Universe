// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
using RepoTools.BuildGraph;

namespace RepoTasks
{
    public class GenerateReleaseManifest : Task
    {
        [Required]
        public ITaskItem[] BuildArtifacts { get; set; }

        [Required]
        public ITaskItem[] PackageArtifacts { get; set; }

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
            var shippingPackages = PackageArtifacts.Where(p => p.GetMetadata("Category") != "noship").Select(p => p.ItemSpec);
            var buildArtifacts = BuildArtifacts.Select(ArtifactInfo.Parse)
                .OfType<ArtifactInfo.Package>()
                .Where(p => !p.IsSymbolsArtifact);

            var repositories = solutions.Select(s =>
                {
                    var repoName = Path.GetFileName(Path.GetDirectoryName(s.FullPath));
                    var repo = new Repository(repoName)
                    {
                        RootDir = Path.GetDirectoryName(s.FullPath)
                    };

                    var packages = buildArtifacts.Where(a => a.RepoName.Equals(repoName, StringComparison.OrdinalIgnoreCase)).ToList();

                    foreach (var proj in s.Projects)
                    {
                        var package = packages.Where(p => p.PackageInfo.Id == proj.PackageId);
                        var projectGroup = package.Any()
                            ? repo.Projects
                            : repo.SupportProjects;

                        projectGroup.Add(new Project(proj.PackageId)
                            {
                                Repository = repo,
                                PackageReferences = new HashSet<PackageReferenceInfo>(proj
                                    .Frameworks
                                    .SelectMany(f => f.Dependencies.Values)
                                    .Concat(proj.Tools.Select(t => new PackageReferenceInfo(t.Id, t.Version, false, null))), new PackageReferenceInfoComparer()),
                                Version = package.SingleOrDefault()?.PackageInfo?.Version?.Version?.ToString(3)
                            });
                    }

                    return repo;
                }).ToList();

            foreach (var repository in repositories)
            foreach (var project in repository.Projects)
            {
                if (buildArtifacts.Any(a => a.PackageInfo.Id.Equals(project.Name, StringComparison.OrdinalIgnoreCase))
                    && shippingPackages.Any(a => a.Equals(project.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var packageElement = new XElement("ReleasePackages");
                    packageElement.Add(new XAttribute("Include", project.Name));
                    // Take the Major.Minor.Patch as the released package
                    packageElement.Add(new XAttribute("Version", project.Version));

                    var dependencyBuilder = new StringBuilder();
                    foreach (var dependency in project.PackageReferences)
                    {
                        if (!dependency.IsImplicitlyDefined)
                        {
                            dependencyBuilder.Append($"{dependency.Id}:{dependency.Version};");
                        }
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
