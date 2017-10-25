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
    public class GenericModifyController : ApiController
    {
        // these values should be from PEC session, but we hard code them
        private const string PharmacyId = "86362";
        // generic controller cannot handle UserId for now

        // api key and such
        private const string apiurl = "https://qa-api.karmadata.com/";

        // allowable lookup
        private string[] allowedEntities = new string[] { "PwProgramEngagement" };
        private string[] allowedOperations = new string[] { "Insert", "Update", "Merge", "Delete" };

        [System.Web.Http.AcceptVerbs(new string[] { "Post" })]
        public async Task<HttpResponseMessage> Modify([FromBody]List<KdModify> modifies)
        {
            try
            {
                // loop through each modify to ensure conditions are correct
                foreach (var modify in modifies)
                {
                    if (!allowedEntities.Contains(modify.Entity)) throw new InvalidOperationException("Entity not modifiable: " + modify.Entity);
                    if (!allowedOperations.Contains(modify.Operation)) throw new InvalidOperationException("Operation not allowed: " + modify.Operation);
                    if (!modify.Values.ContainsKey("PatientId")) throw new ArgumentException("PatientId is empty");
                    if (modify.Values.ContainsKey("KdId")) throw new ArgumentException("Cannot contain KdId");
                    if (modify.Values.ContainsKey("UserId")) throw new ArgumentException("UserId should come from session");
                    if (modify.Values.ContainsKey("PharmacyId")) throw new ArgumentException("PharmacyId should come from session");

                    // inject session specific data
                    modify.Values["PharmacyId"] = PharmacyId;
                }
                
                // we are not verifying that the specific patient exists, should we?
                string apikey = ConfigurationManager.AppSettings["ApiKey"];
                KdClient client = KdClient.ApiClient(apikey, apiurl);

                var modifyResult = await client.Request(modifies);

                // if not success status, throw
                if (!modifyResult.IsSuccessStatusCode) throw new InvalidOperationException(Encoding.UTF8.GetString(await modifyResult.Content.ReadAsByteArrayAsync()));
                var jsonString = Encoding.UTF8.GetString(await modifyResult.Content.ReadAsByteArrayAsync());
                var modifyresponse = JsonConvert.DeserializeObject<JObject>(jsonString);
                if (((bool?)modifyresponse["success"]) != true) throw new InvalidOperationException(jsonString);

                return Request.CreateResponse(HttpStatusCode.OK, jsonString);
            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, e.Message);
            }
        }
    }
}
