namespace BrighterTools.FileStorage;

/// <summary>
/// Represents Storage Tree Node.
/// </summary>
public class StorageTreeNode
{
    /// <summary>
    /// Gets or sets the Name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Children.
    /// </summary>
    public List<StorageTreeNode> Children { get; set; } = [];
    /// <summary>
    /// Gets or sets the S File.
    /// </summary>
    public bool IsFile { get; set; }
}

/// <summary>
/// Represents Storage Tree Builder.
/// </summary>
public static class StorageTreeBuilder
{
    /// <summary>
    /// Builds Tree.
    /// </summary>
    public static StorageTreeNode BuildTree(IEnumerable<string> paths, string rootPath)
    {
        var rootNode = new StorageTreeNode { Name = rootPath };

        foreach (var path in paths)
        {
            var parts = path.Replace(rootPath + "/", string.Empty, StringComparison.Ordinal).Split('/');
            var currentNode = rootNode;

            foreach (var part in parts)
            {
                var existingNode = currentNode.Children.FirstOrDefault(n => n.Name == part);
                if (existingNode == null)
                {
                    existingNode = new StorageTreeNode { Name = part, IsFile = part.Contains('.') };
                    currentNode.Children.Add(existingNode);
                }

                currentNode = existingNode;
            }
        }

        return rootNode;
    }
}

