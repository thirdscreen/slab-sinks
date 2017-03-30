﻿// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using FullScale180.SemanticLogging.Sinks.Tests.TestObjects;
using FullScale180.SemanticLogging.Sinks.Tests.TestSupport;
using FullScale180.SemanticLogging.Utility;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FullScale180.SemanticLogging.Sinks.Tests
{
    [TestClass]
    public class given_elasticsearch_configuration
    {
        private const string DevelopmentElasticsearchEndpoint = "http://localhost:9200";

        [TestMethod]
        public void when_creating_sink_for_null_connection_string_then_throws()
        {
            AssertEx.Throws<ArgumentNullException>(() => new ElasticsearchSink("instanceName", null, "logstash", "etw", true, TimeSpan.FromSeconds(1), 1000, 10000, Timeout.InfiniteTimeSpan, null, null));
        }

        [TestMethod]
        public void when_creating_sink_with_invalid_connection_string_then_throws()
        {
            AssertEx.Throws<UriFormatException>(() => new ElasticsearchSink("instanceName", "InvalidConnection", "logstash", "etw", true, TimeSpan.FromSeconds(1), 1000, 10000, Timeout.InfiniteTimeSpan, null, null));
        }

        [TestMethod]
        public void when_creating_sink_with_small_buffer_size_then_throws()
        {
            AssertEx.Throws<ArgumentException>(() => new ElasticsearchSink("instanceName", DevelopmentElasticsearchEndpoint, "logstash", "etw", true, TimeSpan.FromSeconds(1), 1000, 10, Timeout.InfiniteTimeSpan, null, null));
        }

        [TestMethod]
        public void when_creating_sink_with_invalid_character_in_index_then_throws()
        {
            // Invalid index name characters
            var testInvalidCharacters = new[] { '\\', '/', ' ', 'U', ',', '"', '*', '?', '|', '<', '>' };

            foreach (var invalidChar in testInvalidCharacters)
            {
                AssertEx.Throws<ArgumentException>(() => new ElasticsearchSink("instanceName", DevelopmentElasticsearchEndpoint, string.Format("{0}testindex", invalidChar), "etw", true, TimeSpan.FromSeconds(1), 1000, 10000, Timeout.InfiniteTimeSpan, null, null));
            }

            foreach (var invalidChar in testInvalidCharacters)
            {
                AssertEx.Throws<ArgumentException>(() => new ElasticsearchSink("instanceName", DevelopmentElasticsearchEndpoint, string.Format("test{0}index", invalidChar), "etw", true, TimeSpan.FromSeconds(1), 1000, 10000, Timeout.InfiniteTimeSpan, null, null));
            }

            foreach (var invalidChar in testInvalidCharacters)
            {
                AssertEx.Throws<ArgumentException>(() => new ElasticsearchSink("instanceName", DevelopmentElasticsearchEndpoint, string.Format("testindex{0}", invalidChar), "etw", true, TimeSpan.FromSeconds(1), 1000, 10000, Timeout.InfiniteTimeSpan, null, null));
            }
        }
    }

    [TestClass]
    public class given_elasticsearch_event_entry
    {
        [TestMethod]
        public void when_serializing_a_log_entry_then_object_can_serialize()
        {
            var payload = new Dictionary<string, object> { { "msg", "the message" }, { "date", DateTime.UtcNow } };
            var logObject =
                EventEntryTestHelper.Create(
                    timestamp: DateTimeOffset.UtcNow,
                    payloadNames: payload.Keys,
                    payload: payload.Values);

            var actual = new ElasticsearchEventEntrySerializer("logstash", "slab", "instance", true).Serialize(new[] { logObject });

            Assert.IsNotNull(actual);
            Assert.IsTrue(this.IsValidBulkMessage(actual));
        }

        [TestMethod]
        public void when_serializing_a_log_entry_then_object_can_serialize_process_and_thread_id()
        {
            var payload = new Dictionary<string, object> { { "msg", "the message" }, { "date", DateTime.UtcNow } };
            var logObject =
                EventEntryTestHelper.Create(
                    timestamp: DateTimeOffset.UtcNow,
                    payloadNames: payload.Keys,
                    payload: payload.Values,
                    processId: 300,
                    threadId: 500);

            var actual = new ElasticsearchEventEntrySerializer("logstash", "slab", "instance", true).Serialize(new[] { logObject });

            Assert.IsNotNull(actual);
            Assert.IsTrue(this.IsValidBulkMessage(actual));

            var serializedEntry = actual.Split('\n')[1];
            var jsonObject = JObject.Parse(serializedEntry);

            Assert.AreEqual(300, jsonObject["ProcessId"]);
            Assert.AreEqual(500, jsonObject["ThreadId"]);
        }

        [TestMethod]
        public void when_serializing_a_log_entry_with_activtyid_then_activityid_serialized()
        {
            var payload = new Dictionary<string, object> { { "msg", "the message" }, { "date", DateTime.UtcNow } };
            var logObject =
                EventEntryTestHelper.Create(
                    timestamp: DateTimeOffset.UtcNow,
                    payloadNames: payload.Keys,
                    payload: payload.Values,
                    activityId: Guid.NewGuid(),
                    relatedActivityId: Guid.NewGuid());

            var actual = new ElasticsearchEventEntrySerializer("logstash", "slab", "instance", true).Serialize(new[] { logObject });

            var serializedEntry = actual.Split('\n')[1];
            var jsonObject = JObject.Parse(serializedEntry);

            Assert.AreEqual(logObject.ActivityId.ToString(), jsonObject["ActivityId"]);
            Assert.AreEqual(logObject.RelatedActivityId.ToString(), jsonObject["RelatedActivityId"]);
            Assert.IsNotNull(actual);
            Assert.IsTrue(this.IsValidBulkMessage(actual));
        }

        [TestMethod]
        public void when_serializing_a_log_entry_without_activtyid_then_activityid_not_serialized()
        {
            var payload = new Dictionary<string, object> { { "msg", "the message" }, { "date", DateTime.UtcNow } };
            var logObject =
                EventEntryTestHelper.Create(
                    timestamp: DateTimeOffset.UtcNow,
                    payloadNames: payload.Keys,
                    payload: payload.Values);

            var actual = new ElasticsearchEventEntrySerializer("logstash", "slab", "instance", true).Serialize(new[] { logObject });

            var serializedEntry = actual.Split('\n')[1];
            var jsonObject = JObject.Parse(serializedEntry);

            Assert.IsTrue(jsonObject["ActivityId"] == null);
            Assert.IsTrue(jsonObject["RelatedActivityId"] == null);
            Assert.IsNotNull(actual);
            Assert.IsTrue(this.IsValidBulkMessage(actual));
        }

        [TestMethod]
        public void when_serializing_a_log_entry_without_flattened_payload_then_payload_nested()
        {
            var payload = new Dictionary<string, object> { { "msg", "the message" }, { "date", DateTime.UtcNow } };
            var logObject =
                EventEntryTestHelper.Create(
                    timestamp: DateTimeOffset.UtcNow,
                    payloadNames: payload.Keys,
                    payload: payload.Values);

            var actual = new ElasticsearchEventEntrySerializer("logstash", "slab", "instance", false).Serialize(new[] { logObject });

            var serializedEntry = actual.Split('\n')[1];
            var jsonObject = JObject.Parse(serializedEntry);

            Assert.IsTrue(jsonObject["Payload"]["msg"] != null);
            Assert.IsTrue(jsonObject["Payload"]["date"] != null);
            Assert.IsNotNull(actual);
            Assert.IsTrue(this.IsValidBulkMessage(actual));
        }

        [TestMethod]
        public void when_serializing_a_log_with_jsonprops_entry_then_json_merged()
        {
            var payload = new Dictionary<string, object>
            {
                { "msg", "the message" },
                { "date", DateTime.UtcNow },
                { "_jsonPayload", "{\"test\": \"value\", \"test2\": \"value2\"}"}
            };
            var logObject =
                EventEntryTestHelper.Create(
                    timestamp: DateTimeOffset.UtcNow,
                    payloadNames: payload.Keys,
                    payload: payload.Values);

            var actual = new ElasticsearchEventEntrySerializer("logstash", "slab", "instance", true).Serialize(new[] { logObject });
            
            // Make sure the payload is still valid
            var obj = JObject.Parse(actual.Split('\n')[1]);

            Assert.IsNotNull(actual);
            Assert.IsTrue(this.IsValidBulkMessage(actual));
        }

        [TestMethod]
        public void when_serializing_logentries_can_serialize_valid_bulk_request_format()
        {
            // Note: converting an array does not create valid message for use in elasticsearch bulk operation

            var actual = new ElasticsearchEventEntrySerializer("logstash", "slab", "instance", true).Serialize(new[] { CreateEventEntry(), CreateEventEntry() });

            Assert.IsNotNull(actual);
            Assert.IsTrue(this.IsValidBulkMessage(actual));
        }

        [TestMethod]
        public void when_serializing_with_global_context_then_object_can_serialize()
        {
            var ctx = new Dictionary<string, string>()
            {
                {"TestCtx1", "TestCtxValue1"},
                {"TestCtx2", "TestCtxValue2"}
            };
            var payload = new Dictionary<string, object> { { "msg", "the message" }, { "date", DateTime.UtcNow } };
            var logObject =
                EventEntryTestHelper.Create(
                    timestamp: DateTimeOffset.UtcNow,
                    payloadNames: payload.Keys,
                    payload: payload.Values);

            var actual = new ElasticsearchEventEntrySerializer("logstash", "slab", "instance", true, ctx).Serialize(new[] { logObject });

            Debug.WriteLine(actual);

            Assert.IsNotNull(actual);
            Assert.IsTrue(this.IsValidBulkMessage(actual));
        }

        private static EventEntry CreateEventEntry()
        {
            var payload = new Dictionary<string, object> { { "msg", "the message" }, { "date", DateTime.UtcNow } };
            var logObject =
                EventEntryTestHelper.Create(
                    timestamp: DateTimeOffset.UtcNow,
                    payloadNames: payload.Keys,
                    payload: payload.Values);

            return logObject;
        }

        private bool IsValidBulkMessage(string message)
        {
            // Ignores additional newlines when we split which is fine
            // Note: Except between the header/body documents which we may want to validate
            var entries = message.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // If we don't have at least two items then return false
            if (entries.Length < 2)
            {
                return false;
            }

            bool isHeader = true;
            foreach (var entry in entries)
            {
                var entryObject = JObject.Parse(entry);
                if (isHeader)
                {
                    // Check to see if this is an index header object
                    if (entryObject["index"]["_index"] == null)
                    {
                        return false;
                    }
                }
                else
                {
                    // Simple check to see if we have one of our common properties
                    // The body/document just needs to be valid json which we were able to successfully parse
                    if (entryObject["EventId"] == null)
                    {
                        return false;
                    }
                }

                //Every other entry separated by newline should be a header
                isHeader = !isHeader;
            }

            // Finally, the last item seen should not be header
            return isHeader;
        }
    }

    [TestClass]
    public class given_elasticsearch_response
    {
        [TestMethod]
        public void when_400_error_is_returned_then_batch_fails_and_logs_exception_without_timeout()
        {
            var mockHttpListener = new MockHttpListener();

            using (var collectErrorsListener = new MockEventListener())
            {
                collectErrorsListener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Error, Keywords.All);

                var endpoint = mockHttpListener.Start(new MockHttpListenerResponse()
                {
                    ResponseCode = 400,
                    ContentType = "application/json",
                    Content = "{ \"error\": \"InvalidIndexNameException[[log,stash] Invalid index name [log,stash], must not contain the following characters [\\\\, /, *, ?, \\\", <, >, |,  , ,]]\",\"status\": 400}"
                });

                var sink = new ElasticsearchSink("instance", endpoint, "slabtest", "etw", true, TimeSpan.FromSeconds(1), 100, 800, TimeSpan.FromMinutes(1), null, null);

                sink.OnNext(EventEntryTestHelper.Create());

                var flushCompleteInTime = sink.FlushAsync().Wait(TimeSpan.FromSeconds(45));

                mockHttpListener.Stop();

                // Make sure the exception is logged
                Assert.IsTrue(collectErrorsListener.WrittenEntries.First().Payload.Single(m => m.ToString().Contains("InvalidIndexNameException")) != null);
                Assert.IsTrue(flushCompleteInTime);
            }
        }
    }
}