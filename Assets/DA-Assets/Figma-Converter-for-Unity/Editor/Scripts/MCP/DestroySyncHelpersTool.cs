using DA_Assets.Shared.MCP;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DA_Assets.FCU.MCP
{
    public class DestroySyncHelpersTool : FcuMcpToolBase
    {
        public DestroySyncHelpersTool(FigmaConverterUnity monoBeh, McpToolSO toolSO) : base(monoBeh, toolSO)
        {
        }

        protected override async Task<IReadOnlyList<ContentItem>> ExecuteWithContextAsync(FigmaConverterUnity monoBeh, Dictionary<string, object> args)
        {
            int destroyedCount = await monoBeh.SyncHelpers.DestroySyncHelpersAsync();

            return new[]
            {
                new ContentItem
                {
                    Type = "text",
                    Text = FormatTemplate("success", destroyedCount, monoBeh.GetInstanceID())
                }
            };
        }
    }
}
