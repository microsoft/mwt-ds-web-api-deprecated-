using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.ApplicationInsights;
using System.Diagnostics;

namespace DecisionServiceWebAPI
{
    public class RewardController : ApiController
    {
        // POST api/<controller>
        public HttpResponseMessage PostReward([FromUri] float reward, [FromUri] String eventId, [FromUri] String timestamp)
        {
            var mwttoken = Request.Headers.SingleOrDefault(x => x.Key == "Authorization").Value.First();
            if (string.IsNullOrWhiteSpace(mwttoken))
                return Request.CreateResponse(HttpStatusCode.Forbidden);

            var telemetry = new TelemetryClient();
            var stopwatch = Stopwatch.StartNew();
            telemetry.Context.User.Id = mwttoken;

            try
            {
                telemetry.Context.Operation.Name = "ChooseAction";
                telemetry.Context.Operation.Id = eventId + " " + timestamp;

                // parse input
                var guid = Guid.ParseExact(eventId, "N");
                var timestampParsed = DateTime.ParseExact(timestamp, "o", CultureInfo.InvariantCulture);

                var eventUploader = DecisionServiceStaticClient.AddOrGetExisting("uploader" + mwttoken,
                    _ =>
                    {
                        telemetry.TrackEvent("EventUploader creation");

                        // TODO: either make the constant public or the download of ApplicationTransferMeta should be public...
                        string redirectionBlobLocation = string.Format(DecisionServiceConstants.RedirectionBlobLocation, mwttoken);

                        using (var wc = new WebClient())
                        {
                            string jsonMetadata = wc.DownloadString(redirectionBlobLocation);
                            var metaData = JsonConvert.DeserializeObject<ApplicationClientMetadata>(jsonMetadata);

                            return new EventUploaderASA(
                                metaData.EventHubObservationConnectionString,
                                new BatchingConfiguration
                                {
                                // TODO: these are not production ready configurations. do we need to move those to C&C as well?
                                MaxBufferSizeInBytes = 1,
                                    MaxDuration = TimeSpan.FromSeconds(1),
                                    MaxEventCount = 1,
                                    MaxUploadQueueCapacity = 1,
                                    UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry
                                });
                        }
                    });

                eventUploader.Upload(new Observation
                {
                    Key = guid.ToString("N", CultureInfo.InvariantCulture),
                    TimeStamp = timestampParsed,
                    Value = reward
                });

                stopwatch.Stop();
                telemetry.TrackRequest("ReportReward", DateTime.Now, stopwatch.Elapsed, "200", true);

                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch(Exception e)
            {
                telemetry.TrackException(e);
                return Request.CreateResponse(HttpStatusCode.InternalServerError, e.Message);
            }
        }
    }

}