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
    // newNode - только что добавленный узел
    protected override void OnNodeAdded(BstNode<TKey, TValue> newNode)
    {
        // Для обычного BST ничего делать не нужно
    }

    /// Вызываемый после удаления узла.
    /// parent - родитель удалённого узла
    /// child - узел, который занял место удалённого
    protected override void OnNodeRemoved(BstNode<TKey, TValue>? parent, BstNode<TKey, TValue>? child)
    {
        // Для обычного BST ничего делать не нужно
    }
}

// Запустить тесты только для этого типа v v v
// dotnet test TreeDataStructures.Tests/ --filter TestCategory=BST -r linux-x64 -v normal   