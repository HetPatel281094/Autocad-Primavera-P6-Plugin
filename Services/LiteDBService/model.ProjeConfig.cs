using Autodesk.AutoCAD.ApplicationServices;
using LiteDB;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Autocad_Primavera_P6_Plugin.Services.LiteDBService
{
    [AddINotifyPropertyChangedInterface]
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
        private readonly string projectConfigsCollectionName = "ProjectConfigsColn";

        private void init_ProjectConfigsColn()
        {
            if (!this.pluginDb.CollectionExists(this.projectConfigsCollectionName))
            {
                var ProjectConfigsColn = this.pluginDb.GetCollection<ProjectConfig>(this.projectConfigsCollectionName);
                ProjectConfigsColn.EnsureIndex(x => x.LinkName, unique: true);
                ProjectConfigsColn.EnsureIndex(x => x.ProjectObjectId, unique: true);
                ProjectConfigsColn.EnsureIndex(x => x.ProjectId, unique: true);
            }
        }

        /// <summary>
        /// Inserts or updates a ProjectConfig.
        /// LiteDB Upsert assigns a new Id in-place when Id == 0 (new record),
        /// so the returned object will have its Id populated after insertion.
        /// </summary>
        public ProjectConfig CreateNew_ProjectConfig(ProjectConfig new_ProjectConfig)
        {
            var _colln = pluginDb.GetCollection<ProjectConfig>(projectConfigsCollectionName);
            _colln.Upsert(new_ProjectConfig);
            // LiteDB modifies new_ProjectConfig.Id in-place on insert, so returning
            // the same object gives the caller the newly assigned Id.
            return new_ProjectConfig;
        }

        /// <summary>Returns all saved ProjectConfigs ordered by Id.</summary>
        public List<ProjectConfig> GetAll_ProjectConfigs()
        {
            var _colln = pluginDb.GetCollection<ProjectConfig>(projectConfigsCollectionName);
            return _colln.FindAll().OrderBy(c => c.Id).ToList();
        }

        /// <summary>
        /// Deletes the ProjectConfig with the given Id.
        /// Returns true if a record was removed, false if the Id was not found.
        /// </summary>
        public bool Delete_ProjectConfig(int id)
        {
            var _colln = pluginDb.GetCollection<ProjectConfig>(projectConfigsCollectionName);
            return _colln.Delete(id);
        }

        /// <summary>
        /// Finds the ProjectConfig whose DWG folder path matches or is a parent
        /// of the folder containing the given AutoCAD document.
        /// Returns null when no matching config exists.
        /// </summary>
        public ProjectConfig Find_byAcadDWG(Document document)
        {
            if (document == null) return null;

            var rawDir = Path.GetDirectoryName(document.Name);   // can return null for unsaved docs
            if (string.IsNullOrWhiteSpace(rawDir)) return null;

            string docFolderPath;
            try
            {
                // GetFullPath normalises separators, strips trailing slashes,
                // and resolves any '.' / '..' segments in one call.
                docFolderPath = Path.GetFullPath(rawDir);
            }
            catch (Exception)   // ArgumentException, PathTooLongException, SecurityException, …
            {
                return null;
            }

            var colln = pluginDb.GetCollection<ProjectConfig>(projectConfigsCollectionName);

            // Materialise to IEnumerable so LiteDB doesn't try to translate
            // the private helper into a BsonExpression.
            return colln.FindAll()
                        .FirstOrDefault(config =>
                            IsSameOrChildPath(config.ProjectPlanningDWGFolderPath, docFolderPath));
        }

        private static bool IsSameOrChildPath(string parentPath, string childPath)
        {
            if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(childPath))
                return false;

            string normalizedParent, normalizedChild;
            try
            {
                // GetFullPath makes both paths fully canonical and uses a single,
                // consistent separator — no need to check both '\\' and '/'.
                normalizedParent = Path.GetFullPath(parentPath);
                normalizedChild = Path.GetFullPath(childPath);
            }
            catch (Exception)
            {
                return false;
            }

            // Strip any trailing separator so both sides are in the same form.
            normalizedParent = normalizedParent.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            normalizedChild = normalizedChild.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Exact match  -OR-  child lives under parent.
            // The appended DirectorySeparatorChar prevents "C:\Foo" from
            // falsely matching "C:\FooBar".
            return string.Equals(normalizedParent, normalizedChild,
                                 StringComparison.OrdinalIgnoreCase)
                || normalizedChild.StartsWith(
                       normalizedParent + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
