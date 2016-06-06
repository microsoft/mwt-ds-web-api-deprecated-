using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Configuration;
using System.Web.Http;

namespace DecisionServiceWebAPI
{
    public class Global : System.Web.HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            TelemetryConfiguration.Active.InstrumentationKey = ConfigurationManager.AppSettings["APPINSIGHTS_INSTRUMENTATIONKEY"];
            TelemetryConfiguration.Active.TelemetryChannel.DeveloperMode = true;

            GlobalConfiguration.Configure(config =>
            {
                config.MapHttpAttributeRoutes();

                config.Routes.IgnoreRoute("Html", "{file}.html");

                config.Routes.MapHttpRoute(
                    name: "DefaultApi",
                    routeTemplate: "{controller}"
                );
            });
        }
    }
}