using DA_Assets.Shared.MCP;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DA_Assets.FCU.MCP
{
    public abstract class FcuMcpToolBase : IMcpTool
    {
        protected readonly FigmaConverterUnity monoBeh;
        protected readonly McpToolSO toolSO;

        protected FcuMcpToolBase(FigmaConverterUnity monoBeh, McpToolSO toolSO)
        {
            this.monoBeh = monoBeh;
            this.toolSO = toolSO;
        }

        protected virtual bool RequiresFcuContext => true;

        public string Name => toolSO.ToolName;
        public string Description => toolSO.ToolDescription;
        public InputSchema InputSchema => toolSO.Schema;

        public Task<IReadOnlyList<ContentItem>> ExecuteAsync(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();

            FigmaConverterUnity context = monoBeh;
            if (RequiresFcuContext)
            {
                context ??= FcuMcpContext.GetSelectedOrThrow();
            }

            return ExecuteWithContextAsync(context, args);
        }

        protected abstract Task<IReadOnlyList<ContentItem>> ExecuteWithContextAsync(FigmaConverterUnity monoBeh, Dictionary<string, object> args);

        protected string GetTemplate(string key) => toolSO.GetTemplate(key);

        protected string FormatTemplate(string key, params object[] args) => toolSO.FormatTemplate(key, args);
    }
}
