using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
        [Route("policy")]
        public async Task<HttpResponseMessage> Post([FromUri] int defaultAction = -1)
        {
            return await DecisionUtil.ChooseAction(
                this.Request,
                "Policy",
                (telemetry, input) =>
                {
                    var url = ConfigurationManager.AppSettings["DecisionServiceSettingsUrl"];
                    var client = DecisionServiceClientFactory.AddOrGetExisting(url);
                    return defaultAction != -1 ?
                        client.ChooseAction(input.EventId, input.Context, defaultAction) :
                        client.ChooseAction(input.EventId, input.Context);
                });
        }
    }

    public class DecisionRankerController : ApiController
    {
        // POST 
        [Route("ranker")]
        public async Task<HttpResponseMessage> Post([FromUri] int[] defaultActions)
        {
            return await DecisionUtil.ChooseAction(
                this.Request,
                "Ranker",
                (telemetry, input) =>
                {
                    var url = ConfigurationManager.AppSettings["DecisionServiceSettingsUrl"];
                    var client = DecisionServiceClientFactory.AddOrGetExisting(url);
                    var action = defaultActions != null && defaultActions.Length > 0 ?
                        client.ChooseRanking(input.EventId, input.Context, defaultActions) :
                        client.ChooseRanking(input.EventId, input.Context);

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

                this.UserToken = header.Value.First();

                if (string.IsNullOrWhiteSpace(this.UserToken))
                    throw new UnauthorizedAccessException("AuthorizationToken missing");

                if (this.UserToken != ConfigurationManager.AppSettings["UserToken"])
                    throw new UnauthorizedAccessException();

                this.EventId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            }

            internal string Context { get; private set; }

            internal string UserToken { get; private set; }

            internal string EventId { get; private set; }
        }

        internal static async Task<HttpResponseMessage> ChooseAction<T>(HttpRequestMessage request, string name, Func<TelemetryClient, Input, T> operation)
        {
            var telemetry = new TelemetryClient();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var input = new Input(request, await request.Content.ReadAsStringAsync());

                // actual choose action call
                var action = operation(telemetry, input);

                telemetry.Context.Operation.Name = "ChooseAction";
                telemetry.Context.Operation.Id = input.EventId.ToString();

                var response = new
                {
                    Action = action,
                    EventId = input.EventId
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

