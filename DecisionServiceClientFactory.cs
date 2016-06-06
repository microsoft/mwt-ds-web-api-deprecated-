using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using System;
using System.Collections.Generic;
using System.Configuration;

namespace DecisionServiceWebAPI
{
    static class DecisionServiceClientFactory
    {
        private static DecisionServiceConfiguration CreateConfiguration(string settingsUrl)
        {
            var telemetry = new TelemetryClient();
            telemetry.TrackEvent($"DecisionServiceClient created: '{settingsUrl}'");

            return new DecisionServiceConfiguration(settingsUrl)
            {
                InteractionUploadConfiguration = new BatchingConfiguration
                {
                    // TODO: these are not production ready configurations. do we need to move those to C&C as well?
                    MaxBufferSizeInBytes = 1,
                    MaxDuration = TimeSpan.FromSeconds(1),
                    MaxEventCount = 1,
                    MaxUploadQueueCapacity = 1,
                    UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry
                },
                ModelPollFailureCallback = e => telemetry.TrackException(e, new Dictionary<string, string> { { "Pool failure", "model" } }),
                SettingsPollFailureCallback = e => telemetry.TrackException(e, new Dictionary<string, string> { { "Pool failure", "settings" } })
            };
        }

        public static DecisionServiceClient<string, int, int> AddOrGetExistingPolicy(string settingsUrl)
        {
            return DecisionServiceStaticClient.AddOrGetExisting("policy" + settingsUrl, _ => DecisionService.WithPolicy(CreateConfiguration(settingsUrl)).WithJson());
        }

        public static DecisionServiceClient<string, int[], int[]> AddOrGetExistingRanker(string settingsUrl)
        {
            return DecisionServiceStaticClient.AddOrGetExisting("ranker" + settingsUrl, _ => DecisionService.WithRanker(CreateConfiguration(settingsUrl)).WithJson());
        }
    }
}