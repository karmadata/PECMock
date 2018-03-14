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
    public class EncounterUpdateBody
    {
        public string PatientId { get; set; }
        public string EncounterId { get; set; }
        public List<KdModify> Modifies { get; set; }
    }

    public class EncounterPersistController : ApiController
    {
        // these values should be from PEC session, but we hard code them
        private const string PharmacyId = "86362";
        private const string UserId = "UserId65536";

        // api key and such
        private const string apiurl = "https://qa-api.karmadata.com/";

        private static void ValidateBody(EncounterUpdateBody body)
        {
            if (string.IsNullOrEmpty(body.PatientId)) throw new ArgumentException("PatientId is empty");
            if (string.IsNullOrEmpty(body.EncounterId)) throw new ArgumentException("EncounterId is empty");
            if (body.PatientId.Trim() != body.PatientId) throw new ArgumentException("PatientId has blank space");
            if (body.EncounterId.Trim() != body.EncounterId) throw new ArgumentException("EncounterId has blank space");
        }

        private static void ValidateModify(KdModify modify)
        {
            if (string.IsNullOrEmpty(modify.Entity)) throw new ArgumentException("Missing Entity");
            if (string.IsNullOrEmpty(modify.Operation)) throw new ArgumentException("Missing Operation");
            if (modify.Values == null) throw new ArgumentException("Missing Values");

            if (modify.Values.ContainsKey("KdId")) throw new ArgumentException("Cannot contain KdId");
            if (modify.Values.ContainsKey("UserId")) throw new ArgumentException("UserId should come from session");
            if (modify.Values.ContainsKey("PharmacyId")) throw new ArgumentException("PharmacyId should come from session");

            if (modify.Values.ContainsKey("PatientId")) throw new ArgumentException("PatientId should not be included in each KdModify");
            if (modify.Values.ContainsKey("EncounterId")) throw new ArgumentException("EncounterId should not be included in each KdModify");
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
        public async Task<HttpResponseMessage> CreateNew([FromBody]EncounterUpdateBody body)
        {
            try
            {
                ValidateBody(body);

                string apikey = ConfigurationManager.AppSettings["ApiKey"];
                KdClient client = KdClient.ApiClient(apikey, apiurl);

                // make sure no encounter exists
                List<JObject> encounters = await QueryEncounter(client, PharmacyId, body.PatientId, body.EncounterId);
                if (encounters.Count > 0) throw new InvalidOperationException("Encounter already exists");

                // otherwise proceed to save
                var encounter = new Dictionary<string, object>();
                encounter["PharmacyId"] = PharmacyId;
                encounter["UserId"] = UserId;
                encounter["PatientId"] = body.PatientId;
                encounter["EncounterId"] = body.EncounterId;
                var modify = new KdModify()
                {
                    Operation = "Insert",
                    Entity = "PwEncounter",
                    Values = encounter
                };
                var modifyResult = await client.Request(new List<KdModify>() {modify});

                // if not success status, throw
                if (!modifyResult.IsSuccessStatusCode) throw new InvalidOperationException(Encoding.UTF8.GetString(await modifyResult.Content.ReadAsByteArrayAsync()));
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


        [System.Web.Http.AcceptVerbs(new string[] { "Post" })]
        public async Task<HttpResponseMessage> Update([FromBody]EncounterUpdateBody body)
        {
            try
            {
                var allowedEntities = new string[] { "PwEncounter", "PwEncounterAllergy", "PwEncounterCoordination", "PwEncounterEducation", "PwEncounterMedicalHistory", "PwEncounterGoal", "PwEncounterIntervention", "PwEncounterMed", "PwEncounterMedMTP", "PwEncounterMedRec", "PwEncounterReferral", "PwEncounterSocial", "PwEncounterBiometrics", "PwEncounterNote", "PwEncounterInsurance" };
                ValidateBody(body);

                string apikey = ConfigurationManager.AppSettings["ApiKey"];
                KdClient client = KdClient.ApiClient(apikey, apiurl);

                // check to ensure encounter is updatable
                List<JObject> encounters = await QueryEncounter(client, PharmacyId, body.PatientId, body.EncounterId);
                if (encounters.Count != 1) throw new InvalidOperationException("Did not find the encounter to modify. It may not exist or too many encounters may qualify for this search.");
                var encounterStatus = (string) encounters[0]["Status"];
                if (encounterStatus != "Started" && encounterStatus != "In Progress") throw new InvalidOperationException("Cannot modify an encounter that is not started or in progress");

                // prepare each object for updating
                foreach (var modify in body.Modifies)
                {
                    // check if entity is allowed
                    if (!allowedEntities.Contains(modify.Entity)) throw new InvalidOperationException("Entity not allowed: " + modify.Entity);

                    // perform checks and enforce certain values
                    ValidateModify(modify);
                    modify.Values["PharmacyId"] = PharmacyId;
                    modify.Values["UserId"] = UserId;
                    modify.Values["PatientId"] = body.PatientId;
                    modify.Values["EncounterId"] = body.EncounterId;

                    //
                    switch (modify.Entity)
                    {
                        case "PwEncounter":
                            if (modify.Operation != "Update") throw new InvalidOperationException("Can only update PwEncounter");
                            break;
                        default:
                            break;
                    }
                }

                // call modify api
                var modifyResult = await client.Request(body.Modifies);

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


        [System.Web.Http.AcceptVerbs(new string[] { "Post" })]
        public async Task<HttpResponseMessage> Delete([FromBody]EncounterUpdateBody body)
        {
            try
            {
                ValidateBody(body);

                string apikey = ConfigurationManager.AppSettings["ApiKey"];
                KdClient client = KdClient.ApiClient(apikey, apiurl);

                // make sure encounter exists
                List<JObject> encounters = await QueryEncounter(client, PharmacyId, body.PatientId, body.EncounterId);
                if (encounters.Count == 0) throw new InvalidOperationException("Encounter does not exist");

                // otherwise proceed to save
                var encounter = new Dictionary<string, object>();
                encounter["PharmacyId"] = PharmacyId;
                encounter["UserId"] = UserId;
                encounter["PatientId"] = body.PatientId;
                encounter["EncounterId"] = body.EncounterId;
                var modify = new KdModify()
                {
                    Operation = "Delete",
                    Entity = "PwEncounter",
                    Values = encounter
                };
                var modifyResult = await client.Request(new List<KdModify>() { modify });

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
