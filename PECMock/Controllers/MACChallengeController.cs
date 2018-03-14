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
    //public class MACChallengeSubmitBody
    //{
    //    public string PBM { get; set; }
    //    //public string PharmacyNPI { get; set; }
    //    public string RxNumber { get; set; }
    //    public string PhysicianNPI { get; set; }
    //    public string RxDate { get; set; }      // use string here because we don't know if DateTime would be handled properly?
    //    public string NdcPackageCode { get; set; }
    //    public decimal Quantity { get; set; }   // or do we use int, and can it be null?
    //    public string Unit { get; set; }
    //    public decimal DaysSupply { get; set; }
    //    public decimal Cost { get; set; }
    //    public decimal CostPerUnit { get; set; }
    //    public decimal ReimbursementRequested { get; set; }
    //    public string ReimbursementReason { get; set; }
    //}

    public class MACChallengeController : ApiController
    {
        [System.Web.Http.AcceptVerbs(new string[] { "Post" })]
        public async Task<HttpResponseMessage> Submit([FromBody]JObject submitbody)
        {
            try
            {
                if (submitbody["PBM"] == null) throw new InvalidOperationException("PBM is empty");
                if (submitbody["RxNumber"] == null) throw new InvalidOperationException("RxNumber is empty");
                // mock so if RxNumber is odd we return failure
                if (int.Parse(submitbody["RxNumber"].Value<string>()) % 2 != 0) throw new InvalidOperationException("RxNumber is odd");
                // security: we have to verify that the logged in user can submit on behalf of PharmacyNPI indicated
                return Request.CreateResponse(HttpStatusCode.OK, "success");
            }
            catch (Exception e)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, e.Message);
            }
        }
    }
}