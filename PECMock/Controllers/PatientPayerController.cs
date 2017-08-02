using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.IO;
using System.Threading.Tasks;
using System.Configuration;
using KarmaData.Api.Models;
using KarmaData.Api.Models.Base.Request;
using KarmaData.Api.Models.PW;
using KarmaData.Util.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PECMock.Utility;

namespace PECMock.Controllers
{
    public class PatientPayerController : ApiController
    {
        // these values should be from PEC session, but we hard code them
        private const string PharmacyId = "86362";
        // private const string UserId = "UserId65536";

        // api key and such
        private const string apiurl = "https://qa-api.karmadata.com/";

        private static async Task<List<JObject>> QueryPayerPlan(KdClient client, string payerid)
        {
            KdQuery query = KdQuery.Search(PwEntity.PwPayerPlan)
                .FilterGroup()
                .And(PwEntity.PwPayerPlan, "KdId", "String", KdRequestOperator.Eq, payerid);
            // obtains result
            var result = await client.Request2Objects<JObject>(query);

            // throw error if not success status
            if (!result.IsSuccessStatusCode) throw new InvalidOperationException("Cannot query API");
            return result.Entities;
        }


        [System.Web.Http.AcceptVerbs(new string[] { "Post" })]
        public async Task<HttpResponseMessage> Add([FromBody]KdModify modify)
        {
            try
            {
                if (modify.Entity != "PwPatientPayerPlan") throw new ArgumentException("Entity must be PwPatientPayerPlan");
                if (modify.Operation != "Insert") throw new ArgumentException("Operation must be Insert");
                if (!modify.Values.ContainsKey("PatientId")) throw new ArgumentException("PatientId is empty");
                if (modify.Values.ContainsKey("KdId")) throw new ArgumentException("Cannot contain KdId");
                if (modify.Values.ContainsKey("UserId")) throw new ArgumentException("UserId should come from session");
                if (modify.Values.ContainsKey("PharmacyId")) throw new ArgumentException("PharmacyId should come from session");
                if (!modify.Values.ContainsKey("PwPayerPlan.BIN")) throw new ArgumentException("PwPayerPlan.BIN is empty");
                if (!modify.Values.ContainsKey("PwPayerPlan.PCN")) throw new ArgumentException("PwPayerPlan.PCN is empty");
                if (!modify.Values.ContainsKey("PwPayerPlan.GroupId")) throw new ArgumentException("PwPayerPlan.GroupId is empty");
                if (!modify.Values.ContainsKey("PayerMemberId")) throw new ArgumentException("PayerMemberId is empty");
                modify.Values["PharmacyId"] = PharmacyId;
                string payerId = $"{modify.Values["PwPayerPlan.BIN"]}.{modify.Values["PwPayerPlan.PCN"]}.{modify.Values["PwPayerPlan.GroupId"]}";
                modify.Values["PwPayerPlan.KdId"] = payerId;

                string apikey = ConfigurationManager.AppSettings["ApiKey"];
                KdClient client = KdClient.ApiClient(apikey, apiurl);

                var modifies = new List<KdModify>();
                // check if plan already exists
                List<JObject> payerplans = await QueryPayerPlan(client, payerId);
                if (payerplans.Count == 0)
                {
                    // does not exist, also insert into PwPayerPlan
                    KdModify payerInsert = new KdModify();
                    payerInsert.Entity = "PwPayerPlan";
                    payerInsert.Operation = "Insert";
                    payerInsert.Values = new Dictionary<string, object>();
                    payerInsert.Values["KdId"] = payerId;
                    payerInsert.Values["BIN"] = modify.Values["PwPayerPlan.BIN"];
                    payerInsert.Values["PCN"] = modify.Values["PwPayerPlan.PCN"];
                    payerInsert.Values["GroupId"] = modify.Values["PwPayerPlan.GroupId"];
                    payerInsert.Values["PlanName"] = modify.Values["PwPayerPlan.PlanName"];
                    payerInsert.Values["PlanType"] = modify.Values["PwPayerPlan.PlanType"];
                    payerInsert.Values["PharmacyBenefitManager"] = modify.Values["PwPayerPlan.PharmacyBenefitManager"];

                    modifies.Add(payerInsert);
                }

                // remove data from modify and add it to list of modifies
                modify.Values.Remove("PwPayerPlan.PlanName");
                modify.Values.Remove("PwPayerPlan.PlanType");
                modify.Values.Remove("PwPayerPlan.PharmacyBenefitManager");
                modifies.Add(modify);

                // now insert into PwPatientPayerPlan
                var modifyResult = await client.Request(modifies);

                // if not success status, throw
                if (!modifyResult.IsSuccessStatusCode) throw new InvalidOperationException(Encoding.UTF8.GetString(await modifyResult.Content.ReadAsByteArrayAsync()));
                var jsonString = Encoding.UTF8.GetString(await modifyResult.Content.ReadAsByteArrayAsync());
                var modifyresponse = JsonConvert.DeserializeObject<JObject>(jsonString);
                if (((bool?)modifyresponse["success"]) != true) throw new InvalidOperationException((string)modifyresponse["error"]);

                return Request.CreateResponse(HttpStatusCode.OK, "success");
            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, e.Message);
            }
        }
    }
}
