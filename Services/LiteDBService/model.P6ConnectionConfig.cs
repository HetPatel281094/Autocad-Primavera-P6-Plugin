using LiteDB;
using PropertyChanged;
using System.Collections.Generic;
using System.Linq;

namespace Autocad_Primavera_P6_Plugin.Services.LiteDBService
{
    [AddINotifyPropertyChangedInterface]
    public class P6ConnectionConfig
    {
        [BsonId]
        public int Id { get; set; }
        public string ConnectionName { get; set; }
        public string ServerUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string DatabaseName { get; set; }
        public bool IsDefault { get; set; }
    }

    public partial class LiteDBService
    {
        private readonly string p6ConnectionConfigsCollectionName = "P6ConnectionConfigsColn";

        private void init_P6ConnectionConfigsColn()
        {
            if (!this.pluginDb.CollectionExists(this.p6ConnectionConfigsCollectionName))
            {
                var coln = this.pluginDb.GetCollection<P6ConnectionConfig>(
                    p6ConnectionConfigsCollectionName);
                coln.EnsureIndex(x => x.ConnectionName, unique: true);
            }
        }

        public P6ConnectionConfig Upsert_P6ConnectionConfig(P6ConnectionConfig config)
        {
            var coln = pluginDb.GetCollection<P6ConnectionConfig>(
                p6ConnectionConfigsCollectionName);
            coln.Upsert(config);
            return config;
        }

        public List<P6ConnectionConfig> GetAll_P6ConnectionConfigs()
        {
            var coln = pluginDb.GetCollection<P6ConnectionConfig>(
                p6ConnectionConfigsCollectionName);
            return coln.FindAll().OrderBy(c => c.Id).ToList();
        }

        public bool Delete_P6ConnectionConfig(int id)
        {
            var coln = pluginDb.GetCollection<P6ConnectionConfig>(
                p6ConnectionConfigsCollectionName);
            return coln.Delete(id);
        }

        public P6ConnectionConfig GetDefault_P6ConnectionConfig()
        {
            var coln = pluginDb.GetCollection<P6ConnectionConfig>(
                p6ConnectionConfigsCollectionName);
            return coln.FindAll().FirstOrDefault(c => c.IsDefault);
        }

        public void SetDefault_P6ConnectionConfig(int id)
        {
            var coln = pluginDb.GetCollection<P6ConnectionConfig>(
                p6ConnectionConfigsCollectionName);
            foreach (var c in coln.FindAll())
            {
                c.IsDefault = (c.Id == id);
                coln.Update(c);
            }
        }
    }
}