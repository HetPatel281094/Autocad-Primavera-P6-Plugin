using Autodesk.AutoCAD.ApplicationServices;
using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Autocad_Primavera_P6_Plugin.Services.LiteDBService
{

    public class ProjectConfig
    {
        [BsonId]
        public int Id { get; set; }
        public string LinkName { get; set; }
        public string ProjectObjectId { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ProjectPlanningDWGFolderPath { get; set; }
    };
    public partial class LiteDBService
    {
        private string projectConfigsCollectionName = "ProjectConfigsColn";
        private void init_ProjectConfigsColn()
        {
            if (!this.pluginDb.CollectionExists(this.projectConfigsCollectionName))
            {
                var ProjectConfigsColn = this.pluginDb.GetCollection<ProjectConfig>(this.projectConfigsCollectionName);
                ProjectConfigsColn.EnsureIndex(x => x.ProjectObjectId, unique: true);
            }
        }
        public ProjectConfig CreateNew_ProjectConfig(ProjectConfig new_ProjectConfig)
        {
            var _db = pluginDb;
            var _colln = _db.GetCollection<ProjectConfig>(projectConfigsCollectionName);
            var _insertedId = _colln.Insert(new_ProjectConfig);
            if (_insertedId == 0 || _insertedId == null)
            {
                return null;
            }
            var _insertedConfig = _colln.FindById(_insertedId);
            return _insertedConfig;
        }
        public List<ProjectConfig> GetAll_ProjectConfigs() 
        {
            var _db = pluginDb;
            var _colln = _db.GetCollection<ProjectConfig>(projectConfigsCollectionName);
            var _allConfigs  = _colln.FindAll().ToList();
            return _allConfigs;
        }

        public ProjectConfig Find_byAcadDWG(Document document)
        {
            if (document == null)
            {
                return null;
            };

            var _db = pluginDb;
            var _colln = _db.GetCollection<ProjectConfig>(projectConfigsCollectionName);

            var _docPath = document.Name;
            var _docFolderPath = Path.GetDirectoryName(_docPath);
            var _docFolderPathNr = _docFolderPath.Trim().TrimEnd('\\', '/');

            var _projectConfig = _colln.FindOne(
                config => IsSameOrChildPath(config.ProjectPlanningDWGFolderPath, _docFolderPathNr)
                );

            return _projectConfig;
        }

        private bool IsSameOrChildPath(string parentPath, string childPath)
        {
            var normalizedParentPath = parentPath.Trim().TrimEnd('\\', '/');
            var normalizedChildPath = childPath.Trim().TrimEnd('\\', '/');

            return string.Equals(normalizedParentPath, normalizedChildPath, StringComparison.OrdinalIgnoreCase)
                || normalizedChildPath.StartsWith(
                    normalizedParentPath + "\\",
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
