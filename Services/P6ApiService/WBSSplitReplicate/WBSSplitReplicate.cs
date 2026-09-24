using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Autocad_Primavera_P6_Plugin.Services.P6ApiService.WBSSplitReplicate
{
    public static class WBSSplitReplicate
    {
        public static async Task RecreateMainAndSubActivityCodes(P6ApiService p6Service, int projectObjectId)
        {
            var client = p6Service.Client;

            Project project = await GetProjectAsync_By_ObjectId(client, projectObjectId); // Fields: "ObjectId, Name, Id, WBSObjectId"

            if (project != null)
            {
                await RecreateMainAndSubActivityCodes(p6Service, project);
            }
        }

        private static async Task<Project> GetProjectAsync_By_ObjectId(Client client, int projectObjectId)
        {
            throw new NotImplementedException();
        }

        public static async Task RecreateMainAndSubActivityCodes(P6ApiService p6Service, Project project)
        {
            try
            {
                string MainWBSActCodeName = "Main WBS";

                string SubWBSActCodeName = "Sub WBS";

                Client client = p6Service.Client;

                ActivityCodeType MainWBSCodeType = await GetActivityCodeTypesAsync_By_Name_ProjectObjectId(client, MainWBSActCodeName, project.ObjectId); // Fields: "ObjectId, Name, Scope, ProjectObjectId"

                ActivityCodeType SubWBSCodeType = await GetActivityCodeTypesAsync_By_Name_ProjectObjectId(client, SubWBSActCodeName, project.ObjectId); // Fields: "ObjectId, Name, Scope, ProjectObjectId"

                List<ActivityCode> MainWBSCodes_TopLevel = await GetActivityCodesAsync_TopLevel(client, MainWBSCodeType.ObjectId); // Fields: "ObjectId, CodeValue, Description, ParentObjectId"

                List<ActivityCode> SubWBSCodes_TopLevel = await GetActivityCodesAsync_TopLevel(client, SubWBSCodeType.ObjectId); // Fields: "ObjectId, CodeValue, Description, ParentObjectId"

                bool DeleteMainWBSActivityCodesResult = await DeleteActivityCodeAsync_multiple(client, MainWBSCodes_TopLevel.Select(ac => ac.ObjectId));

                bool DeleteSubWBSActivityCodesResult = await DeleteActivityCodeAsync_multiple(client, SubWBSCodes_TopLevel.Select(ac => ac.ObjectId));

                List<WBS> allWBSs = await ReadAllWBSAsync_By_ObjectId(client, project.WBSObjectId); // Fields: "ObjectId, Code, Name, SequenceNumber"

                List<WBSNode> topLevelWBSNodes = GetTopLevelWBSTreeNodes(allWBSs);

                topLevelWBSNodes.ForEach(
                    async topWBSNode => await RecreateCodes(client, MainWBSCodeType, SubWBSCodeType, topWBSNode)
                );

            }
            catch (Exception e)
            {
                Debug.Print(e.ToString());
                MessageBox.Show(
                    "Error has occured during creation of Main and Subactivity Codes for given project." + e.ToString(),
                    "Recreation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private static async Task RecreateCodes(Client client, ActivityCodeType mainWBSCodeType, ActivityCodeType subWBSCodeType, WBSNode topWBSNode)
        {
            topWBSNode.MainSubType = MainSubTypeEnum.Main_Type;

            bool activityCodeCreateResult = await CreateMainOrSubActivityCode(client, mainWBSCodeType, subWBSCodeType, topWBSNode);

            bool FoundSplitMarker = false;

            foreach(WBSNode childNode in topWBSNode.ChildrenNodes)
            {
                if (IsSplitMarkerWBS(childNode.ThisWBS)) { FoundSplitMarker = true; continue; };

                childNode.MainSubType = FoundSplitMarker ? MainSubTypeEnum.Sub_Type : MainSubTypeEnum.Main_Type;

                await RecreateCodes_Recursive(client, mainWBSCodeType, subWBSCodeType, topWBSNode);
            }
        }

        private static async Task<bool> CreateMainOrSubActivityCode(Client client, ActivityCodeType mainWBSCodeType, ActivityCodeType subWBSCodeType, WBSNode topWBSNode)
        {
            // Can create 2 downstream methods for Main and for sub
            // Check What to Create: Main or Sub
            // Check if Parent and/or Self exist via api call
            // If creating top level code use WBSPath as value
            // If creating child code use ThisWBS.Code as value
            // Use ThisWBS.Name as code description

            throw new NotImplementedException();
        }

        private static async Task RecreateCodes_Recursive(Client client, ActivityCodeType mainWBSCodeType, ActivityCodeType subWBSCodeType, WBSNode topWBSNode)
        {
            throw new NotImplementedException();
        }

        private static bool IsSplitMarkerWBS(WBS wbs)
        {
            return wbs.Code == "SM";
        }

        private static List<WBSNode> GetTopLevelWBSTreeNodes(List<WBS> allWBSs)
        {
            throw new NotImplementedException();
        }

        private static async Task<bool> DeleteActivityCodeAsync_multiple(Client client, IEnumerable<int?> enumerable)
        {
            throw new NotImplementedException();
        }

        private static async Task<List<ActivityCode>> GetActivityCodesAsync_TopLevel(Client client, int? objectId)
        {
            throw new NotImplementedException();
        }

        private static async Task<List<WBS>> ReadAllWBSAsync_By_ObjectId(Client client, int? wBSObjectId)
        {
            throw new NotImplementedException();
        }

        private static async Task<ActivityCodeType> GetActivityCodeTypesAsync_By_Name_ProjectObjectId(Client client, string mainWBSActCodeName, int? objectId)
        {
            throw new NotImplementedException();
        }
    }

    internal enum MainSubTypeEnum{
        Main_Type,
        Sub_Type
    }

    internal class WBSNode
    {
        public MainSubTypeEnum MainSubType { get; internal set; }
        public List<WBSNode> ChildrenNodes { get; internal set; }
        public WBS ThisWBS { get; internal set; }
        public string WBSPath { get; internal set; }
    }
}
