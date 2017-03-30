﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading;
using FullScale180.SemanticLogging.Sinks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
using Newtonsoft.Json;

namespace FullScale180.SemanticLogging
{
    /// <summary>
    /// Factories and helpers for using the <see cref="ElasticsearchSink"/>.
    /// </summary>
    public static class ElasticsearchLog
    {
        /// <summary>
        /// Subscribes to an <see cref="IObservable{EventEntry}" /> using a <see cref="ElasticsearchSink" />.
        /// </summary>
        /// <param name="eventStream">The event stream. Typically this is an instance of <see cref="ObservableEventListener" />.</param>
        /// <param name="instanceName">The name of the instance originating the entries.</param>
        /// <param name="connectionString">The endpoint address for the Elasticsearch Service.</param>
        /// <param name="index">Index name prefix formatted as index-{0:yyyy.MM.DD}</param>
        /// <param name="type">The Elasticsearch entry type</param>
        /// <param name="flattenPayload">Flatten the payload collection when serializing event entries</param>
        /// <param name="bufferingInterval">The buffering interval between each batch publishing. Default value is <see cref="Buffering.DefaultBufferingInterval" />.</param>
        /// <param name="onCompletedTimeout">Defines a timeout interval for when flushing the entries after an <see cref="ElasticsearchSink.OnCompleted" /> call is received and before disposing the sink.</param>
        /// <param name="bufferingCount">Buffering count to send entries sot Elasticsearch. Default value is <see cref="Buffering.DefaultBufferingCount" /></param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered while it's sending to Elasticsearch before the sink starts dropping entries.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Normally, calling <see cref="IDisposable.Dispose" /> on
        /// the <see cref="System.Diagnostics.Tracing.EventListener" /> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null" /> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        /// <param name="userName">The username to authenticate with Elasticsearch using Basic HTTP authentication.</param>
        /// <param name="password">The password to authenticate with Elasticsearch using Basic HTTP authentication.</param>
        /// <param name="globalContextExtension">A dictionary of user defined keys and values to be attached to each log.</param>
        /// <returns>
        /// A subscription to the sink that can be disposed to unsubscribe the sink and dispose it, or to get access to the sink instance.
        /// </returns>
        public static SinkSubscription<ElasticsearchSink> LogToElasticsearch(this IObservable<EventEntry> eventStream,
            string instanceName, string connectionString, string index, string type, bool flattenPayload = true, TimeSpan? bufferingInterval = null,
            TimeSpan? onCompletedTimeout = null,
            int bufferingCount = Buffering.DefaultBufferingCount,
            int maxBufferSize = Buffering.DefaultMaxBufferSize,
            string userName = null, string password = null,
            Dictionary<string,string> globalContextExtension = null)
        {
            var sink = new ElasticsearchSink(instanceName, connectionString, index, type, flattenPayload,
                bufferingInterval ?? Buffering.DefaultBufferingInterval,
                bufferingCount,
                maxBufferSize,
                onCompletedTimeout ?? Timeout.InfiniteTimeSpan,
                userName, password,
                JsonConvert.SerializeObject(globalContextExtension));

            var subscription = eventStream.Subscribe(sink);
            return new SinkSubscription<ElasticsearchSink>(subscription, sink);
        }
        
        /// <summary>
        /// Creates an event listener that logs using a <see cref="ElasticsearchSink" />.
        /// </summary>
        /// <param name="instanceName">The name of the instance originating the entries.</param>
        /// <param name="connectionString">The endpoint address for the Elasticsearch Service.</param>
        /// <param name="index">Index name prefix formatted as index-{0:yyyy.MM.DD}</param>
        /// <param name="type">The Elasticsearch entry type</param>
        /// <param name="flattenPayload">Flatten the payload collection when serializing event entries</param>
        /// <param name="bufferingInterval">The buffering interval between each batch publishing.</param>
        /// <param name="listenerDisposeTimeout">Defines a timeout interval for the flush operation when the listener is disposed.</param>
        /// <param name="bufferingCount">Buffering count to send entries sot Elasticsearch. Default value is <see cref="Buffering.DefaultBufferingCount" /></param>
        /// <param name="maxBufferSize">The maximum number of entries that can be buffered while it's sending to Elasticsearch before the sink starts dropping entries.
        /// This means that if the timeout period elapses, some event entries will be dropped and not sent to the store. Calling <see cref="IDisposable.Dispose" /> on
        /// the <see cref="EventListener" /> will block until all the entries are flushed or the interval elapses.
        /// If <see langword="null" /> is specified, then the call will block indefinitely until the flush operation finishes.</param>
        /// <param name="userName">The username to authenticate with Elasticsearch using Basic HTTP authentication.</param>
        /// <param name="password">The password to authenticate with Elasticsearch using Basic HTTP authentication.</param>
        /// <returns>
        /// An event listener that uses <see cref="ElasticsearchSink" /> to log events.
        /// </returns>
        public static EventListener CreateListener(string instanceName, string connectionString, string index, string type, bool flattenPayload = true,
            TimeSpan? bufferingInterval = null, TimeSpan? listenerDisposeTimeout = null, int bufferingCount = Buffering.DefaultBufferingCount,
            int maxBufferSize = Buffering.DefaultMaxBufferSize, string userName = null, string password = null)
        {
            var listener = new ObservableEventListener();
            listener.LogToElasticsearch(instanceName, connectionString, index, type, flattenPayload, bufferingInterval,
                listenerDisposeTimeout, bufferingCount, maxBufferSize, userName, password);
            return listener;
        }
    }
}