// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public static class NugetVersionExtensions
    {
        public static NuGetVersion IncrementPatch(this NuGetVersion version)
        {
            return new NuGetVersion(version.Major, version.Minor, version.Patch + 1, version.Release);
        }
    }

    public class UpdatePatch : Task
    {
        [Required]
        public ITaskItem[] Artifacts { get; set; }

        [Required]
        public ITaskItem[] PatchPackages { get; set; }

        [Required]
        public ITaskItem[] RepoUpdates { get; set; }

        [Required]
        public ITaskItem[] PackageUpdates { get; set; }

        [Required]
        public string PatchManifestPath { get; set; }

        [Required]
        public string PatchConfigPath { get; set; }

        [Required]
        public string PatchUpdatesPath { get; set; }

        [Required]
        public string Properties { get; set; }

        [Required]
        public ITaskItem[] Solutions { get; set; }

        public override bool Execute()
        {
            var repoUpdates = RepoUpdates.Select(RepoUpdate.Parse);
            var packageUpdates = PackageUpdates.Select(PackageUpdate.Parse);
            var patchPackages = PatchPackages.Select(PatchPackage.Parse);

            var factory = new SolutionInfoFactory(Log, BuildEngine5);
            var props = MSBuildListSplitter.GetNamedProperties(Properties);
            var solutions = factory.Create(Solutions, props, CancellationToken.None);
            var packageArtifacts = Artifacts.Select(ArtifactInfo.Parse)
                .OfType<ArtifactInfo.Package>()
                .Where(p => !p.IsSymbolsArtifact);

            // foreach (var repoUpdate in repoUpdates)
            // {
            //     Log.LogMessage(MessageImportance.High, repoUpdate.RepoName);
            // }
            // foreach (var packageUpdate in packageUpdates)
            // {
            //     Log.LogMessage(MessageImportance.High, $"{packageUpdate.PackageName}:{packageUpdate.PackageVersion}");
            // }
            // foreach (var patchPackage in patchPackages)
            // {
            //     Log.LogMessage(MessageImportance.High, $"{patchPackage.PackageName}:{patchPackage.CurrentVersion}=>{patchPackage.CurrentVersion}");
            // }
            // foreach (var packageArtifact in packageArtifacts)
            // {
            //     Log.LogMessage(MessageImportance.High, $"Package Artifact: {packageArtifact.PackageInfo.Id}");
            // }

            // Generate build graph
            var repositories = solutions.Select(s =>
                {
                    var repoName = Path.GetFileName(Path.GetDirectoryName(s.FullPath));
                    var repo = new Repository(repoName)
                    {
                        RootDir = Path.GetDirectoryName(s.FullPath)
                    };

                    var packages = packageArtifacts.Where(a => a.RepoName.Equals(repoName, StringComparison.OrdinalIgnoreCase)).ToList();

                    foreach (var proj in s.Projects)
                    {
                        var projectGroup = packages.Any(p => p.PackageInfo.Id == proj.PackageId)
                            ? repo.Projects
                            : repo.SupportProjects;

                        projectGroup.Add(new Project(proj.PackageId)
                            {
                                Repository = repo,
                                PackageReferences = new HashSet<string>(proj
                                    .Frameworks
                                    .SelectMany(f => f.Dependencies.Keys)
                                    .Concat(proj.Tools.Select(t => t.Id)), StringComparer.OrdinalIgnoreCase),
                            });
                    }

                    return repo;
                }).ToList();

            var graph = GraphBuilder.Generate(repositories, string.Empty, Log);

            // foreach (var node in graph)
            // {
            //     Log.LogMessage(MessageImportance.High, $"Repository: {node.Repository.Name}");
            //     foreach (var dependentRepo in node.Incoming)
            //     {
            //         Log.LogMessage(MessageImportance.High, $"Incoming: {dependentRepo.Repository.Name}");
            //     }
            //     foreach (var project in node.Repository.Projects)
            //     {
            //         Log.LogMessage(MessageImportance.High, $"  Project: {project.Name}");
            //         foreach (var dependency in project.PackageReferences)
            //         {
            //             Log.LogMessage(MessageImportance.High, $"    Dependency: {dependency}");
            //         }
            //     }
            //     foreach (var project in node.Repository.SupportProjects)
            //     {
            //         Log.LogMessage(MessageImportance.High, $"  SupportProject: {project.Name}");
            //         foreach (var dependency in project.PackageReferences)
            //         {
            //             Log.LogMessage(MessageImportance.High, $"    Dependency: {dependency}");
            //         }
            //     }
            // }

            // Update initial packages and repos
            var packagesToUpdate = new HashSet<PackageUpdate>();

            foreach (var packageUpdate in packageUpdates)
            {
                packagesToUpdate.Add(packageUpdate);
            }
            foreach (var repoUpdate in repoUpdates)
            {
                var repoProjects = graph
                    .Single(n => string.Equals(n.Repository.Name, repoUpdate.RepoName, StringComparison.OrdinalIgnoreCase))
                    .Repository
                    .Projects;
                var unupdatedProjects = patchPackages
                    .Where(patchPackage =>
                        repoProjects.Any(repoProject =>
                            string.Equals(repoProject.Name, patchPackage.PackageName, StringComparison.OrdinalIgnoreCase)
                            && patchPackage.CurrentVersion == patchPackage.PatchedVersion));
                // foreach (var project in unupdatedProjects)
                // {
                //     Log.LogMessage(MessageImportance.High, $"  UnUpdatedPackage: {project.PackageName}:{project.CurrentVersion}");
                // }
                var projectsToUpdate = unupdatedProjects
                        .Select(projectToUpdate => new PackageUpdate
                        {
                            PackageName = projectToUpdate.PackageName,
                            PackageVersion = NuGetVersion.Parse(projectToUpdate.PatchedVersion).IncrementPatch().ToNormalizedString()
                        });
                // foreach (var project in projectsToUpdate)
                // {
                //     Log.LogMessage(MessageImportance.High, $"  PackagesToUpdate: {project.PackageName}:{project.PackageVersion}");
                // }
                packagesToUpdate.UnionWith(projectsToUpdate);
            }

            // Cascade updates
            foreach (var repo in graph.OrderBy(n => TopologicalSort.GetOrder(n)).Select(n => n.Repository))
            {
                // Log.LogMessage(MessageImportance.High, $"Cascading repo {repo.Name}");

                if (repo.Projects.Any(project =>
                    project.PackageReferences.Any(reference =>
                        packagesToUpdate.Any(package =>
                            string.Equals(reference, package.PackageName, StringComparison.OrdinalIgnoreCase)))))
                {
                    // Log.LogMessage(MessageImportance.High, $"{repo.Name} requires cascaded update");

                    var repoProjects = graph
                        .Single(n => string.Equals(n.Repository.Name, repo.Name, StringComparison.OrdinalIgnoreCase))
                        .Repository
                        .Projects;
                    var unupdatedProjects = patchPackages
                        .Where(patchPackage =>
                            repoProjects.Any(repoProject =>
                                string.Equals(repoProject.Name, patchPackage.PackageName, StringComparison.OrdinalIgnoreCase)
                                && patchPackage.CurrentVersion == patchPackage.PatchedVersion));
                    // foreach (var project in unupdatedProjects)
                    // {
                    //     Log.LogMessage(MessageImportance.High, $"  UnUpdatedPackage: {project.PackageName}:{project.CurrentVersion}");
                    // }
                    var projectsToUpdate = unupdatedProjects
                            .Select(projectToUpdate => new PackageUpdate
                            {
                                PackageName = projectToUpdate.PackageName,
                                PackageVersion = NuGetVersion.Parse(projectToUpdate.PatchedVersion).IncrementPatch().ToNormalizedString()
                            });
                    // foreach (var project in projectsToUpdate)
                    // {
                    //     Log.LogMessage(MessageImportance.High, $"  PackagesToUpdate: {project.PackageName}:{project.PackageVersion}");
                    // }
                    packagesToUpdate.UnionWith(projectsToUpdate);
                }
            }

            // Generate patch update file
            var root = new XElement("ItemGroup");
            var doc = new XDocument(new XElement("Project", root));
            foreach (var packageToUpdate in packagesToUpdate)
            {
                Log.LogMessage(MessageImportance.High, $"Updates required: {packageToUpdate.PackageName}={packageToUpdate.PackageVersion}");

                var packageElement = new XElement("PatchUpdate");
                packageElement.Add(new XAttribute("Include", packageToUpdate.PackageName));
                packageElement.Add(new XAttribute("CurrentVersion", packageToUpdate.PackageVersion));
                root.Add(packageElement);
            }

            using (var writer = XmlWriter.Create(PatchUpdatesPath, new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
            }))
            {
                Log.LogMessage(MessageImportance.Normal, $"Generate {PatchUpdatesPath}");
                doc.Save(writer);
            }

            // TODO: Update patch manifest
            return true;
        }
    }
}
