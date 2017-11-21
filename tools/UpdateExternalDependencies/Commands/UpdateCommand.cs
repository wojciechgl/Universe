// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using UpdateExternalDependencies.UpdateProviders;

namespace UpdateExternalDependencies.Commands
{
    internal class UpdateCommand : CommandBase
    {
        private string DependenciesPropsFile {
            get
            {
                return DependenciesPropsFileArgument.Value;
            }
        }

        private string Source {
            get
            {
                return SourceArgument.Value;
            }
        }

        private CommandArgument SourceArgument { get; set; }

        private CommandArgument DependenciesPropsFileArgument { get; set; }

        private static readonly IList<IUpdateProvider> UpdateProviders = new List<IUpdateProvider>
        {
            new NugetUpdateProvider(),
            new LastKnownGoodFileUpdateProvider()
        };

        public override void Configure(CommandLineApplication application)
        {
            SourceArgument = application.Argument("source", "The source of truth");
            DependenciesPropsFileArgument = application.Argument("depsProps", "The dependency file");

            base.Configure(application);
        }

        protected override int Execute()
        {
            var latest = GetLatestDeps();

            UpdateDependencyFile(latest);

            return 0;
        }

        private static DependenciesProps GetDependenciesProps(string dependencyFile)
        {
            var serializer = new XmlSerializer(typeof(DependenciesProps));
            using (var stream = File.OpenRead(dependencyFile))
            {
                return serializer.Deserialize(stream) as DependenciesProps;
            }
        }

        private DependenciesProps GetLatestDeps()
        {
            var dependencies = GetDependenciesProps(DependenciesPropsFile);

            DependenciesProps output = null;

            foreach (var provider in UpdateProviders)
            {
                if (provider.CanHandleSource(Source))
                {
                    output = provider.GetExternalDependencies(dependencies, Source);
                    break;
                }
            }

            if (output == null)
            {
                throw new NotImplementedException($"No UpdateProvider could handle source '{Source}'");
            }

            return output;
        }

        private void UpdateDependencyFile(DependenciesProps dependenciesProps)
        {
            var doc = new XmlDocument();
            doc.Load(DependenciesPropsFile);

            var rootNode = doc.SelectSingleNode("//Project");

            // We don't mess with things which have a VariableName set
            foreach (var externalDep in dependenciesProps.ExternalDependencies.Where(d => d.VariableName == null))
            {
                string version;
                if (externalDep.Version.StartsWith("$("))
                {
                    var variableName = externalDep.Version.TrimStart('$','(').TrimEnd(')');
                    var xmlElement = dependenciesProps.Variables.First(x => x.Name == variableName);

                    version = xmlElement.InnerText;
                }
                else
                {
                    version = externalDep.Version;
                }

                var xmlNodes = rootNode.SelectNodes($"//ItemGroup/ExternalDependency[@Include='{externalDep.Include}']");

                if (xmlNodes.Count == 0)
                {
                    throw new NotImplementedException("Nothing to update.");
                }
                else if(xmlNodes.Count > 0)
                {
                    foreach (XmlElement xmlNode in xmlNodes)
                    {
                        var variableName = xmlNode.SelectNodes("//VariableName");
                        if (variableName.Count == 0)
                        {
                            ((XmlElement)(xmlNodes[0])).SetAttribute("Version", version);
                        }
                    }
                }
            }

            doc.Save(DependenciesPropsFile);
        }

        private void UpdateExternalDependency(ExternalDependency dep, XmlNode rootNode)
        {
            foreach (XmlElement itemGroup in rootNode.SelectNodes("/ItemGroup"))
            {
                foreach (XmlElement externalDependencyElem in itemGroup.SelectNodes("/ExternalDependency"))
                {
                    var include = externalDependencyElem.GetAttribute("Include");
                    if (include == dep.Include)
                    {
                        var variableName = externalDependencyElem.GetAttribute("VariableName");
                        if (dep.VariableName == null || dep.VariableName == variableName)
                        {
                            externalDependencyElem.SetAttribute("Version", dep.Version);
                        }
                    }
                }
            }
        }
    }
}
