﻿using System;
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
    public class PatientPreferenceController : ApiController
    {
        // these values should be from PEC session, but we hard code them
        private const string PharmacyId = "86362";
        private const string UserId = "UserId65536";

        // api key and such
        private const string apiurl = "https://qa-api.karmadata.com/";


        [System.Web.Http.AcceptVerbs(new string[] { "Post" })]
        public async Task<HttpResponseMessage> Merge([FromBody]KdModify modify)
        {
            try
            {
                if (modify.Entity != "PwPatientPreference") throw new ArgumentException("Entity must be PwPatientPreference");
                if (modify.Operation != "Merge") throw new ArgumentException("Operation must be Merge");
                if (!modify.Values.ContainsKey("PatientId")) throw new ArgumentException("PatientId is empty");
                if (modify.Values.ContainsKey("KdId")) throw new ArgumentException("Cannot contain KdId");
                if (modify.Values.ContainsKey("UserId")) throw new ArgumentException("UserId should come from session");
                if (modify.Values.ContainsKey("PharmacyId")) throw new ArgumentException("PharmacyId should come from session");
                modify.Values["PharmacyId"] = PharmacyId;

                string apikey = ConfigurationManager.AppSettings["ApiKey"];
                KdClient client = KdClient.ApiClient(apikey, apiurl);

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
