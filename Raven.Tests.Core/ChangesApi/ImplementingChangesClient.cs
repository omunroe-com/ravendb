﻿using Raven.Client;
using Raven.Client.Changes;
using Raven.Client.Connection;
using Raven.Client.Document;
using Raven.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Raven.Tests.Core.ChangesApi
{
    public class ImplementingChangesClient : RavenCoreTestBase
    {
        private interface IUntypedConnectable : IConnectableChanges
        { }

        private class NoProperInheritanceChangesClientBase : RemoteChangesClientBase<IUntypedConnectable, MockConnectionState>
        {
            public NoProperInheritanceChangesClientBase() :
                base("http://test", "apiKey", null, new HttpJsonRequestFactory(1024), new DocumentConvention(), new MockReplicationInformerBase(), () => { })
            {
            }

            protected override Task SubscribeOnServer()
            {
                throw new NotImplementedException();
            }

            protected override void NotifySubscribers(string type, RavenJObject value, IEnumerable<KeyValuePair<string, MockConnectionState>> connections)
            {
                throw new NotImplementedException();
            }
        }

        class ProperInheritanceChangesClientBase : RemoteChangesClientBase<IUntypedConnectable, MockConnectionState>, IUntypedConnectable
        {
            public ProperInheritanceChangesClientBase() :
                base("http://test", "apiKey", null, new HttpJsonRequestFactory(1024), new DocumentConvention(), new MockReplicationInformerBase(), () => { })
            {
            }

            protected override Task SubscribeOnServer()
            {
                throw new NotImplementedException();
            }

            protected override void NotifySubscribers(string type, RavenJObject value, IEnumerable<KeyValuePair<string, MockConnectionState>> connections)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public void RemoteChangesClientBaseWillFailWhenImproperlyImplemented()
        {
            Assert.Throws<InvalidCastException>(() => new NoProperInheritanceChangesClientBase());
        }

        [Fact]
        public void RemoteChangesClientBaseShouldWork()
        {
            new ProperInheritanceChangesClientBase();
        }


        #region Mocks

        private class MockConnectionState : IChangesConnectionState
        {
            public Task Task
            {
                get { throw new NotImplementedException(); }
            }

            public void Inc()
            {
                throw new NotImplementedException();
            }

            public void Dec()
            {
                throw new NotImplementedException();
            }

            public void Error(Exception e)
            {
                throw new NotImplementedException();
            }
        }

        private class MockReplicationInformerBase : IReplicationInformerBase
        {

            public event EventHandler<FailoverStatusChangedEventArgs> FailoverStatusChanged;

            public int DelayTimeInMiliSec
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public List<OperationMetadata> ReplicationDestinations
            {
                get { throw new NotImplementedException(); }
            }

            public List<OperationMetadata> ReplicationDestinationsUrls
            {
                get { throw new NotImplementedException(); }
            }

            public long GetFailureCount(string operationUrl)
            {
                throw new NotImplementedException();
            }

            public DateTime GetFailureLastCheck(string operationUrl)
            {
                throw new NotImplementedException();
            }

            public int GetReadStripingBase()
            {
                throw new NotImplementedException();
            }

            public Task<T> ExecuteWithReplicationAsync<T>(string method, string primaryUrl, Abstractions.Connection.OperationCredentials primaryCredentials, int currentRequest, int currentReadStripingBase, Func<OperationMetadata, Task<T>> operation)
            {
                throw new NotImplementedException();
            }

            public void ForceCheck(string primaryUrl, bool shouldForceCheck)
            {
                throw new NotImplementedException();
            }

            public bool IsServerDown(Exception exception, out bool timeout)
            {
                throw new NotImplementedException();
            }

            public bool IsHttpStatus(Exception e, params HttpStatusCode[] httpStatusCode)
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }

        #endregion Mocks
    }
}
