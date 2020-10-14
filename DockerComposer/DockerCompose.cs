using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Services;

namespace DockerComposer
{
    public class DockerCompose : IDisposable
    {
        private ICompositeService _compositeService;
        private bool _forceReCreate;
        private bool _forceBuild;
        private bool _keepAlive;
        private bool _isCorrectEnvironment;
        private readonly string _dockerComposeFileName;
        private readonly List<(string serviceName, Func<bool> check)> _healthChecks;
        private readonly List<(string serviceName, string process, long timeout)>
            _processHealthChecks;
        private readonly List<(string serviceName, string portAndProto, long timeout, string address)>
            _portHealthChecks;

        private DockerCompose(string dockerComposeFileName)
        {
            _dockerComposeFileName = dockerComposeFileName;
            _healthChecks = new List<(string serviceName, Func<bool> check)>();
            _processHealthChecks = new List<(string serviceName, string process, long timeout)>();
            _portHealthChecks = new List<(string serviceName, string portAndProto, long timeout, string address)>();
        }

        /// <summary>
        /// Searches for provided docker compose file recursively, uses "docker-compose.yml"
        /// if no name is provided
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static DockerCompose WithComposeFile(string fileName = "docker-compose.yml")
        {
            return new DockerCompose(fileName);
        }
        
        /// <summary>
        /// Allows to Only use composer when on certain environment
        /// </summary>
        /// <param name="environmentVariable">environment variable to check for</param>
        /// <param name="environmentVariableCheck">optional check on the value of the environment variable, by default checks that environment variable exists</param>
        /// <returns></returns>
        public DockerCompose WhenEnvironment(string environmentVariable, Predicate<string> environmentVariableCheck = null)
        {
            var value = Environment.GetEnvironmentVariable(environmentVariable);
            _isCorrectEnvironment = environmentVariableCheck?.Invoke(value) ?? value != null;
            return this;
        }

        /// <summary>
        /// Allows for custom container health checks using the container names
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="check"></param>
        /// <returns></returns>
        public DockerCompose WaitForCheck(string serviceName, Func<bool> check)
        {
            _healthChecks.Add((serviceName, check));
            return this;
        }

        /// <summary>
        /// Tries to bring up the containers defined in the docker compose file
        /// </summary>
        /// <returns></returns>
        public IDisposable Up()
        {
            var composeFileLocation = TryGetDockerComposeFilePath(_dockerComposeFileName);
            var builder = new Builder()
                .UseContainer()
                .UseCompose()
                .FromFile(composeFileLocation)
                .RemoveOrphans();

            if (_forceBuild)
            {
                builder.ForceBuild();
            }

            if (_forceReCreate)
            {
                builder.ForceRecreate();
            }

            foreach (var healthCheck in _healthChecks)
            {
                builder.Wait(healthCheck.serviceName,
                    (service, retryCount) => healthCheck.check() ? 0 : 1000 - retryCount * 100);
            }

            foreach (var portHealthCheck in _portHealthChecks)
            {
                builder.WaitForPort(portHealthCheck.serviceName, portHealthCheck.portAndProto, portHealthCheck.timeout,
                    portHealthCheck.address);
            }

            foreach (var portHealthCheck in _processHealthChecks)
            {
                builder.WaitForProcess(portHealthCheck.serviceName, portHealthCheck.process,
                    portHealthCheck.timeout);
            }

            _compositeService = builder.Build()
                .Start();

            return this;
        }

        public DockerCompose ForceBuild()
        {
            _forceBuild = true;
            return this;
        }

        public DockerCompose ForceReCreate()
        {
            _forceReCreate = true;
            return this;
        }

        public DockerCompose WaitForPort(string service, string portAndProto, long millisTimeout = long.MaxValue,
            string address = null)
        {
            _portHealthChecks.Add((service, portAndProto, millisTimeout, address));
            return this;
        }

        public DockerCompose WaitForProcess(string service, string process, long millisTimeout = long.MaxValue)
        {
            _processHealthChecks.Add((service, process, millisTimeout));
            return this;
        }

        private static string TryGetDockerComposeFilePath(string fileName)
        {
            var filePath = TryGetFilePath(fileName);
            return filePath == null
                ? throw new ArgumentNullException($"could not locate {fileName}")
                : Path.Combine(filePath, fileName);
        }

        private static string TryGetFilePath(string fileName)
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !directory.GetFiles(fileName).Any())
            {
                directory = directory.Parent;
            }

            return directory?.FullName;
        }

        /// <summary>
        /// lets you allow the containers to stay alive and running for consecutive runs
        /// could be useful for running locally when developing
        /// </summary>
        /// <param name="shouldKeepAlive"></param>
        /// <returns></returns>
        public DockerCompose KeepAliveWhen(Func<bool> shouldKeepAlive)
        {
            _keepAlive = shouldKeepAlive();
            return this;
        }

        /// <summary>
        /// lets you allow the containers to stay alive and running for consecutive runs
        /// could be useful for running locally when developing
        /// </summary>
        /// <param name="environmentVariable">environment variable to check for</param>
        /// <param name="environmentVariableCheck">optional check on the value of the environment variable, by default checks that environment variable exists</param>
        /// <returns></returns>
        public DockerCompose KeepAliveWhen(string environmentVariable, Predicate<string> environmentVariableCheck = null)
        {
            var value = Environment.GetEnvironmentVariable(environmentVariable);
            _keepAlive = environmentVariableCheck?.Invoke(value) ?? value != null;
            return this;
        }

        public void Dispose()
        {
            if (_keepAlive)
            {
                return;
            }

            _compositeService?.Stop();
            _compositeService?.Remove(true);
            _compositeService?.Dispose();
        }
    }
}