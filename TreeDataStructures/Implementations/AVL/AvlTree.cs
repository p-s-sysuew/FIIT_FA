using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.AVL;

public class AvlTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, AvlNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override AvlNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);

    protected override void OnNodeAdded(AvlNode<TKey, TValue> newNode)
    {
        Rebalance(newNode);
    }

    protected override void OnNodeRemoved(AvlNode<TKey, TValue>? parent, AvlNode<TKey, TValue>? child)
    {
        Rebalance(parent ?? child);
    }

    private void Rebalance(AvlNode<TKey, TValue>? node)
    {
        while (node != null)
        {
            UpdateHeight(node);

            int balance = Balance(node);

            if (balance > 1)
            {
                if (Balance(node.Left) < 0)
                {
                    RotateDoubleLeft(node);
                }
                else
                {
                    RotateRight(node);
                }

                FixHeights(node);
            }
            else if (balance < -1)
            {
                if (Balance(node.Right) > 0)
                {
                    RotateDoubleRight(node);
                }
                else
                {
                    RotateLeft(node);
                }

                FixHeights(node);
            }

            node = node.Parent;
        }
    }

    private static int Height(AvlNode<TKey, TValue>? node) => node?.Height ?? 0;

    private static int Balance(AvlNode<TKey, TValue>? node) => node == null ? 0 : Height(node.Left) - Height(node.Right);

    private static void UpdateHeight(AvlNode<TKey, TValue>? node)
    {
        node?.Height = 1 + Math.Max(Height(node.Left), Height(node.Right));
    }

    private static void FixHeights(AvlNode<TKey, TValue>? node)
    {
        UpdateHeight(node);

        if (node?.Parent != null)
        {
            UpdateHeight(node.Parent);
            if (node.Parent.Parent != null)
            {
                UpdateHeight(node.Parent.Parent);
            }
        }
    }
}

// Запустить тесты только для этого типа v v v
// dotnet test TreeDataStructures.Tests/ --filter TestCategory=AVL -r linux-x64 -v normal   