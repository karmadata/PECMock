using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.IO;
using System.Threading.Tasks;
using KarmaData.Api.Models;
using KarmaData.Api.Models.Base.Request;
using KarmaData.Api.Models.PW;
using KarmaData.Util.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PECMock.Utility;

namespace PECMock.Controllers
{
    public class EncounterPersistController : ApiController
    {
        // these values should be from PEC session, but we hard code them
        private const string PharmacyId = "86362";
        private const string UserId = "UserId65536";

        // api key and such
        private const string apiurl = "https://qa-api.karmadata.com/";

        //// GET: api/EncounterPersist
        //public IEnumerable<string> Get()
        //{
        //    return new string[] { "value1", "value2" };
        //}

        //// GET: api/EncounterPersist/5
        //public HttpResponseMessage Get(int id)
        //{
        //    return Request.CreateResponse(HttpStatusCode.OK, "Value");
        //}

        // POST: api/EncounterPersist
        public void Post([FromBody]string value)
        {
        }

        // PUT: api/EncounterPersist/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/EncounterPersist/5
        public void Delete(int id)
        {
        }


        private static string GetApiKey()
        {
            return File.ReadAllText("apikey.txt").Trim();
        }

        private static void ValidateObject(Dictionary<string, object> obj)
        {
            if (obj.ContainsKey("KdId")) throw new ArgumentException("Cannot contain KdId");
            if (obj.ContainsKey("UserId")) throw new ArgumentException("UserId should come from session");
            if (obj.ContainsKey("PharmacyId")) throw new ArgumentException("PharmacyId should come from session");

            if (!obj.ContainsKey("PatientId")) throw new ArgumentException("Missing PatientId");
            if (!obj.ContainsKey("EncounterId")) throw new ArgumentException("Missing EncounterId");
        }

        private static async Task<List<JObject>> QueryEncounter(KdClient client, string pharmacyId, string patientId, string encounterId)
        {
            KdQuery query = KdQuery.Search(PwEntity.PwEncounter)
                .FilterGroup()
                .And(PwEntity.PwEncounter, "PharmacyId", "String", KdRequestOperator.Eq, pharmacyId)
                .And(PwEntity.PwEncounter, "PatientId", "String", KdRequestOperator.Eq, patientId)
                .And(PwEntity.PwEncounter, "EncounterId", "String", KdRequestOperator.Eq, encounterId);
            // obtains result
            var result = await client.Request2Objects<JObject>(query);

            // throw error if not success status
            if (!result.IsSuccessStatusCode) throw new InvalidOperationException("Cannot query API");
            return result.Entities;
        }


        [System.Web.Http.AcceptVerbs(new string[] { "Post" })]
        public async Task<HttpResponseMessage> CreateNew([FromBody]Dictionary<string, object> encounter)
        {
            try
            {
                ValidateObject(encounter);
                string patientId = ((string)encounter["PatientId"]).Trim();
                string encounterId = ((string)encounter["EncounterId"]).Trim();
                if (string.IsNullOrEmpty(patientId)) throw new ArgumentException("PatientId cannot be empty");
                if (string.IsNullOrEmpty(encounterId)) throw new ArgumentException("EncounterId cannot be empty");

                string apikey = (string)Config.Read("apikey")["apikey"];
                KdClient client = KdClient.ApiClient(apikey, apiurl);

                // make sure no encounter exists
                List<JObject> encounters = await QueryEncounter(client, PharmacyId, patientId, encounterId);
                if (encounters.Count > 0) throw new InvalidOperationException("Encounter already exists");

                // otherwise proceed to save
                encounter["PharmacyId"] = PharmacyId;
                encounter["UserId"] = UserId;
                var modify = new KdModify()
                {
                    Operation = "Insert",
                    Entity = "PwEncounter",
                    Values = encounter
                };
                var modifyResult = await client.Request(new List<KdModify>() {modify});

                // if not success status, throw
                if (!modifyResult.IsSuccessStatusCode) throw new InvalidOperationException("Modify API failed");
                var jsonString = Encoding.UTF8.GetString(await modifyResult.Content.ReadAsByteArrayAsync());
                var modifyresponse = JsonConvert.DeserializeObject<JObject>(jsonString);
                if (((bool?) modifyresponse["success"]) != true) throw new InvalidOperationException((string)modifyresponse["error"]);

                return Request.CreateResponse(HttpStatusCode.OK, "success");
            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, e.Message);
            }
        }
    }
}
