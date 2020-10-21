using System;
using NzbDrone.Core.Indexers;

namespace Prowlarr.Api.V1.Indexers
{
    public class IndexerResource : ProviderResource
    {
        public bool EnableRss { get; set; }
        public bool EnableAutomaticSearch { get; set; }
        public bool EnableInteractiveSearch { get; set; }
        public bool SupportsRss { get; set; }
        public bool SupportsSearch { get; set; }
        public DownloadProtocol Protocol { get; set; }
        public IndexerPrivacy Privacy { get; set; }
        public IndexerCapabilities Capabilities { get; set; }
        public int Priority { get; set; }
        public DateTime Added { get; set; }
    }

    public class IndexerResourceMapper : ProviderResourceMapper<IndexerResource, IndexerDefinition>
    {
        public override IndexerResource ToResource(IndexerDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            var resource = base.ToResource(definition);

            resource.EnableRss = definition.EnableRss;
            resource.EnableAutomaticSearch = definition.EnableAutomaticSearch;
            resource.EnableInteractiveSearch = definition.EnableInteractiveSearch;
            resource.SupportsRss = definition.SupportsRss;
            resource.SupportsSearch = definition.SupportsSearch;
            resource.Capabilities = definition.Capabilities;
            resource.Protocol = definition.Protocol;
            resource.Privacy = definition.Privacy;
            resource.Priority = definition.Priority;
            resource.Added = definition.Added;

            return resource;
        }

        public override IndexerDefinition ToModel(IndexerResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            var definition = base.ToModel(resource);

            definition.EnableRss = resource.EnableRss;
            definition.EnableAutomaticSearch = resource.EnableAutomaticSearch;
            definition.EnableInteractiveSearch = resource.EnableInteractiveSearch;
            definition.Priority = resource.Priority;
            definition.Privacy = resource.Privacy;
            definition.Added = resource.Added;

            return definition;
        }
    }
}
