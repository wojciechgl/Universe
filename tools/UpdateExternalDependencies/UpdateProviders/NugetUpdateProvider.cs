// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using UpdateExternalDependencies.Commands;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using System.Linq;

namespace UpdateExternalDependencies.UpdateProviders
{
    public class NugetUpdateProvider : IUpdateProvider
    {
        private const string PackageBase = "PackageBaseAddress/3.0.0";



        public bool CanHandleSource(string source)
        {
            return source.EndsWith("v3/index.json");
        }

        public DependenciesProps GetExternalDependencies(DependenciesProps props, string source)
        {
            var packageUrlBase = GetPackageUrlBase(source);

            var versionDict = new Dictionary<string, string>();

            foreach (var externalDependency in props.ExternalDependencies)
            {
                var highestVersion = GetHighestVersion(externalDependency.Include, packageUrlBase);

                if(highestVersion != null)
                {
                    externalDependency.Version = highestVersion;
                }
            }

            return props;
        }

        private static string GetPackageUrlBase(string source)
        {
            string content;
            using (var client = new HttpClient())
            {
                var response = client.GetAsync(source).Result;
                response.EnsureSuccessStatusCode();
                content = response.Content.ReadAsStringAsync().Result;
            }

            var indexResponse = JsonConvert.DeserializeObject<IndexResponse>(content);

            var resource = indexResponse.resources.FirstOrDefault(r => r.Type == PackageBase);

            if (resource == null)
            {
                throw new NotImplementedException($"Nuget source '{source}' doesn't support {PackageBase}");
            }

            return resource.Id;
        }

        private string GetHighestVersion(string packageId, string packageUrlBase)
        {
            var versionUrl = $"{packageUrlBase}{packageId.ToLower()}/index.json";

            string content;
            using (var client = new HttpClient())
            {
                var response = client.GetAsync(versionUrl).Result;

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine($"{packageId} doesn't exist in the source, so it won't be updated.");
                    return null;
                }

                content = response.Content.ReadAsStringAsync().Result;
            }

            var versions = JsonConvert.DeserializeObject<PackageVersions>(content);

            return versions.Versions.Last();
        }

        private class PackageVersions
        {
            public IEnumerable<string> Versions { get; set; }
        }

        private class IndexResponse
        {
            public string version { get; set; }
            public List<Resource> resources { get; set; }
        }

        private class Resource
        {
            [JsonProperty(PropertyName = "@id")]
            public string Id { get; set; }
            [JsonProperty(PropertyName = "@type")]
            public string Type { get; set; }
        }
    }
}
