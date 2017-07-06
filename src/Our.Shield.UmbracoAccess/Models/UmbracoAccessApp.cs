﻿namespace Our.Shield.UmbracoAccess.Models
{
    using Core.Models;
    using Core.UI;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Web;
    using System.Web.Configuration;
    using System.Web.Hosting;
    using Umbraco.Core;
    using Umbraco.Core.Configuration;
    using Umbraco.Web;
    using Umbraco.Web.Routing;

    [AppEditor("/App_Plugins/Shield.UmbracoAccess/Views/UmbracoAccess.html?v=1.0.1")]
    public class UmbracoAccessApp : App<UmbracoAccessConfiguration>
    {
        public override string Id => nameof(UmbracoAccess);

        public override string Name => "Umbraco Access";

        public override string Description => "Secure your backoffice access via IP restrictions";

        public override string Icon => "icon-stop-hand red";

        public override IConfiguration DefaultConfiguration
        {
            get
            {
                return new UmbracoAccessConfiguration
                {
                    BackendAccessUrl = ApplicationSettings.UmbracoPath,
                    IpAddresses = new IpAddress[0],
                    RedirectRewrite = Enums.RedirectRewrite.Redirect,
                    UnauthorisedUrlType = Enums.UnautorisedUrlType.Url
                };
            }
        }

        private static ReaderWriterLockSlim environemntIdsLocker = new ReaderWriterLockSlim();
        private static List<int> environmentIds = new List<int>();

        private static string allowKey = Guid.NewGuid().ToString();

        public override bool Execute(IJob job, IConfiguration c)
        {
            var config = c as UmbracoAccessConfiguration;

            if (environemntIdsLocker.TryEnterUpgradeableReadLock(1))
            {
                try
                {
                    if (!environmentIds.Contains(job.Environment.Id))
                    {
                        if (environemntIdsLocker.TryEnterWriteLock(1))
                        {
                            try
                            {
                                environmentIds.Add(job.Environment.Id);
                            }
                            finally
                            {
                                environemntIdsLocker.ExitWriteLock();
                            }
                        }
                        else
                        {
                            return false;
                        }

                        //if (ExecuteFirstTime(job, config))
                        //{
                        //    return true;
                        //}
                    }
                }
                finally
                {
                    environemntIdsLocker.ExitUpgradeableReadLock();
                }
            }
            else
            {
                return false;
            }


            job.UnwatchWebRequests();

            if (!config.Enable)
            {
                return true;
            }

            var umbracoContext = UmbracoContext.Current;

            if(umbracoContext == null)
            {
                var fakeHttpContext = new HttpContextWrapper(HttpContext.Current ?? new HttpContext(new SimpleWorkerRequest("replace-with-some-guid.aspx", "", new StringWriter())));

                umbracoContext = UmbracoContext.EnsureContext(
                    fakeHttpContext,
                    ApplicationContext.Current,
                    new Umbraco.Web.Security.WebSecurity(fakeHttpContext, ApplicationContext.Current),
                    UmbracoConfig.For.UmbracoSettings(),
                    UrlProviderResolver.Current.Providers,
                    false);
            }

            string url = string.Empty;
            var journalMessage = new JournalMessage();

            switch (config.UnauthorisedUrlType)
            {
                case Enums.UnautorisedUrlType.Url:
                    if (!string.IsNullOrEmpty(config.UnauthorisedUrl))
                    {
                        url = config.UnauthorisedUrl;
                    }
                    else
                    {
                        journalMessage.Message = "Error: No Unauthorised Url set in configuration";
                    }
                    break;

                case Enums.UnautorisedUrlType.XPath:
                    var xpathNode = umbracoContext.ContentCache.GetSingleByXPath(config.UnauthorisedUrlXPath);
                    if(xpathNode != null)
                    {
                        url = xpathNode.Url;
                    }
                    else
                    {
                        journalMessage.Message = "Error: Unable to get the unauthorised Url from the specified XPath expression.";
                    }
                    break;

                case Enums.UnautorisedUrlType.ContentPicker:
                    int id;
                    if(int.TryParse(config.UnauthorisedUrlContentPicker, out id))
                    {
                        var contentPickerNode = umbracoContext.ContentCache.GetById(id);
                        if (contentPickerNode != null)
                        {
                            url = contentPickerNode.Url;
                        }
                        else
                        {
                            journalMessage.Message = "Error: Unable to get the unauthorised Url from the selcted unauthorised url content picker. Please ensure the selected page is published and not deleted.";
                        }
                    }
                    else
                    {
                        journalMessage.Message = "Error: Unable to parse the selected unauthorised Url content picker id to integer. Please ensure a valid content node is selected.";
                    }
                    break;

                default:
                    journalMessage.Message = "Error: Unable to determine which method to use to get the unauthorised url. Please ensure Url, XPath or Content Picker is selected.";
                    break;
            }

            if (string.IsNullOrEmpty(url))
            {
                if(!string.IsNullOrEmpty(journalMessage.Message))
                {
                    job.WriteJournal(journalMessage);
                }

                return true;
            }

            var ipv6s = new List<IPAddress>();

            foreach (var ip in config.IpAddresses)
            {
                if (ip.ipAddress.Equals("127.0.0.1"))
                    ip.ipAddress = "::1";

                IPAddress tempIp;
                if (!IPAddress.TryParse(ip.ipAddress, out tempIp))
                {
                    job.WriteJournal(new JournalMessage(""));
                    continue;
                }

                ipv6s.Add(tempIp.MapToIPv6());
            }

            var currentAppBackendUrl = Umbraco.Core.IO.IOHelper.ResolveUrl(config.BackendAccessUrl.EndsWith("/") ? config.BackendAccessUrl : config.BackendAccessUrl + "/");

            if (currentAppBackendUrl != ApplicationSettings.UmbracoPath)
            {
                job.WatchWebRequests(new Regex($"^{ currentAppBackendUrl.TrimEnd('/') }(/){{0,1}}(.*){{0,1}}$", RegexOptions.IgnoreCase), 10, (count, httpApp) =>
                {
                    var rewritePath = httpApp.Context.Request.Url.AbsolutePath.Replace(currentAppBackendUrl, ApplicationSettings.UmbracoPath, StringComparison.InvariantCultureIgnoreCase);
                    httpApp.Context.Response.Clear();

                    httpApp.Context.Items.Add(allowKey, true);
                    httpApp.Context.RewritePath(rewritePath);

                    return WatchCycle.Restart;
                });

                job.WatchWebRequests(new Regex($"^{ ApplicationSettings.UmbracoPath.TrimEnd('/') }(/){{0,1}}$", RegexOptions.IgnoreCase), 20, (count, httpApp) =>
                {
                    if ((bool?)httpApp.Context.Items[allowKey] == true)
                    {
                        httpApp.Context.Items[allowKey] = false;
                        return WatchCycle.Continue;
                    }

                    if (config.RedirectRewrite == Enums.RedirectRewrite.Redirect)
                    {
                        httpApp.Context.Response.Redirect(url, true);
                        return WatchCycle.Stop;
                    }

                    httpApp.Context.RewritePath(url);
                    return WatchCycle.Restart;
                });
            }

            job.WatchWebRequests(new Regex($"^{ ApplicationSettings.UmbracoPath.TrimEnd('/') }(/){{0,1}}$", RegexOptions.IgnoreCase), 1000, (count, httpApp) =>
            {
                var userIp = GetUserIp(httpApp);

                if (userIp == null || !ipv6s.Any(x => x.Equals(userIp)))
                {
                    if (config.RedirectRewrite == Enums.RedirectRewrite.Redirect)
                    {
                        httpApp.Context.Response.Redirect(url, true);
                        return WatchCycle.Stop;
                    }
                    else
                    {
                        httpApp.Context.RewritePath(url);
                        return WatchCycle.Restart;
                    }
                }

                return WatchCycle.Continue;
            });

            return true;
        }

        private static ReaderWriterLockSlim webConfigLocker = new ReaderWriterLockSlim();

        private bool ExecuteFirstTime(IJob job, UmbracoAccessConfiguration config)
        {
            if (new Regex($"^{ config.BackendAccessUrl.Trim('~').TrimEnd('/') }(/){{0,1}}$").IsMatch(ApplicationSettings.UmbracoPath))
            {
                return false;
            }

            if (!webConfigLocker.TryEnterUpgradeableReadLock(1))
            {
                return true;
            }

            try
            {
                var path = HttpRuntime.AppDomainAppPath;
                if (!System.IO.Directory.Exists(path + ApplicationSettings.UmbracoPath.TrimEnd('/')))
                {
                    job.WriteJournal(new JournalMessage($"Unable to Rename and/or move directory from: { ApplicationSettings.UmbracoPath } to: { config.BackendAccessUrl }\nThe directory {ApplicationSettings.UmbracoPath} cannot be found"));
                    return false;
                }

                var webConfig = WebConfigurationManager.OpenWebConfiguration("~");
                var appSettings = (AppSettingsSection)webConfig.GetSection("appSettings");

                var umbracoPath = appSettings.Settings["umbracoPath"];
                var umbracoReservedPaths = appSettings.Settings["umbracoReservedPaths"];

                var regex = new Regex($"^({ umbracoPath.Value.Trim('~').TrimEnd('/') }(/){{0,1}})$");

                if (!regex.IsMatch(ApplicationSettings.UmbracoPath) && !regex.IsMatch(umbracoReservedPaths.Value))
                {
                    job.WriteJournal(new JournalMessage($"Unable to make neccessary changes to the web.config, appSetting keys: umbracoPath & umbracoReservedPaths doesn't contain the expected values"));
                    return false;
                }

                System.IO.Directory.Move(path + ApplicationSettings.UmbracoPath.Trim('/'), path + config.BackendAccessUrl.Trim('/'));

                umbracoReservedPaths.Value = umbracoReservedPaths.Value.Replace(umbracoPath.Value.TrimEnd('/'), config.BackendAccessUrl);
                umbracoPath.Value = $"~{config.BackendAccessUrl.TrimEnd('/')}";
                webConfig.Save();
            }
            catch(Exception ex)
            {
                job.WriteJournal(new JournalMessage($"Unexpected error occured, exception:\n{ex.Message}"));
                return false;
            }
            finally
            {
                webConfigLocker.ExitUpgradeableReadLock();
            }

            if (HttpContext.Current != null)
            {
                var response = HttpContext.Current.Response;
                response.Clear();
                response.Redirect(HttpContext.Current.Request.Url.ToString(), true);
            }

            HttpRuntime.UnloadAppDomain();

            return true;
        }

        private IPAddress GetUserIp(HttpApplication app)
        {
            var ip = app.Context.Request.UserHostAddress;

            if (ip.Equals("127.0.0.1"))
                ip = "::1";

            IPAddress tempIp;

            if(IPAddress.TryParse(ip, out tempIp))
            {
                return tempIp.MapToIPv6();
            }

            return null;
        }
    }
}