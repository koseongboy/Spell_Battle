using DA_Assets.FCU;
using DA_Assets.Shared.MCP;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DA_Assets.FCU.MCP
{
    public class GetFrameListTool : FcuMcpToolBase
    {
        public GetFrameListTool(FigmaConverterUnity monoBeh, McpToolSO toolSO) : base(monoBeh, toolSO)
        {
        }

        protected override Task<IReadOnlyList<ContentItem>> ExecuteWithContextAsync(FigmaConverterUnity monoBeh, Dictionary<string, object> args)
        {
            string text = FcuMcpReadContent.GetFrameListText(
                monoBeh,
                GetTemplate("empty"),
                GetTemplate("instruction"),
                GetTemplate("hierarchy_prefix"),
                GetTemplate("agent_data_prefix"));

            IReadOnlyList<ContentItem> response = new[]
            {
                new ContentItem
                {
                    Type = "text",
                    Text = text
                }
            };

            return Task.FromResult(response);
        }
    }
}
