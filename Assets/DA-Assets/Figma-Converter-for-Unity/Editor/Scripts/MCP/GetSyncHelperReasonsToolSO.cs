using DA_Assets.Shared.MCP;
using UnityEngine;

namespace DA_Assets.FCU.MCP
{
    [CreateAssetMenu(fileName = "GetSyncHelperReasonsTool", menuName = "D.A. Assets/FCU/MCP Tools/Get SyncHelper Reasons")]
    public class GetSyncHelperReasonsToolSO : McpToolSO
    {
        public override IMcpTool CreateInstance(object context)
        {
            return new GetSyncHelperReasonsTool(context as FigmaConverterUnity, this);
        }
    }
}
