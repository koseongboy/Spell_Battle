using DA_Assets.Shared.MCP;
using UnityEngine;

namespace DA_Assets.FCU.MCP
{
    [CreateAssetMenu(fileName = "FcuInstancesResource", menuName = "D.A. Assets/FCU/MCP Resources/Instances")]
    public class FcuInstancesResourceSO : McpResourceSO
    {
        public override IMcpResource CreateInstance(object context)
        {
            return new FcuInstancesResource(context as FigmaConverterUnity, this);
        }
    }
}
