﻿using System.IO;
using System.Net.Http;
using Raven.Client.Documents.Session;

namespace Raven.Client.Documents.Commands
{
    public class StreamResult
    {
        public HttpResponseMessage Response { get; set; }
        public Stream Stream { get; set; }
    }

    public class StreamResult<TType>
    {
        /// <summary>
        /// Document ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Document change vector.
        /// </summary>
        public string ChangeVector { get; set; }

        /// <summary>
        /// Document metadata.
        /// </summary>
        public IMetadataDictionary Metadata { get; set; }

        /// <summary>
        /// Document deserialized to <c>TType</c>.
        /// </summary>
        public TType Document { get; set; }
    }
}