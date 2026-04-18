using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class Treap<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, TreapNode<TKey, TValue>>
{
    /// <summary>
    /// Разрезает дерево с корнем <paramref name="root"/> на два поддерева:
    /// Left: все ключи <= <paramref name="key"/>
    /// Right: все ключи > <paramref name="key"/>
    /// </summary>
    protected virtual (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right) Split(TreapNode<TKey, TValue>? root, TKey key)
    {
        if (root == null)
        {
            return (null, null); // Пустое дерево
        }

        // Корень и его левое поддерево идут в левую часть
        if (Comparer.Compare(root.Key, key) <= 0)
        {
            var (middleLeft, right) = Split(root.Right, key); // Рекурсивно режем правое поддерево

            root.Right = middleLeft; // Присоединяем среднюю часть к корню
            middleLeft?.Parent = root;

            root.Parent = null;
            return (root, right); // Корень в левом дереве
        }
        else // Корень идёт в правое дерево
        {
            var (left, middleRight) = Split(root.Left, key); // Рекурсивно режем левое поддерево

            root.Left = middleRight;
            middleRight?.Parent = root;

            root.Parent = null;
            return (left, root); // Корень в правом дереве
        }
    }

    /// <summary>
    /// Сливает два дерева в одно.
    /// Важное условие: все ключи в <paramref name="left"/> должны быть меньше ключей в <paramref name="right"/>.
    /// Слияние происходит на основе Priority (куча).
    /// </summary>
    protected virtual TreapNode<TKey, TValue>? Merge(TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? right)
    {
        if (left == null)
        {
            right?.Parent = null;
            return right; // Левое пусто — возвращаем правое
        }

        if (right == null)
        {
            left.Parent = null;
            return left; // Правое пусто — возвращаем левое
        }

        // У кого приоритет выше, тот становится корнем
        if (left.Priority > right.Priority)
        {
            left.Right = Merge(left.Right, right); // Рекурсивно сливаем правое поддерево левого
            left.Right?.Parent = left;
            left.Parent = null;
            return left;
        }
        else
        {
            right.Left = Merge(left, right.Left); // Рекурсивно сливаем левое поддерево правого
            right.Left?.Parent = right;
            right.Parent = null;
            return right;
        }
    }

    public override void Add(TKey key, TValue value)
    {
        var existing = FindNode(key);
        if (existing != null)
        {
            existing.Value = value; // Обновляем существующий ключ
            return;
        }

        var newNode = CreateNode(key, value);

        var (left, right) = Split(Root, key); // Режем по ключу

        Root = Merge(Merge(left, newNode), right); // Вставляем новый узел между left и right

        Root?.Parent = null;

        Count++;
    }

    public override bool Remove(TKey key)
    {
        var node = FindNode(key);
        if (node == null)
        {
            return false; // Узел не найден
        }

        var merged = Merge(node.Left, node.Right); // Сливаем детей удаляемого узла

        if (node.Parent == null)
        {
            Root = merged; // Удаляем корень
            Root?.Parent = null;
        }
        else if (node.IsLeftChild)
        {
            node.Parent.Left = merged; // Присоединяем результат к левому родителю
            merged?.Parent = node.Parent;
        }
        else
        {
            node.Parent.Right = merged; // Присоединяем результат к правому родителю
            merged?.Parent = node.Parent;
        }

        Count--;
        return true;
    }

    protected override TreapNode<TKey, TValue> CreateNode(TKey key, TValue value)
    {
        return new TreapNode<TKey, TValue>(key, value);
    }
    
    protected override void OnNodeAdded(TreapNode<TKey, TValue> newNode)
    {
        
    }

    protected override void OnNodeRemoved(TreapNode<TKey, TValue>? parent, TreapNode<TKey, TValue>? child)
    {
        // Можно переопределить для логирования
    }
}

// Запустить тесты только для этого типа v v v
// dotnet test TreeDataStructures.Tests/ --filter TestCategory=Treap -r linux-x64 -v normal
