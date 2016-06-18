using DecisionServiceWebAPI.Eval;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace DecisionServiceWebAPI
{
    public class EvaluationController : ApiController
    {
        [Route("eval")]
        public async Task<HttpResponseMessage> Post([FromUri] string windowType = "5m", [FromUri]int maxNumPolicies = 5)
        {
            var policyRegex = "Constant Policy (.*)";
            var regex = new Regex(policyRegex);

            try
            {
                // TODO: optimize perf
                string connectionString = "";
                var storageAccount = CloudStorageAccount.Parse(connectionString);
                var blobClient = storageAccount.CreateCloudBlobClient();
                var evalContainer = blobClient.GetContainerReference(ApplicationBlobConstants.OfflineEvalContainerName);
                var evalBlobs = evalContainer.ListBlobs(useFlatBlobListing: true);
                var evalData = new Dictionary<string, EvalD3>();

                foreach (var evalBlob in evalBlobs)
                {
                    var evalBlockBlob = (CloudBlockBlob)evalBlob;
                    if (evalBlockBlob != null)
                    {
                        var evalTextData = await evalBlockBlob.DownloadTextAsync();
                        var evalLines = evalTextData.Split(new string[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var l in evalLines)
                        {
                            var evalResult = JsonConvert.DeserializeObject<EvalResult>(l);
                            if (evalResult.WindowType != windowType)
                            {
                                continue;
                            }
                            string policyNumber = regex.Match(evalResult.PolicyName).Groups[1].Value;
                            int policyNumberInt;
                            if (int.TryParse(policyNumber, out policyNumberInt) && policyNumberInt > maxNumPolicies)
                            {
                                continue;
                            }
                            if (evalData.ContainsKey(evalResult.PolicyName))
                            {
                                var timeToCost = evalData[evalResult.PolicyName].values;
                                if (timeToCost.ContainsKey(evalResult.LastWindowTime))
                                {
                                    timeToCost[evalResult.LastWindowTime] = evalResult.AverageCost;
                                }
                                else
                                {
                                    timeToCost.Add(evalResult.LastWindowTime, evalResult.AverageCost);
                                }
                            }
                            else
                            {
                                evalData.Add(evalResult.PolicyName, new EvalD3 { key = evalResult.PolicyName, values = new Dictionary<DateTime, float>() });
                            }
                        }
                    }
                }
                return this.Request.CreateResponse(
                    HttpStatusCode.OK,
                    evalData.Values.Select(a => new { key = a.key, values = a.values.Select(v => new object[] { v.Key, v.Value }) }),
                    JsonMediaTypeFormatter.DefaultMediaType);
            }
            catch (Exception ex)
            {
                return this.Request.CreateResponse(HttpStatusCode.InternalServerError, $"Unable to load evaluation result: {ex.ToString()}");
            }
        }
    }
}