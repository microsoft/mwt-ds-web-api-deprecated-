using Microsoft.Azure;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.Contract;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using VW;
using VW.Serializer;

namespace DecisionServiceWebAPI
{
    public class VowpalWabbitValidationController : ApiController
    {
        private ApplicationClientMetadata metaData;
        private DateTime lastDownload;

        public async Task<HttpResponseMessage> Post()
        {
            var header = this.Request.Headers.SingleOrDefault(x => x.Key == "Authorization");

            if (header.Value == null)
                throw new UnauthorizedAccessException("AuthorizationToken missing");

            var userToken = header.Value.First();

            if (string.IsNullOrWhiteSpace(userToken))
                throw new UnauthorizedAccessException("AuthorizationToken missing");

            if (this.metaData == null || lastDownload + TimeSpan.FromMinutes(1) < DateTime.Now)
            {
                this.metaData = ApplicationMetadataUtil.DownloadMetadata<ApplicationClientMetadata>(CloudConfigurationManager.GetSetting("DecisionServiceTrainerSettingsUrl"));
                lastDownload = DateTime.Now;
            }

            using (var vw = new VowpalWabbit(new VowpalWabbitSettings(metaData.TrainArguments)
            {
                EnableStringExampleGeneration = true,
                EnableStringFloatCompact = true
            }))
            using (var serializer = new VowpalWabbitJsonSerializer(vw))
            using (var example = serializer.ParseAndCreate(new JsonTextReader(new StreamReader(await Request.Content.ReadAsStreamAsync()))))
            {
                return Request.CreateResponse(HttpStatusCode.OK, example.VowpalWabbitString);
            }
        }
    }
}