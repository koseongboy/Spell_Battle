using UnityEngine;

namespace DA_Assets.Shared.MCP
{
    public abstract class McpResourceSO : ScriptableObject
    {
        [Header("Resource Identity")]
        public string ResourceUri;
        public string ResourceName;

        [TextArea(2, 4)]
        public string ResourceDescription;

        public string MimeType = "text/plain";

        public abstract IMcpResource CreateInstance(object context);
    }
}
