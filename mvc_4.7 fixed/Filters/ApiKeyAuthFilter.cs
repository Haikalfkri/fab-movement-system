using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;           
using System.Web.Http.Controllers; 
using System.Web.Http.Filters;   
using ok1.Models.ApiResponse;

namespace FabReport.Filters
{
    public class ApiKeyAuthFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            try
            {
                IEnumerable<string> apiKeyValues;
                if (!actionContext.Request.Headers.TryGetValues("X-Api-Key", out apiKeyValues))
                {
                    actionContext.Response = actionContext.Request.CreateResponse(
                        HttpStatusCode.Unauthorized,
                        new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Unauthorized.",
                            Error = "API Key is required."
                        }
                    );
                    return;
                }

                var apiKey = apiKeyValues.FirstOrDefault();
                var validApiKey = ConfigurationManager.AppSettings["ExternalSystemApiKey"];

                if (string.IsNullOrEmpty(apiKey) || apiKey != validApiKey)
                {
                    actionContext.Response = actionContext.Request.CreateResponse(
                        HttpStatusCode.Unauthorized,
                        new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Unauthorized.",
                            Error = "Invalid API Key."
                        }
                    );
                    return;
                }

                base.OnActionExecuting(actionContext);
            }
            catch (Exception ex)
            {
                actionContext.Response = actionContext.Request.CreateResponse(
                    HttpStatusCode.InternalServerError,
                    new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Internal server error.",
                        Error = ex.Message
                    }
                );
            }
        }
    }
}