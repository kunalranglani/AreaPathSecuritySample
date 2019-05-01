using CommandLine;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Graph.Client;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Identity.Client;
using Microsoft.VisualStudio.Services.Security;
using Microsoft.VisualStudio.Services.Security.Client;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AreaPathSecuritySample
{
    class Program
    {
        static Guid securityNamespaceId = new Guid("83e28ad4-2d72-4ceb-97b0-c7726d5502c3");

        static void Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args);

            string accountUrl = null;
            string projectName = null;
            string areaPathName = null;
            string groupName = null;

            result.WithParsed((options) =>
            {
                accountUrl = options.AccountUrl;
                projectName = options.ProjectName;
                areaPathName = options.AreaPath;
                groupName = options.GroupName;
            });

            result.WithNotParsed((e) =>
            {
                Console.WriteLine("Usage: AreaPathSecuritySample.exe -a yourAccountUrl -p yourPojectName -c yourAreaPathName -g yourGroupName");
                Environment.Exit(0);
            });

            Console.WriteLine("You might see a login screen if you have never signed in to your account using this app.");

            VssConnection connection = new VssConnection(new Uri(accountUrl), new VssClientCredentials());

            // Get the team project
            TeamProject project = GetProject(connection, projectName);

            // Get the area path
            WorkItemTrackingHttpClient workClient = connection.GetClient<WorkItemTrackingHttpClient>();
            WorkItemClassificationNode areaPath = workClient.GetClassificationNodeAsync(project.Id, TreeStructureGroup.Areas, path: areaPathName).Result;

            // Get the group
            Identity group = GetGroup(connection, groupName);

            // Get the acls for the area path
            SecurityHttpClient securityClient = connection.GetClient<SecurityHttpClient>();
            IEnumerable<AccessControlList> acls = securityClient.QueryAccessControlListsAsync(securityNamespaceId, null, null, false, false).Result;

            AccessControlList areaPathAcl = acls.FirstOrDefault(x => x.Token.EndsWith(areaPath.Identifier.ToString()));

            // Add group to the area path security with read/write perms for work items in this area path
            AccessControlEntry entry = new AccessControlEntry(group.Descriptor, 48, 0, null);
            var aces = securityClient.SetAccessControlEntriesAsync(securityNamespaceId, areaPathAcl.Token, new List<AccessControlEntry> { entry }, false).Result;

            Console.WriteLine("Successfully added your group to the area path.");
        }

        private static TeamProject GetProject(VssConnection connection, string projectName)
        {
            ProjectHttpClient projectClient = connection.GetClient<ProjectHttpClient>();
            IEnumerable<TeamProjectReference> projects = projectClient.GetProjects(top: 10000).Result;

            TeamProjectReference project = projects.FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

            return projectClient.GetProject(project.Id.ToString(), true).Result;
        }

        private static Identity GetGroup(VssConnection connection, string groupName)
        {
            GraphHttpClient graphClient = connection.GetClient<GraphHttpClient>();
            PagedGraphGroups groups = graphClient.ListGroupsAsync().Result;

            // This program assumes that the group we need is in the first batch of groups returned by the api. Ideally you need to page through
            // the api results to find your group.
            GraphGroup group = groups.GraphGroups.FirstOrDefault(x => x.DisplayName.Equals(groupName, StringComparison.OrdinalIgnoreCase));

            GraphStorageKeyResult storageKey = graphClient.GetStorageKeyAsync(group.Descriptor).Result;

            Guid id = storageKey.Value;

            IdentityHttpClient identityClient = connection.GetClient<IdentityHttpClient>();
            return identityClient.ReadIdentityAsync(id).Result;
        }
    }
}
