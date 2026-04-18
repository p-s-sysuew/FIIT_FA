using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.BST;


// Реализация обычного дерева BST
public class BinarySearchTree<TKey, TValue> 
    : BinarySearchTreeBase<TKey, TValue, BstNode<TKey, TValue>>
{

    // Создание нового узла дерева.
    protected override BstNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        return new BstNode<TKey, TValue>(key, value);
    }


    // Вызываемый сразу после успешной вставки нового узла.
    protected override void OnNodeAdded(BstNode<TKey, TValue> newNode)
    {
    }

    // Вызываемый после удаления узла.
    protected override void OnNodeRemoved(BstNode<TKey, TValue>? parent, BstNode<TKey, TValue>? child)
    {

    }
}

// Запустить тесты только для этого типа v v v
// dotnet test TreeDataStructures.Tests/ --filter TestCategory=BST -r linux-x64 -v normal   