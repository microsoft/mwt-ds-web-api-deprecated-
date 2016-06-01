using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using System;
using System.Collections.Generic;

namespace DecisionServiceWebAPI
{
    static class DecisionServiceClientFactory
    {
        private static DecisionServiceConfiguration Create(string mwttoken)
        {
            var telemetry = new TelemetryClient();
            telemetry.Context.User.Id = mwttoken;
            telemetry.TrackEvent("DecisionServiceClient creation");

            return new DecisionServiceConfiguration(mwttoken)
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

        public static DecisionServiceClient<string, int, int> AddOrGetExistingPolicy(string mwttoken)
        {
            return DecisionServiceStaticClient.AddOrGetExisting("policy" + mwttoken, _ => DecisionService.WithPolicy(Create(mwttoken)).WithJson());
        }

        public static DecisionServiceClient<string, int[], int[]> AddOrGetExistingRanker(string mwttoken)
        {
            return DecisionServiceStaticClient.AddOrGetExisting("ranker" + mwttoken, _ => DecisionService.WithRanker(Create(mwttoken)).WithJson());
        }
    }
}