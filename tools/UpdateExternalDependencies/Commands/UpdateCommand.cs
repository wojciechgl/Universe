// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace UpdateExternalDependencies.Commands
{
    internal class UpdateCommand : CommandBase
    {
        private string DependenciesProps {
            get
            {
                return DependenciesPropsFile.Value;
            }
        }

        private CommandArgument SourceArgument { get; set; }

        private CommandArgument DependenciesPropsFile { get; set; }

        private IList<IUpdateProvider> UpdateProviders = new List<IUpdateProvider>
        {
            new NugetUpdateProvider()
        };

        public override void Configure(CommandLineApplication application)
        {
            SourceArgument = application.Argument("source", "The source of truth");
            DependenciesPropsFile = application.Argument("depsProps", "The dependency file");

            base.Configure(application);
        }

        protected override int Execute()
        {
            var latest = GetLatestDeps(DependenciesProps);

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

        private static DependenciesProps GetLatestDeps(string dependenciesProps)
        {
            var dependencies = GetDependenciesProps(dependenciesProps);

            foreach (var provider in UpdateProviders)
            {
                
            }

            throw new NotImplementedException();
        }

        private void UpdateDependencyFile(DependenciesProps dependenciesProps)
        {
            throw new NotImplementedException();
        }
    }

    [XmlRoot("Project")]
    public class DependenciesProps
    {
        [XmlElement("ItemGroup")]
        public List<ItemGroup> ItemGroups { get; set; }

        [XmlElement("PropertyGroup")]
        public List<PropertyGroup> PropertyGroups { get; set; }
    }

    public class PropertyGroup
    {
        [XmlElement]
        public List<XmlElement> Variables { get; set; }
    }

    public class ItemGroup
    {
        [XmlElement("ExternalDependency")]
        public List<ExternalDependency> ExternalDependencies { get; set; }
    }

    public class ExternalDependency
    {
        [XmlAttribute("Include")]
        public string Include { get; set; }

        [XmlAttribute("Version")]
        public string Version { get; set; }

        [XmlAttribute("Source")]
        public string Source { get; set; }

        [XmlAttribute("Private")]
        public bool Private { get; set; }

        [XmlAttribute("Mirror")]
        public bool Mirror { get; set; }

        [XmlElement("VariableName")]
        public string VariableName { get; set; }
    }
}
