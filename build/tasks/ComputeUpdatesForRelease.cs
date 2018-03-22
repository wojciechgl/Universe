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

    public class ComputeUpdatesForRelease : Task
    {

        [Required]
        public ITaskItem[] BuildArtifacts { get; set; }

        [Required]
        public ITaskItem[] PackageArtifacts { get; set; }

        [Required]
        public ITaskItem[] ReleasePackages { get; set; }

        [Required]
        public ITaskItem[] SourceUpdates { get; set; }

        [Required]
        public ITaskItem[] ExternalDependencies { get; set; }

        [Required]
        public string ReleaseBuildConfigPath { get; set; }

        [Required]
        public string ReleaseUpdatesPath { get; set; }

        [Required]
        public string Properties { get; set; }

        [Required]
        public ITaskItem[] Solutions { get; set; }

        public override bool Execute()
        {
            var sourceUpdates = SourceUpdates.Select(Package.Parse);
            var releasePackages = ReleasePackages.Select(ReleasePackage.Parse);
            var externalDependencies = ExternalDependencies.Where(item => item.GetMetadata("Private") != "true").Select(Package.Parse);

            var factory = new SolutionInfoFactory(Log, BuildEngine5);
            var props = MSBuildListSplitter.GetNamedProperties(Properties);
            var solutions = factory.Create(Solutions, props, CancellationToken.None);
            var shippingPackages = PackageArtifacts.Where(p => p.GetMetadata("Category") != "noship").Select(p => p.ItemSpec);
            var buildArtifact = BuildArtifacts.Select(ArtifactInfo.Parse)
                .OfType<ArtifactInfo.Package>()
                .Where(p => !p.IsSymbolsArtifact);

            // foreach (var repoUpdate in sourceUpdates)
            // {
            //     Log.LogMessage(MessageImportance.High, repoUpdate.Name);
            // }
            // foreach (var packageUpdate in dependencyUpdates)
            // {
            //     Log.LogMessage(MessageImportance.High, $"{packageUpdate.Name}:{packageUpdate.Version}");
            // }
            // foreach (var patchPackage in releasePackages)
            // {
            //     Log.LogMessage(MessageImportance.High, $"ReleasePackage: {patchPackage.Name}");
            //     Log.LogMessage(MessageImportance.High, $"  Dependency: {patchPackage.Dependencies.Aggregate((a, b) => a + ";" + b)}");
            // }
            // foreach (var packageArtifact in buildArtifact)
            // {
            //     Log.LogMessage(MessageImportance.High, $"Package Artifact: {packageArtifact.PackageInfo.Id} ");
            // }

            // Generate build graph
            var repositories = solutions.Select(s =>
                {
                    var repoName = Path.GetFileName(Path.GetDirectoryName(s.FullPath));
                    var repo = new Repository(repoName)
                    {
                        RootDir = Path.GetDirectoryName(s.FullPath)
                    };

                    var packages = buildArtifact.Where(a => a.RepoName.Equals(repoName, StringComparison.OrdinalIgnoreCase)).ToList();

                    foreach (var proj in s.Projects)
                    {
                        var projectGroup = packages.Any(p => p.PackageInfo.Id == proj.PackageId)
                            ? repo.Projects
                            : repo.SupportProjects;

                        projectGroup.Add(new Project(proj.PackageId)
                            {
                                Repository = repo,
                                PackageReferences = new HashSet<PackageReferenceInfo>(proj
                                    .Frameworks
                                    .SelectMany(f => f.Dependencies.Values)
                                    .Concat(proj.Tools.Select(t => new PackageReferenceInfo(t.Id, t.Version, false, null))), new PackageReferenceInfoComparer())
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

            var packageComparer = new IPackageComparer();
            var packagesToUpdate = new HashSet<Package>(packageComparer);
            var releasedDependencies = new HashSet<ReleasePackageDependency>(packageComparer);

            // Gather release set of external dependencies
            foreach (var releasePackage in releasePackages)
            foreach (var dependency in releasePackage.Dependencies)
            {
                var duplicatedDependencies = releasedDependencies.Where(d =>
                    string.Equals(dependency.Name, d.Name, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(dependency.Version, d.Version, StringComparison.OrdinalIgnoreCase));
                if (duplicatedDependencies.Any())
                {
                    Log.LogError($"Found dependency {dependency.Name} with multiple versions: {duplicatedDependencies.Select(d => d.Version).Aggregate((a, b) => a + ", " + b)}");
                }

                releasedDependencies.Add(dependency);
            }

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            foreach (var externalDependency in externalDependencies)
            {
                if (releasedDependencies.Any(releasedDependency =>
                    string.Equals(releasedDependency.Name, externalDependency.Name, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(releasedDependency.Version, externalDependency.Version, StringComparison.OrdinalIgnoreCase)))
                {
                    packagesToUpdate.Add(externalDependency);
                }
            }

            foreach (var sourceUpdate in sourceUpdates)
            {
                // var repoProjects = graph
                //     .Single(n => string.Equals(n.Repository.Name, repoUpdate.RepoName, StringComparison.OrdinalIgnoreCase))
                //     .Repository
                //     .Projects;
                // var unupdatedProjects = releasePackages
                //     .Where(patchPackage =>
                //         repoProjects.Any(repoProject =>
                //             string.Equals(repoProject.Name, patchPackage.PackageName, StringComparison.OrdinalIgnoreCase)
                //             && patchPackage.CurrentVersion == patchPackage.PatchedVersion));
                // // foreach (var project in unupdatedProjects)
                // // {
                // //     Log.LogMessage(MessageImportance.High, $"  UnUpdatedPackage: {project.PackageName}:{project.CurrentVersion}");
                // // }
                // var projectsToUpdate = unupdatedProjects
                //         .Select(projectToUpdate => new ReleaseUpdate
                //         {
                //             PackageName = projectToUpdate.PackageName,
                //             PackageVersion = NuGetVersion.Parse(projectToUpdate.PatchedVersion).IncrementPatch().ToNormalizedString()
                //         });
                // // foreach (var project in projectsToUpdate)
                // // {
                // //     Log.LogMessage(MessageImportance.High, $"  PackagesToUpdate: {project.PackageName}:{project.PackageVersion}");
                // // }
                // packagesToUpdate.UnionWith(projectsToUpdate);

                if (!graph.Any(node =>
                    node.Repository.Projects.Any(project =>
                        string.Equals(project.Name, sourceUpdate.Name, StringComparison.OrdinalIgnoreCase))))
                {
                    Log.LogError($"The source project to be updated, {sourceUpdate.Name}, does not exist in this patch.");
                }

                var currentSourceVersion = releasePackages.Single(p => string.Equals(p.Name, sourceUpdate.Name, StringComparison.OrdinalIgnoreCase)).Version;
                sourceUpdate.Version = NuGetVersion.Parse(currentSourceVersion).IncrementPatch().ToNormalizedString();

                packagesToUpdate.Add(sourceUpdate);
            }

            // foreach (var packageToUpdate in packagesToUpdate)
            // {
            //     Log.LogMessage(MessageImportance.High, $"Base changes required: {packageToUpdate.Name}={packageToUpdate.Version}");
            // }

            // Cascade updates
            foreach (var repo in graph.OrderBy(n => TopologicalSort.GetOrder(n)).Select(n => n.Repository))
            {
                // Log.LogMessage(MessageImportance.High, $"Cascading repo {repo.Name}");
                // var projectUpdated = repo.Projects.Where(project =>
                //     packagesToUpdate.Any(package =>
                //         string.Equals(project.Name, package.Name, StringComparison.OrdinalIgnoreCase)));
                // var dependencyUpdated = repo.Projects.Where(project =>
                //     packagesToUpdate.Any(package =>
                //         project.PackageReferences.Any(reference => {
                //             if (reference.IsImplicitlyDefined)
                //             {
                //                 return false;
                //             }
                //             Log.LogMessage(MessageImportance.High, $"{repo.Name}-{package.Name}-{reference.Id} updated to {package.Version}");
                //             return string.Equals(reference.Id, package.Name, StringComparison.OrdinalIgnoreCase);
                //         })));

                // foreach (var package in projectUpdated)
                // {
                //     Log.LogMessage(MessageImportance.High, $"{repo.Name}-{package.Name} project updated");
                // }
                // foreach (var package in dependencyUpdated)
                // {
                //     Log.LogMessage(MessageImportance.High, $"{repo.Name}-{package.Name} dependency updated");
                // }

                if (repo.Projects.Any(project =>
                    packagesToUpdate.Any(package =>
                        string.Equals(project.Name, package.Name, StringComparison.OrdinalIgnoreCase))
                        || project.PackageReferences.Any(reference =>
                            !reference.IsImplicitlyDefined
                            && packagesToUpdate.Any(package =>
                                string.Equals(reference.Id, package.Name, StringComparison.OrdinalIgnoreCase)))))
                {
                    // Log.LogMessage(MessageImportance.High, $"{repo.Name} requires cascaded update");

                    var repoProjects = graph
                        .Single(n => string.Equals(n.Repository.Name, repo.Name, StringComparison.OrdinalIgnoreCase))
                        .Repository
                        .Projects;
                    // foreach (var project in repoProjects)
                    // {
                    //     Log.LogMessage(MessageImportance.High, $"  RepoProjects: {project.Name}");
                    // }
                    var repoProjectArtifact = buildArtifact.Where(packageArtifact =>
                        repoProjects.Any(repoProject =>
                            string.Equals(repoProject.Name, packageArtifact.PackageInfo.Id, StringComparison.OrdinalIgnoreCase)));
                    // foreach (var projectArtifact in repoProjectArtifact)
                    // {
                    //     Log.LogMessage(MessageImportance.High, $"  RepoProjectArtifact: {projectArtifact.PackageInfo.Id}:{projectArtifact.PackageInfo.Version.ToNormalizedString()}");
                    // }
                    var releasePackage = releasePackages.Where(package =>
                        repoProjectArtifact.Any(packageArtifact =>
                            string.Equals(package.Name, packageArtifact.PackageInfo.Id, StringComparison.OrdinalIgnoreCase)
                            && package.Version == packageArtifact.PackageInfo.Version.Version.ToString(3)));
                    // foreach (var project in unupdatedProjects)
                    // {
                    //     Log.LogMessage(MessageImportance.High, $"  UnUpdatedPackage: {project.PackageInfo.Id}:{project.PackageInfo.Version.ToNormalizedString()}");
                    // }
                    var projectsToUpdate = releasePackage
                            .Select(packageToUpdate => new Package
                            {
                                Name = packageToUpdate.Name,
                                Version = NuGetVersion.Parse(packageToUpdate.Version).IncrementPatch().ToNormalizedString()
                            });
                    // foreach (var project in projectsToUpdate)
                    // {
                    //     Log.LogMessage(MessageImportance.High, $"  PackagesToUpdate: {project.PackageName}:{project.PackageVersion}");
                    // }
                    packagesToUpdate.UnionWith(projectsToUpdate);
                }
            }

            // Generate release update file
            var root = new XElement("ItemGroup");
            var doc = new XDocument(new XElement("Project", root));
            foreach (var packageToUpdate in packagesToUpdate)
            {
                Log.LogMessage(MessageImportance.High, $"Updates required: {packageToUpdate.Name}={packageToUpdate.Version}");

                var packageElement = new XElement("ReleaseUpdate");
                packageElement.Add(new XAttribute("Include", packageToUpdate.Name));
                packageElement.Add(new XAttribute("Version", packageToUpdate.Version));
                root.Add(packageElement);
            }

            using (var writer = XmlWriter.Create(ReleaseUpdatesPath, new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
            }))
            {
                Log.LogMessage(MessageImportance.Normal, $"Generate {ReleaseUpdatesPath}");
                doc.Save(writer);
            }

            // Update release manifest
            var releaseBuildRoot = new XElement("ItemGroup");
            var releaseBuildDoc = new XDocument(new XElement("Project", releaseBuildRoot));

            foreach (var repo in graph.Select(n => n.Repository))
            {
                var repoContainsUpdatedProjects = false;
                foreach (var project in repo.Projects)
                {
                    if (shippingPackages.Any(package => string.Equals(project.Name, package, StringComparison.OrdinalIgnoreCase)))
                    {
                        var releasedVersion = releasePackages.Single(rp => string.Equals(rp.Name, project.Name, StringComparison.OrdinalIgnoreCase)).Version;
                        var releaseUpdateVersion = packagesToUpdate.SingleOrDefault(p => string.Equals(p.Name, project.Name, StringComparison.OrdinalIgnoreCase))?.Version ??
                            buildArtifact.Single(p => string.Equals(p.PackageInfo.Id, project.Name, StringComparison.OrdinalIgnoreCase)).PackageInfo.Version.Version.ToString(3);

                        if (!string.Equals(releasedVersion, releaseUpdateVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            repoContainsUpdatedProjects = true;
                            var packageElement = new XElement("UpdatedPackages");
                            packageElement.Add(new XAttribute("Include", project.Name));
                            packageElement.Add(new XAttribute("Version", releaseUpdateVersion));
                            releaseBuildRoot.Add(packageElement);
                        }
                    }
                }

                if (repoContainsUpdatedProjects)
                {
                    var packageElement = new XElement("UpdatedRepos");
                    packageElement.Add(new XAttribute("Include", repo.Name));
                    releaseBuildRoot.Add(packageElement);
                }
            }

            using (var writer = XmlWriter.Create(ReleaseBuildConfigPath, new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
            }))
            {
                Log.LogMessage(MessageImportance.Normal, $"Generate {ReleaseBuildConfigPath}");
                releaseBuildDoc.Save(writer);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
