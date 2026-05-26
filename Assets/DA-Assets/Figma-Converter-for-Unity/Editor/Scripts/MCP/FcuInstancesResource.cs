using DA_Assets.Shared.MCP;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DA_Assets.FCU.MCP
{
    public class FcuInstancesResource : FcuMcpResourceBase
    {
        public FcuInstancesResource(FigmaConverterUnity monoBeh, McpResourceSO resourceSO) : base(monoBeh, resourceSO)
        {
        }

        protected override bool RequiresFcuContext => false;

        protected override Task<IReadOnlyList<ResourceContentItem>> ReadWithContextAsync(FigmaConverterUnity monoBeh)
        {
            IReadOnlyList<ResourceContentItem> response = new[]
            {
                new ResourceContentItem
                {
                    Uri = Uri,
                    MimeType = MimeType,
                    Text = FcuMcpReadContent.GetInstancesJson()
                }
            };

            return Task.FromResult(response);
        }
    }
}
