using System;
using Microsoft.Extensions.CommandLineUtils;

namespace UpdateExternalDependencies
{
    class Program
    {
        static int Main(string[] args)
        {
            var application = new CommandLineApplication()
            {
                Name = "dependencies"
            };

            new RootCommand().Configure(application);

            try
            {
                return application.Execute(args);
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine($"Exception thrown: '{ex.ToString()}'");
                return 1;
            }
        }
    }
}
