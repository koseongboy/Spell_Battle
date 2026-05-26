using DA_Assets.Shared.MCP;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DA_Assets.FCU.MCP
{
    public abstract class FcuMcpResourceBase : IMcpResource
    {
        protected readonly FigmaConverterUnity monoBeh;
        protected readonly McpResourceSO resourceSO;

        protected FcuMcpResourceBase(FigmaConverterUnity monoBeh, McpResourceSO resourceSO)
        {
            this.monoBeh = monoBeh;
            this.resourceSO = resourceSO;
        }

        protected virtual bool RequiresFcuContext => true;

        public string Uri => resourceSO.ResourceUri;
        public string Name => resourceSO.ResourceName;
        public string Description => resourceSO.ResourceDescription;
        public string MimeType => resourceSO.MimeType;

        public Task<IReadOnlyList<ResourceContentItem>> ReadAsync()
        {
            FigmaConverterUnity context = monoBeh;
            if (RequiresFcuContext)
            {
                context ??= FcuMcpContext.GetSelectedOrThrow();
            }

            return ReadWithContextAsync(context);
        }

        protected abstract Task<IReadOnlyList<ResourceContentItem>> ReadWithContextAsync(FigmaConverterUnity monoBeh);
    }
}
