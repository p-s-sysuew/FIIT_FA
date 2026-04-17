using System.ComponentModel;
using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
{
    protected override RbNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        return new RbNode<TKey, TValue>(key, value);
    }

    protected override void OnNodeAdded(RbNode<TKey, TValue> newNode)
    {
        FixInsertion(newNode);

        Root?.Color = RbColor.Black;
    }
    protected override void OnNodeRemoved(RbNode<TKey, TValue>? parent, RbNode<TKey, TValue>? child)
    {
    }

    protected override void RemoveNode(RbNode<TKey, TValue> node)
    {
        if (node == null)
        {
            return;
        }

        RbNode<TKey, TValue> y = node;
        RbColor originalColor = y.Color;

        RbNode<TKey, TValue>? x;
        RbNode<TKey, TValue>? xParent;

        if (node.Left == null)
        {
            x = node.Right;
            xParent = node.Parent;
            Transplant(node, node.Right);
        }
        else if (node.Right == null)
        {
            x = node.Left;
            xParent = node.Parent;
            Transplant(node, node.Left);
        }
        else
        {
            y = Minimum(node.Right);
            originalColor = y.Color;
            x = y.Right;

            if (y.Parent == node)
            {
                xParent = y;
            }
            else
            {
                xParent = y.Parent;

                Transplant(y, y.Right);
                y.Right = node.Right;
                y.Right.Parent = y;
            }

            Transplant(node, y);
            y.Left = node.Left;
            y.Left.Parent = y;

            y.Color = node.Color;
        }

        if (originalColor == RbColor.Black)
        {
            FixDeletion(x, xParent);
        }

        Root?.Color = RbColor.Black;
    }

    private void FixInsertion(RbNode<TKey, TValue> x)
    {
        while (x.Parent != null && x.Parent.Color == RbColor.Red)
        {
            var parent = x.Parent;
            var grandParent = parent.Parent;

            if (grandParent == null)
            {
                break;
            }

            if (parent.IsLeftChild)
            {
                var uncle = grandParent.Right;

                // 1 Красный дядя
                if (Color(uncle) == RbColor.Red)
                {
                    parent.Color = RbColor.Black;
                    uncle!.Color = RbColor.Black;
                    grandParent.Color = RbColor.Red;
                    x = grandParent;
                }
                else
                {
                    // 2 LR
                    if (x.IsRightChild)
                    {
                        x = parent;
                        RotateLeft(x);
                        parent = x.Parent!;
                        grandParent = parent.Parent;
                    }

                    // 3 LL
                    parent.Color = RbColor.Black;
                    grandParent!.Color = RbColor.Red;
                    RotateRight(grandParent);
                }
            }
            else
            {
                var uncle = grandParent.Left; 

                // 1 Красный дядя
                if (Color(uncle) == RbColor.Red)
                {
                    parent.Color = RbColor.Black;
                    uncle!.Color = RbColor.Black;
                    grandParent.Color = RbColor.Red;
                    x = grandParent;
                }
                else
                {
                    // 2 LR
                    if (x.IsLeftChild)
                    {
                        x = parent;
                        RotateRight(x);
                        parent = x.Parent!;
                        grandParent = parent.Parent;
                    }

                    // 3 LL
                    parent.Color = RbColor.Black;
                    grandParent!.Color = RbColor.Red;
                    RotateLeft(grandParent);
                }
            }
        }

        Root?.Color = RbColor.Black;
    }

    private void FixDeletion(RbNode<TKey, TValue>? x, RbNode<TKey, TValue>? xParent)
    {
        while (x != Root && Color(x) == RbColor.Black)
        {
            if (x == xParent?.Left)
            {
                var brother = xParent!.Right;

                // 1 Red Brother
                if (Color(brother) == RbColor.Red)
                {
                    brother?.Color = RbColor.Black;
                    xParent.Color = RbColor.Red;
                    RotateLeft(xParent);
                    brother = xParent.Right;
                }

                // 2 Чёрные племянники.
                if (Color(brother?.Left) == RbColor.Black && Color(brother?.Right) == RbColor.Black)
                {
                    brother?.Color = RbColor.Red;

                    x = xParent;
                    xParent = x.Parent;
                }
                else
                {
                    // 3 Ближний племянник - красный
                    if (Color(brother?.Right) == RbColor.Black)
                    {
                        brother?.Left?.Color = RbColor.Black;
                        brother?.Color = RbColor.Red;
                        if (brother != null)
                        {
                            RotateRight(brother);
                        }

                        brother = xParent.Right;
                    }

                    // 4 Дальний племянник - красный
                    brother?.Color = xParent.Color;
                    xParent.Color = RbColor.Black;
                    brother?.Right?.Color = RbColor.Black;

                    RotateLeft(xParent);
                    x = Root;
                    xParent = null;
                }
            }
            else
            {
                var brother = xParent!.Left;

                // 1 Red Brother
                if (Color(brother) == RbColor.Red)
                {
                    brother?.Color = RbColor.Black;
                    xParent.Color = RbColor.Red;
                    RotateRight(xParent);
                    brother = xParent.Left;
                }

                // 2 Чёрные племянники.
                if (Color(brother?.Left) == RbColor.Black && Color(brother?.Right) == RbColor.Black)
                {
                    brother?.Color = RbColor.Red;

                    x = xParent;
                    xParent = x.Parent;
                }
                else
                {
                    // 3 Ближний племянник - красный
                    if (Color(brother?.Left) == RbColor.Black)
                    {
                        brother?.Right?.Color = RbColor.Black;
                        brother?.Color = RbColor.Red;
                        if (brother != null)
                        {
                            RotateLeft(brother);
                        }

                        brother = xParent.Left;
                    }

                    // 4 Дальний племянник - красный
                    brother?.Color = xParent.Color;
                    xParent.Color = RbColor.Black;
                    brother?.Left?.Color = RbColor.Black;

                    RotateRight(xParent);
                    x = Root;
                    xParent = null;
                }
            }
        }
    }
    
    private static RbColor Color(RbNode<TKey, TValue>? node)
    {
        return node?.Color ?? RbColor.Black;
    }
}

// Запустить тесты только для этого типа v v v
// dotnet test TreeDataStructures.Tests/ --filter TestCategory=RB -r linux-x64 -v normal   