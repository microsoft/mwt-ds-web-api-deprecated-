using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using System.Web.Http;

namespace DecisionServiceWebAPI
{
    public class DecisionPolicyController : ApiController
    {
        // POST api/decisionPolicy
        public async Task<HttpResponseMessage> Post([FromUri] int? defaultAction = null)
        {
            return await DecisionUtil.ChooseAction(
                this.Request,
                "Policy",
                (telemetry, input) =>
                {
                    var client = DecisionServiceClientFactory.AddOrGetExistingPolicy(input.MwtToken);
                    return defaultAction != null ?
                        client.ChooseAction(input.EventId, input.Context, (int)defaultAction) :
                        client.ChooseAction(input.EventId, input.Context);
                });
        }
    }

    public class DecisionRankerController : ApiController
    {
        // POST api/decisionRanker
        public async Task<HttpResponseMessage> Post([FromUri] int[] defaultActions)
        {
            return await DecisionUtil.ChooseAction(
                this.Request,
                "Ranker",
                (telemetry, input) =>
                {
                    var client = DecisionServiceClientFactory.AddOrGetExistingRanker(input.MwtToken);
                    var action = defaultActions != null && defaultActions.Length > 0 ?
                        client.ChooseAction(input.EventId, input.Context, defaultActions) :
                        client.ChooseAction(input.EventId, input.Context);

                    return action;
                });
        }
    }

    internal static class DecisionUtil
    {
        internal sealed class Input
        {
            internal Input(HttpRequestMessage request, string context)
            {
                this.Context = context;

                var header = request.Headers.SingleOrDefault(x => x.Key == "Authorization");

                if (header.Value == null)
                    throw new UnauthorizedAccessException("AuthorizationToken missing");

                this.MwtToken = header.Value.First();

                if (string.IsNullOrWhiteSpace(this.MwtToken))
                    throw new UnauthorizedAccessException("AuthorizationToken missing");

                this.EventId = new UniqueEventID
                {
                    Key = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                    TimeStamp = DateTime.UtcNow
                };
            }

            internal string Context { get; private set; }

            internal string MwtToken { get; private set; }

            internal UniqueEventID EventId { get; private set; }
        }

        internal static async Task<HttpResponseMessage> ChooseAction<T>(HttpRequestMessage request, string name, Func<TelemetryClient, Input, T> operation)
        {
            var telemetry = new TelemetryClient();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var input = new Input(request, await request.Content.ReadAsStringAsync());
                telemetry.Context.User.Id = input.MwtToken;

                // actual choose action call
                var action = operation(telemetry, input);

                telemetry.Context.Operation.Name = "ChooseAction";
                telemetry.Context.Operation.Id = input.EventId.Key + " " + input.EventId.TimeStamp.ToString("o", CultureInfo.InvariantCulture);

                var response = new
                {
                    Action = action,
                    EventId = input.EventId.Key,
                    TimeStamp = input.EventId.TimeStamp,
                };

                stopwatch.Stop();
                telemetry.TrackRequest(name, DateTime.Now, stopwatch.Elapsed, "200", true);

                return request.CreateResponse(
                    HttpStatusCode.OK,
                    response,
                    JsonMediaTypeFormatter.DefaultMediaType);
            }
            catch (Exception e)
            {
                telemetry.TrackException(e);
                return request.CreateResponse(HttpStatusCode.InternalServerError, e.Message);
            }
        }
    }
}

