using System.Collections.Generic;

namespace AssetStudioCLI
{
    public class TreeNode
    {
        public string Text;
        public List<TreeNode> Nodes { get; } = new List<TreeNode>();
        public bool Checked;
    }
}
