﻿using FastTests.Voron;
using Raven.Server.Indexing;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_9381 : StorageTest
    {
        [Fact]
        public void Lucene_directory_must_be_aware_of_created_outputs()
        {
            using (var tx = Env.WriteTransaction())
            {
                var dir = new LuceneVoronDirectory(tx, Env);

                var state = new VoronState(tx);

                dir.CreateOutput("file", state);
                Assert.True(dir.FileExists("file", state));

                dir.DeleteFile("file", state);
                Assert.False(dir.FileExists("file", state));
            }
        }
    }
}