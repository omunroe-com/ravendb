﻿using System.Collections.Generic;
using System.Linq;
using Raven.Client.Documents.Operations;
using Raven.Client.Util;
using Sparrow.Json.Parsing;

namespace Raven.Server.Commercial
{
    public class SecuredSetupInfo
    {
        public License License { get; set; }
        public string Email { get; set; }
        public string Domain { get; set; }
        public string Challenge { get; set; }
        public List<NodeInfo> NodeSetupInfos { get; set; }
        
        public DynamicJsonValue ToJson()
        {
            return new DynamicJsonValue
            {
                [nameof(License)] = License.ToJson(),
                [nameof(Email)] = Email,
                [nameof(Domain)] = Domain,
                [nameof(Challenge)] = Challenge,
                [nameof(NodeSetupInfos)] = NodeSetupInfos.Select(node => node.ToJson()).ToArray()
            };
        }

        public class NodeInfo
        {
            public string NodeTag { get; set; }
            public string ServerUrl { get; set; }
            public string PublicServerUrl { get; set; }
            public int Port { get; set; }
            public string Hostname { get; set; }
            public string Certificate { get; set; }
            public List<string> Ips { get; set; }


            public DynamicJsonValue ToJson()
            {
                return new DynamicJsonValue
                {
                    [nameof(NodeTag)] = NodeTag,
                    [nameof(ServerUrl)] = ServerUrl,
                    [nameof(PublicServerUrl)] = PublicServerUrl,
                    [nameof(Port)] = Port,
                    [nameof(Hostname)] = Hostname,
                    [nameof(Certificate)] = Certificate,
                    [nameof(Ips)] = Ips.ToArray(),
                };
            }
        }
    }

    public class UnsecuredSetupInfo
    {
        public string ServerUrl { get; set; }
        public string PublicServerUrl { get; set; }

        public DynamicJsonValue ToJson()
        {
            return new DynamicJsonValue
            {
                [nameof(ServerUrl)] = ServerUrl,
                [nameof(PublicServerUrl)] = PublicServerUrl
            };
        }
    }
    
    public class ClaimDomainInfo
    {
        public License License { get; set; }
        public string Domain { get; set; }

        public DynamicJsonValue ToJson()
        {
            return new DynamicJsonValue
            {
                [nameof(License)] = License.ToJson(),
                [nameof(Domain)] = Domain
            };
        }
    }

    public class RegistrationResult
    {
        public RegistrationStatus Status { get; set; }

        public DynamicJsonValue ToJson()
        {
            return new DynamicJsonValue
            {
                [nameof(Status)] = Status.ToString()
            };
        }
    }

    public enum RegistrationStatus
    {
        Pending,
        Done,
        Error
    }

    public enum SetupMode
    {
        Initial,
        LetsEncrypt,
        Secured,
        Unsecured
    }

    public class SetupProgressAndResult : IOperationResult, IOperationProgress
    {
        public long Processed { get; set; }
        public long Total { get; set; }
        public readonly List<string> Messages;

        public SetupProgressAndResult()
        {
            Messages = new List<string>();
        }

        public string Message { get; private set; }

        public DynamicJsonValue ToJson()
        {
            return new DynamicJsonValue(GetType())
            {
                [nameof(Processed)] = Processed,
                [nameof(Total)] = Total,
                [nameof(Messages)] = Messages
        };
        }

        public void AddWarning(string message)
        {
            AddMessage("WARNING", message);
        }

        public void AddInfo(string message)
        {
            AddMessage("INFO", message);
        }

        public void AddError(string message)
        {
            AddMessage("ERROR", message);
        }

        private void AddMessage(string type, string message)
        {
            Message = $"[{SystemTime.UtcNow:T} {type}] {message}";
            Messages.Add(Message);
        }

        public bool ShouldPersist => false;
    }
}